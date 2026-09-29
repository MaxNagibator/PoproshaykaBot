using NSubstitute.ExceptionExtensions;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Chat;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Tests.Polls;
using PoproshaykaBot.Core.Tests.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Net;

namespace PoproshaykaBot.Core.Tests.Twitch.Chat;

[TestFixture]
public sealed class ChatIngestionServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _eventSubClient = Substitute.For<ITwitchEventSubClient>();

        _helix = Substitute.For<ITwitchHelixClient>();
        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("sub-id");

        _broadcasterIdProvider = Substitute.For<IBroadcasterIdProvider>();
        _broadcasterIdProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(BroadcasterId);

        _botUserIdProvider = Substitute.For<IBotUserIdProvider>();
        _botUserIdProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(BotId);

        _settings = new()
        {
            Twitch =
            {
                Channel = "test-channel",
            },
        };

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);

        _settingsManager.Current.Returns(_settings);

        _eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        _timeProvider = new() { UtcNow = new(2026, 9, 22, 18, 0, 0, TimeSpan.Zero) };

        _service = new(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _botUserIdProvider,
            _settingsManager,
            _eventBus,
            _timeProvider,
            NullLogger<ChatIngestionService>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _service.StopAsync(NullProgress, CancellationToken.None);
    }

    private static readonly IProgress<string> NullProgress = new Progress<string>(_ => { });
    private const string BroadcasterId = "12345";
    private const string BotId = "67890";

    private ITwitchEventSubClient _eventSubClient = null!;
    private ITwitchHelixClient _helix = null!;
    private IBroadcasterIdProvider _broadcasterIdProvider = null!;
    private IBotUserIdProvider _botUserIdProvider = null!;
    private SettingsManager _settingsManager = null!;
    private AppSettings _settings = null!;
    private InMemoryEventBus _eventBus = null!;
    private TestTimeProvider _timeProvider = null!;
    private ChatIngestionService _service = null!;

    [Test]
    public async Task Приём_чата_сообщает_о_каждом_подключении_и_разрыве_ровно_один_раз()
    {
        var facts = new List<string>();
        _eventBus.Subscribe<ChatIngestionStarted>(@event => facts.Add($"start {@event.At:HH:mm}"));
        _eventBus.Subscribe<ChatIngestionStopped>(@event => facts.Add($"stop {@event.At:HH:mm}"));

        _eventSubClient.SessionId.Returns("session-1");
        await _service.StartAsync(NullProgress, CancellationToken.None);

        _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(10);

        _eventSubClient.OnDisconnected += Raise.Event<EventSubAsyncHandler<EventSubDisconnectedArgs>>(
            new EventSubDisconnectedArgs("обрыв"),
            CancellationToken.None);

        _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(1);

        _eventSubClient.OnSessionWelcome += Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(
            new EventSubSessionWelcomeArgs("session-2", null),
            CancellationToken.None);

        _timeProvider.UtcNow = _timeProvider.UtcNow.AddMinutes(20);

        await _service.StopAsync(NullProgress, CancellationToken.None);
        await _service.StopAsync(NullProgress, CancellationToken.None);

        Assert.That(facts, Is.EqualTo(new[] { "start 18:00", "stop 18:10", "start 18:11", "stop 18:31" }));
    }

    [Test]
    public async Task Остановка_удаляет_подписку_последней_сессии_по_id_из_ответа_на_создание()
    {
        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("sub-1", "sub-2");

        _eventSubClient.SessionId.Returns("session-1");
        await _service.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnDisconnected += Raise.Event<EventSubAsyncHandler<EventSubDisconnectedArgs>>(
            new EventSubDisconnectedArgs("обрыв"),
            CancellationToken.None);

        _eventSubClient.OnSessionWelcome += Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(
            new EventSubSessionWelcomeArgs("session-2", null),
            CancellationToken.None);

        await _service.StopAsync(NullProgress, CancellationToken.None);
        await _service.StopAsync(NullProgress, CancellationToken.None);

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-2", Arg.Any<CancellationToken>());
        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync("sub-1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Подписка_полученная_после_разрыва_удаляется_даже_при_отменённом_токене_обработчика()
    {
        var created = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(created.Task);

        var deleted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _helix.DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var token = call.Arg<CancellationToken>();

                if (token.IsCancellationRequested)
                {
                    return Task.FromCanceled<bool>(token);
                }

                deleted.TrySetResult(call.Arg<string>());
                return Task.FromResult(true);
            });

        _eventSubClient.SessionId.Returns((string?)null);
        await _service.StartAsync(NullProgress, CancellationToken.None);

        using var handler = new CancellationTokenSource();

        _eventSubClient.OnSessionWelcome += Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(
            new EventSubSessionWelcomeArgs("session-1", null),
            handler.Token);

        _eventSubClient.OnDisconnected += Raise.Event<EventSubAsyncHandler<EventSubDisconnectedArgs>>(
            new EventSubDisconnectedArgs("обрыв"),
            CancellationToken.None);

        await handler.CancelAsync();
        created.SetResult("sub-late");

        Assert.That(await deleted.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo("sub-late"));
        Assert.That(_service.IsJoined, Is.False);
    }

    [Test]
    public async Task Остановка_удаляет_все_переиспользованные_по_409_подписки_текущей_сессии()
    {
        SetUpConflict();

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(new List<EventSubSubscriptionInfo>
            {
                ChatSubscription("sub-old-session", "session-0", BroadcasterId, BotId),
                ChatSubscription("sub-other-channel", "session-1", "99999", BotId),
                ChatSubscription("sub-other-bot", "session-1", BroadcasterId, "11111"),
                ChatSubscription("sub-reused", "session-1", BroadcasterId, BotId),
                ChatSubscription("sub-reused-twin", "session-1", BroadcasterId, BotId),
            });

        _eventSubClient.SessionId.Returns("session-1");
        await _service.StartAsync(NullProgress, CancellationToken.None);

        Assert.That(_service.IsJoined, Is.True);

        await _service.StopAsync(NullProgress, CancellationToken.None);

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-reused", Arg.Any<CancellationToken>());
        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-reused-twin", Arg.Any<CancellationToken>());
        await _helix.Received(2).DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static IEnumerable<TestCaseData> ConflictsWithoutOwnSubscription()
    {
        yield return new TestCaseData(new HelixRequestException(HttpMethod.Get,
            "/helix/eventsub/subscriptions",
            HttpStatusCode.InternalServerError,
            "oops",
            null), null).SetArgDisplayNames("поиск: HTTP 500");

        yield return new TestCaseData(new HttpRequestException("сеть недоступна"), null).SetArgDisplayNames("поиск: сеть");

        yield return new TestCaseData(null, new List<EventSubSubscriptionInfo>())
            .SetArgDisplayNames("подписки нет в списке");

        yield return new TestCaseData(null, new List<EventSubSubscriptionInfo> { ChatSubscription("sub-foreign", "session-0", BroadcasterId, BotId) })
            .SetArgDisplayNames("подписка на чужой активной сессии");
    }

    [TestCaseSource(nameof(ConflictsWithoutOwnSubscription))]
    public async Task Ответ_409_без_подписки_на_текущей_сессии_не_открывает_приём_и_ничего_не_удаляет(Exception? failure, List<EventSubSubscriptionInfo>? listed)
    {
        var starts = 0;
        _eventBus.Subscribe<ChatIngestionStarted>(_ => starts++);

        SetUpConflict();

        if (failure is null)
        {
            _helix.GetEventSubSubscriptionsByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(listed!);
        }
        else
        {
            _helix.GetEventSubSubscriptionsByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(failure);
        }

        _eventSubClient.SessionId.Returns("session-1");

        Assert.DoesNotThrowAsync(() => _service.StartAsync(NullProgress, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_service.IsJoined, Is.False);
            Assert.That(starts, Is.Zero);
        }

        await _service.StopAsync(NullProgress, CancellationToken.None);

        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestCase(false, TestName = "Приём чата открывается повтором, как только подписка другой сессии освобождается")]
    [TestCase(true, TestName = "Разрыв EventSub гасит повтор подписки чата")]
    public async Task Повтор_после_подписки_на_другой_активной_сессии(bool disconnect)
    {
        var time = new ManualTimeProvider();
        var service = new ChatIngestionService(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _botUserIdProvider,
            _settingsManager,
            _eventBus,
            time,
            NullLogger<ChatIngestionService>.Instance);

        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(ConflictException()), Task.FromResult("sub-retried"));

        _helix.GetEventSubSubscriptionsByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([ChatSubscription("sub-foreign", "session-0", BroadcasterId, BotId)]);

        _eventSubClient.SessionId.Returns("session-1");

        await service.StartAsync(NullProgress, CancellationToken.None);

        Assert.That(service.IsJoined, Is.False);

        await time.WaitForPendingTimersAsync(1);

        if (disconnect)
        {
            _eventSubClient.OnDisconnected += Raise.Event<EventSubAsyncHandler<EventSubDisconnectedArgs>>(new EventSubDisconnectedArgs("обрыв"),
                CancellationToken.None);

            await time.WaitForPendingTimersAsync(0);
        }

        time.Advance(EventSubSubscriptionRetry.FirstDelay);

        if (disconnect)
        {
            Assert.That(service.IsJoined, Is.False);
        }
        else
        {
            Assert.That(() => service.IsJoined, Is.True.After(5000, 10));
        }

        await service.StopAsync(NullProgress, CancellationToken.None);

        await _helix.Received(disconnect ? 1 : 2)
            .CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());

        await _helix.Received(disconnect ? 0 : 1).DeleteEventSubSubscriptionAsync("sub-retried", Arg.Any<CancellationToken>());
        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync("sub-foreign", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Подписка_отключённой_сессии_удаляется_и_приём_открывается_на_текущей()
    {
        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw ConflictException(), _ => "sub-new");

        var stale = ChatSubscription("sub-stale", "session-0", BroadcasterId, BotId) with { Status = "websocket_disconnected" };
        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(new List<EventSubSubscriptionInfo> { stale });

        _eventSubClient.SessionId.Returns("session-1");
        await _service.StartAsync(NullProgress, CancellationToken.None);

        Assert.That(_service.IsJoined, Is.True);
        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-stale", Arg.Any<CancellationToken>());

        await _service.StopAsync(NullProgress, CancellationToken.None);

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-new", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Переиспользованная_подписка_найденная_после_остановки_удаляется_и_приём_не_открывает()
    {
        var starts = 0;
        _eventBus.Subscribe<ChatIngestionStarted>(_ => starts++);

        SetUpConflict();

        var pending = new TaskCompletionSource<IReadOnlyList<EventSubSubscriptionInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _helix.GetEventSubSubscriptionsByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(pending.Task);

        _eventSubClient.SessionId.Returns("session-1");
        var start = _service.StartAsync(NullProgress, CancellationToken.None);

        await _service.StopAsync(NullProgress, CancellationToken.None);

        pending.SetResult([ChatSubscription("sub-late", "session-1", BroadcasterId, BotId)]);
        await start;

        Assert.Multiple(() =>
        {
            Assert.That(starts, Is.Zero);
            Assert.That(_service.IsJoined, Is.False);
        });

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-late", Arg.Any<CancellationToken>());
    }

    private static EventSubSubscriptionInfo ChatSubscription(string id, string sessionId, string broadcasterId, string botId)
    {
        return new(id,
            "channel.chat.message",
            "enabled",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = broadcasterId,
                ["user_id"] = botId,
            });
    }

    private void SetUpConflict()
    {
        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(ConflictException());
    }

    private static HelixRequestException ConflictException()
    {
        return new(HttpMethod.Post,
            "/helix/eventsub/subscriptions",
            HttpStatusCode.Conflict,
            "subscription already exists",
            null);
    }

    private static IEnumerable<TestCaseData> DeleteFailures()
    {
        yield return new TestCaseData(new HelixRequestException(HttpMethod.Delete,
            "/helix/eventsub/subscriptions",
            HttpStatusCode.InternalServerError,
            "oops",
            "{\"access_token\":\"секрет\"}")).SetArgDisplayNames("HTTP 500");

        yield return new TestCaseData(new HttpRequestException("сеть недоступна")).SetArgDisplayNames("сеть");
        yield return new TestCaseData(new OperationCanceledException()).SetArgDisplayNames("таймаут остановки");
    }

    [TestCaseSource(nameof(DeleteFailures))]
    public async Task Отказ_удаления_подписки_не_роняет_остановку(Exception failure)
    {
        var stops = 0;
        _eventBus.Subscribe<ChatIngestionStopped>(_ => stops++);

        _helix.DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(failure);

        _eventSubClient.SessionId.Returns("session-1");
        await _service.StartAsync(NullProgress, CancellationToken.None);

        Assert.DoesNotThrowAsync(() => _service.StopAsync(NullProgress, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stops, Is.EqualTo(1));
            Assert.That(_service.IsJoined, Is.False);
        }

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-id", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Подписка_завершившаяся_после_остановки_не_открывает_приём_и_удаляется()
    {
        var starts = 0;
        _eventBus.Subscribe<ChatIngestionStarted>(_ => starts++);

        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(pending.Task);

        _eventSubClient.SessionId.Returns("session-1");
        var start = _service.StartAsync(NullProgress, CancellationToken.None);

        await _service.StopAsync(NullProgress, CancellationToken.None);

        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        pending.SetResult("sub-late");
        await start;

        Assert.Multiple(() =>
        {
            Assert.That(starts, Is.Zero);
            Assert.That(_service.IsJoined, Is.False);
        });

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-late", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Отзыв_подписки_на_чат_пересоздаёт_её_в_текущей_сессии()
    {
        var facts = new List<string>();
        _eventBus.Subscribe<ChatIngestionStarted>(_ => facts.Add("start"));
        _eventBus.Subscribe<ChatIngestionStopped>(_ => facts.Add("stop"));

        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("sub-1", "sub-2");

        _eventSubClient.SessionId.Returns("session-1");
        await _service.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnRevocation += Raise.Event<EventSubAsyncHandler<EventSubRevocationArgs>>(
            new EventSubRevocationArgs("sub-other", "stream.online", "authorization_revoked"),
            CancellationToken.None);

        _eventSubClient.OnRevocation += Raise.Event<EventSubAsyncHandler<EventSubRevocationArgs>>(
            new EventSubRevocationArgs("sub-1", "channel.chat.message", "moderator_removed"),
            CancellationToken.None);

        _eventSubClient.OnRevocation += Raise.Event<EventSubAsyncHandler<EventSubRevocationArgs>>(
            new EventSubRevocationArgs("sub-1", "channel.chat.message", "moderator_removed"),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(facts, Is.EqualTo(new[] { "start", "stop", "start" }));
            Assert.That(_service.IsJoined, Is.True);
        }

        await _helix.Received(2).CreateEventSubSubscriptionAsync("channel.chat.message",
            "1",
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            "session-1",
            Arg.Any<CancellationToken>());

        await _service.StopAsync(NullProgress, CancellationToken.None);

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-2", Arg.Any<CancellationToken>());
        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync("sub-1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Отзыв_подписки_на_чат_без_активной_сессии_заканчивает_приём()
    {
        _eventSubClient.SessionId.Returns("session-1");
        await _service.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.SessionId.Returns((string?)null);

        _eventSubClient.OnRevocation += Raise.Event<EventSubAsyncHandler<EventSubRevocationArgs>>(
            new EventSubRevocationArgs("sub-id", "channel.chat.message", "authorization_revoked"),
            CancellationToken.None);

        Assert.That(_service.IsJoined, Is.False);

        await _helix.Received(1).CreateEventSubSubscriptionAsync(Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StartAsync_WhenSessionAlreadyActive_CreatesChatSubscriptionImmediately()
    {
        _eventSubClient.SessionId.Returns("existing-session");

        await _service.StartAsync(NullProgress, CancellationToken.None);

        await _helix.Received(1)
            .CreateEventSubSubscriptionAsync("channel.chat.message",
                "1",
                Arg.Is<IReadOnlyDictionary<string, string>>(d =>
                    d["broadcaster_user_id"] == BroadcasterId
                    && d["user_id"] == BotId),
                "existing-session",
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Новая_сессия_гасит_признак_подключения_к_чату_до_попыток_подписки()
    {
        _eventSubClient.SessionId.Returns("session-1");

        await _service.StartAsync(NullProgress, CancellationToken.None);

        Assert.That(_service.IsJoined, Is.True);

        _broadcasterIdProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(string.Empty);

        _eventSubClient.OnSessionWelcome += Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(
            new EventSubSessionWelcomeArgs("session-2", null),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_service.IsJoined, Is.False);
            Assert.That(_service.JoinedAt, Is.Null);
        });
    }

    [Test]
    public async Task StartAsync_WhenNoActiveSession_DoesNotCallHelixUntilWelcome()
    {
        _eventSubClient.SessionId.Returns((string?)null);

        await _service.StartAsync(NullProgress, CancellationToken.None);

        await _helix.DidNotReceive()
            .CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }
}
