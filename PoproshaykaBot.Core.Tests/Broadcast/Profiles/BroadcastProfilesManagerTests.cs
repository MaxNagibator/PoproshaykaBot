using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Tests.Polls;

namespace PoproshaykaBot.Core.Tests.Broadcast.Profiles;

[TestFixture]
public class BroadcastProfilesManagerTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broadcast-profiles-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _store = new(filePath: Path.Combine(_tempDir, "broadcast-profiles.json"));
        _applier = Substitute.For<IChannelInformationApplier>();
        _applier.ApplyAsync(Arg.Any<BroadcastProfile>(), Arg.Any<CancellationToken>()).Returns(true);
        _applier.ApplyPatchAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(true);
        _eventBus = Substitute.For<IEventBus>();
        _clock = new() { UtcNow = new(2026, 4, 30, 12, 0, 0, TimeSpan.Zero) };
        _manager = new(_store,
            _applier,
            _eventBus,
            _clock,
            NullLogger<BroadcastProfilesManager>.Instance);
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
    private BroadcastProfilesStore _store = null!;
    private IChannelInformationApplier _applier = null!;
    private IEventBus _eventBus = null!;
    private TestTimeProvider _clock = null!;
    private BroadcastProfilesManager _manager = null!;

    [Test]
    public void Add_SavesProfile_AndRaisesChanged()
    {
        var profile = new BroadcastProfile
        {
            Name = "First",
        };

        _manager.Upsert(profile);

        Assert.That(_store.Load().Profiles, Has.Count.EqualTo(1));
        _eventBus.Received(1).PublishAsync(Arg.Any<BroadcastProfilesChanged>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Upsert_DuplicateName_Throws()
    {
        var profile = new BroadcastProfile
        {
            Name = "X",
        };

        _manager.Upsert(profile);

        Assert.Throws<InvalidOperationException>(() =>
        {
            var broadcastProfile = new BroadcastProfile
            {
                Name = "x",
            };

            _manager.Upsert(broadcastProfile);
        });
    }

    [Test]
    public void Upsert_DuplicateObsScene_Throws()
    {
        _manager.Upsert(new()
        {
            Name = "A",
            ObsSceneName = "Игра",
        });

        Assert.Throws<InvalidOperationException>(() =>
            _manager.Upsert(new()
            {
                Name = "B",
                ObsSceneName = "игра",
            }));
    }

    [Test]
    public void Upsert_EmptyObsScene_NoUniquenessConflict()
    {
        var profile = new BroadcastProfile
        {
            Name = "A",
            ObsSceneName = string.Empty,
        };

        _manager.Upsert(profile);

        Assert.DoesNotThrow(() =>
        {
            var broadcastProfile = new BroadcastProfile
            {
                Name = "B",
                ObsSceneName = string.Empty,
            };

            _manager.Upsert(broadcastProfile);
        });
    }

    [Test]
    public async Task ApplyAsync_PlaceholderTitle_PreservesObsSceneNameInEvent()
    {
        BroadcastProfile? captured = null;
        _applier.ApplyAsync(Arg.Do<BroadcastProfile>(p => captured = p), Arg.Any<CancellationToken>()).Returns(true);
        var profile = new BroadcastProfile
        {
            Name = "X",
            Title = "Серия #{n}",
            CurrentNumber = 7,
            ObsSceneName = "Игра",
        };

        _manager.Upsert(profile);
        await _manager.ApplyAsync(profile.Id, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.Title, Is.EqualTo("Серия #7"));
            Assert.That(captured.ObsSceneName, Is.EqualTo("Игра"));
        }
    }

    [Test]
    public void Upsert_SameProfileEdited_Succeeds()
    {
        var profile = new BroadcastProfile
        {
            Name = "X",
            Title = "old",
        };

        _manager.Upsert(profile);
        profile.Title = "new";
        _manager.Upsert(profile);

        Assert.That(_store.Load().Profiles, Has.Count.EqualTo(1));
        Assert.That(_store.Load().Profiles[0].Title, Is.EqualTo("new"));
    }

    [Test]
    public void Remove_DeletesProfile()
    {
        var profile = new BroadcastProfile
        {
            Name = "X",
        };

        _manager.Upsert(profile);
        _manager.Remove(profile.Id);

        Assert.That(_store.Load().Profiles, Is.Empty);
    }

    [Test]
    public async Task ApplyAsync_UpdatesLastAppliedAndCallsApplier()
    {
        var profile = new BroadcastProfile
        {
            Name = "X",
        };

        _manager.Upsert(profile);

        await _manager.ApplyAsync(profile.Id, CancellationToken.None);

        await _applier.Received(1)
            .ApplyAsync(Arg.Is<BroadcastProfile>(p => p.Id == profile.Id), Arg.Any<CancellationToken>());

        Assert.That(_store.Load().LastAppliedProfileId, Is.EqualTo(profile.Id));
    }

    [Test]
    public async Task ApplyByNameAsync_CaseInsensitive()
    {
        var profile = new BroadcastProfile
        {
            Name = "MyProfile",
        };

        _manager.Upsert(profile);

        var applied = await _manager.ApplyByNameAsync("myprofile", CancellationToken.None);

        Assert.That(applied, Is.Not.Null);
        await _applier.Received(1)
            .ApplyAsync(Arg.Is<BroadcastProfile>(p => p.Id == profile.Id), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyByNameAsync_NotFound_ReturnsNull()
    {
        var applied = await _manager.ApplyByNameAsync("nope", CancellationToken.None);

        Assert.That(applied, Is.Null);
        await _applier.DidNotReceiveWithAnyArgs().ApplyAsync(null!, CancellationToken.None);
    }

    [Test]
    public async Task ApplyAsync_PlaceholderInTitle_RendersWithCurrentNumber()
    {
        var profile = new BroadcastProfile
        {
            Name = "X",
            Title = "Серия #{n}",
            CurrentNumber = 14,
        };

        _manager.Upsert(profile);

        await _manager.ApplyAsync(profile.Id, CancellationToken.None);

        await _applier.Received(1)
            .ApplyAsync(Arg.Is<BroadcastProfile>(p => p.Title == "Серия #14"),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyAsync_PlaceholderAndSuccess_DoesNotChangeCurrentNumber()
    {
        var profile = new BroadcastProfile
        {
            Name = "X",
            Title = "Серия #{n}",
            CurrentNumber = 14,
        };

        _manager.Upsert(profile);

        await _manager.ApplyAsync(profile.Id, CancellationToken.None);

        Assert.That(_store.Load().Profiles[0].CurrentNumber, Is.EqualTo(14));
    }

    [Test]
    public async Task ApplyAsync_PlaceholderAndSuccess_SetsLastApplyAt()
    {
        var profile = new BroadcastProfile
        {
            Name = "X",
            Title = "Серия #{n}",
            CurrentNumber = 14,
        };

        _manager.Upsert(profile);

        await _manager.ApplyAsync(profile.Id, CancellationToken.None);

        Assert.That(_store.Load().Profiles[0].LastApplyAt, Is.EqualTo(_clock.UtcNow));
    }

    [Test]
    public async Task ApplyAsync_PlaceholderAndApplierFailure_DoesNotTouchProfile()
    {
        _applier.ApplyAsync(Arg.Any<BroadcastProfile>(), Arg.Any<CancellationToken>()).Returns(false);
        var profile = new BroadcastProfile
        {
            Name = "X",
            Title = "Серия #{n}",
            CurrentNumber = 14,
        };

        _manager.Upsert(profile);

        await _manager.ApplyAsync(profile.Id, CancellationToken.None);

        var stored = _store.Load().Profiles[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.CurrentNumber, Is.EqualTo(14));
            Assert.That(stored.LastApplyAt, Is.Null);
        }
    }

    [Test]
    public void AdvanceCurrentNumber_StoredProfile_MutatesAutoAdvanceAt_PreservesLastApplyAt()
    {
        var manualApplyAt = _clock.UtcNow.AddHours(-2);
        var profile = new BroadcastProfile
        {
            Name = "X",
            Title = "Серия #{n}",
            CurrentNumber = 14,
            LastApplyAt = manualApplyAt,
        };

        _manager.Upsert(profile);

        var advancedAt = _clock.UtcNow.AddMinutes(-1);
        var result = _manager.AdvanceCurrentNumber(profile.Id, 14, 15, advancedAt);

        var stored = _store.Load().Profiles[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(stored.CurrentNumber, Is.EqualTo(15));
            Assert.That(stored.LastAutoAdvanceAt, Is.EqualTo(advancedAt));
            Assert.That(stored.LastApplyAt, Is.EqualTo(manualApplyAt));
        }
    }

    [Test]
    public void AdvanceCurrentNumber_UnknownId_ReturnsFalse_AndDoesNotPersist()
    {
        var result = _manager.AdvanceCurrentNumber(Guid.NewGuid(), 0, 99, _clock.UtcNow);

        Assert.That(result, Is.False);
    }

    [Test]
    public void AdvanceCurrentNumber_ExpectedNumberMismatch_ReturnsFalse_AndDoesNotOverwrite()
    {
        var profile = new BroadcastProfile
        {
            Name = "X",
            Title = "Серия #{n}",
            CurrentNumber = 14,
        };

        _manager.Upsert(profile);

        _store.Mutate(bp => bp.Profiles[0].CurrentNumber = 20);

        var result = _manager.AdvanceCurrentNumber(profile.Id, 14, 15, _clock.UtcNow);

        var stored = _store.Load().Profiles[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(stored.CurrentNumber, Is.EqualTo(20));
            Assert.That(stored.LastAutoAdvanceAt, Is.Null);
        }
    }

    [Test]
    public async Task ApplyAsync_NoPlaceholder_DoesNotTouchProfile()
    {
        var profile = new BroadcastProfile
        {
            Name = "X",
            Title = "Без номера",
            CurrentNumber = 5,
        };

        _manager.Upsert(profile);

        await _manager.ApplyAsync(profile.Id, CancellationToken.None);

        var stored = _store.Load().Profiles[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.CurrentNumber, Is.EqualTo(5));
            Assert.That(stored.LastApplyAt, Is.Null);
        }
    }
}
