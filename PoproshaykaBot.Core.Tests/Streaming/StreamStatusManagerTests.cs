using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Tests.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Tests.Polls;
using PoproshaykaBot.Core.Tests.Server;
using PoproshaykaBot.Core.Tests.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Net;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Streaming;

[TestFixture]
public sealed class StreamStatusManagerTests
{
    [SetUp]
    public void SetUp()
    {
        _apiStream = null;
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

        _clock = new() { UtcNow = new(2026, 4, 30, 12, 0, 0, TimeSpan.Zero) };

        _targetChannel = new("test-channel");

        _manager = new(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _targetChannel,
            _settingsManager,
            _eventBus,
            _clock,
            NullLogger<StreamStatusManager>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _manager.DisposeAsync();
    }

    private static readonly IProgress<string> NullProgress = new Progress<string>(_ => { });
    private const string BroadcasterId = "12345";

    private ITwitchEventSubClient _eventSubClient = null!;
    private ITwitchHelixClient _helix = null!;
    private IBroadcasterIdProvider _broadcasterIdProvider = null!;
    private FakeTargetChannelProvider _targetChannel = null!;
    private SettingsManager _settingsManager = null!;
    private AppSettings _settings = null!;
    private InMemoryEventBus _eventBus = null!;
    private TestTimeProvider _clock = null!;
    private StreamStatusManager _manager = null!;
    private HelixStreamInfo? _apiStream;

    private static EventSubNotificationArgs StreamOnlineNotification()
    {
        return new("stream.online",
            "1",
            "msg-1",
            DateTime.UtcNow,
            JsonSerializer.SerializeToElement(new { }));
    }

    private static EventSubNotificationArgs StreamOnlineNotification(string streamId, DateTime startedAt)
    {
        return new("stream.online",
            "1",
            "msg-1",
            DateTime.UtcNow,
            JsonSerializer.SerializeToElement(new
            {
                @event = new
                {
                    id = streamId,
                    broadcaster_user_id = BroadcasterId,
                    broadcaster_user_login = "bobito217",
                    broadcaster_user_name = "Bobito217",
                    type = "live",
                    started_at = startedAt,
                },
            }));
    }

    private static HelixStreamInfo SampleStream(string id = "stream-1", string title = "Тест")
    {
        return new(id,
            BroadcasterId,
            "bobito217",
            "Bobito217",
            "509658",
            "Just Chatting",
            "live",
            title,
            42,
            DateTime.UtcNow,
            "ru",
            "https://example.com/{width}x{height}.jpg",
            ["Russian"],
            false);
    }

    [Test]
    public async Task StopAsync_DrainsPendingMetadataRetryLoop()
    {
        var firstCallStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                firstCallStarted.TrySetResult();
                return Task.FromResult<HelixStreamInfo?>(null);
            });

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnNotification +=
            Raise.Event<EventSubAsyncHandler<EventSubNotificationArgs>>(StreamOnlineNotification(), CancellationToken.None);

        await firstCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(_manager.MetadataRetryTask, Is.Not.Null,
            "после stream.online retry-цикл должен быть зарегистрирован");

        var retryTask = _manager.MetadataRetryTask!;

        await _manager.StopAsync(NullProgress, CancellationToken.None);

