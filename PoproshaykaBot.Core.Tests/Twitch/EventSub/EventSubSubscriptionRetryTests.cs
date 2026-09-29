using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Tests.Server;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Net;

namespace PoproshaykaBot.Core.Tests.Twitch.EventSub;

[TestFixture]
public sealed class EventSubSubscriptionRetryTests
{
    [SetUp]
    public void SetUp()
    {
        _helix = Substitute.For<ITwitchHelixClient>();
        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns([
                new EventSubSubscriptionInfo("sub-foreign",
                    Type,
                    "enabled",
                    "session-other",
                    new Dictionary<string, string> { ["broadcaster_user_id"] = BroadcasterId }),
            ]);

        _eventSubClient = Substitute.For<ITwitchEventSubClient>();
        _eventSubClient.SessionId.Returns(CurrentSession);

        _time = new();
        _logger = new();
        _retry = new(_helix, _eventSubClient, _time, _logger);
        _retried = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _retry.CancelAndDrainAsync(CancellationToken.None);
    }

    private const string Type = "channel.update";
    private const string BroadcasterId = "12345";
    private const string CurrentSession = "session-current";

    private static readonly IReadOnlyDictionary<string, string> Condition = new Dictionary<string, string>
    {
        ["broadcaster_user_id"] = BroadcasterId,
    };

    private ITwitchHelixClient _helix = null!;
    private ITwitchEventSubClient _eventSubClient = null!;
    private ManualTimeProvider _time = null!;
    private RecordingLogger<EventSubSubscriptionRetryTests> _logger = null!;
    private EventSubSubscriptionRetry _retry = null!;
    private TaskCompletionSource<EventSubSubscribeResult> _retried = null!;

    public enum StopKind
    {
        None = 0,
        SessionChanged = 1,
        Cancel = 2,
        CancelAndDrain = 3,
        CallerToken = 4,
    }

    [Test]
    public async Task Занятая_подписка_берётся_повтором_с_растущей_выдержкой_без_предупреждения_на_каждую_попытку()
    {
        SetUpCreate(Conflict(), Conflict(), Conflict(), Task.FromResult("sub-new"));

        var first = await CreateAsync(CancellationToken.None);

        Assert.That(first.Outcome, Is.EqualTo(EventSubSubscribeOutcome.TakenByOtherSession));

        foreach (var seconds in new[] { 5, 10 })
        {
            await _time.WaitForPendingTimersAsync(1);
            _time.Advance(TimeSpan.FromSeconds(seconds) - TimeSpan.FromMilliseconds(1));
            await ReceivedCreates(1 + (seconds == 5 ? 0 : 1));
            _time.Advance(TimeSpan.FromMilliseconds(1));
        }

        await _time.WaitForPendingTimersAsync(1);
        _time.Advance(TimeSpan.FromSeconds(20));

        var retried = await _retried.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(retried.Outcome, Is.EqualTo(EventSubSubscribeOutcome.Created));
            Assert.That(retried.SubscriptionIds, Is.EqualTo(new[] { "sub-new" }));
            Assert.That(_logger.Entries.Count(x => x.Level >= LogLevel.Warning), Is.EqualTo(1));
            Assert.That(_time.PendingTimers, Is.Zero);
        }

