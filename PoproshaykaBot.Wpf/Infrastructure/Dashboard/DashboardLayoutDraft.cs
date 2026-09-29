using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Wpf.Infrastructure.Dashboard;

public sealed class DashboardLayoutDraft
{
    private const int UndoDepth = 20;
    private const int MaxDepth = 32;
    private const double MinimumShare = 0.05;

    private readonly List<DashboardLayoutSettings> _undo = [];

    private DashboardLayoutSettings _layout;
    private DashboardLayoutSettings? _gestureBase;
    private bool _gestureRemembered;

    public DashboardLayoutDraft(DashboardLayoutSettings? layout)
    {
        _layout = Clone(layout) ?? DashboardLayoutDefaults.Create();
    }

    public DashboardLayoutSettings Layout => _layout;

    public bool CanUndo => _undo.Count > 0;

    public int Version { get; private set; }

    public static DashboardLayoutSettings? Clone(DashboardLayoutSettings? layout)
    {
        return layout is null ? null : JsonStoreClone.DeepClone(layout);
    }

    public void Replace(DashboardLayoutSettings layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        EndGridGesture();
        _layout = layout;
        _undo.Clear();
        Version++;
    }

    public bool Undo()
    {
        EndGridGesture();

        if (_undo.Count == 0)
        {
            return false;
        }

        _layout = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Version++;

        return true;
    }

    public void ResetToDefaults()
    {
        EndGridGesture();
        Remember(Clone(_layout)!);

        var previous = _layout;

        _layout = DashboardLayoutReconciler.ResetToDefaults(DashboardLayoutDefaults.Create(), previous);

        foreach (var tile in previous.Tiles)
        {
            Collapse(_layout, tile.TypeId, tile.IsCollapsed);
        }

        DashboardLayoutReconciler.SyncRoot(_layout);
        Version++;
    }

    public bool SetCollapsed(string typeId, bool isCollapsed)
    {
        ArgumentNullException.ThrowIfNull(typeId);

        if (!Collapse(_layout, typeId, isCollapsed))
        {
            return false;
        }

        foreach (var layout in _undo)
        {
            Collapse(layout, typeId, isCollapsed);
        }

        if (_gestureBase is not null)
        {
            Collapse(_gestureBase, typeId, isCollapsed);
        }

        return true;
    }

    public void BeginGridGesture()
    {
        _gestureBase = Clone(_layout);
        _gestureRemembered = false;
    }

    public void EndGridGesture()
    {
        _gestureBase = null;
        _gestureRemembered = false;
    }

    public IReadOnlyList<string> SetGridSize(int columnCount, int rowCount)
    {
        var columns = Math.Clamp(columnCount, DashboardLayoutDefaults.MinColumnCount, DashboardLayoutDefaults.MaxColumnCount);
        var rows = Math.Clamp(rowCount, DashboardLayoutDefaults.MinRowCount, DashboardLayoutDefaults.MaxRowCount);

        if (columns == _layout.ColumnCount && rows == _layout.RowCount)
        {
            return [];
        }

        var previous = _gestureBase is null ? Clone(_layout)! : null;
        var next = Clone(_gestureBase ?? _layout)!;
        var hidden = Regrid(next, columns, rows);

        _layout = next;

        if (previous is not null)
        {
            Remember(previous);
        }
        else if (!_gestureRemembered)
        {
            Remember(Clone(_gestureBase)!);
            _gestureRemembered = true;
        }

        Version++;

        return hidden;
    }

    public bool Resize(IReadOnlyList<int> path, IReadOnlyList<double> weights)
    {
        return Apply(root => DashboardPaneEditor.TryResize(root, path, weights, out var result) ? result : null)
            == DashboardEditStatus.Applied;
    }

    public DashboardEditStatus Move(IReadOnlyList<int> sourcePath, IReadOnlyList<int> targetPath, PaneSide side)
    {
        return Apply(root => DashboardPaneEditor.TryMove(root, sourcePath, targetPath, side, out var result) ? result : null);
    }

    public DashboardEditStatus Add(string typeId, string targetTypeId, PaneSide side)
    {
        return Apply(root => DashboardPaneEditor.TryFindPath(root, targetTypeId, out var targetPath)
            ? Split(root, targetPath, side, typeId)
            : null);
    }

    public DashboardEditStatus AddAt(string typeId, IReadOnlyList<int> targetPath, PaneSide side)
    {
        return Apply(root => Split(root, targetPath, side, typeId));
    }

    public DashboardRemoveStatus Remove(string typeId)
    {
        return RemoveWhere(root => DashboardPaneEditor.TryFindPath(root, typeId, out var path) ? path : null);
    }

