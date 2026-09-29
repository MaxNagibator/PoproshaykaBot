using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows;

namespace PoproshaykaBot.Wpf.Infrastructure.Dashboard;

public static class DashboardBandBuilder
{
    public static IReadOnlyList<TileBand> Build(IReadOnlyList<DashboardTilePlacement> placements, int columnCount, int rowCount)
    {
        ArgumentNullException.ThrowIfNull(placements);

        var bands = new List<TileBand>();

        foreach (var (start, end) in SplitColumns(placements, columnCount))
        {
            var members = placements
                .Where(p => p.Column >= start && p.Column + p.ColumnSpan <= end)
                .ToList();

            if (members.Count == 0)
            {
                continue;
            }

            var columns = new TrackSize[end - start];

            for (var column = start; column < end; column++)
            {
                columns[column - start] = ComputeColumn(members, column);
            }

            var rows = new TrackSize[rowCount];

            for (var row = 0; row < rowCount; row++)
            {
                rows[row] = ComputeRow(members, row);
            }

            var slots = members
                .Select(p => new TileSlot(p.Tile, p.Row, p.Column - start, p.RowSpan, p.ColumnSpan))
                .ToList();

            bands.Add(new(ComputeBandWidth(members, columns), columns, rows, slots));
        }

        return bands;
    }

    private static bool Stretches(DashboardTilePlacement placement)
    {
        return placement.Tile.FillsAvailableSpace && !placement.IsCollapsed;
    }

    private static bool Grows(DashboardTilePlacement placement)
    {
        return placement.Tile.GrowsWithSpace && !placement.IsCollapsed;
    }

    private static IEnumerable<(int Start, int End)> SplitColumns(IReadOnlyList<DashboardTilePlacement> placements, int columnCount)
    {
        var start = 0;

        for (var seam = 1; seam < columnCount; seam++)
        {
            if (placements.Any(p => p.Column < seam && seam < p.Column + p.ColumnSpan))
            {
                continue;
            }

            yield return (start, seam);
            start = seam;
        }

        yield return (start, columnCount);
    }

    private static TrackSize ComputeColumn(IReadOnlyList<DashboardTilePlacement> members, int column)
    {
        var covering = members
            .Where(p => p.Column <= column && column < p.Column + p.ColumnSpan)
            .ToList();

        if (covering.Any(Stretches))
        {
            return new(new(1, GridUnitType.Star), double.PositiveInfinity);
        }

        return new(GridLength.Auto, Ceiling(covering, p => p.ScaledMaxWidth, p => p.ColumnSpan));
    }

    private static TrackSize ComputeRow(IReadOnlyList<DashboardTilePlacement> members, int row)
    {
        var covering = members
            .Where(p => p.Row <= row && row < p.Row + p.RowSpan)
            .ToList();

        if (covering.Any(Stretches))
        {
            return new(new(1, GridUnitType.Star), double.PositiveInfinity, Floor(covering, double.PositiveInfinity));
        }

        var ceiling = Ceiling(covering, p => p.ScaledMaxHeight, p => p.RowSpan);
        var floor = Floor(covering, ceiling);

        return covering.Any(Grows)
            ? new(new(1, GridUnitType.Star), ceiling, floor)
            : new(GridLength.Auto, ceiling, floor);
    }

    private static TrackSize ComputeBandWidth(IReadOnlyList<DashboardTilePlacement> members, IReadOnlyList<TrackSize> columns)
    {
        if (members.Any(Stretches))
        {
            return new(new(1, GridUnitType.Star), double.PositiveInfinity);
        }

        var ceiling = columns.Sum(c => c.Max);

        return new(GridLength.Auto, double.IsInfinity(ceiling) ? double.PositiveInfinity : ceiling);
    }

    private static double Floor(IReadOnlyList<DashboardTilePlacement> covering, double ceiling)
    {
        if (covering.Count == 0)
        {
            return 0;
        }

        var floor = covering.Max(p => (p.IsCollapsed ? DashboardTileViewModel.ScaledCollapsedHeaderHeight : p.Tile.ScaledMinHeight) / p.RowSpan);

        return Math.Min(floor, ceiling);
    }

    private static double Ceiling(
        IReadOnlyList<DashboardTilePlacement> covering,
        Func<DashboardTilePlacement, double?> size,
        Func<DashboardTilePlacement, int> span)
    {
        if (covering.Count == 0 || covering.Any(p => size(p) is null))
        {
            return double.PositiveInfinity;
        }

        return covering.Max(p => size(p)!.Value / (double)span(p));
    }
}
