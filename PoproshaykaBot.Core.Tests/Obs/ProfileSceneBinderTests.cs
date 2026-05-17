using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Infrastructure.Events.Obs;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Tests.Polls;

namespace PoproshaykaBot.Core.Tests.Obs;

[TestFixture]
public class ProfileSceneBinderTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "profile-scene-binder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _bus = new(NullLogger<InMemoryEventBus>.Instance);
        _profilesStore = new(filePath: Path.Combine(_tempDir, "broadcast-profiles.json"));
        _applier = Substitute.For<IChannelInformationApplier>();
        _applier.ApplyAsync(Arg.Any<BroadcastProfile>(), Arg.Any<CancellationToken>()).Returns(true);
        _clock = new() { UtcNow = new(2026, 5, 17, 12, 0, 0, TimeSpan.Zero) };
        _manager = new(_profilesStore, _applier, _bus, _clock, NullLogger<BroadcastProfilesManager>.Instance);
        _obs = Substitute.For<IObsSceneController>();
        _obs.IsConnected.Returns(true);
        _obsStore = new(_bus, filePath: Path.Combine(_tempDir, "obs-integration.json"));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_binder != null)
        {
            await _binder.DisposeAsync();
        }

        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan EchoWindow = TimeSpan.FromSeconds(5);

    private string _tempDir = null!;
    private InMemoryEventBus _bus = null!;
    private BroadcastProfilesStore _profilesStore = null!;
    private IChannelInformationApplier _applier = null!;
    private TestTimeProvider _clock = null!;
    private BroadcastProfilesManager _manager = null!;
    private IObsSceneController _obs = null!;
    private ObsIntegrationStore _obsStore = null!;
    private ProfileSceneBinder? _binder;

    [Test]
    public async Task ProfileApplied_SceneSyncOff_DoesNotSwitchScene()
    {
        CreateBinder(applySceneOnProfile: false);

        await _bus.PublishAsync(new BroadcastProfileApplying(Profile("Game")));

        await _obs.DidNotReceiveWithAnyArgs().SetCurrentSceneAsync(null!, CancellationToken.None);
    }

    [Test]
    public async Task ProfileApplied_SceneSyncOn_Connected_SwitchesScene()
    {
        CreateBinder(applySceneOnProfile: true);
        var sceneSet = new TaskCompletionSource();
        _obs.When(o => o.SetCurrentSceneAsync("Игра", Arg.Any<CancellationToken>()))
            .Do(_ => sceneSet.TrySetResult());

        await _bus.PublishAsync(new BroadcastProfileApplying(Profile("Игра")));

        await sceneSet.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await _obs.Received(1).SetCurrentSceneAsync("Игра", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProfileApplied_ObsDisconnected_DoesNotSwitchScene()
    {
        _obs.IsConnected.Returns(false);
        CreateBinder(applySceneOnProfile: true);

        await _bus.PublishAsync(new BroadcastProfileApplying(Profile("Игра")));

        await _obs.DidNotReceiveWithAnyArgs().SetCurrentSceneAsync(null!, CancellationToken.None);
    }

    [Test]
    public async Task ProfileApplied_EmptyScene_DoesNotSwitchScene()
    {
        CreateBinder(applySceneOnProfile: true);

        await _bus.PublishAsync(new BroadcastProfileApplying(Profile(string.Empty)));

        await _obs.DidNotReceiveWithAnyArgs().SetCurrentSceneAsync(null!, CancellationToken.None);
    }

    [Test]
    public async Task SceneChanged_MapsToInactiveProfile_AppliesProfile()
    {
        var profile = new BroadcastProfile { Name = "P", ObsSceneName = "Игра" };
        _manager.Upsert(profile);
        CreateBinder(applyProfileOnScene: true);

        await _bus.PublishAsync(new ObsCurrentProgramSceneChanged("Игра"));

        var applied = await WaitForApplyAsync();
        Assert.That(applied, Is.True);
        await _applier.Received(1)
            .ApplyAsync(Arg.Is<BroadcastProfile>(p => p.Id == profile.Id), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SceneChanged_MapsToActiveProfile_Skipped()
    {
        var profile = new BroadcastProfile { Name = "P", ObsSceneName = "Игра" };
        _manager.Upsert(profile);
        _profilesStore.Mutate(bp => bp.LastAppliedProfileId = profile.Id);
        CreateBinder(applyProfileOnScene: true);

        await _bus.PublishAsync(new ObsCurrentProgramSceneChanged("Игра"));

        Assert.That(await WaitForApplyAsync(), Is.False);
    }

    [Test]
    public async Task SceneChanged_UnknownScene_DoesNotApply()
    {
        _manager.Upsert(new() { Name = "P", ObsSceneName = "Игра" });
        CreateBinder(applyProfileOnScene: true);

        await _bus.PublishAsync(new ObsCurrentProgramSceneChanged("Заставка"));

        Assert.That(await WaitForApplyAsync(), Is.False);
    }

    [Test]
    public async Task SceneChanged_ProfileSyncOff_Ignored()
    {
        _manager.Upsert(new() { Name = "P", ObsSceneName = "Игра" });
        CreateBinder(applyProfileOnScene: false);

        await _bus.PublishAsync(new ObsCurrentProgramSceneChanged("Игра"));

        Assert.That(await WaitForApplyAsync(), Is.False);
    }

    [Test]
    public async Task SceneChanged_EchoOfOwnSwitch_Suppressed()
    {
        var profile = new BroadcastProfile { Name = "P", ObsSceneName = "Игра" };
        _manager.Upsert(profile);
        CreateBinder(true, true);

        await _bus.PublishAsync(new BroadcastProfileApplying(Profile("Игра")));

        await _bus.PublishAsync(new ObsCurrentProgramSceneChanged("Игра"));

        Assert.That(await WaitForApplyAsync(), Is.False);
    }

    [Test]
    public async Task SceneChanged_AfterEchoWindowExpires_AppliesProfile()
    {
        var profile = new BroadcastProfile { Name = "P", ObsSceneName = "Игра" };
        _manager.Upsert(profile);
        CreateBinder(true, true);

        await _bus.PublishAsync(new BroadcastProfileApplying(Profile("Игра")));

        _clock.UtcNow = _clock.UtcNow.Add(EchoWindow).AddSeconds(1);

        await _bus.PublishAsync(new ObsCurrentProgramSceneChanged("Игра"));

        Assert.That(await WaitForApplyAsync(), Is.True);
    }

    [Test]
    public async Task ProfileApplied_SceneSwitchFails_DoesNotSuppressSubsequentSwitch()
    {
        var profile = new BroadcastProfile { Name = "P", ObsSceneName = "Игра" };
        _manager.Upsert(profile);
        var switchAttempted = new TaskCompletionSource();
        _obs.SetCurrentSceneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                switchAttempted.TrySetResult();
                return Task.FromException(new InvalidOperationException("OBS отвалился"));
            });

        CreateBinder(true, true);

        await _bus.PublishAsync(new BroadcastProfileApplying(Profile("Игра")));
        await switchAttempted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(100); // даём фоновому catch снять подавление

        await _bus.PublishAsync(new ObsCurrentProgramSceneChanged("Игра"));

        Assert.That(await WaitForApplyAsync(), Is.True);
    }

    private static BroadcastProfile Profile(string scene)
    {
        return new() { Name = "P", ObsSceneName = scene };
    }

    private void CreateBinder(bool applySceneOnProfile = false, bool applyProfileOnScene = false)
    {
        _obsStore.Save(new()
        {
            Enabled = true,
            ApplySceneOnProfile = applySceneOnProfile,
            ApplyProfileOnScene = applyProfileOnScene,
        });

        _binder = new(_profilesStore,
            _manager,
            _obs,
            _obsStore,
            _bus,
            _clock,
            NullLogger<ProfileSceneBinder>.Instance,
            Debounce,
            EchoWindow);
    }

    private async Task<bool> WaitForApplyAsync()
    {
        for (var i = 0; i < 40; i++)
        {
            if (_applier.ReceivedCalls().Any(c => string.Equals(c.GetMethodInfo().Name, nameof(IChannelInformationApplier.ApplyAsync), StringComparison.Ordinal)))
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }
}
