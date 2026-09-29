namespace PoproshaykaBot.Wpf.Tests.Support;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    private DateTimeOffset _now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, this);

        _timers.Add(timer);
        timer.Change(dueTime, period);

        return timer;
    }

    public void Advance(TimeSpan span)
    {
        _now += span;

        foreach (var timer in _timers.ToArray())
        {
            timer.Tick(_now);
        }
    }

    private void Forget(ManualTimer timer)
    {
        _timers.Remove(timer);
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, ManualTimeProvider owner) : ITimer
    {
        private DateTimeOffset? _due;
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            _due = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetUtcNow() + dueTime;

            return true;
        }

        public void Tick(DateTimeOffset now)
        {
            if (_due is not { } due || now < due)
            {
                return;
            }

            _due = _period == Timeout.InfiniteTimeSpan ? null : now + _period;

            callback(state);
        }

        public void Dispose()
        {
            _due = null;
            owner.Forget(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
