using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Stores;
using System.Text;
using System.Text.Json.Nodes;

namespace PoproshaykaBot.Core.Settings.Migrations;

public static class LegacySettingsLayoutMigrator
{
    private static readonly string[] KnownSettingsFiles =
    [
        "settings.json",
        "accounts.json",
        "broadcast-profiles.json",
        "polls.json",
        "obs-chat.json",
        "obs-integration.json",
        "recent-categories.json",
        "dashboard-layout.json",
    ];

    public static void Run(string baseDirectory, string settingsDirectory, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        ArgumentException.ThrowIfNullOrEmpty(settingsDirectory);

        if (PathsAreEqual(baseDirectory, settingsDirectory))
        {
            return;
        }

        if (!Directory.Exists(baseDirectory))
        {
            return;
        }

        Directory.CreateDirectory(settingsDirectory);
        RelocateFlatLayout(baseDirectory, settingsDirectory, logger);
        SplitMonolithicSettings(settingsDirectory, logger);
    }

    private static void RelocateFlatLayout(string baseDirectory, string settingsDirectory, ILogger? logger)
    {
        foreach (var fileName in KnownSettingsFiles)
        {
            var legacy = Path.Combine(baseDirectory, fileName);
            var target = Path.Combine(settingsDirectory, fileName);

            if (!File.Exists(legacy))
            {
                continue;
            }

            if (File.Exists(target))
            {
                logger?.LogWarning("Legacy-файл {Legacy} оставлен на месте: целевой {Target} уже существует",
                    legacy,
                    target);

                continue;
            }

            try
            {
                File.Copy(legacy, target);
                var backupPath = BuildLegacyBackupPath(legacy);
                File.Move(legacy, backupPath);
                logger?.LogInformation("Legacy-файл перенесён: {Legacy} → {Target}; оригинал сохранён как {Backup}",
                    legacy,
                    target,
                    backupPath);
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Не удалось перенести {Legacy} в {Target}", legacy, target);
            }
        }
    }

    private static string BuildLegacyBackupPath(string legacyPath)
    {
        var directory = Path.GetDirectoryName(legacyPath)!;
        var name = Path.GetFileNameWithoutExtension(legacyPath);
        var extension = Path.GetExtension(legacyPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(directory, $"{name}.legacy-{timestamp}{extension}");
    }

    private static void SplitMonolithicSettings(string settingsDirectory, ILogger? logger)
    {
        var settingsFile = Path.Combine(settingsDirectory, "settings.json");

        if (!File.Exists(settingsFile))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(settingsFile, Encoding.UTF8);

            if (JsonNode.Parse(json) is not JsonObject root)
            {
                return;
            }

            var changed = SettingsMigrator.TryMigrate(root, logger, settingsDirectory);

            if (!changed)
            {
                return;
            }

            JsonStoreBackup.CreateBackup(settingsFile, "pre-migration", logger, AccountsTokenRedactor.Redact);
            AtomicFile.Save(settingsFile, root.ToJsonString(JsonStoreOptions.Default), logger);
            logger?.LogInformation("Монолитный settings.json мигрирован и разбит на отдельные файлы (директория {Directory})",
                settingsDirectory);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Ошибка миграции монолитного settings.json в {Directory}", settingsDirectory);
        }
    }

    private static bool PathsAreEqual(string a, string b)
    {
        return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
