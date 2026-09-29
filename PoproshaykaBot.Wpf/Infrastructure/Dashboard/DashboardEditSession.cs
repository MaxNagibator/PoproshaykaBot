using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.Bootstrap;

namespace PoproshaykaBot.Wpf.Infrastructure.Dashboard;

public sealed class DashboardEditSession : IDisposable
{
    public static readonly TimeSpan WriteDelay = TimeSpan.FromMilliseconds(500);

    private readonly DashboardLayoutCoordinator _coordinator;
    private readonly ILogger? _logger;
    private readonly SynchronizationContext? _context;
    private readonly DashboardLayoutDraft _draft;
    private readonly ITimer _timer;
    private readonly object _gate = new();

    private int _baseRevision;
    private bool _dirty;
    private bool _writing;
    private bool _collapsing;
    private bool _disposed;

    public DashboardEditSession(DashboardLayoutCoordinator coordinator, TimeProvider time, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(time);

        _coordinator = coordinator;
        _logger = logger;
        _context = SynchronizationContext.Current;

        var snapshot = coordinator.Read();

        _draft = new(snapshot.Layout);
        _baseRevision = snapshot.Revision;

        _timer = time.CreateTimer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        _coordinator.LayoutChanged += OnLayoutChanged;
    }

    public event EventHandler? Changed;

    public DashboardLayoutSettings Draft => _draft.Layout;

    public bool CanUndo
    {
        get
        {
            lock (_gate)
            {
                return _draft.CanUndo;
            }
        }
    }

    public bool Resize(IReadOnlyList<int> path, IReadOnlyList<double> weights)
    {
        return Edit(draft => draft.Resize(path, weights)
            ? DashboardEditStatus.Applied
            : DashboardEditStatus.Rejected) == DashboardEditStatus.Applied;
    }

    public DashboardEditStatus Move(IReadOnlyList<int> sourcePath, IReadOnlyList<int> targetPath, PaneSide side)
    {
        return Edit(draft => draft.Move(sourcePath, targetPath, side));
    }

    public bool SetCollapsed(string typeId, bool isCollapsed)
    {
        ArgumentNullException.ThrowIfNull(typeId);

        lock (_gate)
        {
            if (_disposed || !_draft.SetCollapsed(typeId, isCollapsed))
            {
                return false;
            }

            _collapsing = true;

            try
            {
                _coordinator.Mutate(current =>
                {
                    var setting = current?.Tiles.FirstOrDefault(tile => string.Equals(tile.TypeId, typeId, StringComparison.Ordinal));

                    if (setting is null || setting.IsCollapsed == isCollapsed)
                    {
                        return null;
                    }

                    setting.IsCollapsed = isCollapsed;

                    return current;
                });
            }
            catch (Exception exception)
            {
                _logger?.DashboardLayoutSaveFailed(exception);
                _draft.SetCollapsed(typeId, !isCollapsed);

                return false;
            }
            finally
            {
                _collapsing = false;
            }
        }

        Raise();

        return true;
    }

    public DashboardPane? PreviewMove(IReadOnlyList<int> sourcePath, IReadOnlyList<int> targetPath, PaneSide side)
    {
        lock (_gate)
        {
            return _disposed
                ? null
                : _draft.Preview(root => DashboardPaneEditor.TryMove(root, sourcePath, targetPath, side, out var result) ? result : null);
        }
    }

    public DashboardEditStatus Add(string typeId, string targetTypeId, PaneSide side)
    {
        return Edit(draft => draft.Add(typeId, targetTypeId, side));
    }

    public DashboardRemoveStatus Remove(string typeId)
    {
        var status = DashboardRemoveStatus.Rejected;
        var changed = false;

        lock (_gate)
        {
            if (_disposed)
            {
                return DashboardRemoveStatus.Rejected;
            }

            var version = _draft.Version;

            status = _draft.Remove(typeId);
            changed = _draft.Version != version;

            if (changed)
            {
                Arm();
            }
        }

        if (changed)
        {
            Raise();
        }

        return status;
    }

    public bool Undo()
    {
        lock (_gate)
        {
            if (_disposed || !_draft.Undo())
            {
                return false;
            }

            Arm();
        }

        Raise();

        return true;
    }

    public void ResetToDefaults()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _draft.ResetToDefaults();
            Arm();
        }

        Raise();
    }

    public void Flush()
    {
        var merged = false;

        lock (_gate)
        {
            if (_disposed || !_dirty)
            {
                return;
            }

            _writing = true;

            try
            {
                var result = _coordinator.Commit(_draft.Layout, _baseRevision, keepDraftRoot: true);

                _dirty = false;
                _baseRevision = result.Snapshot.Revision;
                merged = result.Merged;

                if (merged && DashboardLayoutDraft.Clone(result.Snapshot.Layout) is { } layout)
                {
                    _draft.Replace(layout);
                }
            }
            catch (Exception exception)
            {
                // TODO: отказ записи виден только в журнале; довести его до строки состояния, когда появится второй случай потери правок раскладки
                _logger?.DashboardLayoutSaveFailed(exception);
            }
            finally
            {
                _writing = false;
            }
        }

        if (merged)
        {
            Raise();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
        }

        _coordinator.LayoutChanged -= OnLayoutChanged;
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        Flush();

        lock (_gate)
        {
            _disposed = true;
        }

        _timer.Dispose();
    }

    private DashboardEditStatus Edit(Func<DashboardLayoutDraft, DashboardEditStatus> change)
    {
        DashboardEditStatus status;
        bool changed;

        lock (_gate)
        {
            if (_disposed)
            {
                return DashboardEditStatus.Unavailable;
            }

            var version = _draft.Version;

            status = change(_draft);
            changed = _draft.Version != version;

            if (changed)
            {
                Arm();
            }
        }

        if (changed)
        {
            Raise();
        }

        return status;
    }

    private void Arm()
    {
        _dirty = true;
        _timer.Change(WriteDelay, Timeout.InfiniteTimeSpan);
    }

    private void Raise()
    {
        if (_context is null || _context == SynchronizationContext.Current)
        {
            Changed?.Invoke(this, EventArgs.Empty);

            return;
        }

        _context.Post(_ => Changed?.Invoke(this, EventArgs.Empty), null);
    }

    private void OnLayoutChanged(object? sender, DashboardLayoutChangedEventArgs e)
    {
        lock (_gate)
        {
            if (_collapsing)
            {
                _baseRevision = Math.Max(_baseRevision, e.Snapshot.Revision);

                return;
            }

            if (_writing || _disposed || e.Snapshot.Revision <= _baseRevision
                || DashboardLayoutDraft.Clone(e.Snapshot.Layout) is not { } layout)
            {
                return;
            }

            _draft.Replace(layout);
            _baseRevision = e.Snapshot.Revision;
            _dirty = false;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        Raise();
    }
}
