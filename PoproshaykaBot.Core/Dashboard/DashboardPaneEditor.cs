using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Dashboard;

public static class DashboardPaneEditor
{
    private const int MaxDepth = 32;
    private const double MinimumWeight = 0.05;

    public static bool TryFindPath(DashboardPane root, string typeId, out IReadOnlyList<int> path)
    {
        ArgumentNullException.ThrowIfNull(root);

        path = [];

        if (string.IsNullOrEmpty(typeId)
            || DashboardLayoutTree.IsEmptySlot(typeId)
            || !TryCollectLeaves(root, out var leaves))
        {
            return false;
        }

        var found = leaves.FindIndex(leaf => string.Equals(leaf.TypeId, typeId, StringComparison.Ordinal));

        if (found < 0)
        {
            return false;
        }

        path = leaves[found].Path;

        return true;
    }

    public static bool TrySplit(DashboardPane root, IReadOnlyList<int> targetPath, PaneSide side, string typeId, out DashboardPane result)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(targetPath);

        result = root;

        if (side == PaneSide.None
            || string.IsNullOrEmpty(typeId)
            || DashboardLayoutTree.IsEmptySlot(typeId)
            || !TryCollectLeaves(root, out var leaves)
            || Holds(leaves, typeId)
            || Find(root, targetPath) is not TilePane)
        {
            return false;
        }

        var orientation = side is PaneSide.Left or PaneSide.Right ? SplitOrientation.Columns : SplitOrientation.Rows;
        var before = side is PaneSide.Left or PaneSide.Top;
        if (InsertAt(root, targetPath, orientation, before, typeId) is not { } split || Normalize(split) is not { } normalized)
        {
            return false;
        }

        result = normalized;

