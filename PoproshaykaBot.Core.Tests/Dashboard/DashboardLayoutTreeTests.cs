using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Tests.Dashboard;

[TestFixture]
public sealed class DashboardLayoutTreeTests
{
    public static IEnumerable<TestCaseData> RoundTripCases()
    {
        yield return new TestCaseData(3, 5, DefaultLayout()).SetName("RoundTrip_DefaultLayout");
        yield return new TestCaseData(3, 5, UnevenBandsLayout()).SetName("RoundTrip_UnevenBands");
        yield return new TestCaseData(4, 3, LiveFileLayout()).SetName("RoundTrip_LiveFile");
        yield return new TestCaseData(1, 1, new[] { Tile("only", 0, 0, 1, 1) }).SetName("RoundTrip_SingleTile");
        yield return new TestCaseData(2, 1, new[] { Tile("left", 0, 0, 1, 1), Tile("right", 0, 1, 1, 1) }).SetName("RoundTrip_TwoColumns");
        yield return new TestCaseData(4, 4, NestedLayout()).SetName("RoundTrip_NestedSplits");
    }

    [TestCaseSource(nameof(RoundTripCases))]
    public void Project_OfBuiltTree_ReproducesTheSameGrid(int columnCount, int rowCount, DashboardTileSettings[] tiles)
    {
        var root = DashboardLayoutTree.TryBuild(tiles, columnCount, rowCount);

        Assert.That(root, Is.Not.Null, "Раскладка из сплошной сетки обязана разбираться гильотиной.");

        var projected = DashboardLayoutTree.TryProject(root!, columnCount, rowCount);

        Assert.That(projected, Is.Not.Null);
        Assert.That(projected, Is.EquivalentTo(Rects(tiles)), "Круговой ход обязан вернуть ровно исходные прямоугольники.");
    }

    [Test]
    public void Build_DefaultLayout_CutsColumnsFirstAndKeepsProportions()
    {
        var root = DashboardLayoutTree.TryBuild(DefaultLayout(), 3, 5) as SplitPane;

        Assert.That(root, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root!.Orientation, Is.EqualTo(SplitOrientation.Columns));
            Assert.That(root.Children, Has.Count.EqualTo(3));
            Assert.That(Weights(root.Children), Is.All.EqualTo(1.0 / 3).Within(1e-9));
            Assert.That(root.Children[1].Pane, Is.TypeOf<TilePane>());
            Assert.That(root.Children[2].Pane, Is.TypeOf<TilePane>());
        }

        var left = root!.Children[0].Pane as SplitPane;