    public DashboardRemoveStatus RemoveAt(IReadOnlyList<int> path)
    {
        return RemoveWhere(_ => path);
    }

    public DashboardPane? Preview(Func<DashboardPane, DashboardPane?> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (_layout.Root is not { } root)
        {
            return null;
        }

        return change(root) is { } updated && Fits(updated) ? updated : null;
    }

    public DashboardEditStatus Apply(Func<DashboardPane, DashboardPane?> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (_layout.Root is not { } root)
        {
            return DashboardEditStatus.Unavailable;
        }

        var previous = Clone(_layout)!;

        if (change(root) is not { } updated)
        {
            _layout = previous;

            return DashboardEditStatus.Rejected;
        }

        if (ReferenceEquals(updated, root))
        {
            return DashboardEditStatus.Applied;
        }

        if (!Fits(updated))
        {
            _layout = previous;

            return DashboardEditStatus.GridFull;
        }

        _layout.Root = updated;

        if (!DashboardLayoutReconciler.SyncTiles(_layout))
        {
            _layout = previous;

            return DashboardEditStatus.Rejected;
        }

        EndGridGesture();
        Remember(previous);
        Version++;

        return DashboardEditStatus.Applied;
    }

    private static bool Collapse(DashboardLayoutSettings layout, string typeId, bool isCollapsed)
    {
        var found = false;

        foreach (var tile in layout.Tiles.Where(tile => string.Equals(tile.TypeId, typeId, StringComparison.Ordinal)))
        {
            tile.IsCollapsed = isCollapsed;
            found = true;
        }

        return found;
    }

    private static DashboardPane CarryWeights(DashboardPane rebuilt, DashboardPane previous)
    {
        var carried = rebuilt;

        foreach (var (path, weights) in MatchingWeights(rebuilt, previous, []))
        {
            if (DashboardPaneEditor.TryResize(carried, path, weights, out var resized))
            {
                carried = resized;
            }
        }

        return carried;
    }

