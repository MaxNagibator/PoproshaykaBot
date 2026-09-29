using PoproshaykaBot.Core.Infrastructure;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PoproshaykaBot.Core.Update;

public sealed class UpdateEnvironment : IUpdateEnvironment
{
    private const string UpdatableMetadataKey = "UpdatableBuild";
    private const string RepositoryMetadataKey = "GitHubRepository";
    private const string DefaultRepositorySlug = "MaxNagibator/PoproshaykaBot";

    public UpdateKind Kind { get; } = ResolveKind();

    public string RepositorySlug { get; } = ResolveRepositorySlug();

    public Version CurrentVersion { get; } = ResolveCurrentVersion();

    public int RuntimeMajor { get; } = ResolveRuntimeMajor();

    public string ArchitectureMoniker { get; } = ResolveArchitectureMoniker();

    public string CurrentExecutablePath => Environment.ProcessPath
                                           ?? throw new UpdateException("Не удалось определить путь к исполняемому файлу.");

    public string StagingDirectory => UpdatePaths.StagingDirectory(CurrentExecutablePath);

    private static UpdateKind ResolveKind()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateEnvironment).Assembly;

        var isUpdatableBuild = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(a => string.Equals(a.Key, UpdatableMetadataKey, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(a.Value, "true", StringComparison.OrdinalIgnoreCase));

        if (!isUpdatableBuild)
        {
            return UpdateKind.Unsupported;
        }

        return AppPaths.IsPortable ? UpdateKind.Portable : UpdateKind.FrameworkDependent;
    }

    private static string ResolveRepositorySlug()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateEnvironment).Assembly;

        var slug = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, RepositoryMetadataKey, StringComparison.OrdinalIgnoreCase))
            ?
            .Value;

        return string.IsNullOrWhiteSpace(slug) ? DefaultRepositorySlug : slug.Trim();
    }

    internal static Version ResolveCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateEnvironment).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?
            .InformationalVersion;

        if (UpdateVersioning.TryParseTag(informational, out var fromInformational))
        {
            return fromInformational;
        }

        return assembly.GetName().Version ?? new Version(0, 0);
    }

    private static int ResolveRuntimeMajor()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(UpdateEnvironment).Assembly;

        var frameworkName = assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()
            ?
            .FrameworkName;

        return UpdateRuntime.ParseMajor(frameworkName) ?? Environment.Version.Major;
    }

    private static string ResolveArchitectureMoniker()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            var other => other.ToString().ToLowerInvariant(),
        };
    }
}
