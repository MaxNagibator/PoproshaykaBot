using KeepShell.Bootstrap;
using PoproshaykaBot.Core.Update;
using PoproshaykaBot.Wpf.Bootstrap;

namespace PoproshaykaBot.Wpf.Tests.Shell;

[TestFixture]
public class AppBuildBadgeTests
{
    private const string Folder = @"C:\Sources\PoproshaykaBot\publish\Wpf";

    private static readonly DateTimeOffset Built = new(
        2026,
        9,
        20,
        14,
        33,
        0,
        TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 9, 20, 14, 33, 0)));

    [TestCase(UpdateKind.Unsupported, AppBuildBadge.MissingStampHint)]
    [TestCase(UpdateKind.Portable, AppBuildBadge.ReleaseHint)]
    [TestCase(UpdateKind.FrameworkDependent, AppBuildBadge.ReleaseHint)]
    public void Explains_a_missing_stamp_by_the_kind_of_build(UpdateKind kind, string hint)
    {
        var badge = AppBuildBadge.Build(null, kind, Folder);

        Assert.Multiple(() =>
        {
            Assert.That(badge.Value, Is.EqualTo(AppInfo.Version));
            Assert.That(badge.Tooltip, Does.Contain(hint));
            Assert.That(badge.Tooltip, Does.Contain($"{BuildBadge.FolderPrefix} {Folder}"));
        });
    }

    [TestCase(UpdateKind.Unsupported)]
    [TestCase(UpdateKind.Portable)]
    public void Keeps_the_hint_out_of_a_stamped_build(UpdateKind kind)
    {
        var badge = AppBuildBadge.Build(Stamp(), kind, Folder);

        Assert.Multiple(() =>
        {
            Assert.That(badge.Tooltip, Does.Not.Contain(AppBuildBadge.MissingStampHint));
            Assert.That(badge.Tooltip, Does.Not.Contain(AppBuildBadge.ReleaseHint));
        });
    }

    [Test]
    public void Shows_version_and_short_commit_of_the_stamp()
    {
        var badge = AppBuildBadge.Build(Stamp(), UpdateKind.Unsupported, Folder);

        Assert.Multiple(() =>
        {
            Assert.That(badge.Value, Is.EqualTo("3.2.1+abc1234"));
            Assert.That(badge.Tooltip, Does.StartWith("3.2.1+abc1234def5678"));
            Assert.That(badge.Tooltip, Does.Contain($"{BuildBadge.BuiltPrefix} 20.09.2026 14:33"));
            Assert.That(badge.Tooltip, Does.Contain($"{BuildBadge.FolderPrefix} {Folder}"));
        });
    }

    [Test]
    public void Marks_a_dirty_tree_in_the_value()
    {
        var badge = AppBuildBadge.Build(Stamp(dirty: true), UpdateKind.Unsupported, Folder);

        Assert.Multiple(() =>
        {
            Assert.That(badge.Value, Is.EqualTo($"3.2.1+abc1234, {BuildBadge.DirtyMark}"));
            Assert.That(badge.Tooltip, Does.Contain(BuildBadge.DirtyHint));
        });
    }

    [Test]
    public void Describes_a_stamp_for_the_startup_banner()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AppBuildBadge.Describe(Stamp(dirty: true)),
                Is.EqualTo("коммит abc1234, ветка master, дерево изменено, собрана 20.09.2026 14:33"));

            Assert.That(AppBuildBadge.Describe(Stamp()),
                Is.EqualTo("коммит abc1234, ветка master, собрана 20.09.2026 14:33"));

            Assert.That(AppBuildBadge.Describe(null), Is.Null);
        });
    }

    private static BuildStamp Stamp(bool dirty = false)
    {
        return new()
        {
            Commit = "abc1234def5678",
            Branch = "master",
            Dirty = dirty,
            Version = "3.2.1",
            BuiltAt = Built,
            ExeLength = 1024,
        };
    }
}
