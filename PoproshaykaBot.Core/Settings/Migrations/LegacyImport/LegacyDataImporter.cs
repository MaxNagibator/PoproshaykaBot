using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

public static class LegacyDataImporter
{
    private const string BackupSuffix = "pre-import";

    private static readonly string MonolithRelativePath =
        Path.Combine(LegacyDataCatalog.SettingsFolderName, LegacyDataCatalog.SettingsFileName);

    public static LegacyImportResult Import(string sourcePath, bool overwrite = false, ILogger? logger = null)
    {
        return Import(sourcePath, AppPaths.BaseDirectory, overwrite, logger);
    }

    internal static LegacyImportResult Import(string sourcePath, string targetBaseDirectory, bool overwrite, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(targetBaseDirectory);

        var copied = new List<string>();
        var skipped = new List<LegacyImportSkip>();
        var failures = new List<LegacyImportFailure>();

        string fullSource;
        string fullTarget;

        try
        {
            fullSource = Path.GetFullPath(sourcePath);
            fullTarget = Path.GetFullPath(targetBaseDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            logger?.LogError(exception, "Импорт данных: путь {Path} не удалось разобрать", sourcePath);
            failures.Add(new(string.Empty, $"Не удалось разобрать путь: {sourcePath}"));
            return Build(sourcePath, targetBaseDirectory, copied, skipped, failures, false, false, [], []);
        }

        if (!Directory.Exists(fullSource))
        {
            logger?.LogWarning("Импорт данных: папка-источник {Source} недоступна – переносить нечего", fullSource);
            failures.Add(new(string.Empty, $"Папка-источник недоступна: {fullSource}"));
            return Build(fullSource, fullTarget, copied, skipped, failures, false, false, [], []);
        }

        var settingsDirectory = Path.Combine(fullTarget, LegacyDataCatalog.SettingsFolderName);
        var hadTokens = LegacyDataReader.HasOAuthTokens(fullSource, logger);
        var inPlace = PathComparison.AreEqual(fullSource, fullTarget);
        var snapshot = inPlace || overwrite
            ? new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            : CaptureSettingsFiles(settingsDirectory, logger);

        if (inPlace)
        {
            // TODO: при импорте на месте legacy-файл из корня не заменяет одноимённый в settings/ даже с overwrite – только попадает в UnmigratedLegacyFiles; менять, когда появятся жалобы на «импорт не перенёс старые настройки»
            logger?.LogInformation("Импорт данных: источник совпадает с рабочей папкой {Target} – копирование не требуется, выполняется только миграция раскладки",
                fullTarget);
        }
        else
        {
            Directory.CreateDirectory(fullTarget);
            CopyAll(fullSource, fullTarget, overwrite, copied, skipped, failures, logger);
        }

        var migratedSettingsFiles = new List<string>(LegacySettingsLayoutMigrator.Run(fullTarget, settingsDirectory, logger));

        if (copied.Contains(MonolithRelativePath, StringComparer.OrdinalIgnoreCase))
        {
            RestoreFilesTouchedBySplit(snapshot, fullTarget, migratedSettingsFiles, skipped, failures, logger);
        }

        var leftovers = CollectUnmigratedLegacyFiles(fullTarget, logger);

        var tokensSurvived = LegacyDataReader.HasTokens(
            LegacyDataReader.TryRead(Path.Combine(settingsDirectory, LegacyDataCatalog.AccountsFileName), logger));

        var requiresReauthorization = hadTokens && !tokensSurvived;

        logger?.LogInformation("Импорт данных завершён: скопировано {Copied}, пропущено {Skipped}, с ошибками {Failed}",
            copied.Count,
            skipped.Count,
            failures.Count);

        if (requiresReauthorization)
        {
            logger?.LogWarning("Импорт данных: сохранённые токены доступа не перенесены – потребуется повторная авторизация в Twitch");
        }

        return Build(fullSource, fullTarget, copied, skipped, failures, requiresReauthorization, inPlace, leftovers, migratedSettingsFiles);
    }

    internal static void CopyAll(
        string source,
        string target,
        bool overwrite,
        List<string> copied,
        List<LegacyImportSkip> skipped,
        List<LegacyImportFailure> failures,
        ILogger? logger)
    {
        var files = LegacyDataCatalog.Enumerate(source);

        if (files.Count == 0 && !Directory.Exists(source))
        {
            logger?.LogError("Импорт данных: папка-источник {Source} стала недоступна во время переноса", source);
            failures.Add(new(string.Empty, $"Папка-источник стала недоступна во время переноса: {source}"));
            return;
        }

        foreach (var file in files)
        {
            var targetPath = Path.Combine(target, file.RelativeTargetPath);

            if (PathComparison.AreEqual(file.SourcePath, targetPath))
            {
                skipped.Add(new(file.RelativeTargetPath, LegacyImportSkipReason.SameLocation));

                logger?.LogWarning("Импорт данных: {File} уже лежит на своём месте – копирование пропущено",
                    file.RelativeTargetPath);

                continue;
            }

            if (File.Exists(targetPath) && !overwrite)
            {
                skipped.Add(new(file.RelativeTargetPath, LegacyImportSkipReason.TargetExists));

                logger?.LogWarning("Импорт данных: {File} пропущен – в рабочей папке уже есть такой файл, он оставлен без изменений",
                    file.RelativeTargetPath);

                continue;
            }

            try
            {
                if (File.Exists(targetPath) && !TryBackup(targetPath, file.Name, logger))
                {
                    failures.Add(new(file.RelativeTargetPath, "Резервная копия не создана, файл оставлен без изменений"));

                    logger?.LogError("Импорт данных: {File} не перезаписан – не удалось создать резервную копию прежнего файла",
                        file.RelativeTargetPath);

                    continue;
                }

                CopyAtomic(file.SourcePath, targetPath, file.Name, logger);
                copied.Add(file.RelativeTargetPath);

                logger?.LogInformation("Импорт данных: {Source} перенесён в {Target}", file.SourcePath, targetPath);
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Импорт данных: не удалось перенести {Source} в {Target}", file.SourcePath, targetPath);
                failures.Add(new(file.RelativeTargetPath, exception.Message));
            }
        }
    }

    private static bool TryBackup(string targetPath, string fileName, ILogger? logger)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var pattern = $"{Path.GetFileNameWithoutExtension(targetPath)}.{BackupSuffix}-*{Path.GetExtension(targetPath)}";

        try
        {
            var before = Directory.GetFiles(directory, pattern).Length;
            JsonStoreBackup.CreateBackup(targetPath, BackupSuffix, logger, ResolveRedactor(fileName));
            return Directory.GetFiles(directory, pattern).Length > before;
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Импорт данных: проверка резервной копии для {Target} не удалась", targetPath);
            return false;
        }
    }

