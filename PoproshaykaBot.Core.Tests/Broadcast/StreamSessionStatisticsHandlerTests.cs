using PoproshaykaBot.Core.Broadcast;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Tests.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events.Chat;
using PoproshaykaBot.Core.Infrastructure.Events.Statistics;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Tests.Polls;
using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Core.Tests.Broadcast;

[TestFixture]
public sealed class StreamSessionStatisticsHandlerTests
{
    [SetUp]
    public void SetUp()
    {
        _captured = [];
        _messenger = Substitute.For<IChatMessenger>();
        _streamStatus = Substitute.For<IStreamStatus>();

        _settings = new()
        {
            Twitch =
            {
                Channel = Channel,
                AutoBroadcast =
                {
                    StreamStatusNotificationsEnabled = true,
                    StreamEndStatsMessage = "Итог: {duration} {messages} {chatters}",
                },
            },
        };

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        _settingsManager.Current.Returns(_settings);

        _eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        _eventBus.Subscribe<StreamSessionCompleted>(@event => _captured.Add(@event.Session));

        _timeProvider = new() { UtcNow = StreamStart };

        _tempDirectory = Path.Combine(Path.GetTempPath(), "PoproshaykaBot.SessionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _store = new(NullLogger<ActiveStreamSessionStore>.Instance, Path.Combine(_tempDirectory, "active_session.json"));

        _targetChannel = new(Channel);

        _handler = new(_messenger,
            _streamStatus,
            _targetChannel,
            _settingsManager,
            _timeProvider,
            _eventBus,
            _store,
            NullLogger<StreamSessionStatisticsHandler>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _handler.DisposeAsync();

        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }

    private const string Channel = "test-channel";
    private static readonly DateTime StreamStartUtc = new(2026, 5, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset StreamStart = new(StreamStartUtc);

    private IChatMessenger _messenger = null!;
    private IStreamStatus _streamStatus = null!;
    private FakeTargetChannelProvider _targetChannel = null!;
    private SettingsManager _settingsManager = null!;
    private AppSettings _settings = null!;
    private InMemoryEventBus _eventBus = null!;
    private TestTimeProvider _timeProvider = null!;
    private ActiveStreamSessionStore _store = null!;
    private StreamSessionStatisticsHandler _handler = null!;
    private List<StreamSessionRecord> _captured = null!;
    private string _tempDirectory = null!;

    private static StreamWentOnline Online()
    {
        return new(Channel, new()
        {
            StartedAt = StreamStartUtc,
            ViewerCount = 10,
            Title = "Заголовок",
            GameName = "Игра",
        });
    }

    private static StreamWentOnline OnlineCatchUp(string streamId = "")
    {
        return new(Channel, new()
        {
            Id = streamId,
            StartedAt = StreamStartUtc,
            ViewerCount = 10,
            Title = "Заголовок",
            GameName = "Игра",
        }, true);
    }

    private static ChatMessageReceived Chat(string userId, string displayName, bool isBot = false)
    {
        return new(Channel, Guid.NewGuid().ToString("N"), userId, displayName.ToLowerInvariant(), displayName,
            "сообщение", UserStatus.None, false, new(), isBot);
    }

    private void SeedDraft(
        DateTimeOffset startedAt,
        DateTimeOffset updatedAt,
        long messageCount,
        params (string UserId, string DisplayName, long Count)[] chatters)
    {
        SeedDraft(null, startedAt, updatedAt, messageCount, chatters);
    }

    private void SeedDraft(
        string? streamId,
        DateTimeOffset startedAt,
        DateTimeOffset updatedAt,
        long messageCount,
        params (string UserId, string DisplayName, long Count)[] chatters)
    {
        _store.Save(new()
        {
            Channel = Channel,
            StreamId = streamId,
            StartedAt = startedAt,
            UpdatedAt = updatedAt,
            MessageCount = messageCount,
            PeakViewers = 50,
            ViewerSamplesSum = 300,
            ViewerSamplesCount = 10,
            Title = "Черновой заголовок",
            Game = "Черновая игра",
            Chatters = chatters
                .Select(chatter => new ActiveStreamSessionChatter
                {
                    UserId = chatter.UserId,
                    DisplayName = chatter.DisplayName,
                    MessageCount = chatter.Count,
                })
                .ToList(),
        });
    }

    [Test]
    public async Task StreamWentOffline_AfterSession_PublishesCompletedWithAggregateAndChatters()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);

        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u2", "Bob"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u3", "Carol"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u3", "Carol"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(2);

        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(1));

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Channel, Is.EqualTo(Channel));
            Assert.That(record.MessageCount, Is.EqualTo(6));
            Assert.That(record.ChatterCount, Is.EqualTo(3));
            Assert.That(record.PeakViewers, Is.EqualTo(10));
            Assert.That(record.AverageViewers, Is.EqualTo(10));
            Assert.That(record.Title, Is.EqualTo("Заголовок"));
            Assert.That(record.Game, Is.EqualTo("Игра"));
            Assert.That(record.Duration, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(record.Chatters.Select(c => c.DisplayName), Is.EqualTo(new[] { "Alice", "Carol", "Bob" }));
            Assert.That(record.Chatters.Select(c => c.MessageCount), Is.EqualTo(new long[] { 3, 2, 1 }));
            _messenger.Received(1).Send(Arg.Any<string>());
        }
    }

    [Test]
    public async Task StreamWentOffline_AfterSession_DeletesDraftFile()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        _timeProvider.UtcNow = StreamStart.AddHours(1);

        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_store.Load(), Is.Null);
    }

    [Test]
    public async Task StreamWentOffline_WithIsCatchUpTrue_AndActiveMemorySession_DoesNotPublishOrSend()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);

        await _handler.HandleAsync(new StreamWentOffline(Channel, true), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured, Is.Empty);
            _messenger.DidNotReceive().Send(Arg.Any<string>());
        }
    }

    [Test]
    public async Task StreamWentOffline_NotificationsDisabled_StillPublishesButDoesNotSend()
    {
        _settings.Twitch.AutoBroadcast.StreamStatusNotificationsEnabled = false;

        await _handler.HandleAsync(Online(), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        _timeProvider.UtcNow = StreamStart.AddMinutes(30);

        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured, Has.Count.EqualTo(1));
            Assert.That(_captured[0].MessageCount, Is.EqualTo(1));
            _messenger.DidNotReceive().Send(Arg.Any<string>());
        }
    }

    [Test]
    public async Task StreamWentOffline_WithoutActiveSession_DoesNotPublish()
    {
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Is.Empty);
    }

    [Test]
    public async Task ChatMessageReceived_FromBot_DoesNotCountTowardSession()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);

        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        await _handler.HandleAsync(Chat("bot", "MyBot", true), CancellationToken.None);
        await _handler.HandleAsync(Chat("bot", "MyBot", true), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);

        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured[0].MessageCount, Is.EqualTo(1));
            Assert.That(_captured[0].ChatterCount, Is.EqualTo(1));
            Assert.That(_captured[0].Chatters.Select(c => c.DisplayName), Is.EqualTo(new[] { "Alice" }));
        }
    }

    [Test]
    public async Task DisposeAsync_WithActiveSession_PersistsDraftForResume()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u2", "Bob"), CancellationToken.None);

        await _handler.DisposeAsync();

        var draft = _store.Load();

        Assert.That(draft, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft!.Channel, Is.EqualTo(Channel));
            Assert.That(draft.StartedAt, Is.EqualTo(StreamStart));
            Assert.That(draft.MessageCount, Is.EqualTo(3));
            Assert.That(draft.Chatters, Has.Count.EqualTo(2));
            Assert.That(draft.Chatters.Single(c => string.Equals(c.UserId, "u1", StringComparison.Ordinal)).MessageCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task StreamWentOnline_CatchUp_WithMatchingDraft_ResumesAccumulatedCounters()
    {
        SeedDraft(StreamStart, StreamStart.AddHours(1), 100, ("u1", "Alice", 60), ("u2", "Bob", 40));

        await _handler.HandleAsync(OnlineCatchUp(), CancellationToken.None);

        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u3", "Carol"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(3);

        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(1));

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.MessageCount, Is.EqualTo(102));
            Assert.That(record.ChatterCount, Is.EqualTo(3));
            Assert.That(record.PeakViewers, Is.EqualTo(50));
            Assert.That(record.AverageViewers, Is.EqualTo(30));
            Assert.That(record.StartedAt, Is.EqualTo(StreamStart));
            Assert.That(record.Duration, Is.EqualTo(TimeSpan.FromHours(3)));
            Assert.That(record.Chatters.Select(c => c.DisplayName), Is.EqualTo(new[] { "Alice", "Bob", "Carol" }));
            Assert.That(record.Chatters.Select(c => c.MessageCount), Is.EqualTo(new long[] { 61, 40, 1 }));
        }
    }

    [Test]
    public async Task StreamWentOnline_CatchUp_WithoutDraft_StartsFreshSession()
    {
        await _handler.HandleAsync(OnlineCatchUp(), CancellationToken.None);

        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u2", "Bob"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);

        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(1));
        Assert.That(_captured[0].MessageCount, Is.EqualTo(2));
    }

    [Test]
    public async Task StreamWentOnline_CatchUp_WithDifferentStreamDraft_FinalizesOldAndStartsFresh()
    {
        var oldStart = StreamStart.AddDays(-1);
        SeedDraft(oldStart, oldStart.AddHours(2), 77, ("u9", "Veteran", 77));

        await _handler.HandleAsync(OnlineCatchUp(), CancellationToken.None);

        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);

        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured[0].MessageCount, Is.EqualTo(77));
            Assert.That(_captured[0].StartedAt, Is.EqualTo(oldStart));
            Assert.That(_captured[0].Duration, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(_captured[1].MessageCount, Is.EqualTo(1));
            Assert.That(_captured[1].StartedAt, Is.EqualTo(StreamStart));
        }
    }

    [Test]
    public async Task StreamWentOffline_CatchUp_WithDraft_FinalizesDraftFromDisk()
    {
        SeedDraft(StreamStart, StreamStart.AddHours(4), 55, ("u1", "Alice", 30), ("u2", "Bob", 25));

        await _handler.HandleAsync(new StreamWentOffline(Channel, true), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(1));

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.MessageCount, Is.EqualTo(55));
            Assert.That(record.ChatterCount, Is.EqualTo(2));
            Assert.That(record.StartedAt, Is.EqualTo(StreamStart));
            Assert.That(record.EndedAt, Is.EqualTo(StreamStart.AddHours(4)));
            Assert.That(record.Duration, Is.EqualTo(TimeSpan.FromHours(4)));
            Assert.That(_store.Load(), Is.Null);
            _messenger.DidNotReceive().Send(Arg.Any<string>());
        }
    }

    [Test]
    public async Task StreamWentOffline_CatchUp_WithoutDraft_DoesNothing()
    {
        await _handler.HandleAsync(new StreamWentOffline(Channel, true), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured, Is.Empty);
            _messenger.DidNotReceive().Send(Arg.Any<string>());
        }
    }

    [Test]
    public async Task ChannelUpdated_MidStream_SplitsSessionIntoSegmentsWithSeparateMetrics()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);

        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u2", "Bob"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new ChannelUpdated("Новый заголовок", "ru", "999", "Другая игра", []), CancellationToken.None);

        await _handler.HandleAsync(Chat("u3", "Carol"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(2);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(1));

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Segments, Has.Count.EqualTo(2));
            Assert.That(record.MessageCount, Is.EqualTo(3));
            Assert.That(record.Game, Is.EqualTo("Другая игра"));
            Assert.That(record.Title, Is.EqualTo("Новый заголовок"));

            Assert.That(record.Segments[0].Game, Is.EqualTo("Игра"));
            Assert.That(record.Segments[0].Title, Is.EqualTo("Заголовок"));
            Assert.That(record.Segments[0].MessageCount, Is.EqualTo(2));
            Assert.That(record.Segments[0].StartedAt, Is.EqualTo(StreamStart));
            Assert.That(record.Segments[0].EndedAt, Is.EqualTo(StreamStart.AddHours(1)));

            Assert.That(record.Segments[1].Game, Is.EqualTo("Другая игра"));
            Assert.That(record.Segments[1].Title, Is.EqualTo("Новый заголовок"));
            Assert.That(record.Segments[1].MessageCount, Is.EqualTo(1));
            Assert.That(record.Segments[1].StartedAt, Is.EqualTo(StreamStart.AddHours(1)));
            Assert.That(record.Segments[1].EndedAt, Is.EqualTo(StreamStart.AddHours(2)));
        }
    }

    [Test]
    public async Task ChannelUpdated_SameGameNewTitle_SplitsSessionIntoSegments()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddMinutes(30);
        await _handler.HandleAsync(new ChannelUpdated("Новый заголовок", "ru", "1", "Игра", []), CancellationToken.None);

        await _handler.HandleAsync(Chat("u2", "Bob"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Segments, Has.Count.EqualTo(2));
            Assert.That(record.MessageCount, Is.EqualTo(2));

            Assert.That(record.Segments[0].Game, Is.EqualTo("Игра"));
            Assert.That(record.Segments[0].Title, Is.EqualTo("Заголовок"));
            Assert.That(record.Segments[0].MessageCount, Is.EqualTo(1));
            Assert.That(record.Segments[0].EndedAt, Is.EqualTo(StreamStart.AddMinutes(30)));

            Assert.That(record.Segments[1].Game, Is.EqualTo("Игра"));
            Assert.That(record.Segments[1].Title, Is.EqualTo("Новый заголовок"));
            Assert.That(record.Segments[1].MessageCount, Is.EqualTo(1));
            Assert.That(record.Segments[1].StartedAt, Is.EqualTo(StreamStart.AddMinutes(30)));
        }
    }

    [Test]
    public async Task ChannelUpdated_SameTitleAndGame_KeepsOneSegment()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddMinutes(30);
        await _handler.HandleAsync(new ChannelUpdated("Заголовок", "ru", "1", "Игра", []), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Segments, Has.Count.EqualTo(1));
            Assert.That(record.Segments[0].Title, Is.EqualTo("Заголовок"));
        }
    }

    [Test]
    public async Task ChannelUpdated_TitleAndGameTogether_OpensSingleSegment()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddMinutes(30);
        await _handler.HandleAsync(new ChannelUpdated("Новый заголовок", "ru", "1", "Другая игра", []), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Segments, Has.Count.EqualTo(2));
            Assert.That(record.Segments[1].Game, Is.EqualTo("Другая игра"));
            Assert.That(record.Segments[1].Title, Is.EqualTo("Новый заголовок"));
        }
    }

    [Test]
    public async Task StreamMetadataResolved_ResolvesInitiallyUnknownGame_WithoutSplitting()
    {
        var onlineWithoutGame = new StreamWentOnline(Channel, new()
        {
            StartedAt = StreamStartUtc,
            ViewerCount = 5,
            Title = "Заголовок",
        });

        await _handler.HandleAsync(onlineWithoutGame, CancellationToken.None);

        await _handler.HandleAsync(new StreamMetadataResolved(Channel, new() { Title = "Заголовок", GameName = "Игра", ViewerCount = 5 }),
            CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Segments, Has.Count.EqualTo(1));
            Assert.That(record.Segments[0].Game, Is.EqualTo("Игра"));
        }
    }

    [Test]
    public async Task StreamWentOnline_CatchUp_GameChangedWhileOffline_OpensNewSegment()
    {
        SeedDraft(StreamStart, StreamStart.AddHours(1), 10, ("u1", "Alice", 10));

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(OnlineCatchUp(), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(2);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Segments, Has.Count.EqualTo(2));
            Assert.That(record.Segments[0].Game, Is.EqualTo("Черновая игра"));
            Assert.That(record.Segments[0].MessageCount, Is.EqualTo(10));
            Assert.That(record.Segments[1].Game, Is.EqualTo("Игра"));
        }
    }

    [Test]
    public async Task DisposeAndResume_WithMidStreamCategoryChange_PreservesSegments()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new ChannelUpdated("Второй заголовок", "ru", "55", "Вторая игра", []), CancellationToken.None);
        await _handler.HandleAsync(Chat("u2", "Bob"), CancellationToken.None);
        await _handler.HandleAsync(Chat("u3", "Carol"), CancellationToken.None);

        await _handler.DisposeAsync();

        var resumed = new StreamSessionStatisticsHandler(_messenger,
            _streamStatus,
            _targetChannel,
            _settingsManager,
            _timeProvider,
            _eventBus,
            _store,
            NullLogger<StreamSessionStatisticsHandler>.Instance);

        try
        {
            var resumeOnline = new StreamWentOnline(Channel, new()
            {
                StartedAt = StreamStartUtc,
                ViewerCount = 10,
                Title = "Второй заголовок",
                GameName = "Вторая игра",
            }, true);

            await resumed.HandleAsync(resumeOnline, CancellationToken.None);

            _timeProvider.UtcNow = StreamStart.AddHours(3);
            await resumed.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);
        }
        finally
        {
            await resumed.DisposeAsync();
        }

        Assert.That(_captured, Has.Count.EqualTo(1));

        var record = _captured[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Segments, Has.Count.EqualTo(2));
            Assert.That(record.Segments[0].Game, Is.EqualTo("Игра"));
            Assert.That(record.Segments[0].MessageCount, Is.EqualTo(1));
            Assert.That(record.Segments[1].Game, Is.EqualTo("Вторая игра"));
            Assert.That(record.Segments[1].MessageCount, Is.EqualTo(2));
            Assert.That(record.MessageCount, Is.EqualTo(3));
        }
    }

    [TestCase(20, 1, 43)]
    [TestCase(280, 1, 43)]
    [TestCase(400, 2, 1)]
    public async Task StreamWentOnline_CatchUp_DraftStartedOnLocalClock_ResumesWhileDriftFitsTolerance(
        int driftSeconds,
        int expectedRecords,
        long expectedLastRecordMessages)
    {
        var draftStart = StreamStart.AddSeconds(driftSeconds);
        SeedDraft(draftStart, draftStart.AddMinutes(30), 42, ("u1", "Alice", 42));

        _timeProvider.UtcNow = StreamStart.AddMinutes(30);
        await _handler.HandleAsync(OnlineCatchUp(), CancellationToken.None);

        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(expectedRecords));
        Assert.That(_captured[^1].MessageCount, Is.EqualTo(expectedLastRecordMessages));
    }

    [Test]
    public async Task StreamWentOnline_CatchUp_DifferentStreamId_FinalizesDraftDespiteCloseStart()
    {
        var draftStart = StreamStart.AddSeconds(20);
        SeedDraft("stream-a", draftStart, draftStart.AddMinutes(30), 42, ("u1", "Alice", 42));

        _timeProvider.UtcNow = StreamStart.AddMinutes(30);
        await _handler.HandleAsync(OnlineCatchUp("stream-b"), CancellationToken.None);

        await _handler.HandleAsync(Chat("u2", "Bob"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured[0].MessageCount, Is.EqualTo(42));
            Assert.That(_captured[0].StartedAt, Is.EqualTo(draftStart));
            Assert.That(_captured[1].MessageCount, Is.EqualTo(1));
            Assert.That(_captured[1].StartedAt, Is.EqualTo(StreamStart));
        }
    }

    [Test]
    public async Task StreamWentOnline_CatchUp_SameStreamId_ResumesBeyondTolerance()
    {
        var draftStart = StreamStart.AddMinutes(-30);
        SeedDraft("stream-a", draftStart, StreamStart.AddMinutes(30), 42, ("u1", "Alice", 42));

        _timeProvider.UtcNow = StreamStart.AddMinutes(30);
        await _handler.HandleAsync(OnlineCatchUp("stream-a"), CancellationToken.None);

        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(1));
        Assert.That(_captured[0].MessageCount, Is.EqualTo(43));
    }

    [Test]
    public async Task StreamMetadataResolved_WithCanonicalStart_RewritesSessionStartAndDraft()
    {
        _timeProvider.UtcNow = StreamStart.AddSeconds(20);
        await _handler.HandleAsync(new StreamWentOnline(Channel, null), CancellationToken.None);

        await _handler.HandleAsync(new StreamMetadataResolved(Channel, new()
            {
                Id = "stream-a",
                StartedAt = StreamStartUtc,
                Title = "Заголовок",
                GameName = "Игра",
                ViewerCount = 7,
            }),
            CancellationToken.None);

        var draft = _store.Load();

        Assert.That(draft, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft!.StartedAt, Is.EqualTo(StreamStart));
            Assert.That(draft.StreamId, Is.EqualTo("stream-a"));
            Assert.That(draft.Segments, Has.Count.EqualTo(1));
            Assert.That(draft.Segments[0].StartedAt, Is.EqualTo(StreamStart));
        }

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured[0].StartedAt, Is.EqualTo(StreamStart));
            Assert.That(_captured[0].Duration, Is.EqualTo(TimeSpan.FromHours(1)));
        }
    }

    [Test]
    public async Task StreamMetadataResolved_ForAnotherStream_LeavesSessionStartAlone()
    {
        await _handler.HandleAsync(OnlineCatchUp("stream-a"), CancellationToken.None);

        await _handler.HandleAsync(new StreamMetadataResolved(Channel, new()
            {
                Id = "stream-b",
                StartedAt = StreamStartUtc.AddHours(1),
                Title = "Чужой заголовок",
                GameName = "Чужая игра",
                ViewerCount = 70,
            }),
            CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(2);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_captured[0].StartedAt, Is.EqualTo(StreamStart));
            Assert.That(_captured[0].Title, Is.EqualTo("Заголовок"));
            Assert.That(_captured[0].Game, Is.EqualTo("Игра"));
            Assert.That(_captured[0].PeakViewers, Is.EqualTo(10));
            Assert.That(_captured[0].Segments, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task StreamWentOnline_CatchUp_LegacyDraftWithoutStreamId_LoadsAndResumes()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "active_session.json"),
            """
            {
              "channel": "test-channel",
              "startedAt": "2026-05-17T10:00:20+00:00",
              "updatedAt": "2026-05-17T10:30:00+00:00",
              "messageCount": 42,
              "peakViewers": 50,
              "viewerSamplesSum": 300,
              "viewerSamplesCount": 10,
              "title": "Черновой заголовок",
              "game": "Игра",
              "chatters": [
                {
                  "userId": "u1",
                  "displayName": "Alice",
                  "messageCount": 42
                }
              ],
              "segments": []
            }
            """);

        _timeProvider.UtcNow = StreamStart.AddMinutes(30);
        await _handler.HandleAsync(OnlineCatchUp("stream-a"), CancellationToken.None);

        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Has.Count.EqualTo(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured[0].MessageCount, Is.EqualTo(43));
            Assert.That(_captured[0].StartedAt, Is.EqualTo(StreamStart.AddSeconds(20)));
        }
    }

    [Test]
    public async Task Учёт_обрезан_по_границам_эфира_и_разорван_переподключением()
    {
        await _handler.HandleAsync(new ChatIngestionStarted(StreamStart.AddMinutes(-30)), CancellationToken.None);
        await _handler.HandleAsync(Online(), CancellationToken.None);

        await _handler.HandleAsync(new ChatIngestionStopped(StreamStart.AddMinutes(20)), CancellationToken.None);
        await _handler.HandleAsync(new ChatIngestionStarted(StreamStart.AddMinutes(25)), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddMinutes(79);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);
        await _handler.HandleAsync(new ChatIngestionStopped(StreamStart.AddMinutes(90)), CancellationToken.None);

        var record = _captured.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.TrackedIntervals!.Select(interval => (interval.StartedAt, interval.EndedAt)),
                Is.EqualTo(new[]
                {
                    (StreamStart, StreamStart.AddMinutes(20)),
                    (StreamStart.AddMinutes(25), StreamStart.AddMinutes(79)),
                }));

            Assert.That(record.TrackedDuration, Is.EqualTo(TimeSpan.FromMinutes(74)));
        }
    }

    [Test]
    public async Task Бот_не_подключался_за_эфир_и_это_записано_пустым_учётом_а_не_неизвестностью()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddHours(1);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured.Single().TrackedIntervals, Is.Empty);
            Assert.That(_captured.Single().TrackedDuration, Is.EqualTo(TimeSpan.Zero));
        }
    }

    [Test]
    public async Task Возобновление_после_перезапуска_закрывает_учёт_прошлого_процесса_последним_чекпойнтом()
    {
        _store.Save(new()
        {
            Channel = Channel,
            StartedAt = StreamStart,
            UpdatedAt = StreamStart.AddMinutes(40),
            Segments = [new() { StartedAt = StreamStart, Title = "Заголовок", Game = "Игра" }],
            TrackedIntervals = [new() { StartedAt = StreamStart.AddMinutes(1) }],
        });

        await _handler.HandleAsync(new ChatIngestionStarted(StreamStart.AddMinutes(50)), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddMinutes(50);
        await _handler.HandleAsync(OnlineCatchUp(), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddMinutes(60);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured.Single().TrackedIntervals!.Select(interval => (interval.StartedAt, interval.EndedAt)),
            Is.EqualTo(new[]
            {
                (StreamStart.AddMinutes(1), StreamStart.AddMinutes(40)),
                (StreamStart.AddMinutes(50), StreamStart.AddMinutes(60)),
            }));
    }

    [Test]
    public async Task Черновик_прежней_сборки_даёт_запись_без_учёта_а_не_нулевой_учёт()
    {
        SeedDraft(StreamStart, StreamStart.AddMinutes(30), 5);

        await _handler.HandleAsync(new ChatIngestionStarted(StreamStart.AddMinutes(31)), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddMinutes(31);
        await _handler.HandleAsync(OnlineCatchUp(), CancellationToken.None);

        _timeProvider.UtcNow = StreamStart.AddMinutes(60);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured.Single().TrackedIntervals, Is.Null);
            Assert.That(_captured.Single().TrackedDuration, Is.Null);
        }
    }

    [Test]
    public async Task Брошенный_черновик_закрывает_открытый_учёт_временем_последнего_чекпойнта()
    {
        _store.Save(new()
        {
            Channel = Channel,
            StartedAt = StreamStart,
            UpdatedAt = StreamStart.AddMinutes(30),
            Segments = [new() { StartedAt = StreamStart, Title = "Заголовок", Game = "Игра" }],
            TrackedIntervals = [new() { StartedAt = StreamStart.AddMinutes(5) }],
        });

        _timeProvider.UtcNow = StreamStart.AddHours(2);
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        var record = _captured.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Duration, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(record.TrackedDuration, Is.EqualTo(TimeSpan.FromMinutes(25)));
        }
    }
}
