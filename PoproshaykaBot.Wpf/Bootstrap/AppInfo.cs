using System.Reflection;

namespace PoproshaykaBot.Wpf.Bootstrap;

public static class AppInfo
{
    public const string Name = "PoproshaykaBot";
    public const string RepoSlug = "MaxNagibator/PoproshaykaBot";
    public const string RepositoryUrl = "https://github.com/" + RepoSlug;
    public const string ReleasesUrl = RepositoryUrl + "/releases";

    public const string LogFilePrefix = "bot_log_";
    public const string LogFileGlob = LogFilePrefix + "*.log";

    public const string SessionStartMarker = Name + ".Wpf запускается";

    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var assembly = typeof(AppInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return assembly.GetName().Version?.ToString() ?? "–";
        }

        var plusIndex = informational.IndexOf('+');
        return plusIndex >= 0 ? informational[..plusIndex] : informational;
    }
}
