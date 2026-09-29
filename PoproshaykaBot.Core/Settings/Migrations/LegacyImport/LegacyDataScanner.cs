using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Persistence;

namespace PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

public static class LegacyDataScanner
{
    public static LegacyDataSummary? Inspect(string path, ILogger? logger = null)
    {
        return Inspect(path, AppPaths.BaseDirectory, AppPaths.DefaultBaseDirectory, logger);
    }

    public static IReadOnlyList<LegacyDataSummary> Scan(ILogger? logger = null)
    {
        return Scan(AppPaths.BaseDirectory, AppPaths.DefaultBaseDirectory, logger);
    }

    internal static IReadOnlyList<LegacyDataSummary> Scan(string currentBaseDirectory, string defaultBaseDirectory, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(currentBaseDirectory);
        ArgumentException.ThrowIfNullOrEmpty(defaultBaseDirectory);

        var candidates = new List<LegacyDataSummary>();

        if (LegacyDataCatalog.HasFlatSettingsFiles(currentBaseDirectory)
            && Inspect(currentBaseDirectory, currentBaseDirectory, defaultBaseDirectory, logger) is { } inPlace)
        {
            candidates.Add(inPlace);
        }

        if (!PathComparison.AreEqual(currentBaseDirectory, defaultBaseDirectory)
            && Inspect(defaultBaseDirectory, currentBaseDirectory, defaultBaseDirectory, logger) is { } installed)
        {
            candidates.Add(installed);
        }

        if (candidates.Count > 0)
        {
            logger?.LogInformation("Импорт данных: найдено источников для переноса – {Count}", candidates.Count);
        }

        return candidates;
    }

    internal static LegacyDataSummary? Inspect(
        string path,
        string currentBaseDirectory,
        string defaultBaseDirectory,
        ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            logger?.LogWarning(exception, "Импорт данных: путь {Path} не удалось разобрать", path);
            return null;
        }

        if (!Directory.Exists(fullPath))
        {
            return null;
        }

        var files = LegacyDataCatalog.Enumerate(fullPath);

        if (files.Count == 0)
        {
            return null;
        }

        var settingsRoot = LegacyDataReader.TryRead(
            LegacyDataCatalog.ResolveSettingsFile(fullPath, LegacyDataCatalog.SettingsFileName),
            logger);

        var accountsRoot = LegacyDataReader.TryRead(
            LegacyDataCatalog.ResolveSettingsFile(fullPath, LegacyDataCatalog.AccountsFileName),
            logger);

        var twitch = LegacyDataReader.Property(settingsRoot, "twitch");

        var botLogin = LegacyDataReader.ReadString(LegacyDataReader.Property(accountsRoot, "botAccount"), "login")
                       ?? LegacyDataReader.ReadString(LegacyDataReader.Property(twitch, "botAccount"), "login")
                       ?? LegacyDataReader.ReadString(twitch, "botUsername");

        var users = LegacyDataReader.TryRead(Path.Combine(fullPath, LegacyDataCatalog.UserStatisticsFileName), logger);
        var botStatistics = LegacyDataReader.TryRead(Path.Combine(fullPath, LegacyDataCatalog.BotStatisticsFileName), logger);

        return new()
        {
            SourcePath = fullPath,
            Kind = Classify(fullPath, currentBaseDirectory, defaultBaseDirectory),
            HasMonolithicSettings = File.Exists(Path.Combine(fullPath, LegacyDataCatalog.SettingsFileName)),
            HasSettingsDirectory = Directory.Exists(Path.Combine(fullPath, LegacyDataCatalog.SettingsFolderName)),
            Channel = LegacyDataReader.ReadString(twitch, "channel"),
            BotLogin = botLogin,
            UserStatisticsCount = LegacyDataReader.CountItems(users),
            TotalMessagesProcessed = LegacyDataReader.ReadUInt64(botStatistics, "totalMessagesProcessed"),
            HasOAuthTokens = LegacyDataReader.HasTokens(accountsRoot) || LegacyDataReader.HasTokens(twitch),
            Files = files,
        };
    }

    private static LegacyDataSourceKind Classify(string fullPath, string currentBaseDirectory, string defaultBaseDirectory)
    {
        if (PathComparison.AreEqual(fullPath, currentBaseDirectory))
        {
            return LegacyDataSourceKind.CurrentBaseDirectory;
        }

        if (PathComparison.AreEqual(fullPath, defaultBaseDirectory))
        {
            return LegacyDataSourceKind.DefaultAppDataDirectory;
        }

        return LegacyDataSourceKind.SelectedFolder;
    }
}
