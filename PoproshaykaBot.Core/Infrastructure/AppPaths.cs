using System.Reflection;

namespace PoproshaykaBot.Core.Infrastructure;

public static class AppPaths
{
    public const string BaseDirectoryEnvironmentVariable = "POPROSHAYKA_BASE_DIR";

    private const string PortableMetadataKey = "PortableMode";
    private const string PortableMarkerFileName = "portable.flag";
    private const string AppDataFolderName = "PoproshaykaBot";
    private const string SettingsFolderName = "settings";

    private static readonly Lazy<string?> _baseDirectoryOverride = new(ResolveBaseDirectoryOverride);
    private static readonly Lazy<bool> _isPortable = new(ResolvePortable);
    private static readonly Lazy<string> _baseDirectory = new(ResolveBaseDirectory);
    private static readonly Lazy<string> _defaultBaseDirectory = new(ResolveDefaultBaseDirectory);

    public static bool IsPortable => _isPortable.Value;

    public static bool IsBaseDirectoryOverridden => _baseDirectoryOverride.Value is not null;

    public static string BaseDirectory => _baseDirectory.Value;

    public static string DefaultBaseDirectory => _defaultBaseDirectory.Value;

    public static string SettingsDirectory => Path.Combine(BaseDirectory, SettingsFolderName);

    public static string Combine(string segment)
    {
        return Path.Combine(BaseDirectory, segment);
    }

    public static string Combine(string segment1, string segment2)
    {
        return Path.Combine(BaseDirectory, segment1, segment2);
    }

    public static string Combine(string segment1, string segment2, string segment3)
    {
        return Path.Combine(BaseDirectory, segment1, segment2, segment3);
    }

    public static string SettingsFile(string fileName)
    {
        return Path.Combine(BaseDirectory, SettingsFolderName, fileName);
    }

    private static string? ResolveBaseDirectoryOverride()
    {
        var raw = Environment.GetEnvironmentVariable(BaseDirectoryEnvironmentVariable);
        return string.IsNullOrWhiteSpace(raw) ? null : Path.GetFullPath(raw);
    }

    private static bool ResolvePortable()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppPaths).Assembly;

        var hasMetadata = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(a => string.Equals(a.Key, PortableMetadataKey, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(a.Value, "true", StringComparison.OrdinalIgnoreCase));

        if (hasMetadata)
        {
            return true;
        }

        var executableDirectory = GetExecutableDirectory();

        return File.Exists(Path.Combine(executableDirectory, PortableMarkerFileName));
    }

    private static string ResolveBaseDirectory()
    {
        if (_baseDirectoryOverride.Value is { } overridePath)
        {
            return overridePath;
        }

        return ResolveDefaultBaseDirectory();
    }

    private static string ResolveDefaultBaseDirectory()
    {
        if (IsPortable)
        {
            return GetExecutableDirectory();
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);
    }

    private static string GetExecutableDirectory()
    {
        var processPath = Environment.ProcessPath;

        if (!string.IsNullOrEmpty(processPath))
        {
            var directory = Path.GetDirectoryName(processPath);

            if (!string.IsNullOrEmpty(directory))
            {
                return directory;
            }
        }

        return AppContext.BaseDirectory;
    }
}
