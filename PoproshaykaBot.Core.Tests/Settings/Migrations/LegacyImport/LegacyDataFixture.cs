namespace PoproshaykaBot.Core.Tests.Settings.Migrations.LegacyImport;

internal static class LegacyDataFixture
{
    public const string Settings = """{"twitch":{"channel":"bobito217"}}""";

    public const string SettingsWithTokens = """{"twitch":{"channel":"bobito217","botAccount":{"login":"thebot"},"accessToken":"legacy-access","refreshToken":"legacy-refresh"}}""";

    public const string Accounts = """{"botAccount":{"login":"thebot","accessToken":"secret-access","refreshToken":"secret-refresh"}}""";

    public const string LocalAccounts = """{"botAccount":{"login":"local","accessToken":"local-access","refreshToken":"local-refresh"}}""";

    public const string ObsChat = """{"fontSize":24}""";

    public const string UserStatistics = """[{"userName":"a"},{"userName":"b"},{"userName":"c"}]""";

    public const string BotStatistics = """{"totalMessagesProcessed":1234}""";

    public static string CreateDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void Delete(string directory)
    {
        try
        {
            Directory.Delete(directory, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void Write(string directory, string relativePath, string content)
    {
        var path = Path.Combine(directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public static void BlockBackupPaths(string directory, string fileName, string suffix)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var second = 0; second <= 3; second++)
        {
            var timestamp = DateTime.Now.AddSeconds(second).ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(Path.Combine(directory, $"{name}.{suffix}-{timestamp}{extension}"));
        }
    }

    public static void FillSource(string directory, bool nestedSettings)
    {
        var settingsFolder = nestedSettings ? "settings" : string.Empty;

        Write(directory, Path.Combine(settingsFolder, "settings.json"), Settings);
        Write(directory, Path.Combine(settingsFolder, "accounts.json"), Accounts);
        Write(directory, Path.Combine(settingsFolder, "obs-chat.json"), ObsChat);
        Write(directory, "users_statistics.json", UserStatistics);
        Write(directory, "bot_statistics.json", BotStatistics);
        Write(directory, "chat-zoom.txt", "1.25");
    }
}
