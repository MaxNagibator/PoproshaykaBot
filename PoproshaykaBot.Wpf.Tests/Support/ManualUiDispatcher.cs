using KeepShell.Services.Platform;

namespace PoproshaykaBot.Wpf.Tests.Support;

public sealed class ManualUiDispatcher : IUiDispatcher
{
    private readonly List<ManualTimer> _timers = [];

    public bool HasAccess => true;

    public IReadOnlyList<ManualTimer> Timers => _timers;

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        action();
    }

    public IUiTimer CreateTimer(TimeSpan interval, Action tick)
    {
        var timer = new ManualTimer(interval, tick);

        _timers.Add(timer);

        return timer;
    }

    public sealed class ManualTimer(TimeSpan interval, Action tick) : IUiTimer
    {
        public bool IsRunning { get; private set; }

        public TimeSpan Interval { get; set; } = interval;

        public void Start()
        {
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Tick()
        {
            if (IsRunning)
            {
                tick();
            }
        }
    }
}
