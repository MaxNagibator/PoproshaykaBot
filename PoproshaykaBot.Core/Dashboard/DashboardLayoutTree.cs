using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Dashboard;

public static class DashboardLayoutTree
{
    public const string EmptySlotTypeId = "__empty";

    private const int MaxCells = 4096;
    private const int MaxDepth = 32;

    public static bool IsEmptySlot(string? typeId)
    {
        return string.Equals(typeId, EmptySlotTypeId, StringComparison.Ordinal);
    }

    public static bool IsEmptySlot(DashboardPane? pane)
    {
        return pane is TilePane tile && IsEmptySlot(tile.TypeId);
    }

    public static DashboardPane? TryBuild(IEnumerable<DashboardTileSettings> tiles, int columnCount, int rowCount)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        var rects = CollectVisible(tiles, columnCount, rowCount);

        if (rects is null || Build(rects, 0, 0, rowCount, columnCount) is not { } root)
        {
            return null;
        }

        return Collapse(root, 0);
    }

    public static IReadOnlyList<TileRect>? TryProject(DashboardPane root, int columnCount, int rowCount)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (columnCount <= 0 || rowCount <= 0)
        {
            return null;
        }

        var projected = new List<TileRect>();

        return Project(root, 0, 0, rowCount, columnCount, projected) ? projected : null;
    }

    public static GridSize? TryMeasure(DashboardPane root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return Measure(root, 0);
    }

    public static DashboardPane WithGridWeights(DashboardPane root, IEnumerable<TileRect> rects, int columnCount, int rowCount)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(rects);

        var byTypeId = new Dictionary<string, TileRect>(StringComparer.Ordinal);

        foreach (var rect in rects)
        {
            byTypeId.TryAdd(rect.TypeId, rect);
        }

        return Substitute(root, byTypeId, 0, new(0, 0, Math.Max(rowCount, 0), Math.Max(columnCount, 0)));
    }

    private static DashboardPane Substitute(
        DashboardPane pane,
        IReadOnlyDictionary<string, TileRect> rects,
        int depth,
        GridBounds bounds)
    {
        if (depth > MaxDepth
            || pane is not SplitPane { Orientation: SplitOrientation.Columns or SplitOrientation.Rows, Children.Count: > 1 } split)
        {
            return pane;
        }

        var alongColumns = split.Orientation == SplitOrientation.Columns;
        var start = alongColumns ? bounds.Column : bounds.Row;
        var end = alongColumns ? bounds.ColumnEnd : bounds.RowEnd;
        var extents = new (int Start, int End)?[split.Children.Count];

        for (var index = 0; index < split.Children.Count; index++)
        {
            if (split.Children[index] is not { Pane: not null } slot)
            {
                return pane;
            }

            extents[index] = Extent(slot.Pane, split.Orientation, rects, depth);
        }

        FillGaps(extents, start, end);

        var children = new PaneSlot[split.Children.Count];
        var spans = new int[split.Children.Count];

        for (var index = 0; index < split.Children.Count; index++)
        {
            var slot = split.Children[index];
            var extent = extents[index] ?? (start, start);

            children[index] = new(Substitute(slot.Pane, rects, depth + 1, bounds.Cut(alongColumns, extent)), slot.Weight);
            spans[index] = extent.End > extent.Start ? extent.End - extent.Start : 0;
        }

        ApplySpans(children, spans);

        return new SplitPane(split.Orientation, children);
    }

    private static void FillGaps((int Start, int End)?[] extents, int start, int end)
    {
        var cursor = start;
        var pending = 0;

        for (var index = 0; index <= extents.Length; index++)
        {
            var known = index < extents.Length ? extents[index] : (start: end, end: end);

            if (known is null)
            {
                pending++;

                continue;
            }

            if (pending > 0)
            {
                Spread(extents, index - pending, pending, cursor, known.Value.Start);
            }

            pending = 0;
            cursor = known.Value.End;
        }
    }

    private static void Spread((int Start, int End)?[] extents, int from, int count, int start, int end)
    {
        var total = end - start;

        if (total < count)
        {
            return;
        }

        var offset = start;

        for (var index = 0; index < count; index++)
        {
            var next = start + (int)Math.Round((double)total * (index + 1) / count, MidpointRounding.AwayFromZero);

            extents[from + index] = (offset, next);
            offset = next;
        }
    }

    private static void ApplySpans(PaneSlot[] children, int[] spans)
    {
        var known = 0.0;
        var knownSpan = 0;
        var totalSpan = 0;
        var unknown = 0;

        for (var index = 0; index < children.Length; index++)
        {
            if (spans[index] <= 0)
            {
                return;
            }

            totalSpan += spans[index];

            if (IsExplicit(children[index].Weight))
            {
                known += children[index].Weight!.Value;
                knownSpan += spans[index];
            }
            else
            {
                unknown++;
            }
        }

        if (unknown == 0)
        {
            return;
        }

        var unit = knownSpan > 0 && known > 0 ? known / knownSpan : 1.0 / totalSpan;

        for (var index = 0; index < children.Length; index++)
        {
            if (!IsExplicit(children[index].Weight))
            {
                children[index] = new(children[index].Pane, unit * spans[index]);
            }
        }
    }

    private static (int Start, int End)? Extent(
        DashboardPane? pane,
        SplitOrientation orientation,
        IReadOnlyDictionary<string, TileRect> rects,
        int depth)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        switch (pane)
        {
            case TilePane tile:
                if (string.IsNullOrEmpty(tile.TypeId) || !rects.TryGetValue(tile.TypeId, out var rect))
                {
                    return null;
                }

                return orientation == SplitOrientation.Columns
                    ? (rect.Column, rect.ColumnEnd)
                    : (rect.Row, rect.RowEnd);

            case SplitPane { Children.Count: > 0 } split:
                var start = int.MaxValue;
                var end = int.MinValue;

                foreach (var slot in split.Children)
                {
                    if (slot?.Pane is null)
                    {
                        return null;
                    }

                    if (Extent(slot.Pane, orientation, rects, depth + 1) is not { } child)
                    {
                        continue;
                    }

                    start = Math.Min(start, child.Start);
                    end = Math.Max(end, child.End);
                }

                return end > start ? (start, end) : null;

            default:
                return null;
        }
    }

    private static bool IsExplicit(double? weight)
    {
        return DashboardPaneWeights.IsExplicit(weight);
    }

    private static GridSize? Measure(DashboardPane? pane, int depth)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        switch (pane)
        {
            case TilePane tile when IsEmptySlot(tile.TypeId):
                return new GridSize(0, 0);

            case TilePane tile:
                return string.IsNullOrEmpty(tile.TypeId) ? null : new GridSize(1, 1);

            case SplitPane { Orientation: SplitOrientation.Columns or SplitOrientation.Rows, Children.Count: > 1 } split:
                return MeasureSplit(split, depth);

            default:
                return null;
        }
    }

    private static GridSize? MeasureSplit(SplitPane split, int depth)
    {
        var alongColumns = split.Orientation == SplitOrientation.Columns;
        var columns = 0;
        var rows = 0;

        foreach (var slot in split.Children)
        {
            if (slot is null || Measure(slot.Pane, depth + 1) is not { } size)
            {
                return null;
            }

            columns = alongColumns ? columns + size.Columns : Math.Max(columns, size.Columns);
            rows = alongColumns ? Math.Max(rows, size.Rows) : rows + size.Rows;
        }

        return new(columns, rows);
    }

    private static List<TileRect>? CollectVisible(IEnumerable<DashboardTileSettings> tiles, int columnCount, int rowCount)
    {
        if (columnCount <= 0 || rowCount <= 0 || (long)columnCount * rowCount > MaxCells)
        {
            return null;
        }

        var rects = new List<TileRect>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var occupied = new bool[rowCount, columnCount];

        foreach (var tile in tiles)
        {
            if (tile is null || !tile.IsVisible || IsEmptySlot(tile.TypeId))
            {
                continue;
            }

            if (string.IsNullOrEmpty(tile.TypeId))
            {
                return null;
            }

            var rect = new TileRect(tile.TypeId, tile.Row, tile.Column, tile.RowSpan, tile.ColumnSpan);

            if (rect.RowSpan < 1 || rect.ColumnSpan < 1
                || rect.Row < 0 || rect.Column < 0
                || rect.RowEnd > rowCount || rect.ColumnEnd > columnCount
                || !seen.Add(rect.TypeId))
            {
                return null;
            }

            if (!TryOccupy(occupied, rect))
            {
                return null;
            }

            rects.Add(rect);
        }

        if (rects.Count == 0)
        {
            return null;
        }

        AppendEmptySlots(rects, occupied, columnCount, rowCount);

        return rects;
    }

    private static void AppendEmptySlots(List<TileRect> rects, bool[,] occupied, int columnCount, int rowCount)
    {
        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                if (!occupied[row, column])
                {
                    rects.Add(new(EmptySlotTypeId, row, column, 1, 1));
                }
            }
        }
    }

    private static DashboardPane Collapse(DashboardPane pane, int depth)
    {
        if (depth > MaxDepth || pane is not SplitPane { Children.Count: > 0 } split)
        {
            return pane;
        }

        var children = new List<PaneSlot>(split.Children.Count);

        foreach (var slot in split.Children)
        {
            if (slot?.Pane is null)
            {
                return pane;
            }

            var child = Collapse(slot.Pane, depth + 1);

            if (IsEmptySlot(child) && children.Count > 0 && IsEmptySlot(children[^1].Pane))
            {
                children[^1] = new(children[^1].Pane, Merge(children[^1].Weight, slot.Weight));

                continue;
            }

            children.Add(new(child, slot.Weight));
        }

        return children.Count == 1 ? children[0].Pane : new SplitPane(split.Orientation, children);
    }

    private static double? Merge(double? first, double? second)
    {
        return IsExplicit(first) && IsExplicit(second) ? first!.Value + second!.Value : null;
    }

    private static bool TryOccupy(bool[,] occupied, TileRect rect)
    {
        for (var row = rect.Row; row < rect.RowEnd; row++)
        {
            for (var column = rect.Column; column < rect.ColumnEnd; column++)
            {
                if (occupied[row, column])
                {
                    return false;
                }

                occupied[row, column] = true;
            }
        }

        return true;
    }

    private static DashboardPane? Build(List<TileRect> rects, int row, int column, int rowEnd, int columnEnd)
    {
        if (rects.Count == 0)
        {
            return null;
        }

        if (rects.Count == 1)
        {
            return new TilePane(rects[0].TypeId);
        }

        var columnCuts = FindColumnCuts(rects, column, columnEnd);

        if (columnCuts.Count > 0)
        {
            return BuildSplit(rects, SplitOrientation.Columns, columnCuts, row, column, rowEnd, columnEnd);
        }

        var rowCuts = FindRowCuts(rects, row, rowEnd);

        return rowCuts.Count > 0
            ? BuildSplit(rects, SplitOrientation.Rows, rowCuts, row, column, rowEnd, columnEnd)
            : null;
    }

    private static List<int> FindColumnCuts(List<TileRect> rects, int start, int end)
    {
        var cuts = new List<int>();

        for (var cut = start + 1; cut < end; cut++)
        {
            if (rects.TrueForAll(rect => rect.Column >= cut || rect.ColumnEnd <= cut))
            {
                cuts.Add(cut);
            }
        }

        return cuts;
    }

    private static List<int> FindRowCuts(List<TileRect> rects, int start, int end)
    {
        var cuts = new List<int>();

        for (var cut = start + 1; cut < end; cut++)
        {
            if (rects.TrueForAll(rect => rect.Row >= cut || rect.RowEnd <= cut))
            {
                cuts.Add(cut);
            }
        }

        return cuts;
    }

    private static SplitPane? BuildSplit(
        List<TileRect> rects,
        SplitOrientation orientation,
        List<int> cuts,
        int row,
        int column,
        int rowEnd,
        int columnEnd)
    {
        var alongColumns = orientation == SplitOrientation.Columns;
        var start = alongColumns ? column : row;
        var end = alongColumns ? columnEnd : rowEnd;
        var total = (double)(end - start);

        var boundaries = new List<int> { start };
        boundaries.AddRange(cuts);
        boundaries.Add(end);

        var children = new List<PaneSlot>(boundaries.Count - 1);

        for (var index = 0; index < boundaries.Count - 1; index++)
        {
            var from = boundaries[index];
            var to = boundaries[index + 1];

            var inside = rects.FindAll(rect => alongColumns
                ? rect.Column >= from && rect.ColumnEnd <= to
                : rect.Row >= from && rect.RowEnd <= to);

            var pane = alongColumns
                ? Build(inside, row, from, rowEnd, to)
                : Build(inside, from, column, to, columnEnd);

            if (pane is null)
            {
                return null;
            }

            children.Add(new(pane, (to - from) / total));
        }

        return new SplitPane(orientation, children);
    }

    private static bool Project(DashboardPane pane, int row, int column, int rowEnd, int columnEnd, List<TileRect> projected)
    {
        if (IsEmptySlot(pane))
        {
            return true;
        }

        if (rowEnd <= row || columnEnd <= column)
        {
            return false;
        }

        switch (pane)
        {
            case TilePane tile:
                if (string.IsNullOrEmpty(tile.TypeId))
                {
                    return false;
                }

                projected.Add(new(tile.TypeId, row, column, rowEnd - row, columnEnd - column));
                return true;

            case SplitPane { Orientation: SplitOrientation.Columns or SplitOrientation.Rows, Children.Count: > 1 } split:
                return ProjectSplit(split, row, column, rowEnd, columnEnd, projected);

            default:
                return false;
        }
    }

    private static bool ProjectSplit(SplitPane split, int row, int column, int rowEnd, int columnEnd, List<TileRect> projected)
    {
        var alongColumns = split.Orientation == SplitOrientation.Columns;
        var start = alongColumns ? column : row;
        var end = alongColumns ? columnEnd : rowEnd;
        var sizes = Distribute(split.Children, split.Orientation, end - start);

        if (sizes is null)
        {
            return false;
        }

        var offset = start;

        for (var index = 0; index < split.Children.Count; index++)
        {
            var next = offset + sizes[index];

            var placed = alongColumns
                ? Project(split.Children[index].Pane, row, offset, rowEnd, next, projected)
                : Project(split.Children[index].Pane, offset, column, next, columnEnd, projected);

            if (!placed)
            {
                return false;
            }

            offset = next;
        }

        return true;
    }

    private static double ShareForUnknown(double known, int count, int unknown)
    {
        if (known <= 0)
        {
            return 1.0 / count;
        }

        return known < 1
            ? (1 - known) / unknown
            : known / (count - unknown);
    }

    private static int[]? Distribute(IReadOnlyList<PaneSlot> children, SplitOrientation orientation, int total)
    {
        var minimums = new int[children.Count];
        var required = 0;

        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is not { Pane: not null } slot || Measure(slot.Pane, 0) is not { } size)
            {
                return null;
            }

            minimums[index] = orientation == SplitOrientation.Columns ? size.Columns : size.Rows;
            required += minimums[index];
        }

        if (total < required)
        {
            return null;
        }

        var weights = ResolveWeights(children);

        return weights is null ? null : LayOut(weights, total, minimums);
    }

    public static double[]? ResolveWeights(IReadOnlyList<PaneSlot> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        var count = children.Count;
        var weights = new double[count];
        var known = 0.0;
        var unknown = 0;

        for (var index = 0; index < count; index++)
        {
            if (children[index] is not { Pane: not null })
            {
                return null;
            }

            if (IsExplicit(children[index].Weight))
            {
                weights[index] = children[index].Weight!.Value;
                known += weights[index];
            }
            else
            {
                weights[index] = double.NaN;
                unknown++;
            }
        }

        if (unknown == 0)
        {
            return weights;
        }

        var share = ShareForUnknown(known, count, unknown);

        for (var index = 0; index < count; index++)
        {
            if (double.IsNaN(weights[index]))
            {
                weights[index] = share;
            }
        }

        return weights;
    }

    private static int[] LayOut(double[] weights, int total, int[] minimums)
    {
        var count = weights.Length;
        var sum = weights.Sum();

        if (!double.IsFinite(sum) || sum <= 0)
        {
            Array.Fill(weights, 1.0);
            sum = count;
        }

        var sizes = new int[count];
        var cumulative = 0.0;
        var offset = 0;
        var reserved = minimums.Sum();

        for (var index = 0; index < count; index++)
        {
            cumulative += weights[index] / sum;
            reserved -= minimums[index];

            var boundary = index == count - 1
                ? total
                : (int)Math.Round(cumulative * total, MidpointRounding.AwayFromZero);

            boundary = Math.Clamp(boundary, offset + minimums[index], total - reserved);
            sizes[index] = boundary - offset;
            offset = boundary;
        }

        return sizes;
    }

    private readonly record struct GridBounds(int Row, int Column, int RowEnd, int ColumnEnd)
    {
        public GridBounds Cut(bool alongColumns, (int Start, int End) extent)
        {
            return alongColumns
                ? this with { Column = extent.Start, ColumnEnd = extent.End }
                : this with { Row = extent.Start, RowEnd = extent.End };
        }
    }
}
