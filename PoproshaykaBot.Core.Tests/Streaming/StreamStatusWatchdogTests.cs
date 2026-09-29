using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.Core.Tests.Streaming;

[TestFixture]
public sealed class StreamStatusWatchdogTests
{
    [SetUp]
    public void SetUp()
    {
        _stream = Substitute.For<IStreamStatus>();
        _watchdog = new(_stream, TimeProvider.System, PollInterval, NullLogger<StreamStatusWatchdog>.Instance);
    }

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(40);

    private static readonly IProgress<string> NullProgress = new Progress<string>(_ => { });

    private IStreamStatus _stream = null!;
    private StreamStatusWatchdog _watchdog = null!;

    [Test]
    public async Task StartStop_DoesNotLeakLoopTask()
    {
        await _watchdog.StartAsync(NullProgress, CancellationToken.None);
        await _watchdog.StopAsync(NullProgress, CancellationToken.None);

        Assert.Pass("Start/Stop completed without leaking the loop task");
    }

    [Test]
    public async Task RunLoop_CallsRefreshLiveSnapshotOnlyWhenOnline()
    {
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stream.CurrentStatus.Returns(StreamStatus.Online);
        _stream.RefreshLiveSnapshotAsync()
            .Returns(_ =>
            {
                refreshed.TrySetResult();
                return Task.CompletedTask;
            });

        await _watchdog.StartAsync(NullProgress, CancellationToken.None);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await _watchdog.StopAsync(NullProgress, CancellationToken.None);

        await _stream.Received().RefreshLiveSnapshotAsync();
    }

    [Test]
    public async Task RunLoop_SkipsRefreshWhenOffline_WhileStillTicking()
    {
        var firstOnlineRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCount = 0;

        _stream.CurrentStatus.Returns(StreamStatus.Online);
        _stream.RefreshLiveSnapshotAsync()
            .Returns(_ =>
            {
                Interlocked.Increment(ref refreshCount);
                firstOnlineRefresh.TrySetResult();
                return Task.CompletedTask;
            });

        await _watchdog.StartAsync(NullProgress, CancellationToken.None);

        await firstOnlineRefresh.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var snapshot = Volatile.Read(ref refreshCount);

        _stream.CurrentStatus.Returns(StreamStatus.Offline);

        await Task.Delay(PollInterval * 5);

        await _watchdog.StopAsync(NullProgress, CancellationToken.None);

        Assert.That(Volatile.Read(ref refreshCount), Is.EqualTo(snapshot),
            "После переключения на Offline loop обязан тикать вхолостую – RefreshLiveSnapshotAsync не должен вызываться");
    }
}
