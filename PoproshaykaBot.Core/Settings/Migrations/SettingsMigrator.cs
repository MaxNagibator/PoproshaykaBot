using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Stores;
using System.Text.Json.Nodes;

namespace PoproshaykaBot.Core.Settings.Migrations;

public static class SettingsMigrator
{
    private const string BotAccountKey = "botAccount";
    private const string BroadcasterAccountKey = "broadcasterAccount";

    public static SettingsMigrationResult Migrate(JsonObject root, ILogger? logger = null, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var changed = false;
        var splitFiles = new List<string>();

        if (root["twitch"] is JsonObject twitch)
        {
            changed |= MigrateBotAccount(twitch, logger);
        }

        changed |= RemoveLegacyUiFields(root, logger);

        if (baseDirectory != null)
        {
            changed |= SplitMonolithicSettings(root, baseDirectory, splitFiles, logger);
        }

        return new(changed, splitFiles);
    }

    private static bool SplitMonolithicSettings(JsonObject root, string baseDirectory, List<string> splitFiles, ILogger? logger)
    {
        var changed = false;

        if (root["twitch"] is JsonObject twitch)
        {
            changed |= SplitAccounts(twitch, baseDirectory, splitFiles, logger);
            changed |= TrySplitObject(twitch, "broadcastProfiles", baseDirectory, "broadcast-profiles.json", splitFiles, logger);
            changed |= TrySplitObject(twitch, "polls", baseDirectory, "polls.json", splitFiles, logger);
            changed |= SplitRecentCategories(twitch, baseDirectory, splitFiles, logger);
            changed |= TrySplitObject(twitch, "obsChat", baseDirectory, "obs-chat.json", splitFiles, logger);
        }

        changed |= SplitDashboardLayout(root, baseDirectory, splitFiles, logger);

        return changed;
    }

    private static bool SplitAccounts(JsonObject twitch, string baseDirectory, List<string> splitFiles, ILogger? logger)
    {
        var botAccount = twitch[BotAccountKey] as JsonObject;
        var broadcasterAccount = twitch[BroadcasterAccountKey] as JsonObject;

        if (botAccount == null && broadcasterAccount == null)
        {
            return false;
        }

        var accountsTarget = Path.Combine(baseDirectory, "accounts.json");
        var dto = new JsonObject
        {
            [BotAccountKey] = botAccount?.DeepClone() ?? new JsonObject(),
            [BroadcasterAccountKey] = broadcasterAccount?.DeepClone() ?? new JsonObject(),
        };

        if (!TryWriteSplit(accountsTarget,
                dto.ToJsonString(JsonStoreOptions.Default),
                "accounts.json",
                splitFiles,
                logger,
                AccountsTokenRedactor.Redact))
        {
            return false;
        }

        var changed = false;
        changed |= twitch.Remove(BotAccountKey);
        changed |= twitch.Remove(BroadcasterAccountKey);
        return changed;
    }

    private static bool SplitRecentCategories(JsonObject twitch, string baseDirectory, List<string> splitFiles, ILogger? logger)
    {
        if (twitch["infrastructure"] is not JsonObject infrastructure
            || infrastructure["recentCategories"] is not JsonArray recentCategories)
        {
            return false;
        }

        var target = Path.Combine(baseDirectory, "recent-categories.json");
        var dto = new JsonObject
        {
            ["items"] = recentCategories.DeepClone(),
        };

        return TryWriteSplit(target, dto.ToJsonString(JsonStoreOptions.Default), "recent-categories.json", splitFiles, logger)
               && infrastructure.Remove("recentCategories");
    }

    private static bool SplitDashboardLayout(JsonObject root, string baseDirectory, List<string> splitFiles, ILogger? logger)
    {
        if (root["ui"] is not JsonObject ui
            || !ui.ContainsKey("dashboard") && !ui.ContainsKey("mainWindow"))
        {
            return false;
        }

        var target = Path.Combine(baseDirectory, "dashboard-layout.json");
        var dto = new JsonObject
        {
            ["dashboard"] = ui["dashboard"]?.DeepClone(),
            ["mainWindow"] = ui["mainWindow"]?.DeepClone(),
        };

        return TryWriteSplit(target, dto.ToJsonString(JsonStoreOptions.Default), "dashboard-layout.json", splitFiles, logger)
               && root.Remove("ui");
    }

