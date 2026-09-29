using KeepShell.Bootstrap;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows;

namespace PoproshaykaBot.Wpf.Infrastructure.Dashboard;

public sealed record DashboardTilePlacement(
    DashboardTileViewModel Tile,
    int Row,
    int Column,
    int ColumnSpan,
    int RowSpan,
    int? MaxWidth,
    int? MaxHeight,
    int? AuthoredMaxWidth,
    int? AuthoredMaxHeight,
    bool IsCollapsed)
{
    public double? ScaledMaxWidth => MaxWidth * FontScaleManager.Current;

    public double? ScaledMaxHeight => MaxHeight * FontScaleManager.Current;

    public double? ScaledAuthoredMaxWidth => AuthoredMaxWidth * FontScaleManager.Current;

    public double? ScaledAuthoredMaxHeight => AuthoredMaxHeight * FontScaleManager.Current;
}

public sealed record DashboardPaneModel(
    IReadOnlyList<DashboardTilePlacement> Placements,
    IReadOnlyDictionary<string, DashboardTilePlacement> ByTypeId,
    PaneLayout? Pane,
    IReadOnlyList<TileBand> Bands);

public static class DashboardPaneBuilder
{
    public static DashboardPaneModel Build(
        DashboardLayoutSettings layout,
        IReadOnlyDictionary<string, DashboardTileViewModel> tiles)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(tiles);

        var columnCount = Math.Clamp(layout.ColumnCount, DashboardLayoutDefaults.MinColumnCount, DashboardLayoutDefaults.MaxColumnCount);
        var rowCount = Math.Clamp(layout.RowCount, DashboardLayoutDefaults.MinRowCount, DashboardLayoutDefaults.MaxRowCount);

        var placements = BuildPlacements(layout, tiles, columnCount, rowCount);

        layout.ColumnCount = columnCount;
        layout.RowCount = rowCount;

        DashboardLayoutReconciler.SyncRoot(layout);

        var byTypeId = placements.ToDictionary(placement => placement.Tile.TypeId, StringComparer.Ordinal);

        var pane = layout.Root is null ? null : BuildTree(layout.Root, byTypeId);

