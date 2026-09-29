using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Tests.Dashboard;

[TestFixture]
public sealed class DashboardLayoutReconcilerTests
{
    private static readonly string[] Catalog = ["stream-info", "polls-control", "obs-info"];

    [Test]
    public void AppendMissingTypes_AddsCatalogTypeAbsentFromFileAsHidden()
    {
        var layout = LayoutWith(Tile("stream-info", order: 0, isVisible: true));

        var appended = DashboardLayoutReconciler.AppendMissingTypes(layout, Catalog);

        var polls = layout.Tiles.Single(tile => string.Equals(tile.TypeId, "polls-control", StringComparison.Ordinal));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(appended, Is.True);
            Assert.That(layout.Tiles, Has.Count.EqualTo(3));
            Assert.That(polls.IsVisible, Is.False, "Отсутствующая в файле плитка не должна появляться на дашборде сама.");
            Assert.That(polls.Id, Is.EqualTo("polls-control"));
            Assert.That(layout.Tiles.Select(tile => tile.Order), Is.Unique);
        }
    }

    [Test]
    public void AppendMissingTypes_SecondRun_ChangesNothing()
    {
        var layout = LayoutWith(Tile("stream-info", order: 0, isVisible: true));

        DashboardLayoutReconciler.AppendMissingTypes(layout, Catalog);

        var snapshot = layout.Tiles.Select(tile => (tile.TypeId, tile.Order, tile.IsVisible)).ToList();

        var appended = DashboardLayoutReconciler.AppendMissingTypes(layout, Catalog);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(appended, Is.False, "Повторная загрузка не должна дописывать те же плитки второй раз.");
            Assert.That(layout.Tiles.Select(tile => (tile.TypeId, tile.Order, tile.IsVisible)), Is.EqualTo(snapshot));
        }
    }

    [Test]
    public void AppendMissingTypes_KeepsForeignRecordAndItsOrder()
    {
        var layout = LayoutWith(
            Tile("stream-info", order: 4, isVisible: true),
            Tile("logs", order: 7, isVisible: true));

        DashboardLayoutReconciler.AppendMissingTypes(layout, Catalog);

        var logs = layout.Tiles.Single(tile => string.Equals(tile.TypeId, "logs", StringComparison.Ordinal));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(logs.Order, Is.EqualTo(7), "Чужая запись не должна перенумеровываться.");
            Assert.That(logs.IsVisible, Is.True);
            Assert.That(layout.Tiles.Single(tile => string.Equals(tile.TypeId, "stream-info", StringComparison.Ordinal)).Order, Is.EqualTo(4));
            Assert.That(layout.Tiles.Where(tile => !tile.IsVisible).Select(tile => tile.Order), Is.EquivalentTo(new[] { 8, 9 }));
        }
    }

    [Test]
    public void ReservedEmptySlotTypeId_NeverBecomesARecord()
    {
        var layout = LayoutWith(Tile("stream-info", order: 0, isVisible: true));

        DashboardLayoutReconciler.AppendMissingTypes(layout, [DashboardLayoutTree.EmptySlotTypeId]);
        DashboardLayoutReconciler.AppendPreserved(layout, [Tile(DashboardLayoutTree.EmptySlotTypeId, order: 9, isVisible: false)]);

        Assert.That(layout.Tiles.Select(tile => tile.TypeId), Has.No.Member(DashboardLayoutTree.EmptySlotTypeId),
            "Пустая ячейка дерева не плитка: иначе у пользователя появится фантомная скрытая запись.");
    }

    [Test]
    public void AppendPreserved_SameTypeTwiceInPreserved_IsWrittenOnce()
    {
        var layout = LayoutWith(Tile("stream-info", order: 0, isVisible: true));

        DashboardLayoutReconciler.AppendPreserved(layout,
        [
            Tile("logs", order: 0, isVisible: false),
            Tile("logs", order: 1, isVisible: true),
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(layout.Tiles.Count(tile => string.Equals(tile.TypeId, "logs", StringComparison.Ordinal)), Is.EqualTo(1),
                "Задвоенная запись из файла не должна размножаться при каждом сохранении.");
            Assert.That(layout.Tiles.Select(tile => tile.Order), Is.Unique);
        }
    }

    [Test]
    public void ResetToDefaults_KeepsForeignAndHiddenRecordsFromDisk()
    {
        var persisted = LayoutWith(
            Tile("stream-info", order: 0, isVisible: false),
            Tile("logs", order: 1, isVisible: true),
            Tile("chat-blockers", order: 2, isVisible: false));

        persisted.Tiles[1].Row = 2;
        persisted.Tiles[1].Column = 1;

        var layout = DashboardLayoutReconciler.ResetToDefaults(Defaults(), persisted);

        var logs = layout.Tiles.Single(tile => string.Equals(tile.TypeId, "logs", StringComparison.Ordinal));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(logs.Row, Is.EqualTo(2), "Сброс настроек не должен трогать раскладку чужого хоста.");
            Assert.That(logs.Column, Is.EqualTo(1));
            Assert.That(logs.IsVisible, Is.True);
            Assert.That(layout.Tiles.Single(tile => string.Equals(tile.TypeId, "chat-blockers", StringComparison.Ordinal)).IsVisible, Is.False);
            Assert.That(layout.Tiles.Single(tile => string.Equals(tile.TypeId, "stream-info", StringComparison.Ordinal)).IsVisible, Is.True,
                "Известная плитка возвращается к дефолту, а не остаётся выключенной.");
            Assert.That(layout.Tiles.Select(tile => tile.TypeId), Is.Unique);
            Assert.That(layout.Tiles.Select(tile => tile.Order), Is.Unique);
        }
    }

    [Test]
    public void ResetToDefaults_DoesNotShareTilesWithItsArguments()
    {
        var defaults = Defaults();
        var persisted = LayoutWith(Tile("logs", order: 0, isVisible: true));

        var layout = DashboardLayoutReconciler.ResetToDefaults(defaults, persisted);

        foreach (var tile in layout.Tiles)
        {
            tile.Row = 7;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(defaults.Tiles.Select(tile => tile.Row), Has.None.EqualTo(7));
            Assert.That(persisted.Tiles.Select(tile => tile.Row), Has.None.EqualTo(7));
        }
    }

    [Test]
    public void ResetToDefaults_NoLayoutOnDisk_ReturnsDefaults()
    {
        var layout = DashboardLayoutReconciler.ResetToDefaults(Defaults(), null);

        Assert.That(layout.Tiles.Select(tile => tile.TypeId), Is.EqualTo(new[] { "stream-info", "polls-control" }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MergeConcurrentEdits_TakesCollapseFromDisk(bool collapsedOnDisk)
    {
        var draft = LayoutWith(Tile("stream-info", order: 0, isVisible: true));
        draft.Tiles[0].IsCollapsed = !collapsedOnDisk;

        var persisted = LayoutWith(Tile("stream-info", order: 0, isVisible: true));
        persisted.Tiles[0].IsCollapsed = collapsedOnDisk;

        DashboardLayoutReconciler.MergeConcurrentEdits(draft, persisted);

        Assert.That(draft.Tiles[0].IsCollapsed, Is.EqualTo(collapsedOnDisk),
            "Свёртка плитки, сделанная на дашборде, не должна откатываться устаревшим черновиком настроек.");
    }

    [Test]
    public void MergeConcurrentEdits_KeepsTilesMissingOnDiskAndAppendsTheOnesOnlyThereAlone()
    {
        var draft = LayoutWith(Tile("stream-info", order: 0, isVisible: true));
        draft.Tiles[0].IsCollapsed = true;

        DashboardLayoutReconciler.MergeConcurrentEdits(draft, LayoutWith(Tile("logs", order: 0, isVisible: true)));
        DashboardLayoutReconciler.MergeConcurrentEdits(draft, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft.Tiles[0].IsCollapsed, Is.True);
            Assert.That(draft.Tiles.Select(tile => tile.TypeId), Is.EqualTo(new[] { "stream-info", "logs" }),
                "Запись, появившаяся на диске после снятия черновика, обязана пережить сохранение настроек.");
        }
    }

    [Test]
    public void MergeConcurrentEdits_TileTheUserRemoved_ComesBackHiddenAndNotVisible()
    {
        var draft = LayoutWith(Tile("stream-info", order: 0, isVisible: true));

        var persisted = LayoutWith(
            Tile("stream-info", order: 0, isVisible: true),
            Tile("polls-control", order: 1, isVisible: true));

        DashboardLayoutReconciler.MergeConcurrentEdits(draft, persisted);

        var polls = draft.Tiles.Single(tile => string.Equals(tile.TypeId, "polls-control", StringComparison.Ordinal));

        Assert.That(polls.IsVisible, Is.False,
            "Оба редактора удаляют плитку выбрасыванием записи из черновика, поэтому слияние обязано вернуть её скрытой, а не видимой.");
    }

    [Test]
    public void MergeConcurrentEdits_DraftWithoutATree_DoesNotTakeItFromDisk()
    {
        var draft = TwoColumns();
        var persisted = TwoColumns();

        persisted.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.7),
            new(new TilePane("polls-control"), 0.3),
        ]);

        DashboardLayoutReconciler.MergeConcurrentEdits(draft, persisted);

        Assert.That(draft.Root, Is.Null,
            "Сброс настроек обнуляет дерево вместе с сеткой – совпавшая с дефолтом сетка не повод вернуть старые пропорции.");
    }

    [Test]
    public void MergeConcurrentEdits_DraftThatMovedNothing_TakesTheTreeFromDisk()
    {
        var draft = TwoColumns();

        draft.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.5),
            new(new TilePane("polls-control"), 0.5),
        ]);

        var persisted = TwoColumns();

        persisted.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.7),
            new(new TilePane("polls-control"), 0.3),
        ]);

        DashboardLayoutReconciler.MergeConcurrentEdits(draft, persisted);

        Assert.That(draft.Root, Is.SameAs(persisted.Root),
            "Пропорции, поправленные на дашборде, не должны теряться от сохранения настроек, которое не двигало плитки.");
    }

    [Test]
    public void MergeConcurrentEdits_DraftThatOwnsTheTree_KeepsItsOwnWeights()
    {
        var draft = TwoColumns();

        draft.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.55),
            new(new TilePane("polls-control"), 0.45),
        ]);

        var persisted = TwoColumns();

        persisted.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.5),
            new(new TilePane("polls-control"), 0.5),
        ]);

        var owned = draft.Root;

        DashboardLayoutReconciler.MergeConcurrentEdits(draft, persisted, keepDraftRoot: true);

        Assert.That(draft.Root, Is.SameAs(owned),
            "Сдвиг разделителя не меняет клеток проекции, поэтому без признака владения деревом живая правка затиралась бы диском.");
    }

    [Test]
    public void MergeConcurrentEdits_DraftThatMovedTiles_KeepsItsOwnTree()
    {
        var draft = TwoColumns();
        draft.Tiles[0].ColumnSpan = 2;
        draft.Tiles[1].IsVisible = false;
        draft.Root = new TilePane("stream-info");

        var persisted = TwoColumns();
        persisted.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.7),
            new(new TilePane("polls-control"), 0.3),
        ]);

        DashboardLayoutReconciler.MergeConcurrentEdits(draft, persisted);

        Assert.That(draft.Root, Is.TypeOf<TilePane>(),
            "Черновик, переставивший плитки, владеет и деревом – иначе сетка и дерево разойдутся.");
    }

    [Test]
    public void SyncRoot_LayoutWithoutTree_BuildsItFromTheGrid()
    {
        var layout = TwoColumns();

        DashboardLayoutReconciler.SyncRoot(layout);

        var root = layout.Root as SplitPane;

        Assert.That(root, Is.Not.Null, "Раскладка, которая разрезается гильотиной, обязана получить дерево.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root!.Orientation, Is.EqualTo(SplitOrientation.Columns));
            Assert.That(root.Children, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void SyncRoot_TreeThatStillProjectsIntoTheGrid_IsKept()
    {
        var layout = TwoColumns();
        layout.Root = new SplitPane(SplitOrientation.Columns, [
            new(new TilePane("stream-info"), 0.5),
            new(new TilePane("polls-control"), 0.5),
        ]);

        var before = layout.Root;

        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That(layout.Root, Is.SameAs(before), "Совпадающее с сеткой дерево нельзя пересобирать: пересборка стирает пропорции.");
    }

    [Test]
    public void SyncTilesThenSyncRoot_ATreeWithAContentSizedChild_KeepsItsWeightUnset()
    {
        var layout = TwoColumns();

        layout.Root = new SplitPane(SplitOrientation.Columns, [
            new(new TilePane("stream-info"), null),
            new(new TilePane("polls-control"), 0.5),
        ]);

        var before = layout.Root;

        Assert.That(DashboardLayoutReconciler.SyncTiles(layout), Is.True);

        DashboardLayoutReconciler.SyncRoot(layout);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(layout.Root, Is.SameAs(before),
                "Дерево с плиткой по содержимому обязано пережить запись: пересборка по сетке вернула бы ей долю.");
            Assert.That((layout.Root as SplitPane)?.Children[0].Weight, Is.Null);
        }
    }

    [TestCase(0d)]
    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void SyncRoot_UnusableWeightFromTheFile_BecomesAutoAndKeepsTheNeighbour(double unusable)
    {
        var layout = TwoColumns();

        layout.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), unusable),
            new(new TilePane("polls-control"), 0.5),
        ]);

        Assert.That(DashboardPaneEditor.IsWellFormed(layout.Root), Is.False,
            "Дерево с негодной долей считается негодным, иначе правка редактора продолжит его тиражировать.");

        DashboardLayoutReconciler.SyncRoot(layout);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((layout.Root as SplitPane)?.Children.Select(child => child.Weight), Is.EqualTo(new double?[] { null, 0.5 }),
                "Негодная доля лечится в «по содержимому», топология и доля соседа остаются: один плохой вес не стирает авторскую раскладку.");
            Assert.That(DashboardPaneEditor.IsWellFormed(layout.Root!), Is.True,
                "После сшивки дерево обязано стать годным – иначе файл с плохой долей больше не откроется в редакторе.");
        }
    }

    [Test]
    public void SyncRoot_UnusableWeightInANestedSplit_IsHealedThere()
    {
        var layout = new DashboardLayoutSettings
        {
            ColumnCount = 2,
            RowCount = 2,
            Root = new SplitPane(SplitOrientation.Columns,
            [
                new(new TilePane("stream-info"), 0.5),
                new(new SplitPane(SplitOrientation.Rows,
                [
                    new(new TilePane("polls-control"), double.NaN),
                    new(new TilePane("logs"), 0.5),
                ]), 0.5),
            ]),
        };

        layout.Tiles.Add(Tile("stream-info", order: 0, isVisible: true));
        layout.Tiles[0].RowSpan = 2;
        layout.Tiles.Add(Tile("polls-control", order: 1, isVisible: true));
        layout.Tiles[1].Column = 1;
        layout.Tiles.Add(Tile("logs", order: 2, isVisible: true));
        layout.Tiles[2].Column = 1;
        layout.Tiles[2].Row = 1;

        DashboardLayoutReconciler.SyncRoot(layout);

        var root = layout.Root as SplitPane;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root?.Children.Select(child => child.Weight), Is.EqualTo(new double?[] { 0.5, 0.5 }));
            Assert.That((root?.Children[1].Pane as SplitPane)?.Children.Select(child => child.Weight), Is.EqualTo(new double?[] { null, 0.5 }),
                "Лечение доли идёт вглубь дерева, а не по одному его корню.");
        }
    }

    [TestCase(4, 1)]
    [TestCase(1, 4)]
    public void SyncRoot_EmptyWeightsOnAnUnevenSplit_AreKept(int first, int second)
    {
        var layout = Columns(("stream-info", first), ("polls-control", second));

        layout.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), null),
            new(new TilePane("polls-control"), null),
        ]);

        var before = layout.Root;

        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That(layout.Root, Is.SameAs(before),
            "Пустой вес проецируется по размаху листьев в сетке, иначе первая же запись файла подменяет его явным.");
    }

    [Test]
    public void SyncRoot_AnExplicitWeightNextToAnEmptyOne_KeepsBoth()
    {
        var layout = Columns(("stream-info", 4), ("polls-control", 1));

        layout.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.8),
            new(new TilePane("polls-control"), null),
        ]);

        var before = layout.Root;

        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That(layout.Root, Is.SameAs(before), "Явный вес соседа задаёт масштаб, по которому пустой вес доедет до своих клеток.");
    }

    [Test]
    public void SyncRoot_EmptyWeightsNextToAHole_AreKept()
    {
        var layout = new DashboardLayoutSettings
        {
            ColumnCount = 1,
            RowCount = 5,
            Root = new SplitPane(SplitOrientation.Rows,
            [
                new(new TilePane("stream-info"), null),
                new(new TilePane(DashboardLayoutTree.EmptySlotTypeId), null),
            ]),
        };

        layout.Tiles.Add(Tile("stream-info", order: 0, isVisible: true));
        layout.Tiles[0].RowSpan = 4;

        var before = layout.Root;

        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That(layout.Root, Is.SameAs(before),
            "Размах дыры берётся из сетки как у обычного листа: иначе равные доли дали бы 3:2 вместо 4:1 и дерево пересобралось бы.");
    }

    [Test]
    public void SyncTilesThenSyncRoot_VisibleReservedRecordNextToAHole_DoesNotMoveTheRealTile()
    {
        var layout = new DashboardLayoutSettings
        {
            ColumnCount = 1,
            RowCount = 5,
            Root = new SplitPane(SplitOrientation.Rows,
            [
                new(new TilePane("stream-info"), null),
                new(new TilePane(DashboardLayoutTree.EmptySlotTypeId), null),
            ]),
        };

        layout.Tiles.Add(Tile("stream-info", order: 0, isVisible: true));
        layout.Tiles[0].RowSpan = 4;
        layout.Tiles.Add(Tile(DashboardLayoutTree.EmptySlotTypeId, order: 1, isVisible: true));
        layout.Tiles[1].RowSpan = 4;

        var before = layout.Root;

        Assert.That(DashboardLayoutReconciler.SyncTiles(layout), Is.True);

        DashboardLayoutReconciler.SyncRoot(layout);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(layout.Tiles[0].RowSpan, Is.EqualTo(4),
                "Запись с зарезервированным типом не геометрия: иначе дыра забирает прямоугольник соседа и сдвигает настоящую плитку.");
            Assert.That(layout.Tiles[1].IsVisible, Is.False);
            Assert.That(layout.Root, Is.SameAs(before));
        }
    }

    [Test]
    public void SyncRoot_ExplicitWeightsRoundingUnlikeTheirSpans_AreKept()
    {
        var layout = Columns(("stream-info", 4), ("polls-control", 3), ("logs", 3));

        layout.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.36),
            new(new TilePane("polls-control"), 0.38),
            new(new TilePane("logs"), null),
        ]);

        var before = layout.Root;

        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That(layout.Root, Is.SameAs(before),
            "Доли явных весов и их размахи совпадают лишь после округления, поэтому дерево, которое само проецируется в сетку, остаётся как есть.");
    }

    [Test]
    public void SyncRoot_EmptyWeightsInANestedSplit_AreKept()
    {
        var layout = Columns(("stream-info", 4), ("polls-control", 1));

        layout.RowCount = 3;
        layout.Tiles[0].RowSpan = 3;
        layout.Tiles[1].RowSpan = 2;
        layout.Tiles.Add(Tile("logs", order: 2, isVisible: true));
        layout.Tiles[2].Row = 2;
        layout.Tiles[2].Column = 4;

        layout.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), null),
            new(new SplitPane(SplitOrientation.Rows,
            [
                new(new TilePane("polls-control"), null),
                new(new TilePane("logs"), null),
            ]), null),
        ]);

        var before = layout.Root;

        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That(layout.Root, Is.SameAs(before), "Размах узла – объединение его листьев, поэтому вложенный разрез считается по своей оси.");
    }

    [Test]
    public void SyncRoot_EmptyWeightsOnATreeThatNoLongerMatchesTheGrid_IsRebuilt()
    {
        var layout = Columns(("stream-info", 4), ("polls-control", 1));

        layout.Root = new SplitPane(SplitOrientation.Rows,
        [
            new(new TilePane("stream-info"), null),
            new(new TilePane("polls-control"), null),
        ]);

        DashboardLayoutReconciler.SyncRoot(layout);

        var root = layout.Root as SplitPane;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root?.Orientation, Is.EqualTo(SplitOrientation.Columns),
                "Топология из файла разошлась с сеткой – истина сетка, и дерево пересобирается по ней.");
            Assert.That(root?.Children.Select(child => child.Weight), Is.EqualTo(new double?[] { 0.8, 0.2 }));
        }
    }

    [Test]
    public void SyncTiles_EmptyWeights_KeepTheSpansTheGridAlreadyHas()
    {
        var layout = Columns(("stream-info", 4), ("polls-control", 1));

        layout.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), null),
            new(new TilePane("polls-control"), null),
        ]);

        Assert.That(DashboardLayoutReconciler.SyncTiles(layout), Is.True);

        Assert.That(layout.Tiles.Select(tile => tile.ColumnSpan), Is.EqualTo(new[] { 4, 1 }),
            "Проекция на сетку обязана сохранить размах плитки по содержимому, а не делить колонки поровну.");
    }

    [Test]
    public void SyncRoot_TreeThatNoLongerMatchesTheGrid_IsRebuilt()
    {
        var layout = TwoColumns();
        layout.Root = new SplitPane(SplitOrientation.Rows, [
            new(new TilePane("stream-info"), 0.5),
            new(new TilePane("polls-control"), 0.5),
        ]);

        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That((layout.Root as SplitPane)?.Orientation, Is.EqualTo(SplitOrientation.Columns),
            "После правки сетки чужим хостом истина – сетка, и дерево пересобирается по ней.");
    }

    [Test]
    public void SyncRoot_GridThatNoTreeExpresses_LeavesNoTree()
    {
        var layout = TwoColumns();
        layout.Tiles[1].Column = 0;
        layout.Root = new SplitPane(SplitOrientation.Columns, [
            new(new TilePane("stream-info"), 0.5),
            new(new TilePane("polls-control"), 0.5),
        ]);

        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That(layout.Root, Is.Null, "Неразрезаемая раскладка остаётся без дерева, а не упрощается втихую.");
    }

    [Test]
    public void SyncRoot_GridWithTheSameTypeTwice_LeavesNoTree()
    {
        var layout = TwoColumns();
        layout.Tiles[1].TypeId = "stream-info";
        layout.Root = new SplitPane(SplitOrientation.Columns, [
            new(new TilePane("stream-info"), 0.5),
            new(new TilePane("stream-info"), 0.5),
        ]);

        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That(layout.Root, Is.Null, "Задвоенный TypeId разбор отвергает, и сшивка обязана отвергать его так же.");
    }

    [Test]
    public void SyncTiles_TreeDeeperThanTheGrid_GrowsItAndKeepsTheTileFields()
    {
        var layout = TwoColumns();
        layout.Tiles[0].IsCollapsed = true;
        layout.Tiles[0].MaxWidth = 420;
        layout.Root = SideAndStack();

        Assert.That(DashboardLayoutReconciler.SyncTiles(layout), Is.True);

        var stream = layout.Tiles.Single(tile => string.Equals(tile.TypeId, "stream-info", StringComparison.Ordinal));
        var logs = layout.Tiles.Single(tile => string.Equals(tile.TypeId, "logs", StringComparison.Ordinal));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(layout.RowCount, Is.EqualTo(2), "Сетка обязана дорасти до дерева, иначе проекция потеряет лист.");
            Assert.That((stream.Row, stream.Column, stream.RowSpan, stream.ColumnSpan), Is.EqualTo((0, 0, 2, 1)));
            Assert.That((logs.Row, logs.Column, logs.RowSpan, logs.ColumnSpan), Is.EqualTo((1, 1, 1, 1)));
            Assert.That(stream.IsCollapsed, Is.True, "Проекция дерева на сетку не владеет свёрткой и потолками плитки.");
            Assert.That(stream.MaxWidth, Is.EqualTo(420));
        }
    }

    [Test]
    public void SyncTiles_RecordOutsideTheTree_IsHiddenAndNotDropped()
    {
        var layout = TwoColumns();
        layout.Root = new TilePane("stream-info");

        DashboardLayoutReconciler.SyncTiles(layout);

        var polls = layout.Tiles.Single(tile => string.Equals(tile.TypeId, "polls-control", StringComparison.Ordinal));

        Assert.That(polls.IsVisible, Is.False, "Выпавшая из дерева плитка гасится, а не выбрасывается из файла.");
    }

    [Test]
    public void SyncTiles_ThenSyncRoot_LeavesTheSameTree()
    {
        var layout = TwoColumns();
        layout.Root = SideAndStack();

        var before = layout.Root;

        DashboardLayoutReconciler.SyncTiles(layout);
        DashboardLayoutReconciler.SyncRoot(layout);

        Assert.That(layout.Root, Is.SameAs(before), "Круговой ход дерево – сетка – дерево обязан быть неподвижной точкой.");
    }

    [Test]
    public void SyncTiles_TreeWithTheSameLeafTwice_Refuses()
    {
        var layout = TwoColumns();
        layout.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.5),
            new(new TilePane("stream-info"), 0.5),
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardLayoutReconciler.SyncTiles(layout), Is.False,
                "Задвоенный лист свернулся бы в одну запись, и второй лист исчез бы из файла молча.");
            Assert.That(layout.Tiles.Select(tile => tile.Column), Is.EqualTo(new[] { 0, 1 }));
        }
    }

    [Test]
    public void SyncTiles_WithoutATree_DoesNothing()
    {
        var layout = TwoColumns();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DashboardLayoutReconciler.SyncTiles(layout), Is.False);
            Assert.That(layout.Tiles.Select(tile => tile.Column), Is.EqualTo(new[] { 0, 1 }));
        }
    }

    private static SplitPane SideAndStack()
    {
        return new(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.5),
            new(new SplitPane(SplitOrientation.Rows,
            [
                new(new TilePane("polls-control"), 0.5),
                new(new TilePane("logs"), 0.5),
            ]), 0.5),
        ]);
    }

    private static DashboardLayoutSettings TwoColumns()
    {
        var layout = new DashboardLayoutSettings
        {
            ColumnCount = 2,
            RowCount = 1,
            Tiles =
            [
                Tile("stream-info", order: 0, isVisible: true),
                Tile("polls-control", order: 1, isVisible: true),
            ],
        };

        layout.Tiles[1].Column = 1;

        return layout;
    }

    private static DashboardLayoutSettings Defaults()
    {
        return LayoutWith(
            Tile("stream-info", order: 0, isVisible: true),
            Tile("polls-control", order: 1, isVisible: true));
    }

    private static DashboardLayoutSettings Columns(params (string TypeId, int Span)[] bands)
    {
        var layout = new DashboardLayoutSettings
        {
            ColumnCount = bands.Sum(band => band.Span),
            RowCount = 1,
        };

        var column = 0;

        for (var index = 0; index < bands.Length; index++)
        {
            var tile = Tile(bands[index].TypeId, index, isVisible: true);

            tile.Column = column;
            tile.ColumnSpan = bands[index].Span;
            layout.Tiles.Add(tile);

            column += bands[index].Span;
        }

        return layout;
    }

    private static DashboardLayoutSettings LayoutWith(params DashboardTileSettings[] tiles)
    {
        return new()
        {
            ColumnCount = 3,
            RowCount = 3,
            Tiles = [.. tiles],
        };
    }

    private static DashboardTileSettings Tile(string typeId, int order, bool isVisible)
    {
        return new()
        {
            Id = typeId,
            TypeId = typeId,
            Order = order,
            IsVisible = isVisible,
        };
    }
}
