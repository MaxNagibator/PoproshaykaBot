using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Tests.Settings;

[TestFixture]
public sealed class DashboardLayoutStoreTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("dashboard-layout-store");
        _filePath = Path.Combine(_directory.FullName, "dashboard-layout.json");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private DirectoryInfo _directory = null!;
    private string _filePath = null!;

    private const string DivergedFile = """
        {
          "dashboard": {
            "columnCount": 2,
            "rowCount": 1,
            "tiles": [
              { "id": "stream-info", "typeId": "stream-info", "order": 0, "row": 0, "column": 0, "columnSpan": 1, "rowSpan": 1, "isVisible": true },
              { "id": "twitch-chat", "typeId": "twitch-chat", "order": 1, "row": 0, "column": 1, "columnSpan": 1, "rowSpan": 1, "isVisible": true }
            ],
            "root": {
              "kind": "split",
              "orientation": "Rows",
              "children": [
                { "pane": { "kind": "tile", "typeId": "stream-info" }, "weight": 0.5 },
                { "pane": { "kind": "tile", "typeId": "twitch-chat" }, "weight": 0.5 }
              ]
            }
          }
        }
        """;

    private const string UnusableWeightFile = """
        {
          "dashboard": {
            "columnCount": 2,
            "rowCount": 1,
            "tiles": [
              { "id": "stream-info", "typeId": "stream-info", "order": 0, "row": 0, "column": 0, "columnSpan": 1, "rowSpan": 1, "isVisible": true },
              { "id": "twitch-chat", "typeId": "twitch-chat", "order": 1, "row": 0, "column": 1, "columnSpan": 1, "rowSpan": 1, "isVisible": true }
            ],
            "root": {
              "kind": "split",
              "orientation": "Columns",
              "children": [
                { "pane": { "kind": "tile", "typeId": "stream-info" }, "weight": 0.0 },
                { "pane": { "kind": "tile", "typeId": "twitch-chat" }, "weight": 0.5 }
              ]
            }
          }
        }
        """;

    [Test]
    public void SaveDashboard_WhenTheWriteFails_LeavesTheCacheMatchingDisk()
    {
        var blocked = Path.Combine(_directory.FullName, "blocked.json");
        Directory.CreateDirectory(blocked);

        var store = new DashboardLayoutStore(null, blocked);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => store.SaveDashboard(Layout()), Throws.InstanceOf<IOException>());
            Assert.That(store.LoadDashboard(), Is.Null,
                "Кэш стора, уехавший вперёд неудавшейся записи, следующая удачная запись вернула бы на диск поверх последнего хорошего файла.");
        }
    }

    [Test]
    public void SaveDashboard_LayoutWithoutTree_WritesOneRebuiltFromTheGrid()
    {
        new DashboardLayoutStore(null, _filePath).SaveDashboard(Layout());

        var root = new DashboardLayoutStore(null, _filePath).LoadDashboard()?.Root as SplitPane;

        Assert.That(root, Is.Not.Null, "Дерево обязано пережить запись и чтение файла.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root!.Orientation, Is.EqualTo(SplitOrientation.Columns));
            Assert.That(root.Children, Has.Count.EqualTo(2));
            Assert.That(root.Children[0].Weight, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(root.Children[0].Pane, Is.TypeOf<TilePane>());
        }
    }

    [Test]
    public void SaveDashboard_TileMovedByAnotherHost_RebuildsTheTreeOnWrite()
    {
        var store = new DashboardLayoutStore(null, _filePath);
        store.SaveDashboard(Layout());

        var layout = store.LoadDashboard()!;
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.Tiles[1].Column = 0;
        layout.Tiles[1].Row = 1;

        store.SaveDashboard(layout);

        Assert.That((store.LoadDashboard()?.Root as SplitPane)?.Orientation, Is.EqualTo(SplitOrientation.Rows),
            "Разошедшееся с сеткой дерево не должно доживать до файла.");
    }

    [Test]
    public void SaveDashboard_DoesNotTouchTheCallersObject()
    {
        var layout = Layout();

        new DashboardLayoutStore(null, _filePath).SaveDashboard(layout);

        Assert.That(layout.Root, Is.Null, "Стор сшивает свою копию, а не черновик вызывающего.");
    }

    [Test]
    public void SaveMainWindow_AlsoReconcilesTheDashboard()
    {
        File.WriteAllText(_filePath, DivergedFile);

        new DashboardLayoutStore(null, _filePath).SaveMainWindow(new());

        var root = new DashboardLayoutStore(null, _filePath).LoadDashboard()?.Root as SplitPane;

        Assert.That(root?.Orientation, Is.EqualTo(SplitOrientation.Columns),
            "Запись геометрии окна переписывает весь файл, поэтому сшивку нельзя вешать только на SaveDashboard.");
    }

    [TestCase("0", TestName = "LoadDashboard_ZeroWeightInTheFile_ComesOutAuto")]
    [TestCase("-1", TestName = "LoadDashboard_NegativeWeightInTheFile_ComesOutAuto")]
    public void LoadDashboard_UnusableWeightInTheFile_ComesOutAuto(string unusable)
    {
        File.WriteAllText(_filePath, UnusableWeightFile.Replace("\"weight\": 0.0", $"\"weight\": {unusable}", StringComparison.Ordinal));

        var layout = new DashboardLayoutStore(null, _filePath).LoadDashboard();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((layout?.Root as SplitPane)?.Children.Select(child => child.Weight), Is.EqualTo(new double?[] { null, 0.5 }),
                "Негодная доля из файла обязана стать «по содержимому» уже на чтении, вместе с целой долей соседа.");
            Assert.That(DashboardPaneEditor.IsWellFormed(layout!.Root!), Is.True,
                "Редактор раскладки берёт дерево из стора и отказывает на негодной доле: не вылечив её на чтении, мы запрещаем правку дашборда целиком.");
        }
    }

    [TestCase(double.NaN, TestName = "SaveDashboard_NaNWeightInMemory_WritesAutoWeight")]
    [TestCase(double.PositiveInfinity, TestName = "SaveDashboard_InfiniteWeightInMemory_WritesAutoWeight")]
    [TestCase(0d, TestName = "SaveDashboard_ZeroWeightInMemory_WritesAutoWeight")]
    [TestCase(-1d, TestName = "SaveDashboard_NegativeWeightInMemory_WritesAutoWeight")]
    public void SaveDashboard_UnusableWeightInMemory_WritesAutoWeight(double unusable)
    {
        var layout = Layout();

        layout.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), unusable),
            new(new TilePane("twitch-chat"), 0.5),
        ]);

        var store = new DashboardLayoutStore(null, _filePath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => store.SaveDashboard(layout), Throws.Nothing,
                "Негодная доля лечится до клонирования: сериализатор без AllowNamedFloatingPointLiterals роняет сохранение на NaN и Infinity.");

            var saved = new DashboardLayoutStore(null, _filePath).LoadDashboard();

            Assert.That(saved?.Root, Is.Not.Null);
            Assert.That(DashboardPaneEditor.IsWellFormed(saved!.Root!), Is.True,
                "Дерево на диске обязано быть годным для редактора: негодная доля превращается в «по содержимому».");
            Assert.That((saved.Root as SplitPane)?.Children[0].Weight, Is.Null,
                "Доля негодного слота ложится на диск пустой, а не подправленным числом.");
        }
    }

    private static DashboardLayoutSettings Layout()
    {
        return new()
        {
            ColumnCount = 2,
            RowCount = 1,
            Tiles =
            [
                Tile("stream-info", 0),
                Tile("twitch-chat", 1),
            ],
        };
    }

    private static DashboardTileSettings Tile(string typeId, int column)
    {
        return new()
        {
            Id = typeId,
            TypeId = typeId,
            Order = column,
            Column = column,
            IsVisible = true,
        };
    }
}
