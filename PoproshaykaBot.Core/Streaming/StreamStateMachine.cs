using PoproshaykaBot.Core.Infrastructure.Events.Streaming;

namespace PoproshaykaBot.Core.Streaming;

internal readonly record struct StatusTransition(bool Transitioned, StreamStatus Previous, bool SnapshotChanged);

internal enum OfflineProbeAction
{
    /// <summary>
    /// Текущий статус не Online - расхождение неактуально.
    /// </summary>
    NotOnline = 0,

    /// <summary>
    /// API сообщает офлайн, но локальный Online ещё в пределах допустимого расхождения.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Расхождение превысило порог - статус принудительно переведён в Offline.
    /// </summary>
    ForcedOffline = 2,
}

internal readonly record struct OfflineProbeOutcome(OfflineProbeAction Action, TimeSpan Elapsed, StreamStatus Previous);

internal sealed class StreamStateMachine
{
    private readonly object _lock = new();

    private StreamStatus _status = StreamStatus.Unknown;
    private StreamInfo? _stream;
    private ChannelUpdated? _lastChannelUpdate;
    private DateTime? _firstOfflineFromApiUtc;

    public StreamStatus CurrentStatus
    {
        get
        {
            lock (_lock)
            {
                return _status;
            }
        }
    }

    public StreamInfo? CurrentStream
    {
        get
        {
            lock (_lock)
            {
                return _stream;
            }
        }
    }

    public StatusTransition ApplyOnlineSnapshot(StreamInfo stream)
    {
        lock (_lock)
        {
            _firstOfflineFromApiUtc = null;
            var previous = _status;
            var previousStream = _stream;
            _status = StreamStatus.Online;
            _stream = ApplyOverlayLocked(stream);
            return new(previous != StreamStatus.Online, previous, !StreamInfoMapper.SameSnapshot(previousStream, _stream));
        }
    }

    public StatusTransition MarkOnline(string? streamId, DateTime startedAt)
    {
        lock (_lock)
        {
            var previous = _status;
            var previousStream = _stream;
            _status = StreamStatus.Online;

            if (!string.IsNullOrEmpty(streamId)
                && startedAt != default
                && !string.Equals(_stream?.Id, streamId, StringComparison.Ordinal))
            {
                _stream = ApplyOverlayLocked(new()
                {
                    Id = streamId,
                    StartedAt = DateTime.SpecifyKind(startedAt, DateTimeKind.Utc),
                });
            }

            return new(previous != StreamStatus.Online, previous, !StreamInfoMapper.SameSnapshot(previousStream, _stream));
        }
    }

    public StatusTransition ApplyOffline()
    {
        lock (_lock)
        {
            var previous = _status;
            var hadStream = _stream != null;
            _status = StreamStatus.Offline;
            _stream = null;
            _firstOfflineFromApiUtc = null;
            return new(previous != StreamStatus.Offline, previous, hadStream);
        }
    }

    public OfflineProbeOutcome ProbeOfflineDivergence(DateTime nowUtc, TimeSpan stuckThreshold)
    {
        lock (_lock)
        {
            if (_status != StreamStatus.Online)
            {
                _firstOfflineFromApiUtc = null;
                _stream = null;
                return new(OfflineProbeAction.NotOnline, TimeSpan.Zero, _status);
            }

            _firstOfflineFromApiUtc ??= nowUtc;
            var elapsed = nowUtc - _firstOfflineFromApiUtc.Value;

            if (elapsed >= stuckThreshold)
            {
                var previous = _status;
                _status = StreamStatus.Offline;
                _stream = null;
                _firstOfflineFromApiUtc = null;
                return new(OfflineProbeAction.ForcedOffline, elapsed, previous);
            }

            return new(OfflineProbeAction.Pending, elapsed, _status);
        }
    }

    public bool ApplyChannelUpdate(ChannelUpdated update)
    {
        lock (_lock)
        {
            _lastChannelUpdate = update;

            if (_stream == null)
            {
                return false;
            }

            _stream = StreamInfoMapper.MergeChannelUpdate(_stream, update);
            return true;
        }
    }

    public void UpdateStreamSnapshot(StreamInfo stream)
    {
        lock (_lock)
        {
            _stream = ApplyOverlayLocked(stream);
        }
    }

    public void ResetToUnknown()
    {
        lock (_lock)
        {
            _status = StreamStatus.Unknown;
            _stream = null;
            _firstOfflineFromApiUtc = null;
            _lastChannelUpdate = null;
        }
    }

    public void ForgetChannelUpdate()
    {
        lock (_lock)
        {
            _lastChannelUpdate = null;
        }
    }

    private StreamInfo ApplyOverlayLocked(StreamInfo input)
    {
        return _lastChannelUpdate is null
            ? input
            : StreamInfoMapper.MergeChannelUpdate(input, _lastChannelUpdate);
    }
}