        return new(placements, byTypeId, pane, pane is null ? BuildBands(placements, columnCount, rowCount) : []);
    }

    public static PaneLayout? BuildTree(DashboardPane root, IReadOnlyDictionary<string, DashboardTilePlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(root);

        return BuildPane(root, placements, [], false, starred: true, strip: false);
    }

    public static void ApplyCollapsedStrips(PaneLayout? pane, bool stacked)
    {
        switch (pane)
        {
            case TilePaneLayout leaf:
                leaf.Tile.IsCollapsedToStrip = leaf.Strip && !stacked;
                return;

            case SplitPaneLayout split:
                foreach (var child in split.Children)
                {
                    ApplyCollapsedStrips(child.Pane, stacked);
                }

                return;
        }
    }

    public static int? ResolveMaxSize(int? overrideValue, int? typeDefault)
    {
        if (overrideValue is null)
        {
            return typeDefault;
        }

        return overrideValue > 0 ? overrideValue : null;
    }

    private static List<DashboardTilePlacement> BuildPlacements(
        DashboardLayoutSettings layout,
        IReadOnlyDictionary<string, DashboardTileViewModel> tiles,
        int columnCount,
        int rowCount)
    {
        var placements = new List<DashboardTilePlacement>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tile in layout.Tiles.Where(t => t.IsVisible))
        {
            if (!seen.Add(tile.TypeId) || !tiles.TryGetValue(tile.TypeId, out var vm))
            {
                continue;
            }

            var row = Math.Clamp(tile.Row, 0, rowCount - 1);
            var column = Math.Clamp(tile.Column, 0, columnCount - 1);
            var columnSpan = Math.Clamp(tile.ColumnSpan, 1, columnCount - column);
            var rowSpan = Math.Clamp(tile.RowSpan, 1, rowCount - row);

            placements.Add(new(vm, row, column, columnSpan, rowSpan,
                ResolveMaxSize(tile.MaxWidth, vm.MaxWidth),
                ResolveMaxSize(tile.MaxHeight, vm.MaxHeight),
                ResolveMaxSize(tile.MaxWidth, null),
                ResolveMaxSize(tile.MaxHeight, null),
                tile.IsCollapsed));
        }

        return placements;
    }

    private static bool Stretches(DashboardTilePlacement placement)
    {
        return placement.Tile.FillsAvailableSpace && !placement.IsCollapsed;
    }

    private static bool Grows(DashboardTilePlacement placement)
    {
        return placement.Tile.GrowsWithSpace && !placement.IsCollapsed;
    }

    private static PaneLayout? BuildPane(
        DashboardPane pane,
        IReadOnlyDictionary<string, DashboardTilePlacement> placements,
        int[] path,
        bool alongColumns,
        bool starred,
        bool strip)
    {
        return pane switch
        {
            TilePane tile when DashboardLayoutTree.IsEmptySlot(tile.TypeId) => BuildEmpty(path),
            TilePane tile => placements.TryGetValue(tile.TypeId, out var placement) ? BuildLeaf(placement, path, alongColumns, starred, strip) : null,
            SplitPane { Orientation: SplitOrientation.Columns or SplitOrientation.Rows, Children.Count: > 0 } split => BuildSplit(split, placements, path, strip),
            _ => null,
        };
    }

    private static bool FoldsToStrip(DashboardPane pane, IReadOnlyDictionary<string, DashboardTilePlacement> placements)
    {
        var placed = TileLeaves(pane)
            .Where(tile => DashboardLayoutTree.IsEmptySlot(tile.TypeId) || placements.ContainsKey(tile.TypeId))
            .ToList();

        return placed.Count > 0
            && placed.All(tile => placements.TryGetValue(tile.TypeId, out var placement) && placement.IsCollapsed);
    }

    private static IEnumerable<TilePane> TileLeaves(DashboardPane pane)
    {
        switch (pane)
        {
            case TilePane tile:
                yield return tile;
                yield break;

            case SplitPane split:
                foreach (var leaf in split.Children.SelectMany(child => TileLeaves(child.Pane)))
                {
                    yield return leaf;
                }

                yield break;
        }
    }

    private static PaneLayout BuildEmpty(int[] path)
    {
        var star = new TrackSize(new(1, GridUnitType.Star), double.PositiveInfinity, 0);

        return new EmptyPaneLayout(star, star, path);
    }

    private static PaneLayout BuildLeaf(DashboardTilePlacement placement, int[] path, bool alongColumns, bool starred, bool strip)
    {
        if (placement.IsCollapsed)
        {
            return BuildCollapsed(placement, path, alongColumns, strip);
        }

        var contentWidth = placement.ScaledMaxWidth ?? double.PositiveInfinity;
        var contentHeight = placement.ScaledMaxHeight ?? double.PositiveInfinity;
        var star = new GridLength(1, GridUnitType.Star);
        var widthStar = Stretches(placement);
        var heightStar = Stretches(placement) || Grows(placement);
        var maxWidth = alongColumns && !widthStar && !starred ? contentWidth : placement.ScaledAuthoredMaxWidth ?? double.PositiveInfinity;
        var maxHeight = !alongColumns && !heightStar && !starred ? contentHeight : placement.ScaledAuthoredMaxHeight ?? double.PositiveInfinity;

        return new TilePaneLayout(
            placement.Tile,
            new(widthStar ? star : GridLength.Auto, maxWidth, Math.Min(placement.Tile.ScaledMinWidth, maxWidth)),
            new(heightStar ? star : GridLength.Auto, maxHeight, Math.Min(placement.Tile.ScaledMinHeight, maxHeight)),
            path,
            true,
            contentHeight);
    }

    private static TilePaneLayout BuildCollapsed(DashboardTilePlacement placement, int[] path, bool alongColumns, bool strip)
    {
        var contentWidth = placement.ScaledMaxWidth ?? double.PositiveInfinity;
        var contentHeight = placement.ScaledMaxHeight ?? double.PositiveInfinity;
        var header = Math.Min(DashboardTileViewModel.ScaledCollapsedHeaderHeight, contentHeight);

        if (strip)
        {
            var maxHeight = placement.ScaledAuthoredMaxHeight ?? double.PositiveInfinity;

            return new TilePaneLayout(
                placement.Tile,
                new(GridLength.Auto, contentWidth, Math.Min(DashboardTileViewModel.ScaledCollapsedStripWidth, contentWidth)),
                new(alongColumns ? GridLength.Auto : new(1, GridUnitType.Star), maxHeight, Math.Min(header, maxHeight)),
                path,
                true,
                contentHeight,
                Strip: true);
        }

        var maxWidth = placement.ScaledAuthoredMaxWidth ?? double.PositiveInfinity;

        return new TilePaneLayout(
            placement.Tile,
            new(GridLength.Auto, maxWidth, Math.Min(placement.Tile.ScaledMinWidth, maxWidth)),
            new(GridLength.Auto, contentHeight, header),
            path,
            true,
            contentHeight);
    }

    private static PaneLayout Fill(PaneLayout pane, IReadOnlyDictionary<string, DashboardTilePlacement> placements, bool alongColumns)
    {
        var star = new GridLength(1, GridUnitType.Star);

        var target = pane is TilePaneLayout leaf
            ? BuildLeaf(placements[leaf.Tile.TypeId], leaf.Path, alongColumns, starred: true, strip: false)
            : pane;

        return alongColumns
            ? target with { Width = target.Width with { Length = star } }
            : target with { Height = target.Height with { Length = star } };
    }

    private static PaneLayout? BuildSplit(SplitPane split, IReadOnlyDictionary<string, DashboardTilePlacement> placements, int[] path, bool strip)
    {
        if (DashboardLayoutTree.ResolveWeights(split.Children) is not { } weights)
        {
            return null;
        }

        var children = new List<PaneLayoutSlot>(split.Children.Count);
        var folded = new List<bool>(split.Children.Count);
        var alongColumns = split.Orientation == SplitOrientation.Columns;

        for (var index = 0; index < split.Children.Count; index++)
        {
            var slot = split.Children[index];
            var folds = strip || (alongColumns && FoldsToStrip(slot.Pane, placements));

            if (BuildPane(slot.Pane, placements, [.. path, index], alongColumns, Starred(slot, placements, alongColumns), folds) is { } child)
            {
                var sizesToContent = folds || SizesToContent(child, alongColumns);

                children.Add(new(child, weights[index], HasWeight(slot, placements) && !sizesToContent, sizesToContent));
                folded.Add(folds);
            }
        }

        if (children.Count == 0)
        {
            return null;
        }

        if (children.Count == 1)
        {
            return children[0].Pane;
        }

        if (path.Length > 0)
        {
            GiveRemainder(children, placements, alongColumns);
        }

        var authored = children.Exists(child => child.HasWeight);

        var width = MergeTracks(children, static pane => pane.Width, alongColumns, alongColumns && authored, alongColumns ? null : folded);
        var height = MergeTracks(children, static pane => pane.Height, !alongColumns, !alongColumns && authored, alongColumns ? folded : null);

        return new SplitPaneLayout(split.Orientation, children, width, height, path, children.Count == split.Children.Count);
    }

    private static void GiveRemainder(
        List<PaneLayoutSlot> children,
        IReadOnlyDictionary<string, DashboardTilePlacement> placements,
        bool alongColumns)
    {
        if (children.Exists(child => child.HasWeight || (alongColumns ? child.Pane.Width : child.Pane.Height).Length.IsStar))
        {
            return;
        }

        var last = -1;
        var growing = -1;

        for (var index = 0; index < children.Count; index++)
        {
            if (!TakesRemainder(children[index].Pane, placements, alongColumns))
            {
                continue;
            }

            last = index;

            if (Grows(children[index].Pane, placements))
            {
                growing = index;
            }
        }

        var taker = growing >= 0 ? growing : last;

        if (taker < 0)
        {
            return;
        }

        children[taker] = children[taker] with
        {
            Pane = Fill(children[taker].Pane, placements, alongColumns),
            SizesToContent = false,
        };
    }

    private static bool TakesRemainder(PaneLayout pane, IReadOnlyDictionary<string, DashboardTilePlacement> placements, bool alongColumns)
    {
        return pane switch
        {
            TilePaneLayout leaf => leaf.Fills
                && placements.TryGetValue(leaf.Tile.TypeId, out var placement)
                && !placement.IsCollapsed,
            SplitPaneLayout split => Across(split, alongColumns)
                && split.Children.Any(child => TakesRemainder(child.Pane, placements, alongColumns)),
            _ => false,
        };
    }

    private static bool Across(SplitPaneLayout split, bool alongColumns)
    {
        return (split.Orientation == SplitOrientation.Columns) != alongColumns;
    }

    private static bool Grows(PaneLayout pane, IReadOnlyDictionary<string, DashboardTilePlacement> placements)
    {
        return pane switch
        {
            TilePaneLayout leaf => placements.TryGetValue(leaf.Tile.TypeId, out var placement) && Grows(placement),
            SplitPaneLayout split => split.Children.Any(child => Grows(child.Pane, placements)),
            _ => false,
        };
    }

    private static bool Starred(PaneSlot slot, IReadOnlyDictionary<string, DashboardTilePlacement> placements, bool alongColumns)
    {
        if (!HasWeight(slot, placements))
        {
            return false;
        }

        return slot.Pane is not TilePane tile
            || !placements.TryGetValue(tile.TypeId, out var placement)
            || !SizesToContent(placement, alongColumns);
    }

    private static bool SizesToContent(DashboardTilePlacement placement, bool alongColumns)
    {
        return placement.Tile.SizesToContent
            && !Stretches(placement)
            && (alongColumns || !Grows(placement));
    }

    private static bool SizesToContent(PaneLayout pane, bool alongColumns)
    {
        if ((alongColumns ? pane.Width : pane.Height).Length.IsStar)
        {
            return false;
        }

        return pane switch
        {
            TilePaneLayout leaf => leaf.Tile.SizesToContent,
            SplitPaneLayout split => split.Children.All(child => SizesToContent(child.Pane, alongColumns)),
            _ => false,
        };
    }

    private static bool HasWeight(PaneSlot slot, IReadOnlyDictionary<string, DashboardTilePlacement> placements)
    {
        if (slot.Weight is not { } weight || weight <= 0 || !double.IsFinite(weight))
        {
            return false;
        }

        return slot.Pane is not TilePane tile
            || !placements.TryGetValue(tile.TypeId, out var placement)
            || !placement.IsCollapsed;
    }

    private static TrackSize MergeTracks(
        IReadOnlyList<PaneLayoutSlot> children,
        Func<PaneLayout, TrackSize> axis,
        bool alongSplit,
        bool authored,
        IReadOnlyList<bool>? passive)
    {
        var tracks = children.Select(child => axis(child.Pane)).ToList();
        var ceiling = alongSplit ? tracks.Sum(track => track.Max) : tracks.Max(track => track.Max);
        var floor = alongSplit ? tracks.Sum(track => track.Min) : tracks.Max(track => track.Min);
        var stretched = tracks.Where((_, index) => passive?[index] != true).Any(track => track.Length.IsStar);

        return new(
            authored || stretched ? new(1, GridUnitType.Star) : GridLength.Auto,
            double.IsInfinity(ceiling) ? double.PositiveInfinity : ceiling,
            Math.Min(floor, ceiling));
    }

    private static IReadOnlyList<TileBand> BuildBands(IReadOnlyList<DashboardTilePlacement> placements, int columnCount, int rowCount)
    {
        return DashboardBandBuilder.Build(placements, columnCount, rowCount);
    }
}
