using PoproshaykaBot.Core.Broadcast;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure.Events;
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
        _captured = null;
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

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance);
        _settingsManager.Current.Returns(_settings);

        _eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        _eventBus.Subscribe<StreamSessionCompleted>(@event => _captured = @event.Session);

        _timeProvider = new() { UtcNow = StreamStart };

        _handler = new(_messenger,
            _streamStatus,
            _settingsManager,
            _timeProvider,
            _eventBus,
            NullLogger<StreamSessionStatisticsHandler>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _handler.DisposeAsync();
    }

    private const string Channel = "test-channel";
    private static readonly DateTime StreamStartUtc = new(2026, 5, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset StreamStart = new(StreamStartUtc);

    private IChatMessenger _messenger = null!;
    private IStreamStatus _streamStatus = null!;
    private SettingsManager _settingsManager = null!;
    private AppSettings _settings = null!;
    private InMemoryEventBus _eventBus = null!;
    private TestTimeProvider _timeProvider = null!;
    private StreamSessionStatisticsHandler _handler = null!;
    private StreamSessionRecord? _captured;

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

    private static ChatMessageReceived Chat(string userId, string displayName)
    {
        return new(Channel, Guid.NewGuid().ToString("N"), userId, displayName.ToLowerInvariant(), displayName,
            "сообщение", UserStatus.None, false, new());
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

        Assert.That(_captured, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured!.Channel, Is.EqualTo(Channel));
            Assert.That(_captured.MessageCount, Is.EqualTo(6));
            Assert.That(_captured.ChatterCount, Is.EqualTo(3));
            Assert.That(_captured.PeakViewers, Is.EqualTo(10));
            Assert.That(_captured.AverageViewers, Is.EqualTo(10));
            Assert.That(_captured.Title, Is.EqualTo("Заголовок"));
            Assert.That(_captured.Game, Is.EqualTo("Игра"));
            Assert.That(_captured.Duration, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(_captured.Chatters.Select(c => c.DisplayName), Is.EqualTo(new[] { "Alice", "Carol", "Bob" }));
            Assert.That(_captured.Chatters.Select(c => c.MessageCount), Is.EqualTo(new long[] { 3, 2, 1 }));
            _messenger.Received(1).Send(Arg.Any<string>());
        }
    }

    [Test]
    public async Task StreamWentOffline_WithIsCatchUpTrue_DoesNotPublishOrSend()
    {
        await _handler.HandleAsync(Online(), CancellationToken.None);
        await _handler.HandleAsync(Chat("u1", "Alice"), CancellationToken.None);

        await _handler.HandleAsync(new StreamWentOffline(Channel, true), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_captured, Is.Null);
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
            Assert.That(_captured, Is.Not.Null);
            Assert.That(_captured!.MessageCount, Is.EqualTo(1));
            _messenger.DidNotReceive().Send(Arg.Any<string>());
        }
    }

    [Test]
    public async Task StreamWentOffline_WithoutActiveSession_DoesNotPublish()
    {
        await _handler.HandleAsync(new StreamWentOffline(Channel), CancellationToken.None);

        Assert.That(_captured, Is.Null);
    }
}
