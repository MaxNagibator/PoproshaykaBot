using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Tests.Dashboard;

[TestFixture]
public sealed class DashboardPaneEditorTests
{
    [TestCase(PaneSide.Left, "new", "stream-info")]
    [TestCase(PaneSide.Right, "stream-info", "new")]
    [TestCase(PaneSide.Top, "new", "stream-info")]
    [TestCase(PaneSide.Bottom, "stream-info", "new")]
    public void Split_SingleLeaf_WrapsItIntoTwoEqualHalves(PaneSide side, string first, string second)
    {
        var split = Split(new TilePane("stream-info"), "stream-info", side, "new") as SplitPane;

        Assert.That(split, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(split!.Orientation, Is.EqualTo(side is PaneSide.Left or PaneSide.Right ? SplitOrientation.Columns : SplitOrientation.Rows));
            Assert.That(Leaves(split), Is.EqualTo(new[] { first, second }));
            Assert.That(Weights(split), Is.EqualTo(new double?[] { 0.5, 0.5 }));
        }
    }

    [Test]
    public void Split_AlongTheParentOrientation_InsertsASiblingInsteadOfNesting()
    {
        var root = Columns(Leaf("stream-info", 0.6), Leaf("polls-control", 0.4));

        var split = (SplitPane)Split(root, "stream-info", PaneSide.Right, "obs-info");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Leaves(split), Is.EqualTo(new[] { "stream-info", "obs-info", "polls-control" }),
                "Разрез вдоль оси узла даёт соседа, а не вложенный узел: иначе разделитель двигает не ту границу.");
            Assert.That(Weights(split), Is.EqualTo(new double?[] { 0.3, 0.3, 0.4 }).Within(1e-9));
        }
    }

    [Test]
    public void Split_AcrossTheParentOrientation_NestsAndKeepsTheSlotWeight()
    {
        var root = Columns(Leaf("stream-info", 0.6), Leaf("polls-control", 0.4));

        var split = (SplitPane)Split(root, "stream-info", PaneSide.Bottom, "obs-info");

        var nested = split.Children[0].Pane as SplitPane;

        Assert.That(nested, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(split.Children[0].Weight, Is.EqualTo(0.6).Within(1e-9), "Ширина колонки от разреза по строкам не меняется.");
            Assert.That(nested!.Orientation, Is.EqualTo(SplitOrientation.Rows));
            Assert.That(Leaves(nested), Is.EqualTo(new[] { "stream-info", "obs-info" }));
        }
    }

    [TestCase("stream-info", TestName = "Split_AutoWeightOfATile")]
    [TestCase(DashboardLayoutTree.EmptySlotTypeId, TestName = "Split_AutoWeightOfAHole")]
    public void Split_LeafWithAutoWeight_KeepsBothHalvesAuto(string target)
    {
        var root = Columns(Leaf(target, null), Leaf("polls-control", 0.4));

        Assert.That(DashboardPaneEditor.TrySplit(root, [0], PaneSide.Left, "obs-info", out var result), Is.True);

        Assert.That(Weights((SplitPane)result), Is.EqualTo(new double?[] { null, null, 0.4 }),
            "«По контенту» – это и свёрнутая плитка тоже, разрез не имеет права подменять её долей.");
    }

    [TestCase(new[] { 0 }, PaneSide.Left, "polls-control", TestName = "Split_TypeIdAlreadyInTheTree")]
    [TestCase(new[] { 5 }, PaneSide.Left, "obs-info", TestName = "Split_PathOutsideTheNode")]
    [TestCase(new int[0], PaneSide.Left, "obs-info", TestName = "Split_PathToANodeInsteadOfALeaf")]
    [TestCase(new[] { 0, 0 }, PaneSide.Left, "obs-info", TestName = "Split_PathThroughALeaf")]
    [TestCase(new[] { 0 }, PaneSide.None, "obs-info", TestName = "Split_WithoutASide")]
    [TestCase(new[] { 0 }, PaneSide.Left, "", TestName = "Split_WithoutATypeId")]
    [TestCase(new[] { 0 }, PaneSide.Left, DashboardLayoutTree.EmptySlotTypeId, TestName = "Split_InsertingTheReservedHole")]
    public void Split_ImpossibleRequest_LeavesTheTreeAlone(int[] targetPath, PaneSide side, string typeId)
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardPaneEditor.TrySplit(root, targetPath, side, typeId, out var result), Is.False);
            Assert.That(result, Is.SameAs(root));
        }
    }

    [Test]
    public void Split_TheHoleItself_DividesTheHoleAndNotItsNeighbour()
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.5));

        Assert.That(DashboardPaneEditor.TrySplit(root, [1], PaneSide.Bottom, "obs-info", out var result), Is.True,
            "Пустая ячейка – такой же лист дерева: разрезать её обязано быть можно.");

        var nested = ((SplitPane)result).Children[1].Pane as SplitPane;

        Assert.That(nested, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nested!.Orientation, Is.EqualTo(SplitOrientation.Rows));
            Assert.That(Leaves(nested), Is.EqualTo(new[] { DashboardLayoutTree.EmptySlotTypeId, "obs-info" }));
        }
    }

    [Test]
    public void Split_OneOfSeveralHoles_TouchesOnlyTheAddressedOne()
    {
        var root = Columns(Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.25), Leaf("stream-info", 0.5), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.25));

        Assert.That(DashboardPaneEditor.TrySplit(root, [2], PaneSide.Right, "obs-info", out var result), Is.True);

        Assert.That(Leaves((SplitPane)result), Is.EqualTo(new[] { DashboardLayoutTree.EmptySlotTypeId, "stream-info", DashboardLayoutTree.EmptySlotTypeId, "obs-info" }),
            "Дыры неразличимы по TypeId, поэтому адресует их только путь – первая обязана остаться нетронутой.");
    }

    [Test]
    public void FindPath_TheReservedHole_IsNotAddressableByTypeId()
    {
        var root = Columns(Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.25), Leaf("stream-info", 0.5), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.25));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardPaneEditor.TryFindPath(root, DashboardLayoutTree.EmptySlotTypeId, out var hole), Is.False,
                "Один TypeId на все дыры: резолв по нему выдал бы произвольную из них.");
            Assert.That(hole, Is.Empty);
            Assert.That(DashboardPaneEditor.TryFindPath(root, "stream-info", out var tile), Is.True);
            Assert.That(tile, Is.EqualTo(new[] { 1 }));
        }
    }

    [Test]
    public void Swap_TwoLeaves_ExchangesThemAndLeavesTheWeightsWithThePositions()
    {
        var root = Columns(Leaf("stream-info", 0.7), Rows(Leaf("polls-control", 0.5), Leaf("obs-info", 0.5), 0.3));

        Assert.That(DashboardPaneEditor.TrySwap(root, [0], [1, 1], out var swapped), Is.True);

        var split = (SplitPane)swapped;
        var nested = (SplitPane)split.Children[1].Pane;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Leaves(split), Is.EqualTo(new[] { "obs-info" }));
            Assert.That(Leaves(nested), Is.EqualTo(new[] { "polls-control", "stream-info" }));
            Assert.That(split.Children[0].Weight, Is.EqualTo(0.7).Within(1e-9), "Обмен местами не меняет пропорции – меняются жильцы, а не комнаты.");
        }
    }

    [Test]
    public void Swap_ATileWithAHole_MovesTheTileAndLeavesAHoleBehind()
    {
        var root = Columns(Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.7), Rows(Leaf("polls-control", 0.5), Leaf("stream-info", 0.5), 0.3));

        Assert.That(DashboardPaneEditor.TrySwap(root, [0], [1, 1], out var swapped), Is.True);

        var split = (SplitPane)swapped;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Leaves(split), Is.EqualTo(new[] { "stream-info" }));
            Assert.That(Leaves((SplitPane)split.Children[1].Pane), Is.EqualTo(new[] { "polls-control", DashboardLayoutTree.EmptySlotTypeId }),
                "Плитка уезжает в дыру, а на её месте остаётся дыра – ячейка не может стать ничем.");
            Assert.That(Weights(split), Is.EqualTo(new double?[] { 0.7, 0.3 }), "Обмен меняет жильцов, а не пропорции.");
        }
    }

    [TestCaseSource(nameof(ImpossibleSwaps))]
    public void Swap_ImpossibleRequest_LeavesTheTreeAlone(DashboardPane root, int[] first, int[] second)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardPaneEditor.TrySwap(root, first, second, out var result), Is.False);
            Assert.That(result, Is.SameAs(root));
        }
    }

    public static IEnumerable<TestCaseData> ImpossibleSwaps()
    {
        var pair = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.5));

        yield return new TestCaseData(pair, new[] { 0 }, new[] { 9 }).SetName("Swap_PathOutsideTheNode");
        yield return new TestCaseData(pair, new[] { 0 }, new[] { 0 }).SetName("Swap_OnePathTwice");
        yield return new TestCaseData(pair, Array.Empty<int>(), new[] { 0 }).SetName("Swap_PathToANodeInsteadOfALeaf");

        yield return new TestCaseData(
                Columns(Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.5), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.5)),
                new[] { 0 },
                new[] { 1 })
            .SetName("Swap_TwoHoles");
    }

    [Test]
    public void Move_ATileOntoAHole_DividesTheHoleAndTakesTheTileOutOfItsOldPlace()
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.25), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.25));

        Assert.That(DashboardPaneEditor.TryMove(root, [1], [2], PaneSide.Right, out var result), Is.True);

        Assert.That(Leaves((SplitPane)result), Is.EqualTo(new[] { "stream-info", DashboardLayoutTree.EmptySlotTypeId, "polls-control" }),
            "Бросок на дыру – это перенос: плитка уходит со старого места и делит дыру.");
    }

    [Test]
    public void Move_ATileIntoTheCentreOfAHole_TakesTheWholeHoleAndLeavesNoHoleBehind()
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.25), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.25));

        Assert.That(DashboardPaneEditor.TryMove(root, [1], [2], PaneSide.None, out var result), Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Leaves((SplitPane)result), Is.EqualTo(new[] { "stream-info", "polls-control" }),
                "Бросок в центр дыры занимает её целиком: ни половины дыры, ни дыры на старом месте плитки.");

            Assert.That(DashboardPaneEditor.IsWellFormed(result), Is.True);
        }
    }

    [Test]
    public void Move_TheLastTileIntoTheCentreOfItsOnlyNeighbourHole_LeavesTheTileAlone()
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.5));

        Assert.That(DashboardPaneEditor.TryMove(root, [0], [1], PaneSide.None, out var result), Is.True);

        Assert.That(result, Is.EqualTo(new TilePane("stream-info")),
            "Удаление источника схлопывает узел в одну дыру, и её путь – корень.");
    }

    [Test]
    public void Move_IntoTheCentreOfATile_SwapsTheTwo()
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.25), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.25));

        Assert.That(DashboardPaneEditor.TryMove(root, [0], [1], PaneSide.None, out var result), Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Leaves((SplitPane)result), Is.EqualTo(new[] { "polls-control", "stream-info", DashboardLayoutTree.EmptySlotTypeId }));
            Assert.That(Weights((SplitPane)result), Is.EqualTo(new double?[] { 0.5, 0.25, 0.25 }));
        }
    }

    [Test]
    public void Fill_AHole_PutsTheTileInItsPlaceAndKeepsTheSlotWeight()
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.25), Leaf(DashboardLayoutTree.EmptySlotTypeId, null));

        Assert.That(DashboardPaneEditor.TryFill(root, [2], "obs-info", out var result), Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Leaves((SplitPane)result), Is.EqualTo(new[] { "stream-info", "polls-control", "obs-info" }));
            Assert.That(Weights((SplitPane)result), Is.EqualTo(new double?[] { 0.5, 0.25, null }),
                "Плитка встаёт в слот дыры, а не делит его: доля слота, заданная или нет, остаётся прежней.");
        }
    }

    [TestCaseSource(nameof(ImpossibleFills))]
    public void Fill_ImpossibleRequest_LeavesTheTreeAlone(int[] target, string typeId)
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.25), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.25));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardPaneEditor.TryFill(root, target, typeId, out var result), Is.False);
            Assert.That(result, Is.SameAs(root));
        }
    }

    public static IEnumerable<TestCaseData> ImpossibleFills()
    {
        yield return new TestCaseData(new[] { 0 }, "obs-info").SetName("Fill_ATileInsteadOfAHole");
        yield return new TestCaseData(new[] { 2 }, "stream-info").SetName("Fill_WithATileAlreadyOnThePanel");
        yield return new TestCaseData(new[] { 2 }, DashboardLayoutTree.EmptySlotTypeId).SetName("Fill_WithTheReservedId");
        yield return new TestCaseData(new[] { 2 }, "").SetName("Fill_WithoutAType");
        yield return new TestCaseData(new[] { 9 }, "obs-info").SetName("Fill_PathOutsideTheNode");
        yield return new TestCaseData(Array.Empty<int>(), "obs-info").SetName("Fill_TargetIsANodeInsteadOfALeaf");
    }

    [Test]
    public void Move_ThroughANodeThatCollapses_FollowsTheTargetToItsNewPath()
    {
        var root = Columns(Leaf("stream-info", 0.5), Rows(Leaf("polls-control", 0.5), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.5), 0.5));

        Assert.That(DashboardPaneEditor.TryMove(root, [0], [1, 1], PaneSide.Left, out var result), Is.True,
            "Удаление источника схлопывает узел, и путь цели обязан быть пересчитан, а не взят прежним.");

        var split = (SplitPane)result;
        var nested = split.Children[1].Pane as SplitPane;

        Assert.That(nested, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(split.Orientation, Is.EqualTo(SplitOrientation.Rows));
            Assert.That(Leaves(split), Is.EqualTo(new[] { "polls-control" }));
            Assert.That(nested!.Orientation, Is.EqualTo(SplitOrientation.Columns));
            Assert.That(Leaves(nested), Is.EqualTo(new[] { "stream-info", DashboardLayoutTree.EmptySlotTypeId }));
        }
    }

    [TestCaseSource(nameof(ImpossibleMoves))]
    public void Move_ImpossibleRequest_LeavesTheTreeAlone(int[] source, int[] target, PaneSide side)
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.25), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.25));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardPaneEditor.TryMove(root, source, target, side, out var result), Is.False);
            Assert.That(result, Is.SameAs(root));
        }
    }

    public static IEnumerable<TestCaseData> ImpossibleMoves()
    {
        yield return new TestCaseData(new[] { 0 }, new[] { 0 }, PaneSide.Left).SetName("Move_OntoItself");
        yield return new TestCaseData(new[] { 0 }, new[] { 9 }, PaneSide.Left).SetName("Move_PathOutsideTheNode");
        yield return new TestCaseData(new[] { 0 }, Array.Empty<int>(), PaneSide.Left).SetName("Move_TargetIsANodeInsteadOfALeaf");
        yield return new TestCaseData(new[] { 2 }, new[] { 0 }, PaneSide.Left).SetName("Move_TheHoleItself");
        yield return new TestCaseData(new[] { 2 }, new[] { 0 }, PaneSide.None).SetName("Move_TheHoleIntoTheCentreOfATile");
        yield return new TestCaseData(new[] { 0 }, new[] { 0 }, PaneSide.None).SetName("Move_IntoItsOwnCentre");
    }

    [TestCaseSource(nameof(MovesOntoTheOwnSpot))]
    public void Move_WhereTheTileAlreadyStands_KeepsTheTreeAndItsWeights(SplitPane root, int[] source, int[] target, PaneSide side)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardPaneEditor.TryMove(root, source, target, side, out var result), Is.True,
                "Жест легален – плитку просто некуда переносить.");

            Assert.That(result, Is.SameAs(root),
                "Удаление с повторным разрезом обнулило бы доли узла, хотя порядок листьев тот же.");
        }
    }

    public static IEnumerable<TestCaseData> MovesOntoTheOwnSpot()
    {
        yield return new TestCaseData(Columns(Leaf("stream-info", 0.2), Leaf("polls-control", 0.8)), new[] { 1 }, new[] { 0 }, PaneSide.Right)
            .SetName("Move_RightOfTheLeftNeighbour");

        yield return new TestCaseData(Columns(Leaf("stream-info", 0.2), Leaf("polls-control", 0.8)), new[] { 0 }, new[] { 1 }, PaneSide.Left)
            .SetName("Move_LeftOfTheRightNeighbour");

        yield return new TestCaseData(Rows(Leaf("stream-info", 0.2), Leaf("polls-control", 0.8)), new[] { 1 }, new[] { 0 }, PaneSide.Bottom)
            .SetName("Move_BelowTheUpperNeighbour");

        yield return new TestCaseData(Rows(Leaf("stream-info", 0.2), Leaf("polls-control", 0.8)), new[] { 0 }, new[] { 1 }, PaneSide.Top)
            .SetName("Move_AboveTheLowerNeighbour");

        yield return new TestCaseData(Columns(Leaf("stream-info", 0.2), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.8)), new[] { 0 }, new[] { 1 }, PaneSide.Left)
            .SetName("Move_LeftOfTheHoleItAlreadyBorders");
    }

    [Test]
    public void Move_ToTheOtherSideOfItsNeighbour_ReordersTheLeavesAndLevelsTheWeights()
    {
        var root = Columns(Leaf("stream-info", 0.2), Leaf("polls-control", 0.8));

        Assert.That(DashboardPaneEditor.TryMove(root, [1], [0], PaneSide.Left, out var result), Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.SameAs(root));
            Assert.That(Leaves((SplitPane)result), Is.EqualTo(new[] { "polls-control", "stream-info" }));
            Assert.That(Weights((SplitPane)result), Is.EqualTo(new double?[] { 0.5, 0.5 }));
        }
    }

    [Test]
    public void Remove_OneOfThreeSiblings_SpreadsItsShareOverTheRest()
    {
        var root = Columns(Leaf("stream-info", 0.2), Leaf("polls-control", 0.3), Leaf("obs-info", 0.5));

        var split = (SplitPane)Remove(root, "obs-info")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Leaves(split), Is.EqualTo(new[] { "stream-info", "polls-control" }));
            Assert.That(Weights(split), Is.EqualTo(new double?[] { 0.4, 0.6 }).Within(1e-9));
        }
    }

    [Test]
    public void Remove_LastNeighbourInANode_CollapsesTheNodeIntoItsParent()
    {
        var root = Columns(Leaf("stream-info", 0.6), Rows(Leaf("polls-control", 0.5), Leaf("obs-info", 0.5), 0.4));

        var split = (SplitPane)Remove(root, "obs-info")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Leaves(split), Is.EqualTo(new[] { "stream-info", "polls-control" }));
            Assert.That(Weights(split), Is.EqualTo(new double?[] { 0.6, 0.4 }).Within(1e-9), "Схлопнутый узел отдаёт свою долю уцелевшему ребёнку.");
        }
    }

    [Test]
    public void Remove_NodeThatCollapsesIntoASplitOfTheSameOrientation_IsFlattened()
    {
        var inner = Columns(Leaf("polls-control", 0.5), Leaf("obs-info", 0.5));
        var root = Columns(Leaf("stream-info", 0.5), Rows(new(inner, 0.5), Leaf("logs", 0.5), 0.5));

        var split = (SplitPane)Remove(root, "logs")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Leaves(split), Is.EqualTo(new[] { "stream-info", "polls-control", "obs-info" }),
                "Вложенный узел той же ориентации – лишний уровень: разделитель на нём двигал бы границу, которой нет.");
            Assert.That(Weights(split), Is.EqualTo(new double?[] { 0.5, 0.25, 0.25 }).Within(1e-9));
        }
    }

    [Test]
    public void Remove_FlattenedNodeWithWeightsNotSummingToOne_KeepsTheProportions()
    {
        var inner = Columns(Leaf("polls-control", 2), Leaf("obs-info", 6));
        var root = Columns(Leaf("stream-info", 0.2), new(inner, 0.6), Leaf("logs", 0.2));

        var split = (SplitPane)Remove(root, "logs")!;

        Assert.That(Weights(split), Is.EqualTo(new double?[] { 0.25, 0.1875, 0.5625 }).Within(1e-9),
            "Доли внутри узла относительны его сумме, поэтому разворачивание обязано сперва привести их к долям, а потом умножать.");
    }

    [Test]
    public void Split_AtTheMaximumDepth_Refuses()
    {
        var root = Nested(32);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardPaneEditor.IsWellFormed(root), Is.True);
            Assert.That(DashboardPaneEditor.TrySplit(root, PathOf(root, "leaf-0"), PaneSide.Left, "obs-info", out var result), Is.False,
                "Разрез, уводящий дерево за потолок глубины, не должен отдаваться как успешный: спроецировать его уже нельзя.");
            Assert.That(result, Is.SameAs(root));
        }
    }

    [Test]
    public void Normalize_WeightsThatOverflowTheirSum_StayFiniteOrBecomeAuto()
    {
        var root = Columns(Leaf("stream-info", double.MaxValue), Leaf("polls-control", double.MaxValue / 2));

        var weights = Weights((SplitPane)DashboardPaneEditor.Normalize(root)!);

        Assert.That(weights, Is.All.Matches<double?>(weight => weight is null || double.IsFinite(weight.Value)),
            "Бесконечность в весе валит сериализацию файла, а ноль – рендер: и то и другое обязано выродиться в «по контенту».");
    }

    [Test]
    public void Normalize_WeightsSoSmallTheirScaleOverflows_StayFiniteOrBecomeAuto()
    {
        var root = Columns(Leaf("stream-info", double.Epsilon), Leaf("polls-control", double.Epsilon));

        var weights = Weights((SplitPane)DashboardPaneEditor.Normalize(root)!);

        Assert.That(weights, Is.All.Matches<double?>(weight => weight is null || double.IsFinite(weight.Value)));
    }

    [TestCaseSource(nameof(RemovalsThatLeaveNoTile))]
    public void Remove_TheLastRealTile_IsToldApartFromARefusal(DashboardPane root, int[] path)
    {
        var removal = DashboardPaneEditor.Remove(root, path);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removal.Status, Is.EqualTo(DashboardRemoveStatus.LastTile),
                "«Плиток не осталось» и «удалить нельзя» – разные ответы: хост объясняет их пользователю по-разному.");
            Assert.That(removal.Root, Is.Null);
        }
    }

    public static IEnumerable<TestCaseData> RemovalsThatLeaveNoTile()
    {
        yield return new TestCaseData(new TilePane("stream-info"), Array.Empty<int>()).SetName("LastTile_TheOnlyLeaf");

        yield return new TestCaseData(Columns(Leaf("stream-info", 0.5), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.5)), new[] { 0 })
            .SetName("LastTile_TheOnlyLeafBesideAHole");
    }

    [TestCaseSource(nameof(ImpossibleRemovals))]
    public void Remove_ImpossibleRequest_LeavesTheTreeAlone(DashboardPane root, int[] path)
    {
        var removal = DashboardPaneEditor.Remove(root, path);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removal.Status, Is.EqualTo(DashboardRemoveStatus.Rejected));
            Assert.That(removal.Root, Is.Null);
        }
    }

    public static IEnumerable<TestCaseData> ImpossibleRemovals()
    {
        var pair = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.5));

        yield return new TestCaseData(pair, new[] { 9 }).SetName("Remove_PathOutsideTheNode");
        yield return new TestCaseData(pair, Array.Empty<int>()).SetName("Remove_PathToANodeInsteadOfALeaf");

        yield return new TestCaseData(Columns(Leaf("stream-info", 0.5), Leaf(DashboardLayoutTree.EmptySlotTypeId, 0.5)), new[] { 1 })
            .SetName("Remove_TheHoleItself");
    }

    [Test]
    public void Normalize_WeightsThatDoNotSumToOne_AreScaledToTheirShares()
    {
        var root = Columns(Leaf("stream-info", 2), Leaf("polls-control", 6));

        Assert.That(Weights((SplitPane)DashboardPaneEditor.Normalize(root)!), Is.EqualTo(new double?[] { 0.25, 0.75 }).Within(1e-9));
    }

    [Test]
    public void Normalize_ExplicitWeightsThatLeaveNothingToAuto_AreCappedForIt()
    {
        var root = Columns(Leaf("stream-info", 0.8), Leaf("polls-control", 0.8), Leaf("obs-info", null));

        Assert.That(Weights((SplitPane)DashboardPaneEditor.Normalize(root)!), Is.EqualTo(new double?[] { 1.0 / 3, 1.0 / 3, null }).Within(1e-9),
            "Свёрнутой плитке обязана остаться доля, иначе она исчезнет с панели.");
    }

    [Test]
    public void Normalize_NodeOfAutoOnly_IsLeftAlone()
    {
        var root = Columns(Leaf("stream-info", null), Leaf("polls-control", null));

        Assert.That(Weights((SplitPane)DashboardPaneEditor.Normalize(root)!), Is.EqualTo(new double?[] { null, null }));
    }

    [TestCaseSource(nameof(MalformedTrees))]
    public void Operations_OnAMalformedTree_Refuse(DashboardPane root)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardPaneEditor.IsWellFormed(root), Is.False);
            Assert.That(DashboardPaneEditor.TryFindPath(root, "stream-info", out _), Is.False);
            Assert.That(DashboardPaneEditor.TrySplit(root, [0], PaneSide.Left, "obs-info", out _), Is.False);
            Assert.That(DashboardPaneEditor.TrySwap(root, [0], [1], out _), Is.False);
            Assert.That(DashboardPaneEditor.TryMove(root, [0], [1], PaneSide.Left, out _), Is.False);
            Assert.That(DashboardPaneEditor.Remove(root, [0]).Status, Is.EqualTo(DashboardRemoveStatus.Rejected));
            Assert.That(DashboardPaneEditor.Normalize(root), Is.Null);
        }
    }

    public static IEnumerable<TestCaseData> MalformedTrees()
    {
        yield return new TestCaseData(Columns(Leaf("stream-info", 0.5), Leaf("stream-info", 0.5)))
            .SetName("Malformed_DuplicateTypeId");

        yield return new TestCaseData(new SplitPane(SplitOrientation.Columns, [Leaf("stream-info", 1)]))
            .SetName("Malformed_SplitWithOneChild");

        yield return new TestCaseData(new SplitPane(SplitOrientation.None, [Leaf("stream-info", 0.5), Leaf("polls-control", 0.5)]))
            .SetName("Malformed_SplitWithoutOrientation");

        yield return new TestCaseData(Columns(Leaf(string.Empty, 0.5), Leaf("polls-control", 0.5)))
            .SetName("Malformed_LeafWithoutTypeId");

        yield return new TestCaseData(Nested(64)).SetName("Malformed_TooDeep");

        (double Weight, string Name)[] unusableWeights =
        [
            (0, "Zero"),
            (-1, "Negative"),
            (double.NaN, "NaN"),
            (double.PositiveInfinity, "Infinite"),
        ];

        foreach (var (weight, name) in unusableWeights)
        {
            yield return new TestCaseData(Columns(Leaf("stream-info", weight), Leaf("polls-control", 0.5)))
                .SetName($"Malformed_{name}Weight");
        }
    }

    private static IReadOnlyList<int> PathOf(DashboardPane root, string typeId)
    {
        Assert.That(DashboardPaneEditor.TryFindPath(root, typeId, out var path), Is.True, $"Плитка {typeId} обязана быть в дереве.");

        return path;
    }

    private static DashboardPane Split(DashboardPane root, string target, PaneSide side, string typeId)
    {
        Assert.That(DashboardPaneEditor.TrySplit(root, PathOf(root, target), side, typeId, out var result), Is.True);

        return result;
    }

    private static DashboardPane? Remove(DashboardPane root, string typeId)
    {
        var removal = DashboardPaneEditor.Remove(root, PathOf(root, typeId));

        Assert.That(removal.Status, Is.EqualTo(DashboardRemoveStatus.Removed));

        return removal.Root;
    }

    [Test]
    public void Resize_TheAddressedNode_NormalizesWeightsToOne()
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.5));

        Assert.That(DashboardPaneEditor.TryResize(root, [], [3, 1], out var result), Is.True);
        Assert.That(Weights((SplitPane)result), Is.EqualTo(new double?[] { 0.75, 0.25 }).Within(1e-9));
    }

    [Test]
    public void Resize_ANestedNode_LeavesTheParentSlotWeightsAlone()
    {
        var root = Columns(Rows(Leaf("stream-info", 0.5), Leaf("broadcast-status", 0.5), 0.6), Leaf("twitch-chat", 0.4));

        Assert.That(DashboardPaneEditor.TryResize(root, [0], [0.8, 0.2], out var result), Is.True);

        var split = (SplitPane)result;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Weights(split), Is.EqualTo(new double?[] { 0.6, 0.4 }).Within(1e-9));
            Assert.That(Weights((SplitPane)split.Children[0].Pane), Is.EqualTo(new double?[] { 0.8, 0.2 }).Within(1e-9));
        }
    }

    [Test]
    public void Resize_AShareUnderTheFloor_LiftsItAndTakesTheDifferenceFromTheNeighbour()
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.5));

        Assert.That(DashboardPaneEditor.TryResize(root, [], [0.99, 0.01], out var result), Is.True);

        var weights = Weights((SplitPane)result);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(weights[1], Is.EqualTo(0.05).Within(1e-9),
                "Затянутый до упора разделитель не должен делать соседа недосягаемым.");
            Assert.That(weights[0]!.Value + weights[1]!.Value, Is.EqualTo(1).Within(1e-9));
        }
    }

    [TestCase(0.2, 0.5, 0.3, 0.5, 0.3)]
    [TestCase(0.2, 0.79, 0.01, 0.76, 0.04)]
    public void Resize_ANodeWithAContentSizedChild_LeavesItsWeightUnsetAndSplitsOnlyTheRest(
        double content,
        double first,
        double second,
        double expectedFirst,
        double expectedSecond)
    {
        var root = Columns(Leaf("stream-info", null), Leaf("twitch-chat", 0.4), Leaf("polls-control", 0.4));

        Assert.That(DashboardPaneEditor.TryResize(root, [], [content, first, second], out var result), Is.True);
        Assert.That(Weights((SplitPane)result), Is.EqualTo(new double?[] { null, expectedFirst, expectedSecond }).Within(1e-9),
            "Плитка по содержимому доли не получает, а соседи делят между собой только остаток – иначе первый же разделитель превращает её в звёздочный трек.");
    }

    [Test]
    public void Resize_ANodeWhereOnlyOneChildCarriesAWeight_IsRefused()
    {
        var root = Columns(Leaf("stream-info", null), Leaf("twitch-chat", 0.6));

        Assert.That(DashboardPaneEditor.TryResize(root, [], [0.3, 0.7], out _), Is.False,
            "Двигать нечего: единственная доля узла и так занимает весь остаток.");
    }

    [Test]
    public void Resize_ANodeWithoutAnyWeights_AuthorsThemAll()
    {
        var root = Columns(Leaf("stream-info", null), Leaf("twitch-chat", null));

        Assert.That(DashboardPaneEditor.TryResize(root, [], [0.3, 0.7], out var result), Is.True);
        Assert.That(Weights((SplitPane)result), Is.EqualTo(new double?[] { 0.3, 0.7 }).Within(1e-9),
            "Раскладка без единой доли пишет их с первого разделителя – пустой вес там означает «не задано», а не «по содержимому».");
    }

    [Test]
    public void Resize_APathThatDoesNotLandOnASplit_IsRefused()
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.5));

        Assert.That(DashboardPaneEditor.TryResize(root, [0], [0.5, 0.5], out _), Is.False);
    }

    [TestCase(new[] { 0.5 })]
    [TestCase(new[] { 0.3, 0.3, 0.4 })]
    [TestCase(new[] { 0.5, 0.0 })]
    [TestCase(new[] { 0.5, -0.5 })]
    [TestCase(new[] { 0.5, double.PositiveInfinity })]
    public void Resize_WeightsThatDoNotFitTheNode_AreRefused(double[] weights)
    {
        var root = Columns(Leaf("stream-info", 0.5), Leaf("polls-control", 0.5));

        Assert.That(DashboardPaneEditor.TryResize(root, [], weights, out _), Is.False);
    }

    [TestCase(SplitOrientation.Columns, true)]
    [TestCase(SplitOrientation.Rows, false)]
    public void ClearFixedWeights_LeafFixedAlongTheSplit_LosesItsWeight(SplitOrientation axis, bool expected)
    {
        var root = Columns(Leaf("stream-info", 0.4), Leaf("twitch-chat", 0.6));

        var cleared = DashboardPaneEditor.TryClearFixedWeights(
            root,
            (typeId, orientation) => orientation == axis && string.Equals(typeId, "stream-info", StringComparison.Ordinal),
            out var result);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cleared, Is.EqualTo(expected), "Фиксированность осевая: вдоль другой оси доля обязана остаться.");
            Assert.That(Weights((SplitPane)result), Is.EqualTo(expected ? new double?[] { null, 0.6 } : [0.4, 0.6]));
        }
    }

    [Test]
    public void ClearFixedWeights_NodeOfFixedLeaves_LosesItsWeightAndKeepsTheCrossAxis()
    {
        var root = Columns(Rows(Leaf("stream-info", 0.5), Leaf("broadcast-status", 0.5), 0.3), Leaf("twitch-chat", 0.7));

        var cleared = TryClearAlongColumns(root, out var result, "stream-info", "broadcast-status");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cleared, Is.True);
            Assert.That(Weights((SplitPane)result), Is.EqualTo(new double?[] { null, 0.7 }));
            Assert.That(Weights((SplitPane)((SplitPane)result).Children[0].Pane), Is.EqualTo(new double?[] { 0.5, 0.5 }),
                "Доли внутри узла идут по его собственной оси и обнулению не подлежат.");
        }
    }

    [Test]
    public void ClearFixedWeights_NodeWithAStretchingLeaf_IsLeftAlone()
    {
        var root = Columns(Rows(Leaf("stream-info", 0.5), Leaf("twitch-chat", 0.5), 0.3), Leaf("polls-control", 0.7));

        var cleared = TryClearAlongColumns(root, out var result, "stream-info");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cleared, Is.False, "Узел фиксирован, только когда фиксированы все его листья.");
            Assert.That(result, Is.SameAs(root));
        }
    }

    private static bool TryClearAlongColumns(DashboardPane root, out DashboardPane result, params string[] fixedTypeIds)
    {
        return DashboardPaneEditor.TryClearFixedWeights(
            root,
            (typeId, orientation) => orientation == SplitOrientation.Columns && fixedTypeIds.Contains(typeId, StringComparer.Ordinal),
            out result);
    }

    private static DashboardPane Nested(int depth)
    {
        DashboardPane pane = new TilePane("leaf-0");

        for (var index = 1; index <= depth; index++)
        {
            var orientation = index % 2 == 1 ? SplitOrientation.Rows : SplitOrientation.Columns;

            pane = new SplitPane(orientation, [new(pane, 0.5), Leaf($"leaf-{index}", 0.5)]);
        }

        return pane;
    }

    private static SplitPane Columns(PaneSlot first, PaneSlot second, params PaneSlot[] rest)
    {
        return new(SplitOrientation.Columns, [first, second, .. rest]);
    }

    private static SplitPane Rows(PaneSlot first, PaneSlot second)
    {
        return new(SplitOrientation.Rows, [first, second]);
    }

    private static PaneSlot Rows(PaneSlot first, PaneSlot second, double? weight)
    {
        return new(Rows(first, second), weight);
    }

    private static PaneSlot Leaf(string typeId, double? weight)
    {
        return new(new TilePane(typeId), weight);
    }

    private static string[] Leaves(SplitPane split)
    {
        return split.Children.Select(slot => slot.Pane).OfType<TilePane>().Select(tile => tile.TypeId).ToArray();
    }

    private static double?[] Weights(SplitPane split)
    {
        return split.Children.Select(slot => slot.Weight).ToArray();
    }
}