    private static IEnumerable<(int[] Path, double[] Weights)> MatchingWeights(DashboardPane rebuilt, DashboardPane previous, int[] path)
    {
        if (path.Length > MaxDepth
            || rebuilt is not SplitPane next
            || previous is not SplitPane before
            || next.Orientation != before.Orientation)
        {
            yield break;
        }

        if (WeightsFor(next, before) is { } weights)
        {
            yield return (path, weights);
        }

        foreach (var (nextIndex, beforeIndex) in Aligned(next, before))
        {
            var nested = MatchingWeights(
                next.Children[nextIndex].Pane,
                before.Children[beforeIndex].Pane,
                [.. path, nextIndex]);

            foreach (var match in nested)
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<(int Next, int Before)> Aligned(SplitPane next, SplitPane before)
    {
        var taken = new bool[before.Children.Count];

        for (var index = 0; index < next.Children.Count; index++)
        {
            var signature = Signature(next.Children[index].Pane);

            if (signature.Count == 0)
            {
                continue;
            }

            for (var candidate = 0; candidate < before.Children.Count; candidate++)
            {
                if (taken[candidate] || !signature.All(Signature(before.Children[candidate].Pane).Contains))
                {
                    continue;
                }

                taken[candidate] = true;

                yield return (index, candidate);

                break;
            }
        }
    }

    private static double[]? WeightsFor(SplitPane next, SplitPane before)
    {
        if (next.Children.Count < 2)
        {
            return null;
        }

        var weights = new double[next.Children.Count];
        var carried = new bool[next.Children.Count];
        var carriedTotal = 0.0;

        foreach (var (nextIndex, beforeIndex) in Aligned(next, before))
        {
            if (!IsExplicit(before.Children[beforeIndex].Weight))
            {
                continue;
            }

            weights[nextIndex] = before.Children[beforeIndex].Weight!.Value;
            carried[nextIndex] = true;
            carriedTotal += weights[nextIndex];
        }

        if (carriedTotal <= 0 || !double.IsFinite(carriedTotal))
        {
            return null;
        }

        var gridShare = 1.0 / next.Children.Count;
        var free = 0.0;

        for (var index = 0; index < weights.Length; index++)
        {
            if (carried[index])
            {
                continue;
            }

            weights[index] = IsExplicit(next.Children[index].Weight) ? next.Children[index].Weight!.Value : gridShare;
            free += weights[index];
        }

        if (free >= 1 - MinimumShare)
        {
            return null;
        }

        var scale = (1 - free) / carriedTotal;

        for (var index = 0; index < weights.Length; index++)
        {
            if (carried[index])
            {
                weights[index] *= scale;
            }
        }

        return weights;
    }

    private static bool IsExplicit(double? weight)
    {
        return weight is { } value && value > 0 && double.IsFinite(value);
    }

    private static List<string> Signature(DashboardPane? pane)
    {
        var leaves = new List<string>();

        Collect(pane, leaves, 0);

        return leaves;
    }

    private static void Collect(DashboardPane? pane, List<string> leaves, int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        switch (pane)
        {
            case TilePane tile when !DashboardLayoutTree.IsEmptySlot(tile.TypeId):
                leaves.Add(tile.TypeId);

                return;

            case SplitPane split:
                foreach (var slot in split.Children)
                {
                    Collect(slot?.Pane, leaves, depth + 1);
                }

                return;
        }
    }

    private static bool Fits(DashboardPane root)
    {
        return DashboardLayoutTree.TryMeasure(root) is { } size
            && size.Columns <= DashboardLayoutDefaults.MaxColumnCount
            && size.Rows <= DashboardLayoutDefaults.MaxRowCount;
    }

    private static List<string> Regrid(DashboardLayoutSettings layout, int columns, int rows)
    {
        var hidden = new List<string>();

        if (columns < layout.ColumnCount || rows < layout.RowCount)
        {
            hidden = Shrink(layout, Math.Min(columns, layout.ColumnCount), Math.Min(rows, layout.RowCount));
        }

        if (columns > layout.ColumnCount || rows > layout.RowCount)
        {
            Grow(layout, Math.Max(columns, layout.ColumnCount), Math.Max(rows, layout.RowCount));
        }

        return hidden;
    }

    private static List<string> Shrink(DashboardLayoutSettings layout, int columns, int rows)
    {
        var hidden = Crop(layout, columns, rows);

        // TODO: убавление перестраивает дерево из Tiles – дыра уже половины трека и доли узла со сменившейся
        //       ориентацией теряются; перевести на удаление листов из дерева, когда на это придёт жалоба или тест.
        Rebuild(layout, columns, rows);

        return hidden;
    }

    private static void Grow(DashboardLayoutSettings layout, int columns, int rows)
    {
        if (layout.Root is { } root && DashboardPaneEditor.IsWellFormed(root))
        {
            var (previousColumns, previousRows) = (layout.ColumnCount, layout.RowCount);
            var grown = WithHoles(WithHoles(root, SplitOrientation.Columns, previousColumns, columns), SplitOrientation.Rows, previousRows, rows);

            layout.Root = grown;
            layout.ColumnCount = columns;
            layout.RowCount = rows;

            if (Fits(grown) && DashboardLayoutReconciler.SyncTiles(layout))
            {
                return;
            }

            layout.Root = root;
            layout.ColumnCount = previousColumns;
            layout.RowCount = previousRows;
        }

        Rebuild(layout, columns, rows);
    }

    private static void Rebuild(DashboardLayoutSettings layout, int columns, int rows)
    {
        var before = layout.Root;

        layout.ColumnCount = columns;
        layout.RowCount = rows;
        layout.Root = null;

        DashboardLayoutReconciler.SyncRoot(layout);

        if (layout.Root is not { } rebuilt || before is null)
        {
            return;
        }

        layout.Root = CarryWeights(rebuilt, before);

        if (!DashboardLayoutReconciler.SyncTiles(layout))
        {
            layout.Root = rebuilt;
            DashboardLayoutReconciler.SyncTiles(layout);
        }
    }

    private static DashboardPane WithHoles(DashboardPane root, SplitOrientation orientation, int count, int grown)
    {
        if (count <= 0 || grown <= count)
        {
            return root;
        }

        var keep = (double)count / grown;
        var children = new List<PaneSlot>(grown - count + 1);

        if (root is SplitPane split
            && split.Orientation == orientation
            && DashboardLayoutTree.ResolveWeights(split.Children) is { } resolved
            && resolved.Sum() is var total
            && total > 0
            && double.IsFinite(total))
        {
            foreach (var slot in split.Children)
            {
                children.Add(new(slot.Pane, IsExplicit(slot.Weight) ? slot.Weight!.Value / total * keep : null));
            }
        }
        else
        {
            children.Add(new(root, keep));
        }

        for (var index = count; index < grown; index++)
        {
            children.Add(new(new TilePane(DashboardLayoutTree.EmptySlotTypeId), 1.0 / grown));
        }

        return new SplitPane(orientation, children);
    }

    private static List<string> Crop(DashboardLayoutSettings layout, int columns, int rows)
    {
        var occupied = new bool[rows, columns];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var hidden = new List<string>();

        var shown = layout.Tiles
            .Where(tile => tile.IsVisible && !string.IsNullOrEmpty(tile.TypeId) && !DashboardLayoutTree.IsEmptySlot(tile.TypeId))
            .OrderBy(tile => tile.Row)
            .ThenBy(tile => tile.Column)
            .ToList();

        foreach (var tile in shown)
        {
            if (!seen.Add(tile.TypeId))
            {
                continue;
            }

            if (tile.Row >= rows || tile.Column >= columns || !TryOccupy(occupied, tile, columns, rows))
            {
                tile.IsVisible = false;
                hidden.Add(tile.TypeId);
            }
        }

        if (hidden.Count > 0 && hidden.Count == seen.Count)
        {
            Reflow(shown.Where(tile => !tile.IsVisible).DistinctBy(tile => tile.TypeId, StringComparer.Ordinal), columns, rows, hidden);
        }

        return hidden;
    }

    private static void Reflow(IEnumerable<DashboardTileSettings> tiles, int columns, int rows, List<string> hidden)
    {
        var pairs = tiles
            .Select(tile => (Tile: tile, Placed: new PlacedTile
            {
                TypeId = tile.TypeId,
                Row = tile.Row,
                Column = tile.Column,
                ColumnSpan = tile.ColumnSpan,
                RowSpan = tile.RowSpan,
                MaxWidth = tile.MaxWidth,
                MaxHeight = tile.MaxHeight,
            }))
            .ToList();

        var (_, unplaceable) = DashboardLayoutCalculator.ResolveLayout(pairs.Select(pair => pair.Placed), rows, columns);
        var lost = unplaceable.ToHashSet();

        foreach (var (tile, placed) in pairs.Where(pair => !lost.Contains(pair.Placed)))
        {
            tile.Row = placed.Row;
            tile.Column = placed.Column;
            tile.RowSpan = placed.RowSpan;
            tile.ColumnSpan = placed.ColumnSpan;
            tile.IsVisible = true;
            hidden.Remove(tile.TypeId);
        }
    }

    private static bool TryOccupy(bool[,] occupied, DashboardTileSettings tile, int columns, int rows)
    {
        var row = Math.Max(tile.Row, 0);
        var column = Math.Max(tile.Column, 0);
        var rowEnd = Math.Clamp(tile.Row + tile.RowSpan, row + 1, rows);
        var columnEnd = Math.Clamp(tile.Column + tile.ColumnSpan, column + 1, columns);

        for (var r = row; r < rowEnd; r++)
        {
            for (var c = column; c < columnEnd; c++)
            {
                if (occupied[r, c])
                {
                    return false;
                }
            }
        }

        for (var r = row; r < rowEnd; r++)
        {
            for (var c = column; c < columnEnd; c++)
            {
                occupied[r, c] = true;
            }
        }

        tile.Row = row;
        tile.Column = column;
        tile.RowSpan = rowEnd - row;
        tile.ColumnSpan = columnEnd - column;

        return true;
    }

    private DashboardPane? Split(DashboardPane root, IReadOnlyList<int> targetPath, PaneSide side, string typeId)
    {
        DashboardLayoutReconciler.AppendMissingTypes(_layout, [typeId]);

        return Place(root, targetPath, side, typeId);
    }

    public static DashboardPane? Place(DashboardPane root, IReadOnlyList<int> targetPath, PaneSide side, string typeId)
    {
        if (side == PaneSide.None && DashboardPaneEditor.TryFill(root, targetPath, typeId, out var filled))
        {
            return filled;
        }

        return DashboardPaneEditor.TrySplit(root, targetPath, side == PaneSide.None ? PaneSide.Right : side, typeId, out var result)
            ? result
            : null;
    }

    private DashboardRemoveStatus RemoveWhere(Func<DashboardPane, IReadOnlyList<int>?> locate)
    {
        var status = DashboardRemoveStatus.Rejected;

        var applied = Apply(root =>
        {
            if (locate(root) is not { } path)
            {
                return null;
            }

            var removal = DashboardPaneEditor.Remove(root, path);

            status = removal.Status;

            return removal.Root;
        });

        if (applied == DashboardEditStatus.Applied)
        {
            return DashboardRemoveStatus.Removed;
        }

        return status == DashboardRemoveStatus.LastTile ? DashboardRemoveStatus.LastTile : DashboardRemoveStatus.Rejected;
    }

    private void Remember(DashboardLayoutSettings previous)
    {
        _undo.Add(previous);

        if (_undo.Count > UndoDepth)
        {
            _undo.RemoveAt(0);
        }
    }
}
