using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.WinForms.Tiles;

public sealed partial class DashboardControl : UserControl
{
    private readonly Dictionary<DashboardTileType, DashboardTileHost> _hosts = [];
    private bool _initialized;

    public DashboardControl()
    {
        InitializeComponent();
    }

    [Inject]
    public DashboardLayoutCoordinator LayoutCoordinator { get; internal init; } = null!;

    [Inject]
    public IControlFactory ControlFactory { get; internal init; } = null!;

    [Inject]
    public IDashboardTileCatalog TileCatalog { get; internal init; } = null!;

    public void ReloadDashboard()
    {
        if (!_initialized)
        {
            return;
        }

        var layout = LayoutCoordinator.Mutate(current =>
        {
            var created = current == null || current.Tiles.Count == 0;
            var target = created ? DashboardLayoutDefaults.Create() : current!;

            return DashboardLayoutReconciler.AppendMissingTypes(target, TileCatalog.All.Select(type => type.Id)) || created ? target : null;
        });

        ApplyLayout(layout ?? DashboardLayoutDefaults.Create());
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_initialized)
        {
            return;
        }

        if (this.IsInDesignMode())
        {
            return;
        }

        _initialized = true;
        InitializeHosts();
        ReloadDashboard();
    }

    private static int? ResolveMaxSize(int? overrideValue, int? typeDefault)
    {
        return overrideValue switch
        {
            null => typeDefault,
            <= 0 => null,
            _ => overrideValue,
        };
    }

    private int?[] ComputeColumnFixedWidths(IReadOnlyList<TilePlacement> placements, int columnCount, int rowCount, int collapsedDeviceWidth)
    {
        var widths = new int?[columnCount];

        for (var column = 0; column < columnCount; column++)
        {
            var columnIndex = column;

            var fullColumnTile = placements.FirstOrDefault(p => p.Row == 0
                                                                && p.RowSpan == rowCount
                                                                && p.Column <= columnIndex
                                                                && columnIndex < p.Column + p.ColumnSpan);

            if (fullColumnTile is { IsCollapsed: true })
            {
                widths[column] = collapsedDeviceWidth;
                continue;
            }

            if (fullColumnTile != null)
            {
                widths[column] = null;
                continue;
            }

            var singleColumnTiles = placements
                .Where(p => p.ColumnSpan == 1 && p.Column == columnIndex)
                .ToList();

            var hasFlexSingleColumn = singleColumnTiles.Any(p => p.MaxWidth == null);

            if (hasFlexSingleColumn)
            {
                widths[column] = null;
                continue;
            }

            var compactSingleColumn = singleColumnTiles
                .Where(p => p.MaxWidth.HasValue)
                .ToList();

            widths[column] = compactSingleColumn.Count > 0
                ? LogicalToDeviceUnits(compactSingleColumn.Max(p => p.MaxWidth!.Value))
                : null;
        }

        return widths;
    }

    private int?[] ComputeRowFixedHeights(IReadOnlyList<TilePlacement> placements, int rowCount, int collapsedDeviceHeight)
    {
        var heights = new int?[rowCount];

        for (var row = 0; row < rowCount; row++)
        {
            var rowIndex = row;
            var singleRowTiles = placements
                .Where(p => p.RowSpan == 1 && p.Row == rowIndex)
                .ToList();

            if (singleRowTiles.Count > 0 && singleRowTiles.All(p => p.IsCollapsed))
            {
                heights[row] = collapsedDeviceHeight;
                continue;
            }

            var nonCollapsed = singleRowTiles.Where(p => !p.IsCollapsed).ToList();
            var hasFlexSingleRow = nonCollapsed.Any(p => p.MaxHeight == null);

            if (hasFlexSingleRow)
            {
                heights[row] = null;
                continue;
            }

            var compactSingleRow = nonCollapsed
                .Where(p => p.MaxHeight.HasValue)
                .ToList();

            heights[row] = compactSingleRow.Count > 0
                ? LogicalToDeviceUnits(compactSingleRow.Max(p => p.MaxHeight!.Value))
                : null;
        }

        return heights;
    }

    private void InitializeHosts()
    {
        var hostMargin = new Padding(LogicalToDeviceUnits(4));

        _tilesTableLayoutPanel.SuspendLayout();

        try
        {
            foreach (var type in TileCatalog.All)
            {
                var host = new DashboardTileHost
                {
                    Name = $"_{type.Id}Tile",
                    Dock = DockStyle.Fill,
                    Margin = hostMargin,
                };

                host.SetTitle(type.Title);

                var body = type.CreateBody(ControlFactory);
                host.SetBody(body);

                if (body is IDashboardTileHeaderProvider provider)
                {
                    host.SetHeaderActions(provider.CreateHeaderItems());
                }

                var capturedType = type;
                host.CollapseToggled += (_, _) => OnTileCollapseToggled(capturedType);

                _hosts[type] = host;
                _tilesTableLayoutPanel.Controls.Add(host, 0, 0);

                _ = host.Handle;
                host.Visible = false;
            }
        }
        finally
        {
            _tilesTableLayoutPanel.ResumeLayout();
        }
    }

    private void OnTileCollapseToggled(DashboardTileType type)
    {
        if (!_hosts.TryGetValue(type, out var host))
        {
            return;
        }

        var persisted = false;

        var layout = LayoutCoordinator.Mutate(current =>
        {
            var tile = current?.Tiles.FirstOrDefault(t => string.Equals(t.TypeId, type.Id, StringComparison.Ordinal));

            if (tile == null)
            {
                return null;
            }

            tile.IsCollapsed = !host.IsCollapsed;
            persisted = true;

            return current;
        });

        if (persisted && layout != null)
        {
            ApplyLayout(layout);
        }
    }

    private void ApplyLayout(DashboardLayoutSettings layout)
    {
        var columnCount = Math.Clamp(layout.ColumnCount, DashboardLayoutDefaults.MinColumnCount, DashboardLayoutDefaults.MaxColumnCount);
        var rowCount = Math.Clamp(layout.RowCount, DashboardLayoutDefaults.MinRowCount, DashboardLayoutDefaults.MaxRowCount);

        var placements = BuildPlacements(layout, columnCount, rowCount);

        var sampleHost = _hosts.Values.FirstOrDefault();
        var collapsedHeight = sampleHost != null
            ? sampleHost.HeaderHeight + sampleHost.Margin.Vertical + 2
            : LogicalToDeviceUnits(44);

        var collapsedColumnWidth = sampleHost != null
            ? sampleHost.HeaderHeight + sampleHost.Margin.Horizontal + LogicalToDeviceUnits(16)
            : LogicalToDeviceUnits(56);

        var rowFixedHeights = ComputeRowFixedHeights(placements, rowCount, collapsedHeight);
        var flexRowCount = rowFixedHeights.Count(height => height == null);
        var percentPerFlexRow = flexRowCount > 0 ? 100F / flexRowCount : 100F / rowCount;

        var columnFixedWidths = ComputeColumnFixedWidths(placements, columnCount, rowCount, collapsedColumnWidth);
        var flexColumnCount = columnFixedWidths.Count(width => width == null);
        var percentPerFlexColumn = flexColumnCount > 0 ? 100F / flexColumnCount : 100F / columnCount;

        _tilesTableLayoutPanel.SuspendLayout();

        try
        {
            ConfigureTableStyles(columnCount, rowCount, columnFixedWidths, rowFixedHeights, percentPerFlexColumn, percentPerFlexRow);
            ApplyPlacements(placements);
        }
        finally
        {
            _tilesTableLayoutPanel.ResumeLayout();
        }
    }

    private List<TilePlacement> BuildPlacements(DashboardLayoutSettings layout, int columnCount, int rowCount)
    {
        var placements = new List<TilePlacement>();
        var seenTypes = new HashSet<DashboardTileType>();

        foreach (var tile in layout.Tiles.Where(tile => tile.IsVisible))
        {
            var type = TileCatalog.Find(tile.TypeId);

            if (type == null || !seenTypes.Add(type) || !_hosts.TryGetValue(type, out var host))
            {
                continue;
            }

            var row = Math.Clamp(tile.Row, 0, rowCount - 1);
            var column = Math.Clamp(tile.Column, 0, columnCount - 1);
            var columnSpan = Math.Clamp(tile.ColumnSpan, 1, columnCount - column);
            var rowSpan = Math.Clamp(tile.RowSpan, 1, rowCount - row);

            placements.Add(new(host, type, row, column, columnSpan, rowSpan,
                ResolveMaxSize(tile.MaxHeight, type.MaxHeight),
                ResolveMaxSize(tile.MaxWidth, type.MaxWidth),
                tile.IsCollapsed));
        }

        return placements;
    }

    private void ConfigureTableStyles(
        int columnCount,
        int rowCount,
        IReadOnlyList<int?> columnFixedWidths,
        IReadOnlyList<int?> rowFixedHeights,
        float percentPerFlexColumn,
        float percentPerFlexRow)
    {
        _tilesTableLayoutPanel.ColumnStyles.Clear();
        _tilesTableLayoutPanel.RowStyles.Clear();
        _tilesTableLayoutPanel.ColumnCount = columnCount;
        _tilesTableLayoutPanel.RowCount = rowCount;

        for (var column = 0; column < columnCount; column++)
        {
            _tilesTableLayoutPanel.ColumnStyles.Add(columnFixedWidths[column] is { } fixedWidth
                ? new(SizeType.Absolute, fixedWidth)
                : new ColumnStyle(SizeType.Percent, percentPerFlexColumn));
        }

        for (var row = 0; row < rowCount; row++)
        {
            _tilesTableLayoutPanel.RowStyles.Add(rowFixedHeights[row] is { } fixedHeight
                ? new(SizeType.Absolute, fixedHeight)
                : new RowStyle(SizeType.Percent, percentPerFlexRow));
        }
    }

    private void ApplyPlacements(IReadOnlyList<TilePlacement> placements)
    {
        foreach (var host in _hosts.Values)
        {
            host.Visible = false;
        }

        foreach (var placement in placements)
        {
            _tilesTableLayoutPanel.SetCellPosition(placement.Host, new(placement.Column, placement.Row));
            _tilesTableLayoutPanel.SetColumnSpan(placement.Host, placement.ColumnSpan);
            _tilesTableLayoutPanel.SetRowSpan(placement.Host, placement.RowSpan);
            placement.Host.SetCollapsed(placement.IsCollapsed);
            placement.Host.Visible = true;
        }
    }

    private sealed record TilePlacement(
        DashboardTileHost Host,
        DashboardTileType Type,
        int Row,
        int Column,
        int ColumnSpan,
        int RowSpan,
        int? MaxHeight,
        int? MaxWidth,
        bool IsCollapsed);
}