    private static bool TrySplitObject(
        JsonObject parent,
        string key,
        string baseDirectory,
        string targetFileName,
        List<string> splitFiles,
        ILogger? logger)
    {
        if (parent[key] is not JsonObject value)
        {
            return false;
        }

        var target = Path.Combine(baseDirectory, targetFileName);

        if (!TryWriteSplit(target, value.DeepClone()!.ToJsonString(JsonStoreOptions.Default), targetFileName, splitFiles, logger))
        {
            return false;
        }

        return parent.Remove(key);
    }

    private static bool TryWriteSplit(
        string target,
        string content,
        string targetFileName,
        List<string> splitFiles,
        ILogger? logger,
        Func<string, string>? backupRedactor = null)
    {
        try
        {
            if (File.Exists(target))
            {
                JsonStoreBackup.CreateBackup(target, "pre-migration", logger, backupRedactor);
                logger?.LogWarning("Миграция настроек: целевой {TargetFileName} уже существовал – создан бэкап перед перезаписью авторитетным источником из settings.json",
                    targetFileName);
            }

            AtomicFile.Save(target, content, logger, keepBackup: backupRedactor == null);
            splitFiles.Add(targetFileName);
            logger?.LogInformation("Миграция настроек: данные из settings.json вынесены в {TargetFileName}",
                targetFileName);

            return true;
        }
        catch (Exception exception)
        {
            logger?.LogError(exception,
                "Миграция настроек: не удалось записать {TargetFileName} – легаси-поле в settings.json оставлено без изменений",
                targetFileName);

            return false;
        }
    }

    private static bool MigrateBotAccount(JsonObject twitch, ILogger? logger)
    {
        var changed = false;

        var botAccount = EnsureObject(twitch, BotAccountKey);

        changed |= MoveStringIfMissing(twitch, "botUsername", botAccount, "login", logger, "botUsername → botAccount.login");
        changed |= RemoveIfPresent(twitch, "accessToken", logger, "twitch.accessToken удалён (требуется новый набор прав)");
        changed |= RemoveIfPresent(twitch, "refreshToken", logger, "twitch.refreshToken удалён (требуется новый набор прав)");
        changed |= RemoveIfPresent(twitch, "storedScopes", logger, "twitch.storedScopes удалён (требуется новый набор прав)");
        changed |= RemoveIfPresent(twitch, "scopes", logger, "twitch.scopes удалён (требуется новый набор прав)");

        if (botAccount.Count == 0 && twitch[BotAccountKey] is JsonObject existing && ReferenceEquals(existing, botAccount))
        {
            twitch.Remove(BotAccountKey);
        }

        return changed;
    }

    private static bool RemoveLegacyUiFields(JsonObject root, ILogger? logger)
    {
        if (root["ui"] is not JsonObject ui)
        {
            return false;
        }

        var changed = false;
        changed |= RemoveIfPresent(ui, "showLogsPanel", logger, "ui.showLogsPanel удалён (заменён дашбордом)");
        changed |= RemoveIfPresent(ui, "showChatPanel", logger, "ui.showChatPanel удалён (заменён дашбордом)");
        changed |= RemoveIfPresent(ui, "currentChatViewMode", logger, "ui.currentChatViewMode удалён (legacy-режим чата убран)");
        return changed;
    }

    private static JsonObject EnsureObject(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[key] = created;
        return created;
    }

    private static bool MoveStringIfMissing(JsonObject source, string sourceKey, JsonObject target, string targetKey, ILogger? logger, string description)
    {
        if (!source.ContainsKey(sourceKey))
        {
            return false;
        }

        if (source[sourceKey] is not JsonValue value || !value.TryGetValue(out string? text))
        {
            source.Remove(sourceKey);
            return true;
        }

        if (HasNonEmptyString(target, targetKey) || string.IsNullOrEmpty(text))
        {
            source.Remove(sourceKey);
            return true;
        }

        target[targetKey] = text;
        source.Remove(sourceKey);
        logger?.LogInformation("Миграция настроек: {Description}", description);
        return true;
    }

    private static bool RemoveIfPresent(JsonObject parent, string key, ILogger? logger, string description)
    {
        if (!parent.ContainsKey(key))
        {
            return false;
        }

        parent.Remove(key);
        logger?.LogInformation("Миграция настроек: {Description}", description);
        return true;
    }

    private static bool HasNonEmptyString(JsonObject parent, string key)
    {
        return parent[key] is JsonValue value
               && value.TryGetValue(out string? text)
               && !string.IsNullOrEmpty(text);
    }
}
