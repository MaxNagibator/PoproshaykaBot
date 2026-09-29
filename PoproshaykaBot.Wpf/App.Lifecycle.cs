using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Infrastructure.Runtime;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Update;
using PoproshaykaBot.Wpf.Bootstrap;
using Serilog;
using Serilog.Extensions.Logging;
using System.Diagnostics;
using System.Windows;

namespace PoproshaykaBot.Wpf;

public partial class App
{
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
            HostLog.Error(exception, "Ошибка очистки после обновления");
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
            HostLog.Error(exception, "Ошибка применения запланированного обновления");
            return false;
        }
    }

    internal static bool TryRequestRestart()
    {
        return Interlocked.CompareExchange(ref _restartRequested, 1, 0) == 0;
    }

    internal static void CancelRestart()
    {
        Interlocked.Exchange(ref _restartRequested, 0);
    }

    internal static bool TryRestartForced()
    {
        if (!LaunchReplacement())
        {
            return false;
        }

        HostLog.Information("Завершение работы приложения ({Reason}), код выхода {ExitCode}", AppExitReason.ForcedRestart, -1);
        Log.CloseAndFlush();
        Process.GetCurrentProcess().Kill();
        return true;
    }

    private static bool LaunchReplacement()
    {
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrEmpty(executablePath))
        {
            HostLog.Error("Перезапуск невозможен: путь к исполняемому файлу приложения неизвестен");
            return false;
        }

        var arguments = AppRestart.BuildArguments(_startupArguments, Environment.ProcessId);

        if (!AppRestart.TryLaunch(executablePath, arguments, false, out var failure))
        {
            HostLog.Error(failure, "Не удалось запустить новый экземпляр приложения для перезапуска");
            return false;
        }

        HostLog.Information("Запущен новый экземпляр приложения, он дождётся завершения текущего");
        return true;
    }

    private static void ReportRestartHandoff(int previousProcessId, bool exited)
    {
        if (exited)
        {
            HostLog.Information("Перезапуск: предыдущий экземпляр (PID {ProcessId}) завершился", previousProcessId);
            return;
        }

        HostLog.Warning("Перезапуск: предыдущий экземпляр (PID {ProcessId}) не завершился за {Seconds} с, запуск идёт как обычно",
            previousProcessId,
            (int)AppRestart.HandoffTimeout.TotalSeconds);
    }

    private static void ReportEnvironment(IReadOnlyList<string> arguments)
    {
        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        StartupReport.LogEnvironment(loggerFactory.CreateLogger(nameof(StartupReport)), arguments, AppBuildBadge.StartupLine);
    }

    private static void ReportConfiguration(IServiceProvider serviceProvider)
    {
        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        StartupReport.LogConfiguration(loggerFactory.CreateLogger(nameof(StartupReport)), serviceProvider);
    }

    private static bool StartStreamMonitoring(StreamMonitoringHost host)
    {
        try
        {
            host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            HostLog.Information("Стрим-мониторинг запущен независимо от подключения бота");
            return true;
        }
        catch (Exception ex)
        {
            HostLog.Error(ex, "Ошибка запуска стрим-мониторинга");
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
            HostLog.Warning("Остановка стрим-мониторинга прервана по таймауту завершения");
        }
        catch (Exception ex)
        {
            HostLog.Error(ex, "Ошибка остановки стрим-мониторинга");
        }
    }

    private static bool StartHttpServerIfNeeded(SettingsManager settingsManager, AppLifetime appLifetime)
    {
        var reconcile = HttpServerPortReconciler.Reconcile(settingsManager,
            new SerilogLoggerFactory(Log.Logger).CreateLogger(nameof(HttpServerPortReconciler)));

        if (reconcile.Notice is { } notice)
        {
            StyledMessageBox.Show(notice.Message, notice.Title, MessageBoxButton.OK, ToMessageBoxImage(notice.Severity));
        }

        if (!reconcile.IsResolved)
        {
            HostLog.Warning("Не удалось разрешить конфликт портов. HTTP сервер не запущен");
            StyledMessageBox.Show("Не удалось разрешить конфликт портов. HTTP сервер не запущен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        try
        {
            appLifetime.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            HostLog.Information("HTTP сервер успешно запущен");
            return true;
        }
        catch (Exception ex)
        {
            HostLog.Error(ex, "Ошибка запуска HTTP сервера");
            StyledMessageBox.Show($"Ошибка запуска HTTP сервера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            HostLog.Warning("Остановка AppLifetime прервана по таймауту завершения");
        }
        catch (Exception ex)
        {
            HostLog.Error(ex, "Ошибка остановки AppLifetime");
        }
    }

    private static MessageBoxImage ToMessageBoxImage(PortReconcileSeverity severity)
    {
        return severity switch
        {
            PortReconcileSeverity.Information => MessageBoxImage.Information,
            PortReconcileSeverity.Warning => MessageBoxImage.Warning,
            PortReconcileSeverity.Error => MessageBoxImage.Error,
            _ => MessageBoxImage.None,
        };
    }
}
