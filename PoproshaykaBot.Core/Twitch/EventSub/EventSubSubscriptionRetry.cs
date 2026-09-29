using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Twitch.EventSub;

// TODO: повтор обрывается на миграции session_reconnect (id сессии сменился) и после MaxAttempts попыток (около 5 минут) – тип молчит до следующего session_welcome; при таком молчании после миграции или при долгой работе второго экземпляра – переносить повтор на новую сессию из OnSessionReconnect и повторять без потолка с редкой выдержкой
public sealed class EventSubSubscriptionRetry(
    ITwitchHelixClient helix,
    ITwitchEventSubClient eventSubClient,
    TimeProvider timeProvider,
    ILogger logger)
{
    internal const int MaxAttempts = 8;
    internal static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(1);

    private readonly object _lock = new();
    private readonly Dictionary<string, Pending> _pending = new(StringComparer.Ordinal);
    private readonly HashSet<Pending> _running = [];
    private long _epoch;

    public async Task<EventSubSubscribeResult> CreateAsync(
        string type,
        string version,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        Func<EventSubSubscribeResult, Task> onRetrySubscribed,
        CancellationToken cancellationToken)
    {
        long epoch;

        lock (_lock)
        {
            epoch = _epoch;
        }

        var result = await EventSubSubscriptions.CreateAsync(helix, type, version, condition, sessionId, logger, cancellationToken);

        if (result.Outcome == EventSubSubscribeOutcome.TakenByOtherSession)
        {
            Schedule(epoch, type, version, condition, sessionId, onRetrySubscribed, cancellationToken);
        }

        return result;
    }

    public void Cancel()
    {
        foreach (var pending in Detach())
        {
            pending.Cancellation.Cancel();
        }
    }

    public async Task CancelAndDrainAsync(CancellationToken cancellationToken)
    {
        var running = Detach();

        foreach (var pending in running)
        {
            pending.Stopping = true;
            await pending.Cancellation.CancelAsync();
        }

        try
        {
            await Task.WhenAll(running.Select(x => x.Done.Task)).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Повтор подписок EventSub не завершился до отмены остановки ({Count})", running.Length);
        }
    }

    private Pending[] Detach()
    {
        lock (_lock)
        {
            _epoch++;
            _pending.Clear();
            return _running.ToArray();
        }
    }

    private void Schedule(
        long epoch,
        string type,
        string version,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        Func<EventSubSubscribeResult, Task> onRetrySubscribed,
        CancellationToken cancellationToken)
    {
        var pending = new Pending();
        Pending? replaced;

        lock (_lock)
        {
            if (epoch != _epoch)
            {
                logger.LogDebug("Повтор подписки EventSub {Type} для сессии {SessionId} не ставится: повторы отменены, пока шло первое создание", type, sessionId);
                return;
            }

            _pending.Remove(type, out replaced);
            _pending[type] = pending;
            _running.Add(pending);
        }

        replaced?.Cancellation.Cancel();

        logger.LogInformation("Подписка EventSub {Type} для сессии {SessionId} будет запрошена повторно: до {Attempts} попыток с выдержкой от {FirstSeconds} до {MaxSeconds} с",
            type, sessionId, MaxAttempts, FirstDelay.TotalSeconds, MaxDelay.TotalSeconds);

        _ = RunAsync(type, version, condition, sessionId, pending, onRetrySubscribed, cancellationToken);
    }

    private async Task RunAsync(
        string type,
        string version,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        Pending pending,
        Func<EventSubSubscribeResult, Task> onRetrySubscribed,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(pending.Cancellation.Token, cancellationToken);
        var token = linked.Token;
        var delay = FirstDelay;
        var lastOutcome = EventSubSubscribeOutcome.TakenByOtherSession;
        Exception? lastError = null;

        try
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                await Task.Delay(delay, timeProvider, token);
                delay = delay * 2 < MaxDelay ? delay * 2 : MaxDelay;

                if (!IsCurrent(sessionId))
                {
                    logger.LogDebug("Повтор подписки EventSub {Type} прекращён: сессия {SessionId} сменилась или закрыта", type, sessionId);
                    return;
                }

                EventSubSubscribeResult result;

                try
                {
                    result = await EventSubSubscriptions.CreateAsync(helix, type, version, condition, sessionId, logger, LogLevel.Debug, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    lastError = ex;
                    logger.LogDebug(ex, "Повтор {Attempt}/{Attempts} подписки EventSub {Type}: запрос к Twitch не удался", attempt, MaxAttempts, type);
                    continue;
                }

                lastOutcome = result.Outcome;
                lastError = null;

                if (result.Outcome == EventSubSubscribeOutcome.TakenByOtherSession)
                {
                    logger.LogDebug("Повтор {Attempt}/{Attempts} подписки EventSub {Type}: подписка всё ещё на другой сессии", attempt, MaxAttempts, type);
                    continue;
                }

                if (result.Outcome == EventSubSubscribeOutcome.LookupFailed)
                {
                    logger.LogDebug("Повтор {Attempt}/{Attempts} подписки EventSub {Type}: Twitch снова ответил 409, а найти занявшую её подписку не удалось", attempt, MaxAttempts, type);
                    continue;
                }

                if (!result.IsSubscribed)
                {
                    return;
                }

                if (pending.Cancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested || !IsCurrent(sessionId))
                {
                    if (result.Outcome == EventSubSubscribeOutcome.Reused
                        && !pending.Stopping
                        && !cancellationToken.IsCancellationRequested
                        && IsCurrent(sessionId))
                    {
                        logger.LogInformation("Подписка EventSub {Type} {SubscriptionIds} найдена повтором после его отмены уже на текущей сессии {SessionId} – её создал новый запрос, не удаляем",
                            type, result.SubscriptionIds, sessionId);

                        return;
                    }

                    logger.LogInformation("Подписка EventSub {Type} {SubscriptionIds} получена повтором уже после его отмены или смены сессии {SessionId} – удаляем",
                        type, result.SubscriptionIds, sessionId);

                    await EventSubSubscriptions.DeleteDetachedAsync(helix, type, result.SubscriptionIds, logger);
                    return;
                }

                logger.LogInformation("Подписка EventSub {Type} получена для сессии {SessionId} с {Attempt}-й попытки повтора ({Outcome})",
                    type, sessionId, attempt, result.Outcome);

                await onRetrySubscribed(result);
                return;
            }

            LogGaveUp(type, sessionId, lastOutcome, lastError);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogDebug("Повтор подписки EventSub {Type} для сессии {SessionId} отменён", type, sessionId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Повтор подписки EventSub {Type} для сессии {SessionId} прерван ошибкой – уведомления этого типа не придут до следующего подключения EventSub",
                type, sessionId);
        }
        finally
        {
            Release(type, pending);
        }
    }

    private void LogGaveUp(string type, string sessionId, EventSubSubscribeOutcome lastOutcome, Exception? lastError)
    {
        if (lastError is not null)
        {
            logger.LogWarning(lastError, "Подписка EventSub {Type} не получена после {Attempts} попыток повтора: последняя сорвалась ошибкой запроса к Twitch – на сессию {SessionId} уведомления этого типа не придут до следующего подключения EventSub",
                type, MaxAttempts, sessionId);
        }
        else if (lastOutcome == EventSubSubscribeOutcome.LookupFailed)
        {
            logger.LogWarning("Подписка EventSub {Type} не получена после {Attempts} попыток повтора: Twitch отвечал 409, а найти занявшую её подписку не удалось – на сессию {SessionId} уведомления этого типа не придут до следующего подключения EventSub",
                type, MaxAttempts, sessionId);
        }
        else
        {
            logger.LogWarning("Подписка EventSub {Type} так и осталась на другой сессии после {Attempts} попыток повтора – на сессию {SessionId} уведомления этого типа не придут до следующего подключения EventSub",
                type, MaxAttempts, sessionId);
        }
    }

    private bool IsCurrent(string sessionId)
    {
        return string.Equals(eventSubClient.SessionId, sessionId, StringComparison.Ordinal);
    }

    private void Release(string type, Pending pending)
    {
        lock (_lock)
        {
            if (_pending.TryGetValue(type, out var current) && ReferenceEquals(current, pending))
            {
                _pending.Remove(type);
            }

            _running.Remove(pending);
        }

        pending.Done.TrySetResult();
    }

    private sealed class Pending
    {
        public CancellationTokenSource Cancellation { get; } = new();

        public volatile bool Stopping;

        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
