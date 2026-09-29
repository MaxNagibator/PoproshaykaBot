using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Dashboard;

public sealed class DashboardLayoutCoordinator
{
    private readonly DashboardLayoutStore _store;
    private readonly ILogger<DashboardLayoutCoordinator>? _logger;
    private readonly object _gate = new();
    private int _revision;

    public DashboardLayoutCoordinator(DashboardLayoutStore store, ILogger<DashboardLayoutCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _logger = logger;
    }

    public event EventHandler<DashboardLayoutChangedEventArgs>? LayoutChanged;

    public DashboardLayoutSnapshot Read()
    {
        lock (_gate)
        {
            return new(_store.LoadDashboard(), _revision);
        }
    }

    public DashboardLayoutSettings? Mutate(Func<DashboardLayoutSettings?, DashboardLayoutSettings?> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        DashboardLayoutSnapshot snapshot;

        lock (_gate)
        {
            var layout = change(_store.LoadDashboard());

            if (layout is null)
            {
                return _store.LoadDashboard();
            }

            snapshot = Write(layout);
        }

        LayoutChanged?.Invoke(this, new(snapshot));

        return snapshot.Layout;
    }

    public DashboardLayoutCommitResult Commit(DashboardLayoutSettings layout, int baseRevision, bool keepDraftRoot = false)
    {
        ArgumentNullException.ThrowIfNull(layout);

        DashboardLayoutSnapshot snapshot;
        bool merged;

        lock (_gate)
        {
            merged = baseRevision != _revision;

            DashboardLayoutReconciler.MergeConcurrentEdits(layout, _store.LoadDashboard(), keepDraftRoot);

            if (merged)
            {
                _logger?.LogInformation(
                    "Раскладка дашборда изменилась во время правки настроек (ревизия {BaseRevision} против {Revision}), правки слиты",
                    baseRevision,
                    _revision);
            }

            snapshot = Write(layout);
        }

        LayoutChanged?.Invoke(this, new(snapshot));

        return new(snapshot, merged);
    }

    private DashboardLayoutSnapshot Write(DashboardLayoutSettings layout)
    {
        _store.SaveDashboard(layout);
        _revision++;

        return new(_store.LoadDashboard(), _revision);
    }
}
