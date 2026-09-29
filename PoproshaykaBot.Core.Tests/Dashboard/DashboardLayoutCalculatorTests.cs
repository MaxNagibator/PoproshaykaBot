using PoproshaykaBot.Core.Dashboard;

namespace PoproshaykaBot.Core.Tests.Dashboard;

[TestFixture]
public sealed class DashboardLayoutCalculatorTests
{
    [TestCase(-3, -2, 1, 1, 0, 0, 1, 1)]
    [TestCase(9, 9, 1, 1, 2, 3, 1, 1)]
    [TestCase(0, 2, 9, 9, 0, 2, 3, 2)]
    [TestCase(1, 0, 0, 0, 1, 0, 1, 1)]
    public void ClampPlacement_KeepsTileInsideGrid(
        int row,
        int column,
        int rowSpan,
        int columnSpan,
        int expectedRow,
        int expectedColumn,
        int expectedRowSpan,
        int expectedColumnSpan)
    {
        var placed = new PlacedTile
        {
            TypeId = "tile",
            Row = row,
            Column = column,
            RowSpan = rowSpan,
            ColumnSpan = columnSpan,
        };

        DashboardLayoutCalculator.ClampPlacement(placed, columnCount: 4, rowCount: 3);

        Assert.Multiple(() =>
        {
            Assert.That(placed.Row, Is.EqualTo(expectedRow));
            Assert.That(placed.Column, Is.EqualTo(expectedColumn));
            Assert.That(placed.RowSpan, Is.EqualTo(expectedRowSpan));
            Assert.That(placed.ColumnSpan, Is.EqualTo(expectedColumnSpan));
        });
    }

    [Test]
    public void ResolveLayout_MovesOverlappingTileToFreeCell()
    {
        var first = new PlacedTile { TypeId = "first", Row = 0, Column = 0 };
        var second = new PlacedTile { TypeId = "second", Row = 0, Column = 0 };

        var (_, unplaceable) = DashboardLayoutCalculator.ResolveLayout([first, second], rowCount: 2, columnCount: 2);

        Assert.Multiple(() =>
        {
            Assert.That(unplaceable, Is.Empty);
            Assert.That((first.Row, first.Column), Is.EqualTo((0, 0)));
            Assert.That((second.Row, second.Column), Is.Not.EqualTo((0, 0)));
        });
    }

    [Test]
    public void ResolveLayout_ReportsTileThatDoesNotFit()
    {
        var occupying = new PlacedTile { TypeId = "occupying", Row = 0, Column = 0 };
        var extra = new PlacedTile { TypeId = "extra", Row = 0, Column = 0 };

        var (_, unplaceable) = DashboardLayoutCalculator.ResolveLayout([occupying, extra], rowCount: 1, columnCount: 1);

        Assert.That(unplaceable.Select(tile => tile.TypeId), Is.EqualTo(new[] { "extra" }));
    }
}
