using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Dashboard;

public static class DashboardLayoutReconciler
{
    public static DashboardTileSettings CreateHiddenTile(PlacedTile placed, bool isCollapsed)
    {
        ArgumentNullException.ThrowIfNull(placed);

        return new()
        {
            Id = placed.TypeId,
            TypeId = placed.TypeId,
            Row = placed.Row,
            Column = placed.Column,
            ColumnSpan = placed.ColumnSpan,
            RowSpan = placed.RowSpan,
            IsVisible = false,
            IsCollapsed = isCollapsed,
            MaxHeight = placed.MaxHeight,
            MaxWidth = placed.MaxWidth,
        };
    }

    public static bool AppendMissingTypes(DashboardLayoutSettings layout, IEnumerable<string> catalogTypeIds)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(catalogTypeIds);

        var present = layout.Tiles.Select(tile => tile.TypeId).ToHashSet(StringComparer.Ordinal);
        var order = NextOrder(layout);
        var appended = false;

        foreach (var typeId in catalogTypeIds)
        {
            if (string.IsNullOrEmpty(typeId) || DashboardLayoutTree.IsEmptySlot(typeId) || !present.Add(typeId))
            {
                continue;
            }

            layout.Tiles.Add(new()
            {
                Id = typeId,
                TypeId = typeId,
                Order = order++,
                IsVisible = false,
            });

            appended = true;
        }

