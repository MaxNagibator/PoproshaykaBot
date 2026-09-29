using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace PoproshaykaBot.Core.Infrastructure.Events;

public sealed class InMemoryEventBus(ILogger<InMemoryEventBus> logger, EventBusMetrics? metrics = null) : IEventBus
{
    private const string OutsidePublishScopeName = "вне публикации";

    private readonly Dictionary<Type, List<HandlerRegistration>> _handlers = new();
    private readonly object _syncLock = new();
    private readonly AsyncLocal<PublishScope?> _currentScope = new();
    private readonly EventBusMetrics _metrics = metrics ?? new();

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        var eventTypeName = typeof(TEvent).Name;
        var startedAt = Stopwatch.GetTimestamp();
        HandlerRegistration[] snapshot;

        lock (_syncLock)
        {
            snapshot = _handlers.TryGetValue(typeof(TEvent), out var registrations)
                ? registrations.ToArray()
                : [];
        }

        if (snapshot.Length == 0)
        {
            logger.LogTrace("Нет подписчиков для события {EventType}", eventTypeName);
            _metrics.RecordPublish(eventTypeName, Stopwatch.GetElapsedTime(startedAt), DateTimeOffset.UtcNow);
            return;
        }

        logger.LogDebug("Публикация события {EventType} для {HandlerCount} подписчиков", eventTypeName, snapshot.Length);

        var scope = new PublishScope();
        var previousScope = _currentScope.Value;
        _currentScope.Value = scope;

        try
        {
            foreach (var registration in snapshot)
            {
                try
                {
                    await registration.Invoker(@event!, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _metrics.RecordHandlerFailure(eventTypeName);

                    logger.LogError(ex,
                        "Обработчик события {EventType} завершился с ошибкой",
                        eventTypeName);
                }
            }
        }
        finally
        {
            _currentScope.Value = previousScope;
            _metrics.RecordPublish(eventTypeName, Stopwatch.GetElapsedTime(startedAt), DateTimeOffset.UtcNow);
            StartContinuations(scope, eventTypeName);
        }
    }

    public void ContinueAfterPublish(Func<Task> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var scope = _currentScope.Value;

        if (scope?.TryAdd(continuation) == true)
        {
            logger.LogDebug("Продолжение отложено до конца текущей публикации");
            return;
        }

        _ = RunContinuationAsync(continuation, OutsidePublishScopeName);
    }

    public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        return SubscribeCore<TEvent>(handler, (e, ct) => handler.HandleAsync((TEvent)e, ct));
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        return SubscribeCore<TEvent>(handler, (e, ct) => handler((TEvent)e, ct));
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        return SubscribeCore<TEvent>(handler, (e, _) =>
        {
            handler((TEvent)e);
            return Task.CompletedTask;
        });
    }

    private Subscription SubscribeCore<TEvent>(object key, Func<object, CancellationToken, Task> invoker)
        where TEvent : IEvent
    {
        var registration = new HandlerRegistration(key, invoker);

        lock (_syncLock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var registrations))
            {
                registrations = [];
                _handlers[typeof(TEvent)] = registrations;
            }

            registrations.Add(registration);
        }

        logger.LogDebug("Добавлена подписка на событие {EventType}", typeof(TEvent).Name);

        return new(() =>
        {
            lock (_syncLock)
            {
                if (_handlers.TryGetValue(typeof(TEvent), out var registrations))
                {
                    registrations.Remove(registration);
                }
            }

            logger.LogDebug("Удалена подписка на событие {EventType}", typeof(TEvent).Name);
        });
    }

    private void StartContinuations(PublishScope scope, string scopeName)
    {
        var continuations = scope.Drain();

        if (continuations.Length == 0)
        {
            return;
        }

        logger.LogDebug("Запуск {ContinuationCount} продолжений после публикации события {EventType}", continuations.Length, scopeName);

        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(() => RunContinuationsAsync(continuations, scopeName));
        }
    }

    private async Task RunContinuationsAsync(Func<Task>[] continuations, string scopeName)
    {
        foreach (var continuation in continuations)
        {
            await RunContinuationAsync(continuation, scopeName).ConfigureAwait(false);
        }
    }

    private async Task RunContinuationAsync(Func<Task> continuation, string scopeName)
    {
        _metrics.RecordContinuationStarted();

        try
        {
            await continuation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _metrics.RecordContinuationFailure();
            logger.LogError(ex, "Продолжение публикации ({Scope}) завершилось с ошибкой", scopeName);
        }
    }

    private sealed class PublishScope
    {
        private readonly List<Func<Task>> _continuations = [];
        private readonly object _sync = new();

        private bool _closed;

        public bool TryAdd(Func<Task> continuation)
        {
            lock (_sync)
            {
                if (_closed)
                {
                    return false;
                }

                _continuations.Add(continuation);
                return true;
            }
        }

        public Func<Task>[] Drain()
        {
            lock (_sync)
            {
                _closed = true;
                var drained = _continuations.ToArray();
                _continuations.Clear();
                return drained;
            }
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }

    private sealed record HandlerRegistration(object Key, Func<object, CancellationToken, Task> Invoker);
}