    private static Dictionary<string, byte[]> CaptureSettingsFiles(string settingsDirectory, ILogger? logger)
    {
        var snapshot = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in LegacyDataCatalog.SettingsFiles)
        {
            var path = Path.Combine(settingsDirectory, name);

            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                snapshot[path] = File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                logger?.LogWarning(exception,
                    "Импорт данных: не удалось запомнить прежнее содержимое {Path} – защита от перезаписи при разборе настроек не сработает",
                    path);
            }
        }

        return snapshot;
    }

    private static void RestoreFilesTouchedBySplit(
        Dictionary<string, byte[]> snapshot,
        string target,
        List<string> migratedSettingsFiles,
        List<LegacyImportSkip> skipped,
        List<LegacyImportFailure> failures,
        ILogger? logger)
    {
        foreach (var (path, original) in snapshot)
        {
            var relativePath = Path.GetRelativePath(target, path);
            var fileName = Path.GetFileName(path);

            try
            {
                var untouched = File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(original);

                if (!untouched)
                {
                    AtomicFile.Save(path, original, logger, keepBackup: !LegacyDataCatalog.CarriesTokens(fileName));
                    skipped.Add(new(relativePath, LegacyImportSkipReason.TargetExists));

                    logger?.LogWarning("Импорт данных: {File} возвращён в прежний вид – разбор перенесённого settings.json попытался его заменить, а перезапись выключена",
                        relativePath);
                }

                migratedSettingsFiles.RemoveAll(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Импорт данных: не удалось вернуть прежнее содержимое {File}", relativePath);
                failures.Add(new(relativePath, exception.Message));
            }
        }
    }

    private static IReadOnlyList<string> CollectUnmigratedLegacyFiles(string target, ILogger? logger)
    {
        var leftovers = LegacyDataCatalog.SettingsFiles
            .Where(name => File.Exists(Path.Combine(target, name)))
            .ToList();

        if (leftovers.Count > 0)
        {
            logger?.LogWarning("Импорт данных: в корне рабочей папки остались файлы старой раскладки ({Files}) – в settings/ уже есть свои версии, поэтому они не перенесены",
                string.Join(", ", leftovers));
        }

        return leftovers;
    }

    private static void CopyAtomic(string source, string target, string fileName, ILogger? logger)
    {
        AtomicFile.Save(target,
            temporaryPath => File.Copy(source, temporaryPath, true),
            logger,
            keepBackup: !LegacyDataCatalog.CarriesTokens(fileName));
    }

    private static Func<string, string>? ResolveRedactor(string fileName)
    {
        return LegacyDataCatalog.CarriesTokens(fileName) ? AccountsTokenRedactor.Redact : null;
    }

    private static LegacyImportResult Build(
        string source,
        string target,
        List<string> copied,
        List<LegacyImportSkip> skipped,
        List<LegacyImportFailure> failures,
        bool requiresReauthorization,
        bool inPlace,
        IReadOnlyList<string> unmigratedLegacyFiles,
        IReadOnlyList<string> migratedSettingsFiles)
    {
        return new()
        {
            SourcePath = source,
            TargetPath = target,
            CopiedFiles = copied,
            SkippedFiles = skipped,
            Failures = failures,
            RequiresReauthorization = requiresReauthorization,
            IsInPlaceMigration = inPlace,
            UnmigratedLegacyFiles = unmigratedLegacyFiles,
            MigratedSettingsFiles = migratedSettingsFiles,
        };
    }
}