        Assert.That(left, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(left!.Orientation, Is.EqualTo(SplitOrientation.Rows));
            Assert.That(left.Children, Has.Count.EqualTo(5));
            Assert.That(Weights(left.Children), Is.All.EqualTo(0.2).Within(1e-9));
        }
    }

    [Test]
    public void Build_UnevenBands_TakesProportionFromSpans()
    {
        var root = DashboardLayoutTree.TryBuild(UnevenBandsLayout(), 3, 5) as SplitPane;

        Assert.That(root, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root!.Orientation, Is.EqualTo(SplitOrientation.Columns));
            Assert.That(root.Children[0].Weight, Is.EqualTo(2.0 / 3).Within(1e-9), "Полоса в две колонки из трёх – это две трети ширины.");
            Assert.That(root.Children[1].Weight, Is.EqualTo(1.0 / 3).Within(1e-9));
        }

        var left = (SplitPane)root!.Children[0].Pane;

        Assert.That(Weights(left.Children), Is.EqualTo(new[] { 0.4, 0.4, 0.2 }).Within(1e-9));
    }

    [Test]
    public void Build_LiveFileLayout_SplitsIntoTwoHalves()
    {
        var root = DashboardLayoutTree.TryBuild(LiveFileLayout(), 4, 3) as SplitPane;

        Assert.That(root, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root!.Orientation, Is.EqualTo(SplitOrientation.Columns));
            Assert.That(Weights(root.Children), Is.EqualTo(new[] { 0.5, 0.5 }).Within(1e-9));
            Assert.That(root.Children[1].Pane, Is.TypeOf<TilePane>());
        }

        var left = (SplitPane)root!.Children[0].Pane;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(left.Orientation, Is.EqualTo(SplitOrientation.Rows));
            Assert.That(Weights(left.Children), Is.All.EqualTo(1.0 / 3).Within(1e-9));
        }
    }

    [Test]
    public void Build_Pinwheel_HasNoGuillotineCut()
    {
        DashboardTileSettings[] pinwheel =
        [
            Tile("top", 0, 0, 1, 2),
            Tile("right", 0, 2, 2, 1),
            Tile("bottom", 2, 1, 1, 2),
            Tile("left", 1, 0, 2, 1),
            Tile("center", 1, 1, 1, 1),
        ];

        Assert.That(DashboardLayoutTree.TryBuild(pinwheel, 3, 3), Is.Null, "Вертушка не разрезается сквозным швом, дерева для неё нет.");
    }

    public static IEnumerable<TestCaseData> HoleCases()
    {
        yield return new TestCaseData(2, 2, new[]
        {
            Tile("a", 0, 0, 1, 1),
            Tile("b", 0, 1, 1, 1),
            Tile("c", 1, 0, 1, 1),
        }).SetName("Hole_InTheCorner");

        yield return new TestCaseData(3, 2, new[]
        {
            Tile("a", 0, 0, 1, 1),
            Tile("b", 0, 2, 1, 1),
            Tile("c", 1, 0, 1, 3),
        }).SetName("Hole_InTheMiddleOfARow");

        yield return new TestCaseData(3, 2, new[]
        {
            Tile("a", 0, 0, 1, 1),
            Tile("b", 0, 2, 1, 1),
            Tile("c", 1, 0, 1, 1),
            Tile("d", 1, 1, 1, 1),
        }).SetName("Hole_TwoOfThem");

        yield return new TestCaseData(4, 4, BlockedSeamsLayout()).SetName("Hole_BlocksEverySeamAroundFiveTiles");

        yield return new TestCaseData(3, 4, new[]
        {
            Tile("a", 0, 0, 3, 1),
            Tile("b", 2, 1, 1, 1),
        }).SetName("Hole_BlocksEverySeamAroundATallTile");
    }

    [TestCaseSource(nameof(HoleCases))]
    public void Project_OfTreeWithHoles_ReproducesTheSameGrid(int columnCount, int rowCount, DashboardTileSettings[] tiles)
    {
        var root = DashboardLayoutTree.TryBuild(tiles, columnCount, rowCount);

        Assert.That(root, Is.Not.Null, "Дыра выражается листом-пустышкой, а не отказом от дерева.");

        var projected = DashboardLayoutTree.TryProject(root!, columnCount, rowCount);

        Assert.That(projected, Is.Not.Null);
        Assert.That(projected, Is.EquivalentTo(Rects(tiles)), "Пустышка не порождает записи в Tiles.");
    }

    [Test]
    public void Build_UserGridWithHole_KeepsTheHoleAsAnEmptyLeaf()
    {
        var root = DashboardLayoutTree.TryBuild(UserHoleLayout(), 2, 6) as SplitPane;

        Assert.That(root, Is.Not.Null);

        var top = (SplitPane)root!.Children[0].Pane;
        var right = (SplitPane)top.Children[1].Pane;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Orientation, Is.EqualTo(SplitOrientation.Rows));
            Assert.That(Leaves(root), Is.EqualTo(new[]
            {
                "stream-info",
                "obs-info",
                "broadcast-profiles",
                DashboardLayoutTree.EmptySlotTypeId,
                "twitch-chat",
            }));

            Assert.That(right.Children[1].Pane, Is.EqualTo(new TilePane(DashboardLayoutTree.EmptySlotTypeId)));
            Assert.That(right.Children[1].Weight, Is.EqualTo(1.0 / 3).Within(1e-9), "Дыра в одну строку из трёх – это треть полосы.");
            Assert.That(DashboardLayoutTree.TryMeasure(root), Is.EqualTo(new GridSize(2, 3)), "Пустышка не требует ни строки, ни колонки.");
        }
    }

    [Test]
    public void Build_AdjacentEmptyCells_MergeIntoOneSlot()
    {
        var root = DashboardLayoutTree.TryBuild(BlockedSeamsLayout(), 4, 4) as SplitPane;

        Assert.That(root, Is.Not.Null);

        var first = (SplitPane)root!.Children[0].Pane;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Orientation, Is.EqualTo(SplitOrientation.Columns));
            Assert.That(root.Children, Has.Count.EqualTo(4));

            Assert.That(root.Children[3].Pane, Is.EqualTo(new TilePane(DashboardLayoutTree.EmptySlotTypeId)),
                "Колонка из одних пустышек – одна пустышка, а не четыре с разделителями.");

            Assert.That(Leaves(first), Is.EqualTo(new[]
            {
                DashboardLayoutTree.EmptySlotTypeId,
                "left",
                DashboardLayoutTree.EmptySlotTypeId,
            }));

            Assert.That(first.Children[0].Weight, Is.EqualTo(0.5).Within(1e-9),
                "Две склеенные пустышки держат сумму своих долей.");
        }
    }

    [Test]
    public void Project_UserGridWithHole_ReturnsFourRecords()
    {
        var root = DashboardLayoutTree.TryBuild(UserHoleLayout(), 2, 6);

        Assert.That(root, Is.Not.Null);
        Assert.That(DashboardLayoutTree.TryProject(root!, 2, 6), Is.EquivalentTo(Rects(UserHoleLayout())));
    }

    [Test]
    public void Build_ReservedTypeIdInTheFile_IsNotTakenForATile()
    {
        var reserved = Tile(DashboardLayoutTree.EmptySlotTypeId, 0, 1, 1, 1);

        var root = DashboardLayoutTree.TryBuild([Tile("a", 0, 0, 1, 1), reserved], 2, 1);

        Assert.That(root, Is.Not.Null);
        Assert.That(Leaves(root!), Is.EqualTo(new[] { "a", DashboardLayoutTree.EmptySlotTypeId }),
            "Запись с зарезервированным TypeId – не плитка: её клетки становятся дырой.");
    }

    [Test]
    public void Build_OverlappingTiles_ReturnsNull()
    {
        Assert.That(DashboardLayoutTree.TryBuild([Tile("a", 0, 0, 1, 2), Tile("b", 0, 1, 1, 1)], 2, 1), Is.Null);
    }

    [Test]
    public void Build_TileOutsideTheGrid_ReturnsNull()
    {
        Assert.That(DashboardLayoutTree.TryBuild([Tile("a", 0, 0, 1, 1), Tile("b", 0, 5, 1, 1)], 2, 1), Is.Null);
    }

    [Test]
    public void Build_SameTypeTwice_ReturnsNull()
    {
        Assert.That(DashboardLayoutTree.TryBuild([Tile("a", 0, 0, 1, 1), Tile("a", 0, 1, 1, 1)], 2, 1), Is.Null);
    }

    [Test]
    public void Build_TileWithoutTypeId_ReturnsNull()
    {
        Assert.That(DashboardLayoutTree.TryBuild([Tile(string.Empty, 0, 0, 1, 1)], 1, 1), Is.Null,
            "Лист без TypeId не проецируется обратно, поэтому и строить его нельзя.");
    }

    [Test]
    public void Build_NullRecordInTheFile_IsSkipped()
    {
        var tiles = new DashboardTileSettings?[] { null, Tile("a", 0, 0, 1, 1) };

        Assert.That(DashboardLayoutTree.TryBuild(tiles!, 1, 1), Is.TypeOf<TilePane>(),
            "Дырявый JSON не должен ронять сохранение раскладки.");
    }

    [TestCase(65, 64, TestName = "Build_GridBeyondTheCellCap_ReturnsNull")]
    [TestCase(1000000, 1000000, TestName = "Build_AbsurdGrid_ReturnsNull")]
    public void Build_GridBeyondTheCellCap_ReturnsNull(int columnCount, int rowCount)
    {
        Assert.That(DashboardLayoutTree.TryBuild([Tile("a", 0, 0, 1, 1)], columnCount, rowCount), Is.Null,
            "Размер сетки из файла нельзя брать на веру: аллокация идёт до проверки содержимого.");
    }

    [Test]
    public void Build_HiddenRecords_AreNotPartOfTheTree()
    {
        var hidden = Tile("hidden", 0, 0, 1, 1);
        hidden.IsVisible = false;

        var root = DashboardLayoutTree.TryBuild([Tile("a", 0, 0, 1, 1), Tile("b", 0, 1, 1, 1), hidden], 2, 1);

        Assert.That(root, Is.Not.Null, "Скрытая запись не занимает клетку и не должна ломать разбор.");
        Assert.That(Leaves(root!), Is.EquivalentTo(new[] { "a", "b" }));
    }

    [Test]
    public void Project_SlotsWithoutWeight_ShareTheGridEvenly()
    {
        var root = new SplitPane(SplitOrientation.Columns, [Slot("a"), Slot("b"), Slot("c")]);

        var projected = DashboardLayoutTree.TryProject(root, 6, 1);

        Assert.That(projected, Is.Not.Null);
        Assert.That(projected!.Select(rect => rect.ColumnSpan), Is.EqualTo(new[] { 2, 2, 2 }));
    }

    [Test]
    public void Project_SlotsWithoutWeight_TakeWhatTheKnownOnesLeave()
    {
        var root = new SplitPane(SplitOrientation.Columns, [Slot("a", 0.5), Slot("b"), Slot("c")]);

        var projected = DashboardLayoutTree.TryProject(root, 8, 1);

        Assert.That(projected, Is.Not.Null);
        Assert.That(projected!.Select(rect => rect.ColumnSpan), Is.EqualTo(new[] { 4, 2, 2 }));
    }

    [TestCase(0.5, 0.5, TestName = "Project_UnknownWeight_MatchesATypicalSibling_SumIsOne")]
    [TestCase(2.0, 2.0, TestName = "Project_UnknownWeight_MatchesATypicalSibling_SumAboveOne")]
    public void Project_UnknownWeight_MatchesATypicalSibling(double first, double second)
    {
        var root = new SplitPane(SplitOrientation.Columns, [Slot("a", first), Slot("b", second), Slot("c")]);

        var projected = DashboardLayoutTree.TryProject(root, 6, 1);

        Assert.That(projected, Is.Not.Null);
        Assert.That(projected!.Select(rect => rect.ColumnSpan), Is.EqualTo(new[] { 2, 2, 2 }),
            "Когда известные веса не оставляют остатка, пустой вес равен среднему из соседей.");
    }

    [Test]
    public void Project_WeightsThatOverflowTheirSum_FallBackToEqualShares()
    {
        var root = new SplitPane(SplitOrientation.Columns, [Slot("a", double.MaxValue), Slot("b", double.MaxValue)]);

        var projected = DashboardLayoutTree.TryProject(root, 8, 1);

        Assert.That(projected, Is.Not.Null);
        Assert.That(projected!.Select(rect => rect.ColumnSpan), Is.EqualTo(new[] { 4, 4 }));
    }

    [Test]
    public void Project_SplitWithASingleChild_ReturnsNull()
    {
        Assert.That(DashboardLayoutTree.TryProject(new SplitPane(SplitOrientation.Columns, [Slot("a", 1.0)]), 2, 1), Is.Null,
            "Вырожденный узел разбор не порождает, и держать его в файле незачем.");
    }

    [Test]
    public void Project_SlotWithoutPane_ReturnsNull()
    {
        var root = new SplitPane(SplitOrientation.Columns, [Slot("a", 0.5), null!]);

        Assert.That(DashboardLayoutTree.TryProject(root, 2, 1), Is.Null);
    }

    [Test]
    public void Project_GridNarrowerThanTheNumberOfLeaves_ReturnsNull()
    {
        var root = new SplitPane(SplitOrientation.Columns, [Slot("a", 0.5), Slot("b", 0.5)]);

        Assert.That(DashboardLayoutTree.TryProject(root, 1, 1), Is.Null, "Двум листам в одной колонке места нет, и молча ужимать их нельзя.");
    }

    [Test]
    public void Project_SplitWithoutOrientation_ReturnsNull()
    {
        var root = new SplitPane(SplitOrientation.None, [Slot("a", 0.5), Slot("b", 0.5)]);

        Assert.That(DashboardLayoutTree.TryProject(root, 2, 1), Is.Null);
    }

    [Test]
    public void Project_LopsidedWeights_NeverStarvesALeaf()
    {
        var root = new SplitPane(SplitOrientation.Rows, [Slot("a", 0.98), Slot("b", 0.01), Slot("c", 0.01)]);

        var projected = DashboardLayoutTree.TryProject(root, 1, 5);

        Assert.That(projected, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(projected!.Select(rect => rect.RowSpan), Is.EqualTo(new[] { 3, 1, 1 }));
            Assert.That(projected.Sum(rect => rect.RowSpan), Is.EqualTo(5), "Проекция обязана покрывать сетку целиком.");
        }
    }

    private static double[] Weights(IReadOnlyList<PaneSlot> children)
    {
        return [.. children.Select(slot => slot.Weight ?? double.NaN)];
    }

    private static PaneSlot Slot(string typeId, double? weight = null)
    {
        return new(new TilePane(typeId), weight);
    }

    private static DashboardTileSettings Tile(string typeId, int row, int column, int rowSpan, int columnSpan)
    {
        return new()
        {
            Id = typeId,
            TypeId = typeId,
            Row = row,
            Column = column,
            RowSpan = rowSpan,
            ColumnSpan = columnSpan,
            IsVisible = true,
        };
    }

    private static DashboardTileSettings[] DefaultLayout()
    {
        return
        [
            Tile("stream-info", 0, 0, 1, 1),
            Tile("broadcast-status", 1, 0, 1, 1),
            Tile("broadcast-profiles", 2, 0, 1, 1),
            Tile("polls-control", 3, 0, 1, 1),
            Tile("obs-info", 4, 0, 1, 1),
            Tile("chat-overlay-preview", 0, 1, 5, 1),
            Tile("twitch-chat", 0, 2, 5, 1),
        ];
    }

    private static DashboardTileSettings[] LiveFileLayout()
    {
        return
        [
            Tile("stream-info", 0, 0, 1, 2),
            Tile("broadcast-status", 1, 0, 1, 2),
            Tile("broadcast-profiles", 2, 0, 1, 2),
            Tile("twitch-chat", 0, 2, 3, 2),
        ];
    }

    private static DashboardTileSettings[] UnevenBandsLayout()
    {
        return
        [
            Tile("stream-info", 0, 0, 2, 2),
            Tile("broadcast-status", 2, 0, 2, 2),
            Tile("broadcast-profiles", 4, 0, 1, 2),
            Tile("twitch-chat", 0, 2, 5, 1),
        ];
    }

    private static DashboardTileSettings[] UserHoleLayout()
    {
        return
        [
            Tile("stream-info", 0, 0, 1, 1),
            Tile("broadcast-profiles", 0, 1, 2, 1),
            Tile("obs-info", 1, 0, 2, 1),
            Tile("twitch-chat", 3, 0, 3, 2),
        ];
    }

    private static DashboardTileSettings[] BlockedSeamsLayout()
    {
        return
        [
            Tile("top", 0, 1, 1, 1),
            Tile("middle", 1, 2, 1, 1),
            Tile("left", 2, 0, 1, 1),
            Tile("center", 2, 1, 1, 1),
            Tile("right", 2, 2, 1, 1),
        ];
    }

    private static DashboardTileSettings[] NestedLayout()
    {
        return
        [
            Tile("a", 0, 0, 2, 2),
            Tile("b", 2, 0, 2, 1),
            Tile("c", 2, 1, 2, 1),
            Tile("d", 0, 2, 1, 2),
            Tile("e", 1, 2, 3, 2),
        ];
    }

    [Test]
    public void Measure_NestedTree_CountsTheTracksEverySubtreeNeeds()
    {
        Assert.That(DashboardLayoutTree.TryMeasure(SideAndStack()), Is.EqualTo(new GridSize(3, 2)));
    }

    [Test]
    public void Project_ChildNarrowerByWeightThanItNeeds_StillGetsItsTracks()
    {
        var projected = DashboardLayoutTree.TryProject(SideAndStack(), 3, 2);

        Assert.That(projected, Is.Not.Null, "Доля, которая меньше нужного числа треков, не должна ронять проекцию целиком.");

        Assert.That(projected, Is.EquivalentTo(new[]
        {
            new TileRect("stream-info", 0, 0, 2, 1),
            new TileRect("polls-control", 0, 1, 1, 2),
            new TileRect("logs", 1, 1, 1, 1),
            new TileRect("obs-info", 1, 2, 1, 1),
        }));
    }

    private static SplitPane SideAndStack()
    {
        return new(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.5),
            new(new SplitPane(SplitOrientation.Rows,
            [
                new(new TilePane("polls-control"), 0.5),
                new(new SplitPane(SplitOrientation.Columns,
                [
                    new(new TilePane("logs"), 0.5),
                    new(new TilePane("obs-info"), 0.5),
                ]), 0.5),
            ]), 0.5),
        ]);
    }

    private static List<TileRect> Rects(IEnumerable<DashboardTileSettings> tiles)
    {
        return
        [
            .. tiles
                .Where(tile => tile.IsVisible)
                .Select(tile => new TileRect(tile.TypeId, tile.Row, tile.Column, tile.RowSpan, tile.ColumnSpan)),
        ];
    }

    private static List<string> Leaves(DashboardPane pane)
    {
        return pane switch
        {
            TilePane tile => [tile.TypeId],
            SplitPane split => [.. split.Children.SelectMany(slot => Leaves(slot.Pane))],
            _ => [],
        };
    }
}
