using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Polls.Handlers;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Tests.Polls;

[TestFixture]
public class PollAutoTriggerHandlerTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "poll-auto-trigger-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _polls = new();
        _pollsStore = Substitute.For<PollsStore>(NullLogger<PollsStore>.Instance, null, null);
        _pollsStore.Load().Returns(_polls);

        _profilesManager = Substitute.For<PollProfilesManager>(new PollsStore(filePath: Path.Combine(_tempDir, "polls.json")),
            Substitute.For<IEventBus>(),
            NullLogger<PollProfilesManager>.Instance);

        _controller = Substitute.For<IPollController>();
        _store = Substitute.For<PollSnapshotStore>();
        _store.Current.Returns((PollSnapshot?)null);
        _eventBus = Substitute.For<IEventBus>();

        _clock = new() { UtcNow = new(2026, 4, 24, 10, 0, 0, TimeSpan.Zero) };

        _handler = new(_profilesManager, _controller, _store, _pollsStore, _eventBus, _clock, NullLogger<PollAutoTriggerHandler>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    private string _tempDir = null!;
    private PollsStore _pollsStore = null!;
    private PollsSettings _polls = null!;
    private PollProfilesManager _profilesManager = null!;
    private IPollController _controller = null!;
    private PollSnapshotStore _store = null!;
    private IEventBus _eventBus = null!;
    private PollAutoTriggerHandler _handler = null!;
    private TestTimeProvider _clock = null!;
    private DateTime _now => _clock.UtcNow.UtcDateTime;

    private static PollProfile Profile(PollAutoTriggerEvent @event, Guid? broadcastProfileId = null, int cooldown = 0)
    {
        return new()
        {
            Name = "Auto",
            Title = "Q?",
            Choices = ["A", "B"],
            DurationSeconds = 60,
            AutoTrigger = new()
            {
                Event = @event,
                BroadcastProfileId = broadcastProfileId,
                CooldownMinutes = cooldown,
            },
        };
    }

    [Test]
    public async Task StreamWentOnline_ProfileMatches_StartsPoll()
    {
        var profile = Profile(PollAutoTriggerEvent.StreamOnline);
        _profilesManager.GetAll().Returns([profile]);

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _controller.Received(1).StartAsync(profile, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StreamWentOnline_ProfileNoneEvent_DoesNotStart()
    {
        var profile = Profile(PollAutoTriggerEvent.None);
        _profilesManager.GetAll().Returns([profile]);

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _controller.DidNotReceiveWithAnyArgs().StartAsync(null!, CancellationToken.None);
    }

    [Test]
    public async Task BroadcastProfileApplied_MatchingId_Starts()
    {
        var broadcastProfile = new BroadcastProfile { Name = "Gaming" };
        var pollProfile = Profile(PollAutoTriggerEvent.BroadcastProfileApplied, broadcastProfile.Id);
        _profilesManager.GetAll().Returns([pollProfile]);

        await _handler.HandleAsync(new BroadcastProfileApplied(broadcastProfile), CancellationToken.None);

        await _controller.Received(1).StartAsync(pollProfile, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task BroadcastProfileApplied_NullIdMatchesAny_Starts()
    {
        var broadcastProfile = new BroadcastProfile { Name = "X" };
        var pollProfile = Profile(PollAutoTriggerEvent.BroadcastProfileApplied);
        _profilesManager.GetAll().Returns([pollProfile]);

        await _handler.HandleAsync(new BroadcastProfileApplied(broadcastProfile), CancellationToken.None);

        await _controller.Received(1).StartAsync(pollProfile, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task BroadcastProfileApplied_NonMatchingId_DoesNotStart()
    {
        var appliedProfile = new BroadcastProfile { Name = "X" };
        var pollProfile = Profile(PollAutoTriggerEvent.BroadcastProfileApplied, Guid.NewGuid());
        _profilesManager.GetAll().Returns([pollProfile]);

        await _handler.HandleAsync(new BroadcastProfileApplied(appliedProfile), CancellationToken.None);

        await _controller.DidNotReceiveWithAnyArgs().StartAsync(null!, CancellationToken.None);
    }

    [Test]
    public async Task KillSwitch_Today_BlocksTrigger()
    {
        var profile = Profile(PollAutoTriggerEvent.StreamOnline);
        _profilesManager.GetAll().Returns([profile]);
        _polls.AutoTriggerKillSwitchDateUtc = _now.Date;

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _controller.DidNotReceiveWithAnyArgs().StartAsync(null!, CancellationToken.None);
    }

    [Test]
    public async Task KillSwitch_Yesterday_DoesNotBlock()
    {
        var profile = Profile(PollAutoTriggerEvent.StreamOnline);
        _profilesManager.GetAll().Returns([profile]);
        _polls.AutoTriggerKillSwitchDateUtc = _now.Date.AddDays(-1);

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _controller.Received(1).StartAsync(profile, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Cooldown_Active_Blocks()
    {
        var profile = Profile(PollAutoTriggerEvent.StreamOnline, cooldown: 30);
        _profilesManager.GetAll().Returns([profile]);
        _controller.StartAsync(Arg.Any<PollProfile>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PollSnapshot?>(new("p", profile.Id, "Q", [], _now, _now.AddMinutes(1), false, 0,
                PollSnapshotStatus.Active, null)));

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _controller.Received(1).StartAsync(profile, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Cooldown_Expired_Retriggers()
    {
        var profile = Profile(PollAutoTriggerEvent.StreamOnline, cooldown: 30);
        _profilesManager.GetAll().Returns([profile]);
        _controller.StartAsync(Arg.Any<PollProfile>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PollSnapshot?>(new("p", profile.Id, "Q", [], _now, _now.AddMinutes(1), false, 0,
                PollSnapshotStatus.Active, null)));

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(31);
        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _controller.Received(2).StartAsync(profile, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ActivePollExists_Blocks()
    {
        var profile = Profile(PollAutoTriggerEvent.StreamOnline);
        _profilesManager.GetAll().Returns([profile]);
        _store.Current.Returns(new PollSnapshot("existing", null, "X", [], _now, _now.AddMinutes(1), false, 0,
            PollSnapshotStatus.Active, null));

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _controller.DidNotReceiveWithAnyArgs().StartAsync(null!, CancellationToken.None);
    }

    [Test]
    public async Task StreamWentOffline_ClearsCooldown()
    {
        var profile = Profile(PollAutoTriggerEvent.StreamOnline, cooldown: 60);
        _profilesManager.GetAll().Returns([profile]);
        _controller.StartAsync(Arg.Any<PollProfile>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PollSnapshot?>(new("p", profile.Id, "Q", [], _now, _now.AddMinutes(1), false, 0,
                PollSnapshotStatus.Active, null)));

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);
        await _handler.HandleAsync(new StreamWentOffline("chan"), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _controller.Received(2).StartAsync(profile, Arg.Any<CancellationToken>());
    }
}
