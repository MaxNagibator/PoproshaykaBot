using KeepShell.Services.Platform;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Diagnostics;

namespace PoproshaykaBot.Wpf.Infrastructure.Diagnostics;

public sealed class DiagnosticsSnapshotPublisher
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly Func<DiagnosticsSnapshot> _capture;
    private readonly ILogger<DiagnosticsSnapshotPublisher> _logger;
    private readonly IUiTimer _timer;
    private readonly List<Action<DiagnosticsSnapshot>> _listeners = [];
    private readonly Lock _sync = new();

    private DiagnosticsSnapshot? _last;
    private bool _captureFailed;

    public DiagnosticsSnapshotPublisher(
        IUiDispatcher uiDispatcher,
        Func<DiagnosticsSnapshot> capture,
        ILogger<DiagnosticsSnapshotPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(logger);

        _capture = capture;
        _logger = logger;
        _timer = uiDispatcher.CreateTimer(Interval, OnTick);
    }

    public DiagnosticsSnapshot? Last
    {
        get
        {
            lock (_sync)
            {
                return _last;
            }
        }
    }

    public int ListenerCount
    {
        get
        {
            lock (_sync)
            {
                return _listeners.Count;
            }
        }
    }

    public bool IsRunning => _timer.IsRunning;

    public void Subscribe(Action<DiagnosticsSnapshot> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        bool first;

        lock (_sync)
        {
            if (_listeners.Contains(listener))
            {
                return;
            }

            _listeners.Add(listener);
            first = _listeners.Count == 1;
        }

        if (first)
        {
            _timer.Start();
            OnTick();

            return;
        }

        if (Last is { } snapshot)
        {
            Deliver(listener, snapshot);
        }
    }

    public void Unsubscribe(Action<DiagnosticsSnapshot> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        bool last;

        lock (_sync)
        {
            last = _listeners.Remove(listener) && _listeners.Count == 0;
        }

        if (last)
        {
            _timer.Stop();
        }
    }

    private void OnTick()
    {
        DiagnosticsSnapshot snapshot;

        try
        {
            snapshot = _capture();
        }
        catch (Exception exception)
        {
            LogCaptureFailure(exception);

            return;
        }

        _captureFailed = false;

        Action<DiagnosticsSnapshot>[] listeners;

        lock (_sync)
        {
            _last = snapshot;
            listeners = [.. _listeners];
        }

        foreach (var listener in listeners)
        {
            Deliver(listener, snapshot);
        }
    }

    private void LogCaptureFailure(Exception exception)
    {
        if (_captureFailed)
        {
            _logger.LogDebug(exception, "Снимок диагностики снова не собрался");

            return;
        }

        _captureFailed = true;
        _logger.LogWarning(exception, "Снимок диагностики не собрался, страница остаётся на прежних числах");
    }

    private void Deliver(Action<DiagnosticsSnapshot> listener, DiagnosticsSnapshot snapshot)
    {
        try
        {
            listener(snapshot);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Карточка диагностики не приняла снимок");
        }
    }
}
