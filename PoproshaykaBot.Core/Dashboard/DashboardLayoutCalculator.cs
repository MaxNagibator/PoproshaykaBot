namespace PoproshaykaBot.Core.Dashboard;

public static class DashboardLayoutCalculator
{
    public static void ClampPlacement(PlacedTile placed, int columnCount, int rowCount)
    {
        placed.Row = Math.Clamp(placed.Row, 0, rowCount - 1);
        placed.Column = Math.Clamp(placed.Column, 0, columnCount - 1);
        placed.ColumnSpan = Math.Clamp(placed.ColumnSpan, 1, columnCount - placed.Column);
        placed.RowSpan = Math.Clamp(placed.RowSpan, 1, rowCount - placed.Row);
    }

    public static (bool[,] Occupied, IReadOnlyList<PlacedTile> Unplaceable) ResolveLayout(
        IEnumerable<PlacedTile> placedTiles,
        int rowCount,
        int columnCount)
    {
        var occupied = new bool[rowCount, columnCount];
        var unplaceable = new List<PlacedTile>();

        var ordered = placedTiles
            .OrderBy(tile => tile.Row)
            .ThenBy(tile => tile.Column)
            .ToList();

        foreach (var placed in ordered)
        {
            ClampPlacement(placed, columnCount, rowCount);

            if (TryReserve(occupied, placed.Row, placed.Column, placed.RowSpan, placed.ColumnSpan, rowCount, columnCount))
            {
                continue;
            }

            if (!TryRelocate(occupied, placed, rowCount, columnCount))
            {
                unplaceable.Add(placed);
            }
        }

        return (occupied, unplaceable);
    }

    private static bool TryRelocate(bool[,] occupied, PlacedTile placed, int rowCount, int columnCount)
    {
        return TryPlaceWithSpan(occupied, placed, placed.RowSpan, placed.ColumnSpan, rowCount, columnCount)
               || TryPlaceWithSpan(occupied, placed, 1, 1, rowCount, columnCount);
    }

    private static bool TryPlaceWithSpan(bool[,] occupied, PlacedTile placed, int rowSpan, int columnSpan, int rowCount, int columnCount)
    {
        for (var row = 0; row <= rowCount - rowSpan; row++)
        {
            for (var column = 0; column <= columnCount - columnSpan; column++)
            {
                if (!TryReserve(occupied, row, column, rowSpan, columnSpan, rowCount, columnCount))
                {
                    continue;
                }

                placed.Row = row;
                placed.Column = column;
                placed.RowSpan = rowSpan;
                placed.ColumnSpan = columnSpan;
                return true;
            }
        }

        return false;
    }

    private static bool TryReserve(bool[,] occupied, int row, int column, int rowSpan, int columnSpan, int rowCount, int columnCount)
    {
        if (row < 0 || column < 0 || row + rowSpan > rowCount || column + columnSpan > columnCount)
        {
            return false;
        }

        for (var r = row; r < row + rowSpan; r++)
        {
            for (var c = column; c < column + columnSpan; c++)
            {
                if (occupied[r, c])
                {
                    return false;
                }
            }
        }

        for (var r = row; r < row + rowSpan; r++)
        {
            for (var c = column; c < column + columnSpan; c++)
            {
                occupied[r, c] = true;
            }
        }

        return true;
    }
}
