using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Update;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Update;
using PoproshaykaBot.Core.Update;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Update;

[TestFixture]
public sealed class UpdateApplyModeTests
{
    private const string PreparedVersion = "3.1.0.7";

    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "poproshayka-apply-mode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static string AssetName(string version) => $"PoproshaykaBot-v{version}-x64-portable.exe";

    private static UpdateCandidate Candidate(string version = PreparedVersion)
    {
        var name = AssetName(version);
        return new(Version.Parse(version), "v" + version, new(name, "https://example/download/" + name, 100, "application/octet-stream"), "https://example/notes");
    }

    private static PendingUpdate Pending(string version = PreparedVersion) => new(version, "staged.exe", "app.exe", "hash");

    private UpdateStore Store(UpdateApplyMode mode, bool allowFrameworkDependent = false, string? skippedVersion = null)
    {
        var store = new UpdateStore(null, Path.Combine(_root, "update.json"));
        store.Save(new()
        {
            ApplyMode = mode,
            AllowFrameworkDependentUpdate = allowFrameworkDependent,
            SkippedVersion = skippedVersion,
        });

        return store;
    }

    private static void SetStored(UpdateStore store, Action<UpdateSettings> change)
    {
        var settings = store.Load();
        change(settings);
        store.Save(settings);
    }

    private static UpdateCoordinator Coordinator(
        UpdateStore store,
        IUpdateInstaller installer,
        UpdateKind kind,
        IEventBus? eventBus = null,
        IGitHubReleaseClient? client = null)
    {
        var environment = Substitute.For<IUpdateEnvironment>();
        environment.Kind.Returns(kind);
        environment.CurrentVersion.Returns(Version.Parse("3.1.0.6"));
        environment.ArchitectureMoniker.Returns("x64");

        var checker = new UpdateChecker(client ?? Substitute.For<IGitHubReleaseClient>(), environment, NullLogger<UpdateChecker>.Instance);

        return new(checker, installer, store, eventBus ?? Substitute.For<IEventBus>(), environment, NullLogger<UpdateCoordinator>.Instance);
    }

