namespace PoproshaykaBot.Core.Tests.Twitch.EventSub;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _lock = new();
    private readonly List<ManualTimer> _timers = [];

    private DateTimeOffset _now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    public int PendingTimers
    {
        get
        {
            lock (_lock)
            {
                return _timers.Count(x => x.Due is not null);
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, this);

        lock (_lock)
        {
            _timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan span)
    {
        ManualTimer[] due;

        lock (_lock)
        {
            _now += span;
            due = _timers.Where(x => x.Due is { } at && at <= _now).ToArray();

            foreach (var timer in due)
            {
                timer.Due = null;
            }
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    public async Task WaitForPendingTimersAsync(int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (PendingTimers != count)
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Ожидалось таймеров: {count}, взведено: {PendingTimers}");
            }

            await Task.Delay(5);
        }
    }

    private void Forget(ManualTimer timer)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, ManualTimeProvider owner) : ITimer
    {
        public DateTimeOffset? Due { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._lock)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
            }

            return true;
        }

        public void Fire()
        {
            callback(state);
        }

        public void Dispose()
        {
            owner.Forget(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
