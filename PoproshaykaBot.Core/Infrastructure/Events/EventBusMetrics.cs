using PoproshaykaBot.Core.Diagnostics;
using System.Collections.Concurrent;

namespace PoproshaykaBot.Core.Infrastructure.Events;

public sealed class EventBusMetrics
{
    private readonly ConcurrentDictionary<string, TypeCounters> _byType = new(StringComparer.Ordinal);

    private long _publishedTotal;
    private long _handlerFailures;
    private long _continuationsStarted;
    private long _continuationFailures;

    public void RecordPublish(string eventType, TimeSpan duration, DateTimeOffset publishedAt)
    {
        Interlocked.Increment(ref _publishedTotal);
        _byType.GetOrAdd(eventType, static _ => new()).AddPublish(duration, publishedAt);
    }

    public void RecordHandlerFailure(string eventType)
    {
        Interlocked.Increment(ref _handlerFailures);
        _byType.GetOrAdd(eventType, static _ => new()).AddHandlerFailure();
    }

    public void RecordContinuationStarted()
    {
        Interlocked.Increment(ref _continuationsStarted);
    }

    public void RecordContinuationFailure()
    {
        Interlocked.Increment(ref _continuationFailures);
    }

    public EventBusStatistics Snapshot()
    {
        var byType = _byType
            .Select(pair => pair.Value.ToStatistics(pair.Key))
            .OrderByDescending(item => item.PublishCount)
            .ThenBy(item => item.EventType, StringComparer.Ordinal)
            .ToArray();

        return new(Interlocked.Read(ref _publishedTotal),
            Interlocked.Read(ref _handlerFailures),
            Interlocked.Read(ref _continuationsStarted),
            Interlocked.Read(ref _continuationFailures),
            byType);
    }

    private sealed class TypeCounters
    {
        private long _publishCount;
        private long _handlerFailureCount;
        private long _totalDurationTicks;
        private long _maxDurationTicks;
        private long _lastPublishedAtUtcTicks;

        private static void RaiseTo(ref long target, long value)
        {
            var observed = Interlocked.Read(ref target);

            while (value > observed)
            {
                var previous = Interlocked.CompareExchange(ref target, value, observed);

                if (previous == observed)
                {
                    break;
                }

                observed = previous;
            }
        }

        public void AddPublish(TimeSpan duration, DateTimeOffset publishedAt)
        {
            Interlocked.Increment(ref _publishCount);
            Interlocked.Add(ref _totalDurationTicks, duration.Ticks);

            RaiseTo(ref _maxDurationTicks, duration.Ticks);
            RaiseTo(ref _lastPublishedAtUtcTicks, publishedAt.UtcTicks);
        }

        public void AddHandlerFailure()
        {
            Interlocked.Increment(ref _handlerFailureCount);
        }

        public EventTypeStatistics ToStatistics(string eventType)
        {
            var lastPublishedAt = new DateTimeOffset(Interlocked.Read(ref _lastPublishedAtUtcTicks), TimeSpan.Zero);

            return new(eventType,
                Interlocked.Read(ref _publishCount),
                Interlocked.Read(ref _handlerFailureCount),
                TimeSpan.FromTicks(Interlocked.Read(ref _totalDurationTicks)),
                TimeSpan.FromTicks(Interlocked.Read(ref _maxDurationTicks)),
                lastPublishedAt);
        }
    }
}
