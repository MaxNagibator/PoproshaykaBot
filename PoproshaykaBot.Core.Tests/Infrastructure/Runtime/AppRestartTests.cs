using PoproshaykaBot.Core.Infrastructure.Runtime;
using System.Diagnostics;

namespace PoproshaykaBot.Core.Tests.Infrastructure.Runtime;

[TestFixture]
public class AppRestartTests
{
    private static IEnumerable<TestCaseData> ArgumentCases()
    {
        yield return new TestCaseData(Array.Empty<string>(), new[] { "--restart-after", "42" })
            .SetName("BuildArguments_NoArguments_AddsOnlyRestartKey");

        yield return new TestCaseData(new[] { "--debug-channel", "mrbeast", "--allow-send" },
                new[] { "--debug-channel", "mrbeast", "--allow-send", "--restart-after", "42" })
            .SetName("BuildArguments_CarriesOriginalArguments");

        yield return new TestCaseData(new[] { "--restart-after", "7", "--debug-channel", "x" },
                new[] { "--debug-channel", "x", "--restart-after", "42" })
            .SetName("BuildArguments_ReplacesPreviousRestartKey");

        yield return new TestCaseData(new[] { "--RESTART-AFTER", "7" }, new[] { "--restart-after", "42" })
            .SetName("BuildArguments_RestartKeyIsCaseInsensitive");

        yield return new TestCaseData(new[] { "--debug-channel", "x", "--restart-after" },
                new[] { "--debug-channel", "x", "--restart-after", "42" })
            .SetName("BuildArguments_DropsDanglingRestartKey");

        yield return new TestCaseData(new[] { "--restart-after", "--allow-send" },
                new[] { "--allow-send", "--restart-after", "42" })
            .SetName("BuildArguments_KeepsArgumentAfterRestartKeyWithoutProcessId");

        yield return new TestCaseData(new[] { "--finalize-update", "--allow-send" },
                new[] { "--allow-send", "--restart-after", "42" })
            .SetName("BuildArguments_DropsFinalizeUpdate");
    }

    [TestCaseSource(nameof(ArgumentCases))]
    public void BuildArguments(string[] original, string[] expected)
    {
        Assert.That(AppRestart.BuildArguments(original, 42), Is.EqualTo(expected));
    }

    [Test]
    public void BuildArguments_RepeatedRestarts_KeepOneRestartKeyPointingAtLastProcess()
    {
        var first = AppRestart.BuildArguments(["--debug-channel", "x"], 100);
        var second = AppRestart.BuildArguments(first, 200);

        Assert.Multiple(() =>
        {
            Assert.That(second.Count(argument => string.Equals(argument, AppRestart.RestartAfterArgument, StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(AppRestart.FindPreviousProcessId(second), Is.EqualTo(200));
            Assert.That(second.Take(2), Is.EqualTo(new[] { "--debug-channel", "x" }));
        });
    }

    [TestCase(new string[0], null)]
    [TestCase(new[] { "--allow-send" }, null)]
    [TestCase(new[] { "--restart-after" }, null)]
    [TestCase(new[] { "--restart-after", "abc" }, null)]
    [TestCase(new[] { "--restart-after", "0" }, null)]
    [TestCase(new[] { "--restart-after", "-5" }, null)]
    [TestCase(new[] { "--debug-channel", "x", "--restart-after", "17" }, 17)]
    public void FindPreviousProcessId(string[] arguments, int? expected)
    {
        Assert.That(AppRestart.FindPreviousProcessId(arguments), Is.EqualTo(expected));
    }

    [TestCase(true, false, false, true)]
    [TestCase(true, true, false, false)]
    [TestCase(true, false, true, false)]
    [TestCase(false, false, false, false)]
    [TestCase(false, true, false, false)]
    public void ShouldLaunch_LeavesRelaunchToUpdateSwapAndSkipsFatalExit(bool requested, bool updateApplied, bool fatal, bool expected)
    {
        Assert.That(AppRestart.ShouldLaunch(requested, updateApplied, fatal), Is.EqualTo(expected));
    }

    [Test]
    public void WaitForExit_UnknownProcess_ReturnsImmediately()
    {
        Assert.That(AppRestart.WaitForExit(int.MaxValue, TimeSpan.FromSeconds(5)), Is.True);
    }

    [Test]
    public void WaitForExit_RunningProcess_TimesOut()
    {
        Assert.That(AppRestart.WaitForExit(Environment.ProcessId, TimeSpan.FromMilliseconds(50)), Is.False);
    }

    [Test]
    public void WaitForExit_ProcessExitingDuringWait_ReturnsOnceItExits()
    {
        using var process = Process.Start(new ProcessStartInfo(CommandShell(), "/c ping -n 2 127.0.0.1 >nul")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;

        var stopwatch = Stopwatch.StartNew();
        var exited = AppRestart.WaitForExit(process.Id, TimeSpan.FromSeconds(15));

        Assert.Multiple(() =>
        {
            Assert.That(exited, Is.True);
            Assert.That(process.HasExited, Is.True);
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public void TryLaunch_MissingExecutable_ReportsFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PoproshaykaBot-missing-{Guid.NewGuid():N}.exe");

        var launched = AppRestart.TryLaunch(path, ["--allow-send"], false, out var failure);

        Assert.Multiple(() =>
        {
            Assert.That(launched, Is.False);
            Assert.That(failure, Is.Not.Null);
        });
    }

    private static string CommandShell()
    {
        return Environment.GetEnvironmentVariable("ComSpec") ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
    }
}