    [TestCase(UpdateApplyMode.SilentOnExit, UpdateKind.Portable, false, true)]
    [TestCase(UpdateApplyMode.SilentOnExit, UpdateKind.FrameworkDependent, true, true)]
    [TestCase(UpdateApplyMode.SilentOnExit, UpdateKind.FrameworkDependent, false, false)]
    [TestCase(UpdateApplyMode.SilentOnExit, UpdateKind.Unsupported, true, false)]
    [TestCase(UpdateApplyMode.NotifyAndConfirm, UpdateKind.Portable, false, false)]
    [TestCase(UpdateApplyMode.NotifyAndConfirm, UpdateKind.FrameworkDependent, true, false)]
    public async Task TryPrepareSilentlyAsync_DownloadsOnlyInSilentModeOnUpdatableBuild(
        UpdateApplyMode mode,
        UpdateKind kind,
        bool allowFrameworkDependent,
        bool expectedPrepared)
    {
        var installer = Substitute.For<IUpdateInstaller>();
        using var coordinator = Coordinator(Store(mode, allowFrameworkDependent), installer, kind);

        var prepared = await coordinator.TryPrepareSilentlyAsync(Candidate(), CancellationToken.None);

        Assert.That(prepared, Is.EqualTo(expectedPrepared));
        await installer.Received(expectedPrepared ? 1 : 0)
            .PrepareAsync(Arg.Any<UpdateCandidate>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TryPrepareSilentlyAsync_DoesNotDownloadAgain_WhenSameVersionAlreadyPrepared()
    {
        var installer = Substitute.For<IUpdateInstaller>();
        installer.ReadPending().Returns(Pending());
        using var coordinator = Coordinator(Store(UpdateApplyMode.SilentOnExit), installer, UpdateKind.Portable);

        var prepared = await coordinator.TryPrepareSilentlyAsync(Candidate(), CancellationToken.None);

        Assert.That(prepared, Is.True);
        await installer.DidNotReceiveWithAnyArgs().PrepareAsync(default!, default, default);
        installer.DidNotReceiveWithAnyArgs().DiscardPending(default!);
    }

    [Test]
    public async Task TryPrepareSilentlyAsync_PublishesPrepared_AndSwallowsInstallerFailure()
    {
        var installer = Substitute.For<IUpdateInstaller>();
        var eventBus = Substitute.For<IEventBus>();
        using var coordinator = Coordinator(Store(UpdateApplyMode.SilentOnExit), installer, UpdateKind.Portable, eventBus);

        Assert.That(await coordinator.TryPrepareSilentlyAsync(Candidate(), CancellationToken.None), Is.True);
        await eventBus.Received(1).PublishAsync(Arg.Is<UpdatePrepared>(e => e.Version == PreparedVersion), Arg.Any<CancellationToken>());

        installer.PrepareAsync(Arg.Any<UpdateCandidate>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UpdateException("Нет прав на запись")));

        Assert.That(await coordinator.TryPrepareSilentlyAsync(Candidate("3.1.0.8"), CancellationToken.None), Is.False);
    }

    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    public async Task TryPrepareSilentlyAsync_DiscardsDownload_WhenSkippedOrModeTurnedOffDuringDownload(bool skip, bool turnModeOff, bool deleted)
    {
        var installer = Substitute.For<IUpdateInstaller>();
        var eventBus = Substitute.For<IEventBus>();
        var store = Store(UpdateApplyMode.SilentOnExit);
        installer.DiscardPending(PreparedVersion).Returns(deleted);
        installer.PrepareAsync(Arg.Any<UpdateCandidate>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                SetStored(store, settings =>
                {
                    settings.SkippedVersion = skip ? PreparedVersion : null;
                    settings.ApplyMode = turnModeOff ? UpdateApplyMode.NotifyAndConfirm : UpdateApplyMode.SilentOnExit;
                });
                return Task.CompletedTask;
            });
        using var coordinator = Coordinator(store, installer, UpdateKind.Portable, eventBus);

        var prepared = await coordinator.TryPrepareSilentlyAsync(Candidate(), CancellationToken.None);

        Assert.That(prepared, Is.False);
        installer.Received(1).DiscardPending(PreparedVersion);
        await eventBus.Received(deleted ? 1 : 0).PublishAsync(Arg.Any<UpdateDiscarded>(), Arg.Any<CancellationToken>());
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<UpdatePrepared>(), Arg.Any<CancellationToken>());
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DiscardPreparedUpdateAsync_PublishesOnlyWhenPendingFileIsGone(bool deleted)
    {
        var installer = Substitute.For<IUpdateInstaller>();
        var eventBus = Substitute.For<IEventBus>();
        installer.ReadPending().Returns(Pending());
        installer.DiscardPending(PreparedVersion).Returns(deleted);
        using var coordinator = Coordinator(Store(UpdateApplyMode.NotifyAndConfirm), installer, UpdateKind.Portable, eventBus);

        var discarded = await coordinator.DiscardPreparedUpdateAsync(CancellationToken.None);

        Assert.That(discarded, Is.EqualTo(deleted));
        await eventBus.Received(deleted ? 1 : 0).PublishAsync(Arg.Is<UpdateDiscarded>(e => e.Version == PreparedVersion), Arg.Any<CancellationToken>());
    }

    [Test]
    public void DiscardPending_ReturnsFalse_WhenPendingFileCannotBeDeleted()
    {
        var staging = Path.Combine(_root, "update");
        Directory.CreateDirectory(staging);
        var environment = Substitute.For<IUpdateEnvironment>();
        environment.StagingDirectory.Returns(staging);
        var installer = new UpdateInstaller(Substitute.For<IGitHubReleaseClient>(), environment, NullLogger<UpdateInstaller>.Instance);
        var staged = Path.Combine(staging, "new.exe");
        var pendingPath = Path.Combine(staging, UpdateInstaller.PendingFileName);
        File.WriteAllText(staged, "new");
        File.WriteAllText(pendingPath, JsonSerializer.Serialize(new PendingUpdate(PreparedVersion, staged, "app.exe", "hash"), JsonStoreOptions.Default));

        bool discarded;

        using (new FileStream(pendingPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            discarded = installer.DiscardPending(PreparedVersion);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discarded, Is.False);
            Assert.That(File.Exists(pendingPath), Is.True);
            Assert.That(File.Exists(staged), Is.True);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PrepareAsync_RemovesPartialDownload_OnCancellationOrFailure(bool cancelled)
    {
        var staging = Path.Combine(_root, "update");
        var environment = Substitute.For<IUpdateEnvironment>();
        environment.StagingDirectory.Returns(staging);
        environment.CurrentExecutablePath.Returns(Path.Combine(_root, "Bot.exe"));
        environment.Kind.Returns(UpdateKind.Portable);
        environment.ArchitectureMoniker.Returns("x64");
        var client = Substitute.For<IGitHubReleaseClient>();
        client.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                File.WriteAllText(call.ArgAt<string>(1), "half");
                return cancelled
                    ? Task.FromCanceled(new(true))
                    : Task.FromException(new HttpRequestException("обрыв"));
            });
        var installer = new UpdateInstaller(client, environment, NullLogger<UpdateInstaller>.Instance);

        Assert.CatchAsync(() => installer.PrepareAsync(Candidate(), null, CancellationToken.None));

        Assert.That(Directory.GetFiles(staging, "*.part"), Is.Empty);
    }