        return true;
    }

    public static bool TryFill(DashboardPane root, IReadOnlyList<int> targetPath, string typeId, out DashboardPane result)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(targetPath);

        result = root;

        if (string.IsNullOrEmpty(typeId)
            || DashboardLayoutTree.IsEmptySlot(typeId)
            || !TryCollectLeaves(root, out var leaves)
            || Holds(leaves, typeId)
            || Find(root, targetPath) is not TilePane { TypeId: var targetTypeId }
            || !DashboardLayoutTree.IsEmptySlot(targetTypeId)
            || Substitute(root, targetPath, new TilePane(typeId), 0) is not { } filled)
        {
            return false;
        }

        result = filled;

        return true;
    }

    public static bool TrySwap(DashboardPane root, IReadOnlyList<int> firstPath, IReadOnlyList<int> secondPath, out DashboardPane result)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(firstPath);
        ArgumentNullException.ThrowIfNull(secondPath);

        result = root;

        if (SamePath(firstPath, secondPath)
            || !IsWellFormed(root)
            || Find(root, firstPath) is not TilePane first
            || Find(root, secondPath) is not TilePane second
            || string.Equals(first.TypeId, second.TypeId, StringComparison.Ordinal)
            || Substitute(root, firstPath, second, 0) is not { } half
            || Substitute(half, secondPath, first, 0) is not { } swapped)
        {
            return false;
        }

        result = swapped;

        return true;
    }

    public static bool TryMove(DashboardPane root, IReadOnlyList<int> sourcePath, IReadOnlyList<int> targetPath, PaneSide side, out DashboardPane result)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(targetPath);

        result = root;

        if (SamePath(sourcePath, targetPath) || !TryCollectLeaves(root, out var leaves))
        {
            return false;
        }

        var source = IndexOf(leaves, sourcePath);
        var target = IndexOf(leaves, targetPath);

        if (source < 0 || target < 0 || DashboardLayoutTree.IsEmptySlot(leaves[source].TypeId))
        {
            return false;
        }

        var fills = side == PaneSide.None;

        if (fills && !DashboardLayoutTree.IsEmptySlot(leaves[target].TypeId))
        {
            return TrySwap(root, sourcePath, targetPath, out result);
        }

        if (RemoveAt(root, sourcePath) is not { } without
            || !TryCollectLeaves(without, out var remaining)
            || !Follows(leaves, remaining, source))
        {
            return false;
        }

        var shifted = target > source ? target - 1 : target;
        DashboardPane moved;

        var placed = fills
            ? TryFill(without, remaining[shifted].Path, leaves[source].TypeId, out moved)
            : TrySplit(without, remaining[shifted].Path, side, leaves[source].TypeId, out moved);

        if (!placed)
        {
            return false;
        }

        result = SameStructure(root, moved) ? root : moved;

        return true;
    }

    public static DashboardRemoveResult Remove(DashboardPane root, IReadOnlyList<int> path)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);

        if (!IsWellFormed(root) || Find(root, path) is not TilePane tile || DashboardLayoutTree.IsEmptySlot(tile.TypeId))
        {
            return DashboardRemoveResult.Rejected;
        }

        if (path.Count == 0)
        {
            return DashboardRemoveResult.LastTile;
        }

        if (RemoveAt(root, path) is not { } normalized)
        {
            return DashboardRemoveResult.Rejected;
        }

        return HasTile(normalized) ? DashboardRemoveResult.Removed(normalized) : DashboardRemoveResult.LastTile;
    }

    public static bool TryResize(DashboardPane root, IReadOnlyList<int> path, IReadOnlyList<double> weights, out DashboardPane result)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(weights);

        result = root;

        if (weights.Count < 2 || !IsWellFormed(root))
        {
            return false;
        }

        foreach (var weight in weights)
        {
            if (!IsExplicit(weight))
            {
                return false;
            }
        }

        if (Reweight(root, path, 0, weights) is not { } resized || !IsWellFormed(resized))
        {
            return false;
        }

        result = resized;

        return true;
    }

    public static bool TryClearFixedWeights(DashboardPane root, Func<string, SplitOrientation, bool> isFixed, out DashboardPane result)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(isFixed);

        result = root;

        if (!IsWellFormed(root) || !ClearFixed(root, isFixed, out var relaxed))
        {
            return false;
        }

        result = relaxed;

        return true;
    }

    public static DashboardPane? Normalize(DashboardPane root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return TryCollectLeaves(root, out _) ? NormalizeCore(root) : null;
    }

    public static bool IsWellFormed(DashboardPane root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return TryCollectLeaves(root, out _);
    }

    private static bool SameStructure(DashboardPane first, DashboardPane second)
    {
        switch (first)
        {
            case TilePane left when second is TilePane right:
                return string.Equals(left.TypeId, right.TypeId, StringComparison.Ordinal);

            case SplitPane left when second is SplitPane right:
                if (left.Orientation != right.Orientation || left.Children.Count != right.Children.Count)
                {
                    return false;
                }

                for (var index = 0; index < left.Children.Count; index++)
                {
                    if (!SameStructure(left.Children[index].Pane, right.Children[index].Pane))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    private static bool HasTile(DashboardPane? pane)
    {
        return pane switch
        {
            TilePane tile => !DashboardLayoutTree.IsEmptySlot(tile.TypeId),
            SplitPane split => split.Children.Any(child => HasTile(child.Pane)),
            _ => false,
        };
    }

    private static bool TryCollectLeaves(DashboardPane root, out List<PaneLeaf> leaves)
    {
        leaves = [];

        return Collect(root, [], leaves, new(StringComparer.Ordinal), 0);
    }

    private static bool Collect(DashboardPane? pane, List<int> prefix, List<PaneLeaf> leaves, HashSet<string> seen, int depth)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        switch (pane)
        {
            case TilePane tile when DashboardLayoutTree.IsEmptySlot(tile.TypeId):
                leaves.Add(new(tile.TypeId, [.. prefix]));

                return true;

            case TilePane tile:
                if (string.IsNullOrEmpty(tile.TypeId) || !seen.Add(tile.TypeId))
                {
                    return false;
                }

                leaves.Add(new(tile.TypeId, [.. prefix]));

                return true;

            case SplitPane { Orientation: SplitOrientation.Columns or SplitOrientation.Rows } split:
                if (split.Children is not { Count: > 1 })
                {
                    return false;
                }

                for (var index = 0; index < split.Children.Count; index++)
                {
                    var slot = split.Children[index];

                    prefix.Add(index);

                    var collected = slot is not null
                        && DashboardPaneWeights.IsValid(slot.Weight)
                        && Collect(slot.Pane, prefix, leaves, seen, depth + 1);

                    prefix.RemoveAt(prefix.Count - 1);

                    if (!collected)
                    {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    private static bool Holds(List<PaneLeaf> leaves, string typeId)
    {
        return leaves.Exists(leaf => string.Equals(leaf.TypeId, typeId, StringComparison.Ordinal));
    }

    private static int IndexOf(List<PaneLeaf> leaves, IReadOnlyList<int> path)
    {
        for (var index = 0; index < leaves.Count; index++)
        {
            if (SamePath(leaves[index].Path, path))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool Follows(List<PaneLeaf> before, List<PaneLeaf> after, int removed)
    {
        if (after.Count != before.Count - 1)
        {
            return false;
        }

        for (var index = 0; index < after.Count; index++)
        {
            if (!string.Equals(after[index].TypeId, before[index < removed ? index : index + 1].TypeId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SamePath(IReadOnlyList<int> first, IReadOnlyList<int> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (first[index] != second[index])
            {
                return false;
            }
        }

        return true;
    }

    private static int[] Parent(IReadOnlyList<int> path)
    {
        var parent = new int[path.Count - 1];

        for (var index = 0; index < parent.Length; index++)
        {
            parent[index] = path[index];
        }

        return parent;
    }

    private static DashboardPane? Find(DashboardPane root, IReadOnlyList<int> path)
    {
        DashboardPane? pane = root;

        foreach (var index in path)
        {
            if (pane is not SplitPane split || index < 0 || index >= split.Children.Count)
            {
                return null;
            }

            pane = split.Children[index].Pane;
        }

        return pane;
    }

    private static DashboardPane? Substitute(DashboardPane? pane, IReadOnlyList<int> path, DashboardPane replacement, int depth)
    {
        if (depth == path.Count)
        {
            return replacement;
        }

        if (pane is not SplitPane split)
        {
            return null;
        }

        var index = path[depth];

        if (index < 0 || index >= split.Children.Count || Substitute(split.Children[index].Pane, path, replacement, depth + 1) is not { } rebuilt)
        {
            return null;
        }

        var children = new List<PaneSlot>(split.Children);
        children[index] = split.Children[index] with { Pane = rebuilt };

        return new SplitPane(split.Orientation, children);
    }

    private static DashboardPane? Rebuild(DashboardPane? pane, IReadOnlyList<int> path, int depth, Func<DashboardPane, DashboardPane?> at)
    {
        if (pane is null)
        {
            return null;
        }

        if (depth == path.Count)
        {
            return at(pane);
        }

        if (pane is not SplitPane split)
        {
            return null;
        }

        var index = path[depth];

        if (index < 0 || index >= split.Children.Count || Rebuild(split.Children[index].Pane, path, depth + 1, at) is not { } rebuilt)
        {
            return null;
        }

        var children = new List<PaneSlot>(split.Children);
        children[index] = split.Children[index] with { Pane = rebuilt };

        return Build(split.Orientation, children);
    }

    private static DashboardPane? InsertAt(DashboardPane root, IReadOnlyList<int> path, SplitOrientation orientation, bool before, string typeId)
    {
        if (Find(root, path) is not TilePane leaf)
        {
            return null;
        }

        if (path.Count == 0)
        {
            return new SplitPane(orientation, Pair(new PaneSlot(leaf, 0.5), new(new TilePane(typeId), 0.5), before));
        }

        var index = path[path.Count - 1];

        return Rebuild(root, Parent(path), 0, parent => InsertInto(parent, index, orientation, before, typeId));
    }

    private static DashboardPane? InsertInto(DashboardPane parent, int index, SplitOrientation orientation, bool before, string typeId)
    {
        if (parent is not SplitPane split || index < 0 || index >= split.Children.Count || split.Children[index].Pane is not TilePane leaf)
        {
            return null;
        }

        var slot = split.Children[index];

        if (split.Orientation == orientation)
        {
            var half = slot.Weight is { } weight ? weight / 2 : (double?)null;
            var siblings = new List<PaneSlot>(split.Children);

            siblings.RemoveAt(index);
            siblings.InsertRange(index, Pair(new PaneSlot(leaf, half), new(new TilePane(typeId), half), before));

            return new SplitPane(orientation, siblings);
        }

        var nested = new SplitPane(orientation, Pair(new PaneSlot(leaf, 0.5), new(new TilePane(typeId), 0.5), before));
        var children = new List<PaneSlot>(split.Children);

        children[index] = slot with { Pane = nested };

        return Build(split.Orientation, children);
    }

    private static DashboardPane? RemoveAt(DashboardPane root, IReadOnlyList<int> path)
    {
        if (path.Count == 0)
        {
            return null;
        }

        var index = path[path.Count - 1];

        return Rebuild(root, Parent(path), 0, parent => Drop(parent, index)) is { } replacement ? Normalize(replacement) : null;
    }

    private static DashboardPane? Drop(DashboardPane parent, int index)
    {
        if (parent is not SplitPane split || index < 0 || index >= split.Children.Count)
        {
            return null;
        }

        var remaining = new List<PaneSlot>(split.Children);

        remaining.RemoveAt(index);

        return Build(split.Orientation, remaining);
    }

    private static List<PaneSlot> Pair(PaneSlot existing, PaneSlot inserted, bool before)
    {
        return before ? [inserted, existing] : [existing, inserted];
    }

    private static DashboardPane Build(SplitOrientation orientation, List<PaneSlot> slots)
    {
        var flat = new List<PaneSlot>(slots.Count);

        foreach (var slot in slots)
        {
            if (slot.Pane is SplitPane nested && nested.Orientation == orientation && SpliceTotal(slot.Weight, nested.Children) is { } total)
            {
                foreach (var child in nested.Children)
                {
                    flat.Add(new(child.Pane, slot.Weight!.Value * child.Weight!.Value / total));
                }

                continue;
            }

            flat.Add(slot);
        }

        return flat.Count == 1 ? flat[0].Pane : new SplitPane(orientation, flat);
    }

    private static double? SpliceTotal(double? weight, IReadOnlyList<PaneSlot> children)
    {
        if (!IsExplicit(weight))
        {
            return null;
        }

        var total = 0.0;

        foreach (var child in children)
        {
            if (!IsExplicit(child.Weight))
            {
                return null;
            }

            total += child.Weight!.Value;
        }

        return IsExplicit(total) ? total : null;
    }

    private static bool IsExplicit(double? weight)
    {
        return DashboardPaneWeights.IsExplicit(weight);
    }

    private static double?[]? Distribute(IReadOnlyList<PaneSlot> children, IReadOnlyList<double> weights)
    {
        var weighted = new List<int>(children.Count);
        var total = 0.0;
        var movable = 0.0;

        for (var index = 0; index < children.Count; index++)
        {
            total += weights[index];

            if (!IsExplicit(children[index].Weight))
            {
                continue;
            }

            weighted.Add(index);
            movable += weights[index];
        }

        if (weighted.Count == 0 || weighted.Count == children.Count)
        {
            return [.. Balance(weights).Select(weight => (double?)weight)];
        }

        if (weighted.Count < 2 || !double.IsFinite(total) || total <= 0)
        {
            return null;
        }

        var portion = Math.Clamp(movable / total, MinimumWeight, 1 - MinimumWeight);
        var balanced = Balance([.. weighted.Select(index => weights[index])]);
        var shares = new double?[children.Count];

        for (var position = 0; position < weighted.Count; position++)
        {
            shares[weighted[position]] = balanced[position] * portion;
        }

        return shares;
    }

    private static double[] Balance(IReadOnlyList<double> weights)
    {
        var count = weights.Count;
        var balanced = new double[count];
        var total = 0.0;

        foreach (var weight in weights)
        {
            total += weight;
        }

        if (!double.IsFinite(total) || total <= 0 || MinimumWeight * count >= 1)
        {
            Array.Fill(balanced, 1.0 / count);

            return balanced;
        }

        var deficit = 0.0;
        var free = 0.0;

        for (var index = 0; index < count; index++)
        {
            balanced[index] = weights[index] / total;

            if (balanced[index] < MinimumWeight)
            {
                deficit += MinimumWeight - balanced[index];
            }
            else
            {
                free += balanced[index] - MinimumWeight;
            }
        }

        if (deficit <= 0)
        {
            return balanced;
        }

        var scale = (free - deficit) / free;

        for (var index = 0; index < count; index++)
        {
            balanced[index] = balanced[index] < MinimumWeight
                ? MinimumWeight
                : MinimumWeight + ((balanced[index] - MinimumWeight) * scale);
        }

        return balanced;
    }

    private static DashboardPane? Reweight(DashboardPane pane, IReadOnlyList<int> path, int depth, IReadOnlyList<double> weights)
    {
        if (pane is not SplitPane split)
        {
            return null;
        }

        if (depth == path.Count)
        {
            if (split.Children.Count != weights.Count || Distribute(split.Children, weights) is not { } shares)
            {
                return null;
            }

            var slots = new PaneSlot[weights.Count];

            for (var index = 0; index < weights.Count; index++)
            {
                slots[index] = new(split.Children[index].Pane, shares[index]);
            }

            return new SplitPane(split.Orientation, slots);
        }

        var position = path[depth];

        if (position < 0
            || position >= split.Children.Count
            || Reweight(split.Children[position].Pane, path, depth + 1, weights) is not { } rebuilt)
        {
            return null;
        }

        var children = split.Children.ToArray();

        children[position] = new(rebuilt, split.Children[position].Weight);

        return new SplitPane(split.Orientation, children);
    }

    private static bool ClearFixed(DashboardPane pane, Func<string, SplitOrientation, bool> isFixed, out DashboardPane result)
    {
        result = pane;

        if (pane is not SplitPane split)
        {
            return false;
        }

        var children = new PaneSlot[split.Children.Count];
        var changed = false;

        for (var index = 0; index < split.Children.Count; index++)
        {
            var slot = split.Children[index];
            var nested = ClearFixed(slot.Pane, isFixed, out var relaxed) ? relaxed : slot.Pane;
            var dropped = slot.Weight is not null && IsFixed(slot.Pane, split.Orientation, isFixed);

            changed |= dropped || !ReferenceEquals(nested, slot.Pane);
            children[index] = new(nested, dropped ? null : slot.Weight);
        }

        if (!changed)
        {
            return false;
        }

        result = new SplitPane(split.Orientation, children);

        return true;
    }

    private static bool IsFixed(DashboardPane pane, SplitOrientation orientation, Func<string, SplitOrientation, bool> isFixed)
    {
        return pane switch
        {
            TilePane tile => isFixed(tile.TypeId, orientation),
            SplitPane split => split.Children.Count > 0 && split.Children.All(child => IsFixed(child.Pane, orientation, isFixed)),
            _ => false,
        };
    }

    private static DashboardPane NormalizeCore(DashboardPane pane)
    {
        if (pane is not SplitPane split)
        {
            return pane;
        }

        var count = split.Children.Count;
        var weights = new double?[count];
        var known = 0.0;
        var explicitCount = 0;

        for (var index = 0; index < count; index++)
        {
            if (!IsExplicit(split.Children[index].Weight))
            {
                continue;
            }

            weights[index] = split.Children[index].Weight;
            known += split.Children[index].Weight!.Value;
            explicitCount++;
        }

        var scale = Scale(known, explicitCount, count);
        var children = new List<PaneSlot>(count);

        for (var index = 0; index < count; index++)
        {
            var scaled = weights[index] * scale;

            children.Add(new(NormalizeCore(split.Children[index].Pane), IsExplicit(scaled) ? scaled : null));
        }

        return new SplitPane(split.Orientation, children);
    }

    private static double Scale(double known, int explicitCount, int count)
    {
        if (explicitCount == 0 || known <= 0)
        {
            return 1;
        }

        if (explicitCount == count)
        {
            return 1 / known;
        }

        return known >= 1 ? explicitCount / (double)count / known : 1;
    }

    private readonly record struct PaneLeaf(string TypeId, int[] Path);
}
