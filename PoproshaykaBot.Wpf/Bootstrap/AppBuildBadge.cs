using KeepShell.Bootstrap;
using PoproshaykaBot.Core.Update;
using PoproshaykaBot.Wpf.Infrastructure;
using System.IO;

namespace PoproshaykaBot.Wpf.Bootstrap;

public static class AppBuildBadge
{
    public const string ReleaseHint = "собрана из релиза";
    public const string MissingStampHint = "отметки сборки рядом нет";

    private static readonly BuildStamp? Stamp = BuildStamp.Read(Environment.ProcessPath, AppInfo.Version);

    public static BuildBadge Current { get; } = Build(Stamp, new UpdateEnvironment().Kind, ResolveFolder());

    public static string? StartupLine { get; } = Describe(Stamp);

    public static BuildBadge Build(BuildStamp? stamp, UpdateKind kind, string? folder)
    {
        var noStampHint = kind is UpdateKind.Unsupported ? MissingStampHint : ReleaseHint;

        return BuildBadge.Build(stamp, AppInfo.Version, folder, noStampHint);
    }

    public static string? Describe(BuildStamp? stamp)
    {
        if (stamp is null)
        {
            return null;
        }

        var parts = new List<string>(4);

        if (BuildStamp.Short(stamp.Commit) is { Length: > 0 } commit)
        {
            parts.Add($"коммит {commit}");
        }

        if (stamp.Branch is { Length: > 0 } branch)
        {
            parts.Add($"ветка {branch}");
        }

        if (stamp.Dirty)
        {
            parts.Add("дерево изменено");
        }

        if (stamp.BuiltAt is { } built)
        {
            parts.Add($"собрана {built.LocalDateTime.ToString("dd.MM.yyyy HH:mm", UiCulture.Russian)}");
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string? ResolveFolder()
    {
        return Environment.ProcessPath is { Length: > 0 } exe ? Path.GetDirectoryName(exe) : null;
    }
}