    [Test]
    public async Task TryPrepareSilentlyAsync_DiscardsAlreadyPreparedVersion_WhenItIsSkipped()
    {
        var installer = Substitute.For<IUpdateInstaller>();
        installer.ReadPending().Returns(Pending());
        using var coordinator = Coordinator(Store(UpdateApplyMode.SilentOnExit, skippedVersion: PreparedVersion), installer, UpdateKind.Portable);

        var prepared = await coordinator.TryPrepareSilentlyAsync(Candidate(), CancellationToken.None);

        Assert.That(prepared, Is.False);
        installer.Received(1).DiscardPending(PreparedVersion);
    }

    [Test]
    public async Task CheckNowAsync_KeepsSkipMadeDuringCheck_AndDropsSkippedCandidate()
    {
        var store = Store(UpdateApplyMode.SilentOnExit);
        var client = Substitute.For<IGitHubReleaseClient>();
        client.GetLatestReleaseAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                SetStored(store, settings => settings.SkippedVersion = PreparedVersion);

                return new ReleaseInfo("v" + PreparedVersion, "https://example/notes", "body", false, false,
                [
                    new(AssetName(PreparedVersion), "https://example/download/a", 100, "application/octet-stream"),
                    new("SHA256SUMS-x64.txt", "https://example/download/sums", 100, "text/plain"),
                ]);
            });
        using var coordinator = Coordinator(store, Substitute.For<IUpdateInstaller>(), UpdateKind.Portable, client: client);

        var candidate = await coordinator.CheckNowAsync(CancellationToken.None);

        var stored = store.Load();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(candidate, Is.Null);
            Assert.That(coordinator.LatestCandidate, Is.Null);
            Assert.That(stored.SkippedVersion, Is.EqualTo(PreparedVersion));
            Assert.That(stored.LastCheckUtc, Is.Not.Null);
        }
    }

    [Test]
    public void PrepareAsync_Throws_OnUnsupportedBuild()
    {
        var installer = Substitute.For<IUpdateInstaller>();
        using var coordinator = Coordinator(Store(UpdateApplyMode.SilentOnExit), installer, UpdateKind.Unsupported);

        Assert.ThrowsAsync<UpdateException>(() => coordinator.PrepareAsync(Candidate(), null, CancellationToken.None));
        _ = installer.DidNotReceiveWithAnyArgs().PrepareAsync(default!, default, default);
    }

    [TestCase(PreparedVersion, 1)]
    [TestCase("3.1.0.8", 0)]
    public void SkipVersion_DiscardsPreparedUpdate_OnlyOfThatVersion(string skipped, int expectedDiscards)
    {
        var installer = Substitute.For<IUpdateInstaller>();
        installer.ReadPending().Returns(Pending());
        installer.DiscardPending(Arg.Any<string>()).Returns(true);
        using var coordinator = Coordinator(Store(UpdateApplyMode.SilentOnExit), installer, UpdateKind.Portable);

        coordinator.SkipVersion(skipped);

        installer.Received(expectedDiscards).DiscardPending(Arg.Any<string>());
    }

    [Test]
    public async Task SkipVersion_DuringDownload_LeavesDiscardToTheDownload()
    {
        var installer = Substitute.For<IUpdateInstaller>();
        var download = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        installer.PrepareAsync(Arg.Any<UpdateCandidate>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(download.Task);
        using var coordinator = Coordinator(Store(UpdateApplyMode.SilentOnExit), installer, UpdateKind.Portable);

        var silent = coordinator.TryPrepareSilentlyAsync(Candidate(), CancellationToken.None);
        installer.ReadPending().Returns(Pending("3.1.0.5"));

        coordinator.SkipVersion(PreparedVersion);
        installer.DidNotReceiveWithAnyArgs().DiscardPending(default!);

        download.SetResult();

        Assert.That(await silent, Is.False);
        installer.Received(1).DiscardPending(PreparedVersion);
    }

    [Test]
    public void DiscardPending_RemovesOnlyTheNamedVersion_AndNothingOutsideStaging()
    {
        var staging = Path.Combine(_root, "update");
        Directory.CreateDirectory(staging);
        var outside = Path.Combine(_root, "outside.exe");
        File.WriteAllText(outside, "keep");

        var environment = Substitute.For<IUpdateEnvironment>();
        environment.StagingDirectory.Returns(staging);
        var installer = new UpdateInstaller(Substitute.For<IGitHubReleaseClient>(), environment, NullLogger<UpdateInstaller>.Instance);
        var pendingPath = Path.Combine(staging, UpdateInstaller.PendingFileName);

        File.WriteAllText(pendingPath, JsonSerializer.Serialize(new PendingUpdate(PreparedVersion, outside, "app.exe", "hash"), JsonStoreOptions.Default));
        Assert.That(installer.DiscardPending(PreparedVersion), Is.True);

        var staged = Path.Combine(staging, "new.exe");
        File.WriteAllText(staged, "new");
        File.WriteAllText(pendingPath, JsonSerializer.Serialize(new PendingUpdate(PreparedVersion, staged, "app.exe", "hash"), JsonStoreOptions.Default));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(installer.DiscardPending("3.1.0.8"), Is.False);
            Assert.That(File.Exists(pendingPath), Is.True);
            Assert.That(installer.DiscardPending(PreparedVersion), Is.True);
            Assert.That(File.Exists(outside), Is.True);
            Assert.That(File.Exists(staged), Is.False);
            Assert.That(File.Exists(pendingPath), Is.False);
        }
    }

    [TestCase("3.1.0.6")]
    [TestCase("3.1.0.5")]
    [TestCase("не версия")]
    public void TryApplyPending_DiscardsPendingNotNewerThanCurrent(string pendingVersion)
    {
        var executable = Path.Combine(_root, "Bot.exe");
        var staging = Path.Combine(_root, "update");
        var staged = Path.Combine(staging, "Bot.new.exe");
        var pendingPath = Path.Combine(staging, UpdateInstaller.PendingFileName);
        Directory.CreateDirectory(staging);
        File.WriteAllText(executable, "current");
        File.WriteAllText(staged, "older");
        File.WriteAllText(pendingPath, JsonSerializer.Serialize(new PendingUpdate(pendingVersion, staged, executable, "hash"), JsonStoreOptions.Default));

        var applied = UpdateApplier.TryApplyPending(staging, executable, Version.Parse("3.1.0.6"), NullLogger.Instance);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.False);
            Assert.That(File.ReadAllText(executable), Is.EqualTo("current"));
            Assert.That(File.Exists(executable + ".old"), Is.False);
            Assert.That(File.Exists(staged), Is.False);
            Assert.That(File.Exists(pendingPath), Is.False);
        }
    }

    [Test]
    public async Task BackgroundLoop_HandsFoundCandidateToSilentPrepare()
    {
        var candidate = Candidate();
        var handedOver = new TaskCompletionSource<UpdateCandidate>(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = Substitute.For<IUpdateCoordinator>();
        coordinator.Kind.Returns(UpdateKind.Portable);
        coordinator.IsUpdatable.Returns(true);
        coordinator.CheckNowAsync(Arg.Any<CancellationToken>()).Returns(candidate);
        coordinator.TryPrepareSilentlyAsync(Arg.Any<UpdateCandidate>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                handedOver.TrySetResult(call.Arg<UpdateCandidate>());
                return true;
            });

        using var service = new UpdateBackgroundService(coordinator,
            Store(UpdateApplyMode.SilentOnExit),
            new ShortDelaysOnlyTimeProvider(),
            NullLogger<UpdateBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        var received = await handedOver.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await service.StopAsync(CancellationToken.None);

        Assert.That(received, Is.SameAs(candidate));
    }

    private sealed class ShortDelaysOnlyTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime < TimeSpan.FromHours(1))
            {
                ThreadPool.QueueUserWorkItem(_ => callback(state));
            }

            return new IdleTimer();
        }

        private sealed class IdleTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
