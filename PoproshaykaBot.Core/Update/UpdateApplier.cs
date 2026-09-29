using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Runtime;
using PoproshaykaBot.Core.Settings.Stores;
using System.Text.Json;

namespace PoproshaykaBot.Core.Update;

public static class UpdateApplier
{
    public const string FinalizeArgument = "--finalize-update";

    public static bool TryApplyPending(string stagingDirectory, string currentExecutablePath, ILogger logger)
    {
        return TryApplyPending(stagingDirectory, currentExecutablePath, UpdateEnvironment.ResolveCurrentVersion(), logger);
    }

    internal static bool TryApplyPending(string stagingDirectory, string currentExecutablePath, Version currentVersion, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(stagingDirectory);
        ArgumentException.ThrowIfNullOrEmpty(currentExecutablePath);
        ArgumentNullException.ThrowIfNull(currentVersion);
        ArgumentNullException.ThrowIfNull(logger);

        var pendingPath = Path.Combine(stagingDirectory, UpdateInstaller.PendingFileName);

        if (!File.Exists(pendingPath))
        {
            return false;
        }

        var pending = TryReadPending(pendingPath, logger);

        if (pending is null)
        {
            return false;
        }

        if (!File.Exists(pending.StagedExecutablePath))
        {
            logger.LogWarning("Запланированное обновление недоступно, файл сборки отсутствует");
            TryDelete(pendingPath, logger);
            return false;
        }

        if (!UpdateVersioning.TryParseTag(pending.Version, out var pendingVersion)
            || !UpdateVersioning.IsNewer(pendingVersion, currentVersion))
        {
            logger.LogWarning("Запланированное обновление {PendingVersion} не новее текущей версии {CurrentVersion} – удалено без установки",
                pending.Version, currentVersion);
            DiscardFiles(stagingDirectory, pending, logger);
            return false;
        }

        var plan = UpdateSwapPlanner.Plan(currentExecutablePath, pending.StagedExecutablePath);

        if (!TrySwap(plan, pendingPath, logger))
        {
            return false;
        }

        logger.LogInformation("Версия {Version} установлена, перезапуск приложения", pending.Version);
        Relaunch(plan.CurrentExecutable, logger);
        return true;
    }

    internal static bool DiscardFiles(string stagingDirectory, PendingUpdate pending, ILogger logger)
    {
        var fullStagingDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingDirectory));

        if (!TryDelete(Path.Combine(fullStagingDirectory, UpdateInstaller.PendingFileName), logger))
        {
            return false;
        }

        if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(pending.StagedExecutablePath)),
                fullStagingDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(pending.StagedExecutablePath, logger);
        }

        return true;
    }

    private static PendingUpdate? TryReadPending(string pendingPath, ILogger logger)
    {
        try
        {
            return JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(pendingPath),
                JsonStoreOptions.Default);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось прочитать запланированное обновление {PendingPath}", pendingPath);
            return null;
        }
    }

    private static bool TrySwap(UpdateSwapPlan plan, string pendingPath, ILogger logger)
    {
        if (!TryRemoveStaleBackup(plan.BackupExecutable, logger))
        {
            return false;
        }

        try
        {
            File.Move(plan.CurrentExecutable, plan.BackupExecutable);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось переименовать текущий исполняемый файл – обновление отменено");
            return false;
        }

        try
        {
            File.Move(plan.StagedExecutable, plan.CurrentExecutable);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось установить новую версию – выполняется откат");
            Rollback(plan, logger);
            TryDelete(pendingPath, logger);
            return false;
        }

        return true;
    }

    private static bool TryRemoveStaleBackup(string backupPath, ILogger logger)
    {
        if (!File.Exists(backupPath))
        {
            return true;
        }

        try
        {
            File.Delete(backupPath);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Остался файл предыдущего обновления {BackupPath}, обновление отложено", backupPath);
            return false;
        }
    }

    private static void Rollback(UpdateSwapPlan plan, ILogger logger)
    {
        try
        {
            if (!File.Exists(plan.CurrentExecutable) && File.Exists(plan.BackupExecutable))
            {
                File.Move(plan.BackupExecutable, plan.CurrentExecutable);
                logger.LogInformation("Откат выполнен: восстановлена предыдущая версия");
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось выполнить откат обновления");
        }
    }

    private static void Relaunch(string executablePath, ILogger logger)
    {
        if (!AppRestart.TryLaunch(executablePath, [FinalizeArgument], true, out var failure))
        {
            logger.LogError(failure, "Не удалось перезапустить приложение после обновления");
        }
    }

    private static bool TryDelete(string path, ILogger logger)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось удалить {Path}", path);
            return false;
        }
    }
}