        Assert.That(retryTask.IsCompleted, Is.True,
            "StopAsync должен дождаться завершения фонового retry-цикла");
    }

    [Test]
    public async Task OnDisconnected_ClearsCurrentStream()
    {
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(SampleStream());

        var onlineSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(_ => onlineSignal.TrySetResult());

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        var welcome = new EventSubSessionWelcomeArgs("session-1", 60);
        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(welcome, CancellationToken.None);

        await onlineSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(_manager.CurrentStream, Is.Not.Null);

        _eventSubClient.OnDisconnected +=
            Raise.Event<EventSubAsyncHandler<EventSubDisconnectedArgs>>(new EventSubDisconnectedArgs("test"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_manager.CurrentStatus, Is.EqualTo(StreamStatus.Unknown));
            Assert.That(_manager.CurrentStream, Is.Null,
                "OnDisconnected должен обнулить CurrentStream, иначе StreamInfoCommand отдаст устаревшие данные");
        }
    }

    [Test]
    public async Task RefreshLiveSnapshot_DoesNotImmediatelyTransitionOnlineToOffline()
    {
        var stream = SampleStream();
        var streamSequence = new Queue<HelixStreamInfo?>([stream, null]);

        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(_ => streamSequence.Dequeue());

        var offlineEvents = 0;
        _eventBus.Subscribe<StreamWentOffline>(_ => Interlocked.Increment(ref offlineEvents));

        var onlineSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(_ => onlineSignal.TrySetResult());

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await onlineSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(_manager.CurrentStatus, Is.EqualTo(StreamStatus.Online));

        await _manager.RefreshLiveSnapshotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_manager.CurrentStatus, Is.EqualTo(StreamStatus.Online),
                "первый Offline-ответ от API не должен мгновенно сбрасывать локальный Online");

            Assert.That(offlineEvents, Is.Zero);
        }
    }

    [Test]
    public async Task RefreshLiveSnapshot_ForcesOfflineAfterStuckThreshold()
    {
        var stream = SampleStream();
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(stream, null, null);

        var offlineReceived = new TaskCompletionSource<StreamWentOffline>(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOffline>(@event => offlineReceived.TrySetResult(@event));

        var onlineSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(_ => onlineSignal.TrySetResult());

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await onlineSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(_manager.CurrentStatus, Is.EqualTo(StreamStatus.Online));

        await _manager.RefreshLiveSnapshotAsync();
        Assert.That(_manager.CurrentStatus, Is.EqualTo(StreamStatus.Online));

        var stuckOnlineThreshold = TimeSpan.FromSeconds(_settings.Twitch.Infrastructure.StreamStuckOnlineThresholdSeconds);
        _clock.UtcNow = _clock.UtcNow.Add(stuckOnlineThreshold + TimeSpan.FromSeconds(1));

        await _manager.RefreshLiveSnapshotAsync();

        Assert.That(_manager.CurrentStatus, Is.EqualTo(StreamStatus.Offline),
            "после превышения StuckOnlineThreshold второй Offline-ответ должен принудительно перевести в Offline");

        var offline = await offlineReceived.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(offline.Channel, Is.EqualTo(_settings.Twitch.Channel));
    }

    [Test]
    public async Task StreamOnlineFromEventSub_PublishesStreamWentOnline_WithIsCatchUpFalse()
    {
        var stream = SampleStream();
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns(stream);

        var receivedSignal = new TaskCompletionSource<StreamWentOnline>(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(@event => receivedSignal.TrySetResult(@event));

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnNotification +=
            Raise.Event<EventSubAsyncHandler<EventSubNotificationArgs>>(StreamOnlineNotification(), CancellationToken.None);

        var received = await receivedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(received.IsCatchUp, Is.False);
    }

    [Test]
    public async Task InitializeFromApi_StreamAlreadyOnline_PublishesWithIsCatchUpTrue()
    {
        var stream = SampleStream();
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns(stream);

        var receivedSignal = new TaskCompletionSource<StreamWentOnline>(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(@event => receivedSignal.TrySetResult(@event));

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        var received = await receivedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(received.IsCatchUp, Is.True);
    }

    [Test]
    public async Task InitializeFromApi_StreamAlreadyOffline_PublishesWithIsCatchUpTrue()
    {
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns((HelixStreamInfo?)null);

        var receivedSignal = new TaskCompletionSource<StreamWentOffline>(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOffline>(@event => receivedSignal.TrySetResult(@event));

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        var received = await receivedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(received.IsCatchUp, Is.True,
            "первичный офлайн-снимок не должен триггерить авто-отключение бота – это узнавание состояния, а не настоящий переход стрима в офлайн");
    }

    [Test]
    public async Task StreamOfflineFromEventSub_PublishesStreamWentOffline_WithIsCatchUpFalse()
    {
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns(SampleStream());

        var onlineSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(_ => onlineSignal.TrySetResult());

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await onlineSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var offlineSignal = new TaskCompletionSource<StreamWentOffline>(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOffline>(@event => offlineSignal.TrySetResult(@event));

        var offlineNotification = new EventSubNotificationArgs("stream.offline",
            "1",
            "msg-offline",
            DateTime.UtcNow,
            JsonSerializer.SerializeToElement(new { }));

        _eventSubClient.OnNotification +=
            Raise.Event<EventSubAsyncHandler<EventSubNotificationArgs>>(offlineNotification, CancellationToken.None);

        var received = await offlineSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(received.IsCatchUp, Is.False,
            "переход Online → Offline по живому EventSub-уведомлению – настоящий переход, авто-отключение должно сработать");
    }

    [Test]
    public async Task OnSessionWelcome_CreatesSubscriptionsBeforeFetchingInitialStatus()
    {
        var subscriptionsCreated = 0;
        var streamFetchedBeforeSubscriptions = false;

        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref subscriptionsCreated);
                return Task.FromResult("sub-id");
            });

        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Volatile.Read(ref subscriptionsCreated) < 2)
                {
                    streamFetchedBeforeSubscriptions = true;
                }

                return Task.FromResult<HelixStreamInfo?>(SampleStream());
            });

        var onlineSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(_ => onlineSignal.TrySetResult());

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await onlineSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(streamFetchedBeforeSubscriptions, Is.False,
            "подписки stream.* должны создаваться до запроса начального статуса, иначе Twitch успеет закрыть сессию по 10-секундному дедлайну");
    }

    [Test]
    public async Task OnSessionReconnect_KeepsSubscriptionsAndStreamStatus()
    {
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns(SampleStream());

        var onlineSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(_ => onlineSignal.TrySetResult());

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await onlineSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _helix.ClearReceivedCalls();

        _eventSubClient.OnSessionReconnect +=
            Raise.Event<EventSubAsyncHandler<EventSubReconnectArgs>>(new EventSubReconnectArgs("wss://example.invalid/ws", "session-1", "session-2"),
                CancellationToken.None);

        await _helix.DidNotReceive().CreateEventSubSubscriptionAsync(Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _helix.DidNotReceive().GetStreamAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        Assert.That(_manager.CurrentStatus, Is.EqualTo(StreamStatus.Online),
            "миграция сессии EventSub переносит подписки на стороне Twitch – статус стрима сбрасывать нельзя");
    }

    [Test]
    public async Task RefreshLiveSnapshot_PreservesChannelUpdateValuesOverLaggingHelixSnapshot()
    {
        var oldSnapshot = new HelixStreamInfo("stream-1",
            BroadcasterId,
            "bobito217",
            "Bobito217",
            "509658",
            "Just Chatting",
            "live",
            "Старый заголовок",
            42,
            DateTime.UtcNow,
            "ru",
            "https://example.com/{width}x{height}.jpg",
            ["Russian"],
            false);

        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns(oldSnapshot);

        var onlineSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(_ => onlineSignal.TrySetResult());

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await onlineSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(_manager.CurrentStatus, Is.EqualTo(StreamStatus.Online));

        await _eventBus.PublishAsync(new ChannelUpdated("Новый заголовок", "ru", "999", "Программирование", []));

        await _manager.RefreshLiveSnapshotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_manager.CurrentStream, Is.Not.Null);
            Assert.That(_manager.CurrentStream!.Title, Is.EqualTo("Новый заголовок"),
                "EventSub channel.update авторитетен по title – Helix GetStreams лагает после смены профиля и не должен перетирать свежие значения");

            Assert.That(_manager.CurrentStream.GameId, Is.EqualTo("999"));
            Assert.That(_manager.CurrentStream.GameName, Is.EqualTo("Программирование"));
        }
    }

    [Test]
    public async Task OnSessionWelcome_WhenConflictOnOwnSession_ReusesAndDeletesOnStop()
    {
        var recordingLogger = new RecordingLogger<StreamStatusManager>();
        await using var manager = new StreamStatusManager(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _targetChannel,
            _settingsManager,
            _eventBus,
            _clock,
            recordingLogger);

        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HelixRequestException(HttpMethod.Post,
                "helix/eventsub/subscriptions",
                HttpStatusCode.Conflict,
                "subscription already exists",
                null));

        var initSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                initSignal.TrySetResult();
                return Task.FromResult<HelixStreamInfo?>(null);
            });

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([
                StreamSubscription("sub-online", "stream.online", "session-1"),
                StreamSubscription("sub-offline", "stream.offline", "session-1"),
                StreamSubscription("sub-foreign", "stream.online", "session-0"),
            ]);

        await manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await initSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var complaints = recordingLogger.Entries.Where(e => e.Level >= LogLevel.Warning).ToArray();
        Assert.That(complaints, Is.Empty,
            $"409 с подписками текущей сессии – переиспользование, а не сбой. Найдено: {string.Join(" | ", complaints.Select(e => e.Message))}");

        await manager.StopAsync(NullProgress, CancellationToken.None);

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-online", Arg.Any<CancellationToken>());
        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-offline", Arg.Any<CancellationToken>());
        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync("sub-foreign", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Разрыв_сокета_гасит_повтор_подписки()
    {
        var time = new ManualTimeProvider();
        await using var manager = new StreamStatusManager(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _targetChannel,
            _settingsManager,
            _eventBus,
            time,
            NullLogger<StreamStatusManager>.Instance);

        _helix.CreateEventSubSubscriptionAsync("stream.online",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new HelixRequestException(HttpMethod.Post,
                    "helix/eventsub/subscriptions",
                    HttpStatusCode.Conflict,
                    "subscription already exists",
                    null)),
                Task.FromResult("sub-dead"));

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([StreamSubscription("sub-foreign", "stream.online", "session-0")]);

        _eventSubClient.SessionId.Returns("session-1");
        await manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await time.WaitForPendingTimersAsync(1);

        _eventSubClient.OnDisconnected +=
            Raise.Event<EventSubAsyncHandler<EventSubDisconnectedArgs>>(new EventSubDisconnectedArgs("обрыв"), CancellationToken.None);

        time.Advance(EventSubSubscriptionRetry.MaxDelay);
        await Task.Yield();

        await _helix.Received(1)
            .CreateEventSubSubscriptionAsync("stream.online", Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        Assert.That(time.PendingTimers, Is.Zero);

        await manager.StopAsync(NullProgress, CancellationToken.None);
    }

    [Test]
    public async Task OnSessionWelcome_WhenConflictHeldByOtherLiveSession_WarnsAndDoesNotTouchIt()
    {
        var recordingLogger = new RecordingLogger<StreamStatusManager>();
        await using var manager = new StreamStatusManager(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _targetChannel,
            _settingsManager,
            _eventBus,
            _clock,
            recordingLogger);

        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HelixRequestException(HttpMethod.Post,
                "helix/eventsub/subscriptions",
                HttpStatusCode.Conflict,
                "subscription already exists",
                null));

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([
                StreamSubscription("sub-online", "stream.online", "session-0"),
                StreamSubscription("sub-offline", "stream.offline", "session-0"),
            ]);

        await manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await manager.StopAsync(NullProgress, CancellationToken.None);

        Assert.That(recordingLogger.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("session-0")), Is.EqualTo(2));
        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static EventSubSubscriptionInfo StreamSubscription(string id, string type, string sessionId)
    {
        return new(id,
            type,
            "enabled",
            sessionId,
            new Dictionary<string, string> { ["broadcaster_user_id"] = BroadcasterId });
    }

    [Test]
    public async Task OnRevocation_ForUnrelatedSubscriptionType_DoesNotCallHelix_AfterFix()
    {
        _eventSubClient.SessionId.Returns("session-1");
        await _manager.StartAsync(NullProgress, CancellationToken.None);
        _helix.ClearReceivedCalls();

        _eventSubClient.OnRevocation += Raise.Event<EventSubAsyncHandler<EventSubRevocationArgs>>(new EventSubRevocationArgs("sub-id", "channel.chat.message", "user_removed"),
            CancellationToken.None);

        await Task.Delay(50);

        await _helix.DidNotReceive()
            .CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleStreamOnline_PublishesStreamMetadataResolved_AfterMetadataFetch()
    {
        var stream = SampleStream("stream-online-1");
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns(stream);

        StreamMetadataResolved? received = null;
        var receivedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamMetadataResolved>(@event =>
        {
            received = @event;
            receivedSignal.TrySetResult();
        });

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnNotification +=
            Raise.Event<EventSubAsyncHandler<EventSubNotificationArgs>>(StreamOnlineNotification(), CancellationToken.None);

        await receivedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(received, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(received!.Stream.Id, Is.EqualTo("stream-online-1"));
            Assert.That(received.Channel, Is.EqualTo(_settings.Twitch.Channel));
        }
    }

    [Test]
    public async Task RefreshLiveSnapshot_AfterBareStreamOnline_PublishesStreamMetadataResolved()
    {
        var stream = SampleStream("stream-bare-1");
        var received = new TaskCompletionSource<StreamMetadataResolved>(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamMetadataResolved>(@event => received.TrySetResult(@event));

        await StartWithBareOnlineAsync();

        _apiStream = stream;

        await _manager.RefreshLiveSnapshotAsync();

        var resolved = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resolved.Stream.Id, Is.EqualTo("stream-bare-1"));
            Assert.That(resolved.Stream.ThumbnailUrl, Is.EqualTo("https://example.com/{width}x{height}.jpg"),
                "догон метаданных без смены статуса обязан доехать до подписчиков шины, иначе плитка превью остаётся пустой");

            Assert.That(resolved.Channel, Is.EqualTo(_settings.Twitch.Channel));
        }
    }

    [Test]
    public async Task RefreshLiveSnapshot_WithoutSnapshotChange_DoesNotPublishMetadataResolved()
    {
        var stream = SampleStream();
        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns(stream);

        var metadataEvents = 0;
        _eventBus.Subscribe<StreamMetadataResolved>(_ => Interlocked.Increment(ref metadataEvents));

        var onlineSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(_ => onlineSignal.TrySetResult());

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await onlineSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await _manager.RefreshLiveSnapshotAsync();
        await _manager.RefreshLiveSnapshotAsync();

        Assert.That(metadataEvents, Is.Zero,
            "снимок не изменился – опрос по таймеру не должен превращаться в событие шины каждые 30 секунд");
    }

    [Test]
    public async Task RefreshLiveSnapshot_ConcurrentCalls_PublishMetadataResolvedOnce()
    {
        var metadataEvents = 0;
        _eventBus.Subscribe<StreamMetadataResolved>(_ => Interlocked.Increment(ref metadataEvents));

        await StartWithBareOnlineAsync();

        _apiStream = SampleStream("stream-bare-2");

        await Task.WhenAll(Task.Run(() => _manager.RefreshLiveSnapshotAsync()),
            Task.Run(() => _manager.RefreshLiveSnapshotAsync()));

        Assert.That(metadataEvents, Is.EqualTo(1),
            "одновременные опросы из таймера и кнопки дают одно событие: снимок сравнивается и подменяется под замком состояния");
    }

    [Test]
    public async Task InitializeFromApi_OverlappedByRefresh_DoesNotLetTheStaleSnapshotWin()
    {
        var stale = SampleStream(title: "Старый заголовок");
        var fresh = SampleStream(title: "Свежий заголовок");

        var calls = 0;
        var initializeReachedHelix = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleAnswer = new TaskCompletionSource<HelixStreamInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    initializeReachedHelix.TrySetResult();
                    return staleAnswer.Task;
                }

                return Task.FromResult<HelixStreamInfo?>(fresh);
            });

        StreamMetadataResolved? lastMetadata = null;
        var metadataReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamMetadataResolved>(@event =>
        {
            lastMetadata = @event;
            metadataReceived.TrySetResult();
        });

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnSessionWelcome +=
            Raise.Event<EventSubAsyncHandler<EventSubSessionWelcomeArgs>>(new EventSubSessionWelcomeArgs("session-1", 60), CancellationToken.None);

        await initializeReachedHelix.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var refreshTask = _manager.RefreshLiveSnapshotAsync();

        staleAnswer.SetResult(stale);

        await refreshTask.WaitAsync(TimeSpan.FromSeconds(2));
        await metadataReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_manager.CurrentStream!.Title, Is.EqualTo("Свежий заголовок"),
                "ответ Helix, начатый раньше, не должен лечь поверх более свежего: опрос из welcome и опрос по таймеру идут через один замок");

            Assert.That(lastMetadata!.Stream.Title, Is.EqualTo("Свежий заголовок"),
                "устаревшие метаданные не должны уехать подписчикам последними");
        }
    }

    private async Task StartWithBareOnlineAsync()
    {
        var firstAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _helix.GetStreamAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                firstAttempt.TrySetResult();
                return Task.FromResult(_apiStream);
            });

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnNotification +=
            Raise.Event<EventSubAsyncHandler<EventSubNotificationArgs>>(StreamOnlineNotification(), CancellationToken.None);

        await firstAttempt.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task HandleStreamOnline_PublishesStreamWentOnline_WithIdAndStartFromNotification()
    {
        var startedAt = new DateTime(2026, 5, 17, 10, 0, 0, DateTimeKind.Utc);

        StreamWentOnline? received = null;
        var receivedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _eventBus.Subscribe<StreamWentOnline>(@event =>
        {
            received = @event;
            receivedSignal.TrySetResult();
        });

        await _manager.StartAsync(NullProgress, CancellationToken.None);

        _eventSubClient.OnNotification +=
            Raise.Event<EventSubAsyncHandler<EventSubNotificationArgs>>(StreamOnlineNotification("stream-online-2", startedAt),
                CancellationToken.None);

        await receivedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(received?.Stream, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(received!.Stream!.Id, Is.EqualTo("stream-online-2"));
            Assert.That(received.Stream.StartedAt, Is.EqualTo(startedAt));
        }
    }
}