        await ReceivedCreates(4);
        await _helix.DidNotReceive().DeleteEventSubSubscriptionAsync("sub-foreign", Arg.Any<CancellationToken>());
    }

    [TestCase(StopKind.SessionChanged, TestName = "Повтор прекращается на смене сессии")]
    [TestCase(StopKind.Cancel, TestName = "Повтор прекращается на отмене подписчиком")]
    [TestCase(StopKind.CancelAndDrain, TestName = "Повтор прекращается на остановке подписчика")]
    [TestCase(StopKind.CallerToken, TestName = "Повтор прекращается на отмене токена вызывающего")]
    public async Task Повтор_прекращается_без_висящих_таймеров_и_новых_запросов(StopKind stop)
    {
        using var cts = new CancellationTokenSource();
        SetUpCreate(Conflict(), Task.FromResult("sub-new"));

        await CreateAsync(cts.Token);
        await _time.WaitForPendingTimersAsync(1);

        switch (stop)
        {
            case StopKind.SessionChanged:
                _eventSubClient.SessionId.Returns("session-next");
                _time.Advance(EventSubSubscriptionRetry.FirstDelay);
                break;

            case StopKind.Cancel:
                _retry.Cancel();
                break;

            case StopKind.CancelAndDrain:
                await _retry.CancelAndDrainAsync(CancellationToken.None);
                break;

            case StopKind.CallerToken:
                await cts.CancelAsync();
                break;
        }

        await _time.WaitForPendingTimersAsync(0);
        _time.Advance(EventSubSubscriptionRetry.MaxDelay);

        Assert.That(_retried.Task.IsCompleted, Is.False);
        await ReceivedCreates(1);
    }

    [Test]
    public async Task Повтор_ограничен_потолком_попыток_и_сдаётся_одним_предупреждением()
    {
        SetUpCreate(Conflict());

        await CreateAsync(CancellationToken.None);

        for (var attempt = 0; attempt < EventSubSubscriptionRetry.MaxAttempts; attempt++)
        {
            await _time.WaitForPendingTimersAsync(1);
            _time.Advance(EventSubSubscriptionRetry.MaxDelay);
        }

        await _time.WaitForPendingTimersAsync(0);
        _time.Advance(EventSubSubscriptionRetry.MaxDelay);

        await ReceivedCreates(1 + EventSubSubscriptionRetry.MaxAttempts);

        var warnings = _logger.Entries.Where(x => x.Level >= LogLevel.Warning).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(warnings, Has.Length.EqualTo(2));
            Assert.That(warnings[^1].Message, Does.Contain("после 8 попыток повтора"));
            Assert.That(_retried.Task.IsCompleted, Is.False);
        }
    }

    [Test]
    public async Task Новый_повтор_того_же_типа_заменяет_прежний()
    {
        SetUpCreate(Conflict(), Conflict(), Task.FromResult("sub-new"));

        await CreateAsync(CancellationToken.None);
        await _time.WaitForPendingTimersAsync(1);
        await CreateAsync(CancellationToken.None);
        await _time.WaitForPendingTimersAsync(1);

        _time.Advance(EventSubSubscriptionRetry.FirstDelay);

        await _retried.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _time.WaitForPendingTimersAsync(0);
        await ReceivedCreates(3);
    }

    [TestCase(StopKind.Cancel, TestName = "Подписка, полученная повтором после отмены подписчиком, удаляется, а не отдаётся ему")]
    [TestCase(StopKind.CancelAndDrain, TestName = "Подписка, полученная повтором после остановки подписчика, удаляется, а не отдаётся ему")]
    [TestCase(StopKind.SessionChanged, TestName = "Подписка, полученная повтором после смены сессии, удаляется, а не отдаётся подписчику")]
    public async Task Опоздавшая_подписка_повтора_удаляется_неотменённым_токеном(StopKind stop)
    {
        var session = CurrentSession;
        _eventSubClient.SessionId.Returns(_ => session);

        Task? drain = null;
        var calls = 0;

        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    return Conflict();
                }

                switch (stop)
                {
                    case StopKind.Cancel:
                        _retry.Cancel();
                        break;

                    case StopKind.CancelAndDrain:
                        drain = _retry.CancelAndDrainAsync(CancellationToken.None);
                        break;

                    case StopKind.SessionChanged:
                        session = "session-next";
                        break;
                }

                return Task.FromResult("sub-late");
            });

        var deleted = TrackDeletes();

        await CreateAsync(CancellationToken.None);
        await _time.WaitForPendingTimersAsync(1);
        _time.Advance(EventSubSubscriptionRetry.FirstDelay);

        Assert.That(await deleted.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo("sub-late"));

        if (drain is not null)
        {
            await drain.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.That(_retried.Task.IsCompleted, Is.False);
    }

    [TestCase(StopKind.Cancel, TestName = "Отменённый повтор не удаляет подписку текущей сессии, созданную новым запросом")]
    [TestCase(StopKind.CancelAndDrain, TestName = "Повтор, остановленный вместе с подписчиком, удаляет найденную подписку текущей сессии")]
    [TestCase(StopKind.CallerToken, TestName = "Повтор, отменённый токеном вызывающего, удаляет найденную подписку текущей сессии")]
    public async Task Повтор_после_отмены_решает_судьбу_переиспользованной_подписки_по_виду_отмены(StopKind stop)
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;
        Task? drain = null;

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<EventSubSubscriptionInfo>>([
                new EventSubSubscriptionInfo(calls == 1 ? "sub-foreign" : "sub-replacement",
                    Type,
                    "enabled",
                    calls == 1 ? "session-other" : CurrentSession,
                    new Dictionary<string, string> { ["broadcaster_user_id"] = BroadcasterId }),
            ]));

        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 2)
                {
                    switch (stop)
                    {
                        case StopKind.Cancel:
                            _retry.Cancel();
                            break;

                        case StopKind.CancelAndDrain:
                            drain = _retry.CancelAndDrainAsync(CancellationToken.None);
                            break;

                        case StopKind.CallerToken:
                            cts.Cancel();
                            break;
                    }
                }

                return Conflict();
            });

        var deleted = TrackDeletes();

        await CreateAsync(cts.Token);
        await _time.WaitForPendingTimersAsync(1);
        _time.Advance(EventSubSubscriptionRetry.FirstDelay);

        if (stop == StopKind.Cancel)
        {
            await _retry.CancelAndDrainAsync(CancellationToken.None);
            Assert.That(deleted.Task.IsCompleted, Is.False);
        }
        else
        {
            Assert.That(await deleted.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo("sub-replacement"));

            if (drain is not null)
            {
                await drain.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(_retried.Task.IsCompleted, Is.False);
            Assert.That(_logger.Entries.Any(x => x.Message.Contains("sub-replacement")), Is.True);
        }
    }

    [TestCase(StopKind.Cancel, TestName = "Отмена, пока первое создание ищет подписку после 409, не оставляет повтора")]
    [TestCase(StopKind.CancelAndDrain, TestName = "Остановка, пока первое создание ищет подписку после 409, не оставляет повтора")]
    public async Task Отмена_до_постановки_повтора_не_даёт_ему_стартовать(StopKind stop)
    {
        SetUpCreate(Conflict(), Task.FromResult("sub-new"));

        var lookup = new TaskCompletionSource<IReadOnlyList<EventSubSubscriptionInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>()).Returns(lookup.Task);

        var first = CreateAsync(CancellationToken.None);

        if (stop == StopKind.Cancel)
        {
            _retry.Cancel();
        }
        else
        {
            await _retry.CancelAndDrainAsync(CancellationToken.None);
        }

        lookup.SetResult([
            new EventSubSubscriptionInfo("sub-foreign",
                Type,
                "enabled",
                "session-other",
                new Dictionary<string, string> { ["broadcaster_user_id"] = BroadcasterId }),
        ]);

        Assert.That((await first.WaitAsync(TimeSpan.FromSeconds(5))).Outcome, Is.EqualTo(EventSubSubscribeOutcome.TakenByOtherSession));
        Assert.That(_time.PendingTimers, Is.Zero);

        _time.Advance(EventSubSubscriptionRetry.MaxDelay);
        await ReceivedCreates(1);
    }

    [TestCase(true, TestName = "Сбой поиска после 409 внутри повтора – ещё одна попытка, а не конец повтора")]
    [TestCase(false, TestName = "Повтор, упёршийся в сбои поиска после 409, называет эту причину в итоговом предупреждении")]
    public async Task Сбой_поиска_на_повторе_не_обрывает_повтор(bool recovers)
    {
        if (recovers)
        {
            SetUpCreate(Conflict(), Conflict(), Task.FromResult("sub-new"));
        }
        else
        {
            SetUpCreate(Conflict());
        }

        _helix.GetEventSubSubscriptionsByUserAsync(BroadcasterId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EventSubSubscriptionInfo>>([
                    new EventSubSubscriptionInfo("sub-foreign",
                        Type,
                        "enabled",
                        "session-other",
                        new Dictionary<string, string> { ["broadcaster_user_id"] = BroadcasterId }),
                ]),
                Task.FromException<IReadOnlyList<EventSubSubscriptionInfo>>(new HttpRequestException("сбой сети")));

        await CreateAsync(CancellationToken.None);

        if (recovers)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await _time.WaitForPendingTimersAsync(1);
                _time.Advance(EventSubSubscriptionRetry.MaxDelay);
            }

            var retried = await _retried.Task.WaitAsync(TimeSpan.FromSeconds(5));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(retried.SubscriptionIds, Is.EqualTo(new[] { "sub-new" }));
                Assert.That(_logger.Entries.Count(x => x.Level >= LogLevel.Warning), Is.EqualTo(1));
            }

            return;
        }

        for (var attempt = 0; attempt < EventSubSubscriptionRetry.MaxAttempts; attempt++)
        {
            await _time.WaitForPendingTimersAsync(1);
            _time.Advance(EventSubSubscriptionRetry.MaxDelay);
        }

        await _time.WaitForPendingTimersAsync(0);
        _time.Advance(EventSubSubscriptionRetry.MaxDelay);
        await ReceivedCreates(1 + EventSubSubscriptionRetry.MaxAttempts);

        var warnings = _logger.Entries.Where(x => x.Level >= LogLevel.Warning).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(warnings, Has.Length.EqualTo(2));
            Assert.That(warnings[^1].Message, Does.Contain("найти занявшую её подписку не удалось"));
            Assert.That(_retried.Task.IsCompleted, Is.False);
        }
    }

    [Test]
    public async Task Остановка_дожидается_удаления_подписки_повтором_отменённым_на_welcome()
    {
        var calls = 0;

        _helix.CreateEventSubSubscriptionAsync(Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    return Conflict();
                }

                _retry.Cancel();
                return Task.FromResult("sub-late");
            });

        var deleteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleteGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _helix.DeleteEventSubSubscriptionAsync("sub-late", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                deleteStarted.TrySetResult();
                return deleteGate.Task;
            });

        await CreateAsync(CancellationToken.None);
        await _time.WaitForPendingTimersAsync(1);
        _time.Advance(EventSubSubscriptionRetry.FirstDelay);
        await deleteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var drain = _retry.CancelAndDrainAsync(CancellationToken.None);
        await Task.Delay(50);

        Assert.That(drain.IsCompleted, Is.False);

        deleteGate.SetResult(true);
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private TaskCompletionSource<string> TrackDeletes()
    {
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

        return deleted;
    }

    private Task<EventSubSubscribeResult> CreateAsync(CancellationToken cancellationToken)
    {
        return _retry.CreateAsync(Type,
            "2",
            Condition,
            CurrentSession,
            result =>
            {
                _retried.TrySetResult(result);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    private async Task ReceivedCreates(int count)
    {
        await Task.Yield();

        await _helix.Received(count)
            .CreateEventSubSubscriptionAsync(Type, "2", Condition, CurrentSession, Arg.Any<CancellationToken>());
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
}
