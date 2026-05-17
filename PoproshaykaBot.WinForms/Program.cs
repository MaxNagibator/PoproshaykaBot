using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Broadcast;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Di;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Infrastructure.Logging;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Migrations;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System.Diagnostics;
using Timer = System.Windows.Forms.Timer;

namespace PoproshaykaBot.WinForms;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var isUiSmoke = args.Any(arg => string.Equals(arg, "--ui-smoke", StringComparison.OrdinalIgnoreCase));

        const string OutputTemplate = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

        var uiLogSink = new UiLogSink();

        SelfLog.Enable(message => Debug.WriteLine($"[Serilog] {message}"));

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.Debug(outputTemplate: OutputTemplate)
            .WriteTo.File(AppPaths.Combine("logs", "bot_log_.txt"), rollingInterval: RollingInterval.Day, outputTemplate: OutputTemplate)
            .WriteTo.Sink(uiLogSink, LogEventLevel.Information)
            .CreateLogger();

        try
        {
            Log.Information("Запуск приложения...");
            Log.Information("Режим хранения данных: {Mode}, базовая директория: {BaseDirectory}",
                ResolveStorageMode(),
                AppPaths.BaseDirectory);

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();

            var memoryCheckTimer = CreateMemoryWatchdogTimer();

            if (!isUiSmoke)
            {
                memoryCheckTimer.Start();
            }

            RunApp(isUiSmoke, uiLogSink);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Приложение завершило работу из-за непредвиденной ошибки");
        }
        finally
        {
            Log.Information("Завершение работы приложения");
            Log.CloseAndFlush();
        }
    }

    private static void RunApp(bool isUiSmoke, UiLogSink uiLogSink)
    {
        MigrateLegacySettingsLayout();

        var services = new ServiceCollection();
        ConfigureServices(services, uiLogSink);

        var serviceProvider = services.BuildServiceProvider();
        var appLifetime = serviceProvider.GetRequiredService<AppLifetime>();
        var streamMonitoringHost = serviceProvider.GetRequiredService<StreamMonitoringHost>();
        var appLifetimeStarted = false;
        var streamMonitoringStarted = false;
        try
        {
            serviceProvider.ActivateEventSubscribers(typeof(InfrastructureServiceCollectionExtensions).Assembly);

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
            if (streamMonitoringStarted)
            {
                StopStreamMonitoring(streamMonitoringHost);
            }

            if (appLifetimeStarted)
            {
                StopAppLifetime(appLifetime);
            }

            serviceProvider.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
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

    private static void StopStreamMonitoring(StreamMonitoringHost host)
    {
        try
        {
            host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
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

        if (!ValidateAndResolvePortConflict(settingsManager))
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

    private static void StopAppLifetime(AppLifetime appLifetime)
    {
        try
        {
            appLifetime.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка остановки AppLifetime");
        }
    }

    private static string ResolveStorageMode()
    {
        if (AppPaths.IsBaseDirectoryOverridden)
        {
            return "override";
        }

        return AppPaths.IsPortable ? "portable" : "AppData";
    }

    private static Timer CreateMemoryWatchdogTimer()
    {
        const long ThresholdMb = 1024;
        const long MemoryThreshold = 1024 * 1024 * ThresholdMb;
        const int CheckIntervalMs = 1000;
        const int KillDelayMs = 1000;

        var memoryCheckTimer = new Timer { Interval = CheckIntervalMs };
        memoryCheckTimer.Tick += (_, _) =>
        {
            var currentProcess = Process.GetCurrentProcess();
            var memoryBytes = currentProcess.PrivateMemorySize64;

            if (memoryBytes <= MemoryThreshold)
            {
                return;
            }

            Log.Warning("Обнаружено аномальное потребление памяти: {MemoryBytes} байт", memoryBytes);
            memoryCheckTimer.Stop();

            Task.Run(async () =>
            {
                await Task.Delay(KillDelayMs);
                Log.Fatal("Принудительное завершение процесса из-за утечки памяти");
                await Log.CloseAndFlushAsync();
                currentProcess.Kill();
            });

            var memoryMb = memoryBytes / (1024.0 * 1024.0);
            var killDelaySeconds = KillDelayMs / 1000;
            var secondsWord = GetRussianSecondsWord(killDelaySeconds);
            var message =
                $"""
                 Внимание! Обнаружено аномальное потребление ресурсов.

                 Текущее использование: {memoryMb:F2} MB
                 Установленный лимит: {ThresholdMb:F2} MB

                 У вас есть {killDelaySeconds} {secondsWord}, чтобы прочитать это сообщение, затем процесс будет убит принудительно.
                 """;

            MessageBox.Show(message, "Критическая нагрузка на память", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Environment.FailFast("Пользователь подтвердил закрытие при утечке памяти.");
        };

        return memoryCheckTimer;
    }

    private static void ConfigureServices(IServiceCollection services, UiLogSink uiLogSink)
    {
        services
            .AddCoreInfrastructure(uiLogSink)
            .AddStatistics()
            .AddSettingsStores()
            .AddTwitchClients()
            .AddChatPipeline()
            .AddStreamMonitoring()
            .AddBroadcasting()
            .AddPolls()
            .AddHttpServer()
            .AddObsIntegration()
            .AddDashboardTiles()
            .AddForms();
    }

    private static string GetRussianSecondsWord(int seconds)
    {
        var mod100 = seconds % 100;
        if (mod100 is >= 11 and <= 14)
        {
            return "секунд";
        }

        return (seconds % 10) switch
        {
            1 => "секунда",
            2 or 3 or 4 => "секунды",
            _ => "секунд",
        };
    }

    private static bool ValidateAndResolvePortConflict(SettingsManager settingsManager)
    {
        var settings = settingsManager.Current;
        var redirectUri = settings.Twitch.RedirectUri;
        var serverPort = settings.Twitch.HttpServerPort;

        if (!RedirectUriPortResolver.TryResolve(redirectUri, out var redirectPort))
        {
            Log.Error("Некорректный RedirectUri: {RedirectUri}", redirectUri);
            MessageBox.Show($"Некорректный RedirectUri: {redirectUri}\n\nПожалуйста, исправьте URI в настройках OAuth.",
                "Ошибка конфигурации",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return false;
        }

        if (redirectPort == serverPort)
        {
            return true;
        }

        Log.Information("Конфликт портов. Обновление порта с {OldPort} на {NewPort}", serverPort, redirectPort);
        settings.Twitch.HttpServerPort = redirectPort;
        settingsManager.SaveSettings(settings);

        var message = $"""
                       Обнаружен конфликт портов:

                       • RedirectUri использует порт: {redirectPort}
                       • HTTP сервер был настроен на порт: {serverPort}

                       Для корректной работы OAuth порт HTTP сервера был автоматически обновлен до {redirectPort}.

                       Если вы хотите использовать другой порт, пожалуйста, измените его вручную в настройках HTTP сервера и RedirectUri.
                       """;

        MessageBox.Show(message,
            "Порт обновлен",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        return true;
    }
}
