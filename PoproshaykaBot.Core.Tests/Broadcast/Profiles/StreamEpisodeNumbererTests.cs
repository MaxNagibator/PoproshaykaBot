using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Tests.Polls;

namespace PoproshaykaBot.Core.Tests.Broadcast.Profiles;

[TestFixture]
public class StreamEpisodeNumbererTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "stream-episode-numberer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _broadcastProfiles = new();
        _profilesStore = Substitute.For<BroadcastProfilesStore>(NullLogger<BroadcastProfilesStore>.Instance, null, null);
        _profilesStore.Load().Returns(_broadcastProfiles);

        _profilesManager = Substitute.For<BroadcastProfilesManager>(new BroadcastProfilesStore(filePath: Path.Combine(_tempDir, "broadcast-profiles.json")),
            Substitute.For<IChannelInformationApplier>(),
            Substitute.For<IEventBus>(),
            TimeProvider.System,
            NullLogger<BroadcastProfilesManager>.Instance);

        _profilesManager.AdvanceCurrentNumber(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTimeOffset>())
            .Returns(call =>
            {
                var id = call.Arg<Guid>();
                var expected = call.ArgAt<int>(1);
                var nextValue = call.ArgAt<int>(2);
                var advancedAt = call.Arg<DateTimeOffset>();
                var stored = _broadcastProfiles.Profiles.FirstOrDefault(p => p.Id == id);

                if (stored == null)
                {
                    return false;
                }

                if (stored.CurrentNumber != expected)
                {
                    return false;
                }

                stored.CurrentNumber = nextValue;
                stored.LastAutoAdvanceAt = advancedAt;
                return true;
            });

        _applier = Substitute.For<IChannelInformationApplier>();
        _applier.ApplyPatchAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _eventBus = Substitute.For<IEventBus>();
        _clock = new() { UtcNow = new(2026, 4, 30, 12, 0, 0, TimeSpan.Zero) };

        _handler = new(_profilesStore,
            _profilesManager,
            _applier,
            _eventBus,
            _clock,
            NullLogger<StreamEpisodeNumberer>.Instance);
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
    private BroadcastProfilesStore _profilesStore = null!;
    private BroadcastProfilesSettings _broadcastProfiles = null!;
    private BroadcastProfilesManager _profilesManager = null!;
    private IChannelInformationApplier _applier = null!;
    private IEventBus _eventBus = null!;
    private TestTimeProvider _clock = null!;
    private StreamEpisodeNumberer _handler = null!;

    private BroadcastProfile RegisterActiveNumberedProfile(int currentNumber, DateTimeOffset? lastApplyAt = null)
    {
        var profile = new BroadcastProfile
        {
            Id = Guid.NewGuid(),
            Name = "Серия",
            Title = "Серия #{n}",
            CurrentNumber = currentNumber,
            LastApplyAt = lastApplyAt,
        };

        _broadcastProfiles.LastAppliedProfileId = profile.Id;
        _broadcastProfiles.Profiles.Add(profile);
        _profilesManager.Find(profile.Id).Returns(profile);
        return profile;
    }

    [Test]
    public async Task FreshOnline_NeverManuallyApplied_DoesNothing()
    {
        var profile = RegisterActiveNumberedProfile(currentNumber: 14);

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _applier.DidNotReceiveWithAnyArgs().ApplyPatchAsync(default, default, default, default);
        Assert.That(profile.CurrentNumber, Is.EqualTo(14));
    }

    [Test]
    public async Task PreviouslyApplied_OnlineAfterWindow_AppliesIncrementedTitle_AndAdvancesCurrentNumber()
    {
        var profile = RegisterActiveNumberedProfile(14, _clock.UtcNow.AddMinutes(-30));

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _applier.Received(1).ApplyPatchAsync("Серия #15", null, null, Arg.Any<CancellationToken>());
        Assert.That(profile.CurrentNumber, Is.EqualTo(15));
    }

    [Test]
    public async Task CatchUpOnline_DoesNothing()
    {
        var profile = RegisterActiveNumberedProfile(currentNumber: 14);

        await _handler.HandleAsync(new StreamWentOnline("chan", null, true), CancellationToken.None);

        await _applier.DidNotReceiveWithAnyArgs().ApplyPatchAsync(default, default, default, default);
        Assert.That(profile.CurrentNumber, Is.EqualTo(14));
    }

    [Test]
    public async Task NoActiveProfile_DoesNothing()
    {
        _broadcastProfiles.LastAppliedProfileId = null;

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _applier.DidNotReceiveWithAnyArgs().ApplyPatchAsync(default, default, default, default);
    }

    [Test]
    public async Task ProfileWithoutPlaceholder_DoesNothing()
    {
        var profile = new BroadcastProfile
        {
            Id = Guid.NewGuid(),
            Name = "Plain",
            Title = "Просто стрим",
            CurrentNumber = 5,
        };

        _broadcastProfiles.LastAppliedProfileId = profile.Id;
        _broadcastProfiles.Profiles.Add(profile);
        _profilesManager.Find(profile.Id).Returns(profile);

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _applier.DidNotReceiveWithAnyArgs().ApplyPatchAsync(default, default, default, default);
        Assert.That(profile.CurrentNumber, Is.EqualTo(5));
    }

    [Test]
    public async Task RecentApply_DoesNothing()
    {
        var profile = RegisterActiveNumberedProfile(14, _clock.UtcNow.AddMinutes(-5));

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _applier.DidNotReceiveWithAnyArgs().ApplyPatchAsync(default, default, default, default);
        Assert.That(profile.CurrentNumber, Is.EqualTo(14));
    }

    [Test]
    public async Task ApplyOlderThanWindow_DoesIncrement()
    {
        var profile = RegisterActiveNumberedProfile(14, _clock.UtcNow.AddMinutes(-30));

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _applier.Received(1).ApplyPatchAsync("Серия #15", null, null, Arg.Any<CancellationToken>());
        Assert.That(profile.CurrentNumber, Is.EqualTo(15));
    }

    [Test]
    public async Task RecentOffline_DoesNothing()
    {
        var profile = RegisterActiveNumberedProfile(currentNumber: 14);

        await _handler.HandleAsync(new StreamWentOffline("chan"), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(3);
        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _applier.DidNotReceiveWithAnyArgs().ApplyPatchAsync(default, default, default, default);
        Assert.That(profile.CurrentNumber, Is.EqualTo(14));
    }

    [Test]
    public async Task OldOffline_DoesIncrement()
    {
        var profile = RegisterActiveNumberedProfile(14, _clock.UtcNow.AddMinutes(-30));

        await _handler.HandleAsync(new StreamWentOffline("chan"), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(20);
        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        await _applier.Received(1).ApplyPatchAsync("Серия #15", null, null, Arg.Any<CancellationToken>());
        Assert.That(profile.CurrentNumber, Is.EqualTo(15));
    }

    [Test]
    public async Task ApplierFailure_DoesNotAdvance()
    {
        _applier.ApplyPatchAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var lastApplyAt = _clock.UtcNow.AddMinutes(-30);
        var profile = RegisterActiveNumberedProfile(14, lastApplyAt);

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile.CurrentNumber, Is.EqualTo(14));
            Assert.That(profile.LastApplyAt, Is.EqualTo(lastApplyAt));
        }
    }

    [Test]
    public async Task SuccessfulAdvance_SetsLastAutoAdvanceAt_AndPreservesLastApplyAt()
    {
        var lastApplyAt = _clock.UtcNow.AddMinutes(-30);
        var profile = RegisterActiveNumberedProfile(14, lastApplyAt);

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile.LastAutoAdvanceAt, Is.EqualTo(_clock.UtcNow));
            Assert.That(profile.LastApplyAt, Is.EqualTo(lastApplyAt));
        }
    }

    [Test]
    public async Task ConsecutiveOnlineWithinRecentApplyWindow_OfPreviousAutoAdvance_StillIncrementsAgain()
    {
        var profile = RegisterActiveNumberedProfile(14, _clock.UtcNow.AddHours(-2));

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);
        Assert.That(profile.CurrentNumber, Is.EqualTo(15));

        await _handler.HandleAsync(new StreamWentOffline("chan"), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(12);
        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        Assert.That(profile.CurrentNumber, Is.EqualTo(16));
    }

    [Test]
    public async Task NumbererPassesExpectedCurrentNumberToManager()
    {
        var profile = RegisterActiveNumberedProfile(14, _clock.UtcNow.AddMinutes(-30));

        await _handler.HandleAsync(new StreamWentOnline("chan", null), CancellationToken.None);

        _profilesManager.Received(1).AdvanceCurrentNumber(profile.Id, 14, 15, _clock.UtcNow);
    }
}
