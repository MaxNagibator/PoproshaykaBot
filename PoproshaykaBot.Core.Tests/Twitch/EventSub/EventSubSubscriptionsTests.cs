using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using PoproshaykaBot.Core.Tests.Server;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Net;

namespace PoproshaykaBot.Core.Tests.Twitch.EventSub;

[TestFixture]
public sealed class EventSubSubscriptionsTests
{
    [SetUp]
    public void SetUp()
    {
        _helix = Substitute.For<ITwitchHelixClient>();
        _helix.DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _logger = new();
    }

    private const string Type = "stream.online";
    private const string BroadcasterId = "12345";
    private const string CurrentSession = "session-current";

    private static readonly IReadOnlyDictionary<string, string> Condition = new Dictionary<string, string>
    {
        ["broadcaster_user_id"] = BroadcasterId,
    };

    private ITwitchHelixClient _helix = null!;
    private RecordingLogger<EventSubSubscriptionsTests> _logger = null!;

    private static IEnumerable<TestCaseData> Conflicts()
    {
        yield return new TestCaseData(
                new[] { Subscription("sub-own", CurrentSession, "enabled") },
                EventSubSubscribeOutcome.Reused,
                new[] { "sub-own" })
            .SetArgDisplayNames("своя сессия");

        yield return new TestCaseData(
                new[]
                {
                    Subscription("sub-own-1", CurrentSession, "enabled"),
                    Subscription("sub-own-2", CurrentSession, "enabled"),
                    Subscription("sub-stale", "session-old", "websocket_disconnected"),
                },
                EventSubSubscribeOutcome.Reused,
                new[] { "sub-own-1", "sub-own-2" })
            .SetArgDisplayNames("несколько на своей сессии");

        yield return new TestCaseData(
                new[] { Subscription("sub-foreign", "session-other", "enabled") },
                EventSubSubscribeOutcome.TakenByOtherSession,
                Array.Empty<string>())
            .SetArgDisplayNames("чужая активная сессия");

        yield return new TestCaseData(
                new[]
                {
                    Subscription("sub-other-type", CurrentSession, "enabled", "stream.offline"),
                    Subscription("sub-other-channel", CurrentSession, "enabled", Type, "99999"),
                },
                EventSubSubscribeOutcome.Unresolved,
                Array.Empty<string>())
            .SetArgDisplayNames("совпадений нет");
    }

    [TestCaseSource(nameof(Conflicts))]
    public async Task Ответ_409_классифицируется_по_сессии_и_статусу_найденной_подписки(
        EventSubSubscriptionInfo[] listed,
        EventSubSubscribeOutcome expectedOutcome,
        string[] expectedIds)
    {
        SetUpCreate(Conflict());
        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns(listed);

        var result = await CreateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(expectedOutcome));
            Assert.That(result.SubscriptionIds, Is.EqualTo(expectedIds));
            Assert.That(result.IsSubscribed, Is.EqualTo(expectedIds.Length > 0));
            Assert.That(_logger.Entries.Any(e => e.Level == LogLevel.Warning), Is.EqualTo(expectedIds.Length == 0));
        }

        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Подписка_отключённой_сессии_удаляется_и_создаётся_заново_на_текущей()
    {
        SetUpCreate(Conflict(), Task.FromResult("sub-new"));
        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([
                Subscription("sub-stale-1", "session-old", "websocket_disconnected"),
                Subscription("sub-stale-2", "session-older", "websocket_network_timeout"),
                Subscription("sub-revoked", CurrentSession, "authorization_revoked"),
            ]);

        var result = await CreateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(EventSubSubscribeOutcome.Created));
            Assert.That(result.SubscriptionIds, Is.EqualTo(new[] { "sub-new" }));
        }

        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-stale-1", Arg.Any<CancellationToken>());
        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-stale-2", Arg.Any<CancellationToken>());
        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-revoked", Arg.Any<CancellationToken>());
        await _helix.Received(2).CreateEventSubSubscriptionAsync(Type, "1", Condition, CurrentSession, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Повторный_409_после_уборки_не_зацикливается_и_не_подключает()
    {
        SetUpCreate(Conflict());
        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([Subscription("sub-stale", "session-old", "websocket_disconnected")]);

        var result = await CreateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(EventSubSubscribeOutcome.Unresolved));
            Assert.That(result.IsSubscribed, Is.False);
        }

        await _helix.Received(2).CreateEventSubSubscriptionAsync(Type, "1", Condition, CurrentSession, Arg.Any<CancellationToken>());
        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-stale", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Несостоявшийся_поиск_не_подключает_и_ничего_не_удаляет()
    {
        SetUpCreate(Conflict());
        _helix.GetEventSubSubscriptionsByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("сеть недоступна"));

        var result = await CreateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(EventSubSubscribeOutcome.LookupFailed));
            Assert.That(result.IsSubscribed, Is.False);
        }

        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Ошибка_создания_кроме_409_уходит_вызывающему()
    {
        SetUpCreate(Task.FromException<string>(new HelixRequestException(HttpMethod.Post,
            "/helix/eventsub/subscriptions",
            HttpStatusCode.InternalServerError,
            "oops",
            null)));

        var ex = Assert.ThrowsAsync<HelixRequestException>(CreateAsync);

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    public async Task Чужая_активная_сессия_не_мешает_убрать_подписку_отключённой()
    {
        SetUpCreate(Conflict());
        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([
                Subscription("sub-foreign", "session-other", "enabled"),
                Subscription("sub-stale", "session-old", "websocket_disconnected"),
            ]);

        var result = await CreateAsync();

        Assert.That(result.Outcome, Is.EqualTo(EventSubSubscribeOutcome.TakenByOtherSession));
        await _helix.Received(1).DeleteEventSubSubscriptionAsync("sub-stale", Arg.Any<CancellationToken>());
        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync("sub-foreign", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Отмена_удаления_не_считается_сбоем_и_не_шлёт_оставшиеся_запросы()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _helix.DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await EventSubSubscriptions.DeleteAsync(_helix, Type, ["sub-1", "sub-2"], _logger, cts.Token);

        Assert.That(_logger.Entries.Where(x => x.Level >= LogLevel.Warning), Is.Empty);
        await _helix.Received(1).DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private Task<EventSubSubscribeResult> CreateAsync()
    {
        return EventSubSubscriptions.CreateAsync(_helix, Type, "1", Condition, CurrentSession, _logger, CancellationToken.None);
    }

    private void SetUpCreate(Task<string> first, params Task<string>[] rest)
    {
        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(first, rest);
    }

    private static Task<string> Conflict()
    {
        return Task.FromException<string>(new HelixRequestException(HttpMethod.Post,
            "/helix/eventsub/subscriptions",
            HttpStatusCode.Conflict,
            "subscription already exists",
            null));
    }

    private static EventSubSubscriptionInfo Subscription(string id, string sessionId, string status, string type = Type, string broadcasterId = BroadcasterId)
    {
        return new(id,
            type,
            status,
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = broadcasterId,
            });
    }
}
