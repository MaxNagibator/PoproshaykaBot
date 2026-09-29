namespace PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

internal static class LegacyDataCatalog
{
    public const string SettingsFolderName = "settings";
    public const string SettingsFileName = "settings.json";
    public const string AccountsFileName = "accounts.json";
    public const string UserStatisticsFileName = "users_statistics.json";
    public const string BotStatisticsFileName = "bot_statistics.json";
    public const string StreamSessionsFileName = "stream_sessions.json";
    public const string PollsHistoryFileName = "polls-history.json";
    public const string UiPreferencesFileName = "ui-preferences.toml";

    public static readonly string[] RootFiles =
    [
        UserStatisticsFileName,
        BotStatisticsFileName,
        StreamSessionsFileName,
        PollsHistoryFileName,
        "unknown_commands.txt",
        "chat-blockers.txt",
        "chat-zoom.txt",
    ];

    public static readonly string[] SettingsFiles =
    [
        SettingsFileName,
        AccountsFileName,
        "broadcast-profiles.json",
        "dashboard-layout.json",
        "obs-chat.json",
        "obs-integration.json",
        "polls.json",
        "recent-categories.json",
        "update.json",
        UiPreferencesFileName,
    ];

    public static readonly string[] StoreOwnedSettingsFiles =
    [
        .. SettingsFiles.Where(name => !string.Equals(name, UiPreferencesFileName, StringComparison.OrdinalIgnoreCase)),
    ];

    public static List<LegacyDataFile> Enumerate(string sourceDirectory)
    {
        var files = new List<LegacyDataFile>();

        foreach (var name in RootFiles)
        {
            var path = Path.Combine(sourceDirectory, name);

            if (TryDescribe(name, path, name) is { } file)
            {
                files.Add(file);
            }
        }

        foreach (var name in SettingsFiles)
        {
            var relativeTarget = Path.Combine(SettingsFolderName, name);
            var path = ResolveSettingsFile(sourceDirectory, name);

            if (path is not null && TryDescribe(name, path, relativeTarget) is { } file)
            {
                files.Add(file);
            }
        }

        return files;
    }

    public static string? ResolveSettingsFile(string sourceDirectory, string fileName)
    {
        var nested = Path.Combine(sourceDirectory, SettingsFolderName, fileName);

        if (File.Exists(nested))
        {
            return nested;
        }

        var flat = Path.Combine(sourceDirectory, fileName);

        return File.Exists(flat) ? flat : null;
    }

    public static bool CarriesTokens(string fileName)
    {
        return string.Equals(fileName, AccountsFileName, StringComparison.OrdinalIgnoreCase)
               || string.Equals(fileName, SettingsFileName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasFlatSettingsFiles(string sourceDirectory)
    {
        return SettingsFiles.Any(name => File.Exists(Path.Combine(sourceDirectory, name)));
    }

    private static LegacyDataFile? TryDescribe(string name, string path, string relativeTargetPath)
    {
        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
            {
                return null;
            }

            return new(name, path, relativeTargetPath, info.Length, info.LastWriteTimeUtc);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
