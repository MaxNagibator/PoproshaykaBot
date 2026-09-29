using NSubstitute.ExceptionExtensions;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Polling;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Tests.Debugging;
using PoproshaykaBot.Core.Tests.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Tests.Polls;

[TestFixture]
public sealed class PollEventSubscriberTests
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

        _helix.GetPollsAsync(Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<HelixPollInfo>());

        _broadcasterIdProvider = Substitute.For<IBroadcasterIdProvider>();
        _broadcasterIdProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(BroadcasterId);

        _availability = Substitute.For<PollsAvailabilityService>(Substitute.For<ITwitchHelixClient>(),
            _broadcasterIdProvider,
            new FakeTargetChannelProvider(),
            new AccountsStore(),
            NullLogger<PollsAvailabilityService>.Instance);

        _availability.GetAsync(Arg.Any<CancellationToken>()).Returns(PollsAvailability.Available);

        _eventBus = new(NullLogger<InMemoryEventBus>.Instance);

        _subscriber = new(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _availability,
            _eventBus,
            TimeProvider.System,
            NullLogger<PollEventSubscriber>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _subscriber.StopAsync(NullProgress, CancellationToken.None);
    }

    private static readonly IProgress<string> NullProgress = new Progress<string>(_ => { });
    private const string BroadcasterId = "12345";

    private ITwitchEventSubClient _eventSubClient = null!;
    private ITwitchHelixClient _helix = null!;
    private IBroadcasterIdProvider _broadcasterIdProvider = null!;
    private PollsAvailabilityService _availability = null!;
    private InMemoryEventBus _eventBus = null!;
    private PollEventSubscriber _subscriber = null!;

    [Test]
    public async Task StartAsync_WhenSessionAlreadyActive_CreatesPollSubscriptionsImmediately()
    {
        _eventSubClient.SessionId.Returns("existing-session");

        await _subscriber.StartAsync(NullProgress, CancellationToken.None);

        foreach (var type in new[] { "channel.poll.begin", "channel.poll.progress", "channel.poll.end" })
        {
            await _helix.Received(1)
                .CreateEventSubSubscriptionAsync(type,
                    "1",
                    Arg.Is<IReadOnlyDictionary<string, string>>(d => d["broadcaster_user_id"] == BroadcasterId),
                    "existing-session",
                    Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Остановка_удаляет_созданные_и_переиспользованные_по_409_подписки()
    {
        _helix.CreateEventSubSubscriptionAsync("channel.poll.progress",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HelixRequestException(HttpMethod.Post,
                "helix/eventsub/subscriptions",
                System.Net.HttpStatusCode.Conflict,
                "subscription already exists",
                null));

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([
                new EventSubSubscriptionInfo("sub-progress",
                    "channel.poll.progress",
                    "enabled",
                    "session-1",
                    new Dictionary<string, string> { ["broadcaster_user_id"] = BroadcasterId }),
            ]);

        _eventSubClient.SessionId.Returns("session-1");
        await _subscriber.StartAsync(NullProgress, CancellationToken.None);

        Assert.That(_subscriber.IsHealthy, Is.True);

        await _subscriber.StopAsync(NullProgress, CancellationToken.None);

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-progress", Arg.Any<CancellationToken>());
        await _helix.Received(3).DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Подписчик_здоров_только_когда_повтор_добрал_занятый_тип()
    {
        var time = new ManualTimeProvider();
        var subscriber = new PollEventSubscriber(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _availability,
            _eventBus,
            time,
            NullLogger<PollEventSubscriber>.Instance);

        _helix.CreateEventSubSubscriptionAsync("channel.poll.progress",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new HelixRequestException(HttpMethod.Post,
                    "helix/eventsub/subscriptions",
                    System.Net.HttpStatusCode.Conflict,
                    "subscription already exists",
                    null)),
                Task.FromResult("sub-progress-retried"));

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([
                new EventSubSubscriptionInfo("sub-progress-foreign",
                    "channel.poll.progress",
                    "enabled",
                    "session-previous",
                    new Dictionary<string, string> { ["broadcaster_user_id"] = BroadcasterId }),
            ]);

        _eventSubClient.SessionId.Returns("session-1");
        await subscriber.StartAsync(NullProgress, CancellationToken.None);

        Assert.That(subscriber.IsHealthy, Is.False);

        await time.WaitForPendingTimersAsync(1);
        time.Advance(EventSubSubscriptionRetry.FirstDelay);

        Assert.That(() => subscriber.IsHealthy, Is.True.After(5000, 10));

        await subscriber.StopAsync(NullProgress, CancellationToken.None);

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-progress-retried", Arg.Any<CancellationToken>());
        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync("sub-progress-foreign", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Повтор_добравший_тип_раньше_конца_основного_цикла_не_оставляет_подписчика_нездоровым()
    {
        var time = new ManualTimeProvider();
        var subscriber = new PollEventSubscriber(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _availability,
            _eventBus,
            time,
            NullLogger<PollEventSubscriber>.Instance);

        _helix.CreateEventSubSubscriptionAsync("channel.poll.begin",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Conflict(), Task.FromResult("sub-begin-retried"));

        var endCreated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        _helix.CreateEventSubSubscriptionAsync("channel.poll.end",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(endCreated.Task);

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([ForeignPollSubscription("channel.poll.begin")]);

        _eventSubClient.SessionId.Returns("session-1");
        var start = subscriber.StartAsync(NullProgress, CancellationToken.None);

        await time.WaitForPendingTimersAsync(1);
        time.Advance(EventSubSubscriptionRetry.FirstDelay);

        await _helix.Received(2)
            .CreateEventSubSubscriptionAsync("channel.poll.begin", Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), "session-1", Arg.Any<CancellationToken>());

        await time.WaitForPendingTimersAsync(0);

        endCreated.SetResult("sub-end");
        await start.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(() => subscriber.IsHealthy, Is.True.After(5000, 10));

        await subscriber.StopAsync(NullProgress, CancellationToken.None);
    }

    [Test]
    public async Task Разрыв_сокета_гасит_повтор_занятого_типа()
    {
        var time = new ManualTimeProvider();
        var subscriber = new PollEventSubscriber(_eventSubClient,
            _helix,
            _broadcasterIdProvider,
            _availability,
            _eventBus,
            time,
            NullLogger<PollEventSubscriber>.Instance);

        _helix.CreateEventSubSubscriptionAsync("channel.poll.begin",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Conflict(), Task.FromResult("sub-begin-dead"));

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([ForeignPollSubscription("channel.poll.begin")]);

        _eventSubClient.SessionId.Returns("session-1");
        await subscriber.StartAsync(NullProgress, CancellationToken.None);
        await time.WaitForPendingTimersAsync(1);

        _eventSubClient.OnDisconnected += Raise.Event<EventSubAsyncHandler<EventSubDisconnectedArgs>>(new EventSubDisconnectedArgs("обрыв"),
            CancellationToken.None);

        time.Advance(EventSubSubscriptionRetry.MaxDelay);
        await Task.Yield();

        await _helix.Received(1)
            .CreateEventSubSubscriptionAsync("channel.poll.begin", Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        Assert.That(time.PendingTimers, Is.Zero);

        await subscriber.StopAsync(NullProgress, CancellationToken.None);
    }

    [Test]
    public async Task Отзыв_подписки_опросов_пересоздаёт_ровно_отозванный_тип()
    {
        _helix.CreateEventSubSubscriptionAsync("channel.poll.begin",
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns("sub-begin-first", "sub-begin-restored");

        _eventSubClient.SessionId.Returns("session-1");
        await _subscriber.StartAsync(NullProgress, CancellationToken.None);
        _helix.ClearReceivedCalls();

        _eventSubClient.OnRevocation += Raise.Event<EventSubAsyncHandler<EventSubRevocationArgs>>(new EventSubRevocationArgs("sub-begin-first", "channel.poll.begin", "user_removed"),
            CancellationToken.None);

        await _helix.Received(1)
            .CreateEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await _helix.Received(1)
            .CreateEventSubSubscriptionAsync("channel.poll.begin",
                "1",
                Arg.Is<IReadOnlyDictionary<string, string>>(d => d["broadcaster_user_id"] == BroadcasterId),
                "session-1",
                Arg.Any<CancellationToken>());

        Assert.That(_subscriber.IsHealthy, Is.True);

        await _subscriber.StopAsync(NullProgress, CancellationToken.None);

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-begin-restored", Arg.Any<CancellationToken>());
        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync("sub-begin-first", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Отзыв_при_недоступных_опросах_не_пересоздаёт_подписку()
    {
        _eventSubClient.SessionId.Returns("session-1");
        await _subscriber.StartAsync(NullProgress, CancellationToken.None);
        _helix.ClearReceivedCalls();

        _availability.GetAsync(Arg.Any<CancellationToken>()).Returns(PollsAvailability.NoBroadcasterToken);

        _eventSubClient.OnRevocation += Raise.Event<EventSubAsyncHandler<EventSubRevocationArgs>>(new EventSubRevocationArgs("sub-id", "channel.poll.end", "authorization_revoked"),
            CancellationToken.None);

        await _helix.DidNotReceive()
            .CreateEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        Assert.That(_subscriber.IsHealthy, Is.False);
    }

    private static EventSubSubscriptionInfo ForeignPollSubscription(string type)
    {
        return new(type + "-foreign",
            type,
            "enabled",
            "session-previous",
            new Dictionary<string, string> { ["broadcaster_user_id"] = BroadcasterId });
    }

    private static Task<string> Conflict()
    {
        return Task.FromException<string>(new HelixRequestException(HttpMethod.Post,
            "helix/eventsub/subscriptions",
            System.Net.HttpStatusCode.Conflict,
            "subscription already exists",
            null));
    }

    [Test]
    public void PollEventSubscriber_ImplementsStreamHostedComponent_AfterFix()
    {
        Assert.That(typeof(PollEventSubscriber).GetInterfaces(),
            Does.Contain(typeof(IStreamHostedComponent)),
            "голосования принадлежат broadcaster-сессии – подписчик должен быть IStreamHostedComponent наравне с ChannelUpdateSubscriber, иначе IRC-дисконнект бота гасит channel.poll.*");
    }

    [Test]
    public async Task StartAsync_WhenNoActiveSession_DoesNotCallHelixUntilWelcome()
    {
        _eventSubClient.SessionId.Returns((string?)null);

        await _subscriber.StartAsync(NullProgress, CancellationToken.None);

        await _helix.DidNotReceive()
            .CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [TestCase("COMPLETED")]
    [TestCase("ARCHIVED")]
    [TestCase("TERMINATED")]
    [TestCase("SOMETHING_NEW")]
    public async Task StartAsync_WhenHelixReturnsNoActivePolls_DoesNotPublishPollStarted(string status)
    {
        _eventSubClient.SessionId.Returns("existing-session");
        _helix.GetPollsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Poll("finished", status, DateTime.UtcNow.AddDays(-30))]);

        var started = new List<PollStarted>();
        using var subscription = _eventBus.Subscribe<PollStarted>(started.Add);

        await _subscriber.StartAsync(NullProgress, CancellationToken.None);

        Assert.That(started, Is.Empty,
            "Get Polls отдаёт и завершённые, и незнакомые по статусу голосования – восстанавливать из них нечего");
    }

    [Test]
    public async Task StartAsync_WhenHelixReturnsFinishedAndActivePolls_PublishesActiveOnce()
    {
        _eventSubClient.SessionId.Returns("existing-session");
        _helix.GetPollsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                Poll("finished", "COMPLETED", DateTime.UtcNow.AddDays(-30)),
                Poll("running", "ACTIVE", null),
            ]);

        var started = new List<PollStarted>();
        using var subscription = _eventBus.Subscribe<PollStarted>(started.Add);

        await _subscriber.StartAsync(NullProgress, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(started, Has.Count.EqualTo(1));
            Assert.That(started[0].Snapshot.PollId, Is.EqualTo("running"));
        }
    }

    private static HelixPollInfo Poll(string id, string status, DateTime? endedAt)
    {
        return new(id,
            BroadcasterId,
            "Вопрос?",
            [new("c1", "А", 3, 0, 0), new("c2", "Б", 1, 0, 0)],
            false,
            0,
            status,
            60,
            DateTime.UtcNow.AddMinutes(-5),
            endedAt);
    }
}
