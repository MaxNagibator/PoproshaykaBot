using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Settings.Stores;

public class DashboardLayoutStore
{
    private readonly ILogger<DashboardLayoutStore>? _logger;
    private readonly JsonStore<DashboardLayoutFileDto> _store;

    public DashboardLayoutStore(ILogger<DashboardLayoutStore>? logger = null, string? filePath = null, SettingsWriteGate? gate = null)
    {
        var path = filePath ?? AppPaths.SettingsFile("dashboard-layout.json");

        _logger = logger;
        _store = new(path, logger, gate: gate);

        _logger?.LogDebug("DashboardLayoutStore инициализирован из {FilePath}", path);
    }

    public virtual DashboardLayoutSettings? LoadDashboard()
    {
        var layout = _store.Load().Dashboard;

        if (layout?.Root is { } root)
        {
            layout.Root = DashboardPaneWeights.Sanitize(root);
        }

        return layout;
    }

    public virtual MainWindowSettings? LoadMainWindow()
    {
        return _store.Load().MainWindow;
    }

    public virtual void SaveDashboard(DashboardLayoutSettings layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var snapshot = JsonStoreClone.DeepClone(Sanitized(layout));
        MutateFile(state => state.Dashboard = snapshot);

        _logger?.LogDebug("DashboardLayoutStore: раскладка дашборда сохранена");
    }

    public virtual void SaveMainWindow(MainWindowSettings window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var snapshot = JsonStoreClone.DeepClone(window);
        MutateFile(state => state.MainWindow = snapshot);

        _logger?.LogDebug("DashboardLayoutStore: параметры главного окна сохранены");
    }

    private static DashboardLayoutSettings Sanitized(DashboardLayoutSettings layout)
    {
        if (layout.Root is not { } root)
        {
            return layout;
        }

        var sanitized = DashboardPaneWeights.Sanitize(root);

        if (ReferenceEquals(sanitized, root))
        {
            return layout;
        }

        return new()
        {
            ColumnCount = layout.ColumnCount,
            RowCount = layout.RowCount,
            Tiles = layout.Tiles,
            Root = sanitized,
        };
    }

    private void MutateFile(Action<DashboardLayoutFileDto> mutator)
    {
        _store.Mutate(state =>
        {
            mutator(state);

            if (state.Dashboard is not null)
            {
                DashboardLayoutReconciler.SyncRoot(state.Dashboard);
            }
        });
    }

    private sealed class DashboardLayoutFileDto
    {
        public DashboardLayoutSettings? Dashboard { get; set; }
        public MainWindowSettings? MainWindow { get; set; }
    }
}
