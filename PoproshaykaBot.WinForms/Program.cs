using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Broadcast;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Di;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Infrastructure.Logging;
using PoproshaykaBot.Core.Infrastructure.Runtime;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Migrations;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Update;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System.Diagnostics;

namespace PoproshaykaBot.WinForms;

public static class Program
{
    private static MemoryWatchdog? _memoryWatchdog;
    private static AppExitReason _exitReason = AppExitReason.None;

    internal static bool IsUiSmoke { get; private set; }

    internal static DebugChannelOverride DebugChannel { get; private set; } = DebugChannelOverride.None;

    [STAThread]
    private static void Main(string[] args)
    {
        var isUiSmoke = args.Any(arg => string.Equals(arg, "--ui-smoke", StringComparison.OrdinalIgnoreCase));
        IsUiSmoke = isUiSmoke;
        DebugChannel = DebugChannelOverride.Parse(args);
        var isFinalizeUpdate = args.Any(arg => string.Equals(arg, UpdateApplier.FinalizeArgument, StringComparison.OrdinalIgnoreCase));

        const string OutputTemplate = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

        var uiLogSink = new UiLogSink();

        SelfLog.Enable(message => Debug.WriteLine($"[Serilog] {message}"));

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.Debug(outputTemplate: OutputTemplate)
            .WriteTo.File(AppPaths.Combine("logs", "bot_log_.txt"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 50L * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 31,
                outputTemplate: OutputTemplate)
            .WriteTo.Sink(uiLogSink, LogEventLevel.Information)
            .CreateLogger();

        Mutex? singleInstanceMutex = null;

        try
        {
            Log.Information("Запуск приложения...");

            singleInstanceMutex = SingleInstanceGate.TryAcquire(isFinalizeUpdate);

            if (singleInstanceMutex is null)
            {
                Log.Information("Обнаружен уже запущенный экземпляр приложения. Завершение работы");
                _exitReason = AppExitReason.AlreadyRunning;

                if (!isUiSmoke)
                {
                    MessageBox.Show("PoproshaykaBot уже запущен.\n\nОдновременно может работать только один экземпляр приложения.",
                        "Приложение уже запущено",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
            }

            ReportEnvironment(args);

            if (isFinalizeUpdate)
            {
                FinalizeUpdate();
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();

            _memoryWatchdog = isUiSmoke
                ? null
                : new MemoryWatchdog(new SerilogLoggerFactory(Log.Logger).CreateLogger(nameof(MemoryWatchdog)), Log.CloseAndFlush);

            RunApp(isUiSmoke, uiLogSink);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Приложение завершило работу из-за непредвиденной ошибки");
            _exitReason = AppExitReason.Failed;
            Environment.ExitCode = 1;

            if (!isUiSmoke)
            {
                MessageBox.Show(ex.ToString(),
                    "PoproshaykaBot – ошибка запуска",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            Log.Information("Завершение работы приложения ({Reason}), код выхода {ExitCode}", _exitReason, Environment.ExitCode);
            Log.CloseAndFlush();
            _memoryWatchdog?.Dispose();
            singleInstanceMutex?.Dispose();
        }
    }

    private static void RunApp(bool isUiSmoke, UiLogSink uiLogSink)
    {
        MigrateLegacySettingsLayout();

        var services = new ServiceCollection();
        ConfigureServices(services, uiLogSink);

        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var appLifetime = serviceProvider.GetRequiredService<AppLifetime>();
        var streamMonitoringHost = serviceProvider.GetRequiredService<StreamMonitoringHost>();
        var appLifetimeStarted = false;
        var streamMonitoringStarted = false;
        try
        {
            serviceProvider.ActivateEventSubscribers(typeof(InfrastructureServiceCollectionExtensions).Assembly);
            ReportConfiguration(serviceProvider);

            var settingsManager = serviceProvider.GetRequiredService<SettingsManager>();
            appLifetimeStarted = StartHttpServerIfNeeded(isUiSmoke, settingsManager, appLifetime);

            if (!isUiSmoke)
            {
                streamMonitoringStarted = StartStreamMonitoring(streamMonitoringHost);
            }

            var mainForm = serviceProvider.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }
        finally
        {
            StopAllComponents(serviceProvider, appLifetime, streamMonitoringHost, appLifetimeStarted, streamMonitoringStarted, isUiSmoke);
            _memoryWatchdog?.Dispose();
            _exitReason = AppExitReason.Normal;

            if (!isUiSmoke && ApplyPendingUpdate())
            {
                _exitReason = AppExitReason.UpdatePending;
            }
        }
    }

    private static void StopAllComponents(
        ServiceProvider serviceProvider,
        AppLifetime appLifetime,
        StreamMonitoringHost streamMonitoringHost,
        bool appLifetimeStarted,
        bool streamMonitoringStarted,
        bool isUiSmoke)
    {
        using var shutdownWatchdog = isUiSmoke
            ? null
            : new ShutdownWatchdog(new SerilogLoggerFactory(Log.Logger).CreateLogger(nameof(ShutdownWatchdog)),
                Log.CloseAndFlush,
                ShutdownDeadlines.HardDeadline);

        using var shutdownTimeout = isUiSmoke ? null : new CancellationTokenSource(ShutdownDeadlines.SoftDeadline);
        var shutdownToken = shutdownTimeout?.Token ?? CancellationToken.None;

        if (streamMonitoringStarted)
        {
            StopStreamMonitoring(streamMonitoringHost, shutdownToken);
        }

        if (appLifetimeStarted)
        {
            StopAppLifetime(appLifetime, shutdownToken);
        }

        serviceProvider.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static void FinalizeUpdate()
    {
        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger(nameof(UpdateFinalizer));

        try
        {
            var executablePath = Environment.ProcessPath;

            if (string.IsNullOrEmpty(executablePath))
            {
                return;
            }

            UpdateFinalizer.Run(UpdatePaths.StagingDirectory(executablePath), executablePath, logger);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Ошибка очистки после обновления");
        }
    }

    private static bool ApplyPendingUpdate()
    {
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrEmpty(executablePath))
        {
            return false;
        }

        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger(nameof(UpdateApplier));

        try
        {
            return UpdateApplier.TryApplyPending(UpdatePaths.StagingDirectory(executablePath), executablePath, logger);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Ошибка применения запланированного обновления");
            return false;
        }
    }

    private static void ReportEnvironment(string[] args)
    {
        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        StartupReport.LogEnvironment(loggerFactory.CreateLogger(nameof(StartupReport)), args);
    }

    private static void ReportConfiguration(IServiceProvider serviceProvider)
    {
        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        StartupReport.LogConfiguration(loggerFactory.CreateLogger(nameof(StartupReport)), serviceProvider);
    }

    private static void MigrateLegacySettingsLayout()
    {
        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger(nameof(LegacySettingsLayoutMigrator));

        try
        {
            LegacySettingsLayoutMigrator.Run(AppPaths.BaseDirectory, AppPaths.SettingsDirectory, logger);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Ошибка пред-миграции layout-а настроек");
        }
    }

    private static bool StartStreamMonitoring(StreamMonitoringHost host)
    {
        try
        {
            host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Log.Information("Стрим-мониторинг запущен независимо от подключения бота");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка запуска стрим-мониторинга");
            return false;
        }
    }

    private static void StopStreamMonitoring(StreamMonitoringHost host, CancellationToken cancellationToken)
    {
        try
        {
            host.StopAsync(cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Остановка стрим-мониторинга прервана по таймауту завершения");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка остановки стрим-мониторинга");
        }
    }

    private static bool StartHttpServerIfNeeded(bool isUiSmoke, SettingsManager settingsManager, AppLifetime appLifetime)
    {
        if (isUiSmoke)
        {
            Log.Information("Запуск в режиме UI smoke-теста. HTTP сервер и сетевые подсистемы отключены");
            return false;
        }

        var reconcile = HttpServerPortReconciler.Reconcile(settingsManager,
            new SerilogLoggerFactory(Log.Logger).CreateLogger(nameof(HttpServerPortReconciler)));

        if (reconcile.Notice is { } notice)
        {
            MessageBox.Show(notice.Message, notice.Title, MessageBoxButtons.OK, ToMessageBoxIcon(notice.Severity));
        }

        if (!reconcile.IsResolved)
        {
            Log.Warning("Не удалось разрешить конфликт портов. HTTP сервер не запущен");
            MessageBox.Show("Не удалось разрешить конфликт портов. HTTP сервер не запущен.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        try
        {
            appLifetime.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Log.Information("HTTP сервер успешно запущен");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка запуска HTTP сервера");
            MessageBox.Show($"Ошибка запуска HTTP сервера: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private static void StopAppLifetime(AppLifetime appLifetime, CancellationToken cancellationToken)
    {
        try
        {
            appLifetime.StopAsync(cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Остановка AppLifetime прервана по таймауту завершения");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка остановки AppLifetime");
        }
    }

    private static void ConfigureServices(IServiceCollection services, UiLogSink uiLogSink)
    {
        services
            .AddCoreInfrastructure(uiLogSink)
            .AddStatistics()
            .AddSettingsStores()
            .AddDebugChannel(DebugChannel)
            .AddTwitchClients()
            .AddChatPipeline()
            .AddStreamMonitoring()
            .AddBroadcasting()
            .AddPolls()
            .AddHttpServer()
            .AddObsIntegration()
            .AddSelfUpdate()
            .AddDashboardTiles()
            .AddForms();
    }

    private static MessageBoxIcon ToMessageBoxIcon(PortReconcileSeverity severity)
    {
        return severity switch
        {
            PortReconcileSeverity.Information => MessageBoxIcon.Information,
            PortReconcileSeverity.Error => MessageBoxIcon.Error,
            _ => MessageBoxIcon.None,
        };
    }
}