        return appended;
    }

    public static void AppendPreserved(DashboardLayoutSettings layout, IEnumerable<DashboardTileSettings> preserved)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(preserved);

        var written = layout.Tiles.Select(tile => tile.TypeId).ToHashSet(StringComparer.Ordinal);
        var order = NextOrder(layout);

        foreach (var tile in preserved)
        {
            if (DashboardLayoutTree.IsEmptySlot(tile.TypeId) || !written.Add(tile.TypeId))
            {
                continue;
            }

            var clone = JsonStoreClone.DeepClone(tile);
            clone.Order = order++;
            layout.Tiles.Add(clone);
        }
    }

    public static DashboardLayoutSettings ResetToDefaults(DashboardLayoutSettings defaults, DashboardLayoutSettings? persisted)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        var layout = JsonStoreClone.DeepClone(defaults);

        if (persisted is not null)
        {
            AppendPreserved(layout, persisted.Tiles);
        }

        return layout;
    }

    public static void SyncRoot(DashboardLayoutSettings layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.Root is { } root)
        {
            var sanitized = DashboardPaneWeights.Sanitize(root);

            layout.Root = sanitized;

            if (ProjectsInto(sanitized, layout))
            {
                return;
            }
        }

        layout.Root = DashboardLayoutTree.TryBuild(layout.Tiles, layout.ColumnCount, layout.RowCount);
    }

    public static bool SyncTiles(DashboardLayoutSettings layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.Root is null
            || !DashboardPaneEditor.IsWellFormed(layout.Root)
            || DashboardLayoutTree.TryMeasure(layout.Root) is not { } minimum)
        {
            return false;
        }

        var columnCount = Math.Max(layout.ColumnCount, minimum.Columns);
        var rowCount = Math.Max(layout.RowCount, minimum.Rows);

        var placed = DashboardLayoutTree.WithGridWeights(layout.Root, VisibleRects(layout), columnCount, rowCount);

        if (DashboardLayoutTree.TryProject(placed, columnCount, rowCount) is not { } projected)
        {
            return false;
        }

        var records = new Dictionary<string, DashboardTileSettings>(StringComparer.Ordinal);

        foreach (var tile in layout.Tiles)
        {
            records.TryAdd(tile.TypeId, tile);
        }

        var order = NextOrder(layout);

        foreach (var rect in projected)
        {
            if (!records.TryGetValue(rect.TypeId, out var tile))
            {
                tile = new()
                {
                    Id = rect.TypeId,
                    TypeId = rect.TypeId,
                    Order = order++,
                };

                records[rect.TypeId] = tile;
                layout.Tiles.Add(tile);
            }

            tile.Row = rect.Row;
            tile.Column = rect.Column;
            tile.RowSpan = rect.RowSpan;
            tile.ColumnSpan = rect.ColumnSpan;
            tile.IsVisible = true;
        }

        var leaves = projected.Select(rect => rect.TypeId).ToHashSet(StringComparer.Ordinal);

        foreach (var tile in layout.Tiles)
        {
            if (!leaves.Contains(tile.TypeId))
            {
                tile.IsVisible = false;
            }
        }

        layout.ColumnCount = columnCount;
        layout.RowCount = rowCount;

        return true;
    }

    public static void MergeConcurrentEdits(DashboardLayoutSettings layout, DashboardLayoutSettings? persisted, bool keepDraftRoot = false)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (persisted is null)
        {
            return;
        }

        var collapsed = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var tile in persisted.Tiles)
        {
            collapsed.TryAdd(tile.TypeId, tile.IsCollapsed);
        }

        foreach (var tile in layout.Tiles)
        {
            if (collapsed.TryGetValue(tile.TypeId, out var isCollapsed))
            {
                tile.IsCollapsed = isCollapsed;
            }
        }

        if (!keepDraftRoot && layout.Root is not null && KeepsGeometry(layout, persisted))
        {
            layout.Root = persisted.Root;
        }

        AppendPreserved(layout, HiddenCopies(layout, persisted));
    }

    public static string DescribeHiddenTiles(IReadOnlyCollection<string> titles)
    {
        ArgumentNullException.ThrowIfNull(titles);

        if (titles.Count == 0)
        {
            return string.Empty;
        }

        var names = string.Join(", ", titles.Select(title => $"«{title}»"));

        return titles.Count == 1
            ? $"Плитка {names} не поместилась в сетку и выключена – верните её из палитры."
            : $"Плитки не поместились в сетку и выключены: {names} – верните их из палитры.";
    }

    private static List<DashboardTileSettings> HiddenCopies(DashboardLayoutSettings layout, DashboardLayoutSettings persisted)
    {
        var present = layout.Tiles.Select(tile => tile.TypeId).ToHashSet(StringComparer.Ordinal);
        var missing = new List<DashboardTileSettings>();

        foreach (var tile in persisted.Tiles)
        {
            if (present.Contains(tile.TypeId))
            {
                continue;
            }

            var clone = JsonStoreClone.DeepClone(tile);
            clone.IsVisible = false;
            missing.Add(clone);
        }

        return missing;
    }

    // TODO: черновик настроек владеет геометрией, видимостью и потолками, диск – только IsCollapsed; когда дашборд начнёт писать мимо страницы настроек ещё одно поле записи, добавить его сюда
    private static bool KeepsGeometry(DashboardLayoutSettings layout, DashboardLayoutSettings persisted)
    {
        if (layout.ColumnCount != persisted.ColumnCount || layout.RowCount != persisted.RowCount)
        {
            return false;
        }

        var expected = VisibleRects(persisted);

        return VisibleRects(layout).All(expected.Remove) && expected.Count == 0;
    }

    private static List<TileRect> VisibleRects(DashboardLayoutSettings layout)
    {
        return layout.Tiles
            .Where(tile => tile is { IsVisible: true } && !DashboardLayoutTree.IsEmptySlot(tile.TypeId))
            .Select(tile => new TileRect(tile.TypeId, tile.Row, tile.Column, tile.RowSpan, tile.ColumnSpan))
            .ToList();
    }

    private static bool ProjectsInto(DashboardPane root, DashboardLayoutSettings layout)
    {
        var rects = VisibleRects(layout);

        if (rects.Select(rect => rect.TypeId).Distinct(StringComparer.Ordinal).Count() != rects.Count)
        {
            return false;
        }

        return Matches(root) || Matches(DashboardLayoutTree.WithGridWeights(root, rects, layout.ColumnCount, layout.RowCount));

        bool Matches(DashboardPane pane)
        {
            var projected = DashboardLayoutTree.TryProject(pane, layout.ColumnCount, layout.RowCount);

            if (projected is null || projected.Count != rects.Count)
            {
                return false;
            }

            var expected = new List<TileRect>(rects);

            return projected.All(expected.Remove);
        }
    }

    private static int NextOrder(DashboardLayoutSettings layout)
    {
        return layout.Tiles.Count == 0 ? 0 : layout.Tiles.Max(tile => tile.Order) + 1;
    }
}
