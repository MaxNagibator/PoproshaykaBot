using KeepShell.Testing;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using PoproshaykaBot.Wpf.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Tests.Dashboard;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class DashboardKeepAliveTests
{
    private static readonly string[] Dictionaries =
    [
        "pack://application:,,,/KeepShell;component/Resources/Themes/Light.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Themes/Tokens.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Converters.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Styles/Controls.xaml",
        "pack://application:,,,/PoproshaykaBot.Wpf;component/Infrastructure/Converters/AppConverters.xaml",
        "pack://application:,,,/PoproshaykaBot.Wpf;component/Resources/TableStyles.xaml",
        "pack://application:,,,/PoproshaykaBot.Wpf;component/Resources/RatingStyles.xaml",
    ];

    [OneTimeSetUp]
    public void EnsureApplication()
    {
        TestResources.Ensure(Dictionaries);
    }

    [TestCase(880, true, TestName = "Нулевой размер не выводит дашборд из обычного режима")]
    [TestCase(600, false, TestName = "Нулевой размер не выводит дашборд из стопки")]
    public void Zero_sized_pass_keeps_the_mode_the_page_left_with(double width, bool canEdit)
    {
        var store = new FakeLayoutStore(LayoutOf("stream-info", "broadcast-status"));

        using var viewModel = new DashboardViewModel(Tiles(), new(store), TimeProvider.System);

        var host = Arrange(viewModel, width, 653);

        Assert.That(viewModel.CanEdit, Is.EqualTo(canEdit), $"Ширина {width} обязана дать ожидаемый режим – иначе проверять нечего.");

        Resize(host, 0, 0);

        Assert.That(viewModel.CanEdit, Is.EqualTo(canEdit),
            "Скрытая страница меряется нулём, и пересчёт по нему переключает режим панели без единого изменения окна.");
    }

    [Test]
    public void A_layout_change_on_the_hidden_page_waits_for_a_real_size()
    {
        var store = new FakeLayoutStore(LayoutOf("stream-info", "broadcast-status"));

        using var viewModel = new DashboardViewModel(Tiles(), new(store), TimeProvider.System);

        var host = Arrange(viewModel, 600, 653);
        var view = (DashboardView)host.Children[0];

        Assert.That(viewModel.CanEdit, Is.False, "Ширина 600 обязана дать стопку – иначе проверять нечего.");

        Resize(host, 0, 0);
        viewModel.Reload();
        Update(host);

        Assert.That(viewModel.CanEdit, Is.False,
            "Скрытая страница меряется нулём, и перестройка по LayoutChanged вывела бы её из стопки без единого изменения окна.");

        Resize(host, 600, 653);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CanEdit, Is.False, "Показ вернул прежний размер, режим обязан остаться прежним.");
            Assert.That(HostOf(view, "stream-info"), Is.Not.Null, "Отложенная перестройка обязана догнать при первом же настоящем размере.");
        });
    }

    [Test]
    public void Tile_returned_to_the_layout_gets_a_live_host()
    {
        var store = new FakeLayoutStore(LayoutOf("stream-info", "broadcast-status", "twitch-chat"));
        var tiles = Tiles();

        using var viewModel = new DashboardViewModel(tiles, new(store), TimeProvider.System);

        var host = Arrange(viewModel, 880, 653);
        var view = (DashboardView)host.Children[0];
        var first = HostOf(view, "twitch-chat");

        Assert.That(first, Is.Not.Null, "Плитка чата обязана лечь на панель до того, как её убирают.");

        store.SaveDashboard(LayoutOf("stream-info", "broadcast-status"));
        viewModel.Reload();
        Update(host);

        Assert.That(HostOf(view, "twitch-chat"), Is.Null, "Убранная из раскладки плитка не должна оставаться на панели.");

        store.SaveDashboard(LayoutOf("stream-info", "broadcast-status", "twitch-chat"));
        viewModel.Reload();
        Update(host);

        var second = HostOf(view, "twitch-chat");

        Assert.That(second, Is.Not.Null, "Возвращённая в раскладку плитка обязана лечь на панель.");
        Assert.That(second, Is.Not.SameAs(first),
            "Хост ушедшей плитки освобождён по Unloaded вместе с WebView2, и выданный заново он остался бы пустым до перезапуска.");
    }

    [Test]
    public void Collapsing_tiles_moves_no_host_and_folds_a_fully_collapsed_column_into_a_strip()
    {
        var tiles = DashboardPaneLayoutTests.MiddleColumnTiles();
        var store = new FakeLayoutStore(DashboardPaneLayoutTests.MiddleColumnLayout());

        using var viewModel = new DashboardViewModel(tiles, new(store), TimeProvider.System);

        var host = Arrange(viewModel, 1600, 900);
        var view = (DashboardView)host.Children[0];
        var parents = tiles.ToDictionary(tile => tile.TypeId, tile => VisualTreeHelper.GetParent(HostOf(view, tile.TypeId)!), StringComparer.Ordinal);
        var chatWidth = HostOf(view, "twitch-chat")!.ActualWidth;
        var preview = HostOf(view, "stream-preview")!;
        var overlay = HostOf(view, "chat-overlay")!;

        Collapse(tiles, "stream-preview");
        Update(host);

        Assert.That(preview.ActualWidth, Is.EqualTo(overlay.ActualWidth).Within(0.5),
            "Свёрнутая плитка – шапка во всю ширину колонки, а не узкий заголовок у её левого края.");

        Collapse(tiles, "chat-overlay");
        Update(host);

        Assert.Multiple(() =>
        {
            foreach (var tile in tiles)
            {
                Assert.That(VisualTreeHelper.GetParent(HostOf(view, tile.TypeId)!), Is.SameAs(parents[tile.TypeId]),
                    $"Свёртка не меняет форму дерева, и хост «{tile.TypeId}» обязан остаться на месте: перецепление уводит окно WebView2 из дерева и обратно, это и есть мигание.");
            }

            Assert.That(preview.ActualWidth, Is.LessThan(DashboardTileViewModel.ScaledCollapsedStripWidth + 12),
                "Колонка из одних свёрнутых плиток – вертикальная полоса шириной с полосу.");
            Assert.That(preview.ActualHeight + overlay.ActualHeight, Is.EqualTo(view.ActualHeight - 16).Within(1),
                "Полосы делят высоту колонки между собой.");
            Assert.That(HostOf(view, "twitch-chat")!.ActualWidth, Is.GreaterThan(chatWidth + 200),
                "Ширину свёрнутой колонки забирает сосед, а не пустое место.");
        });
    }

    private static void Collapse(IEnumerable<DashboardTileViewModel> tiles, string typeId)
    {
        tiles.Single(tile => string.Equals(tile.TypeId, typeId, StringComparison.Ordinal)).IsCollapsed = true;
    }

    private static Grid Arrange(DashboardViewModel viewModel, double width, double height)
    {
        var host = new Grid();

        host.Children.Add(new DashboardView { DataContext = viewModel });
        Resize(host, width, height);

        return host;
    }

    private static void Resize(Grid host, double width, double height)
    {
        host.Width = width;
        host.Height = height;

        Update(host);
    }

    private static void Update(Grid host)
    {
        host.Measure(new(host.Width, host.Height));
        host.Arrange(new(0, 0, host.Width, host.Height));
        host.UpdateLayout();
    }

    private static ContentControl? HostOf(DashboardView view, string typeId)
    {
        return Descendants(view)
            .OfType<ContentControl>()
            .FirstOrDefault(content => content.Content is DashboardTileViewModel tile
                && string.Equals(tile.TypeId, typeId, StringComparison.Ordinal));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static DashboardTileViewModel[] Tiles()
    {
        return
        [
            new FakeTile("stream-info", fills: true),
            new FakeTile("broadcast-status", fills: true),
            new FakeTile("twitch-chat", fills: true),
        ];
    }

    private static DashboardLayoutSettings LayoutOf(params string[] typeIds)
    {
        var layout = new DashboardLayoutSettings
        {
            ColumnCount = 1,
            RowCount = typeIds.Length,
        };

        foreach (var typeId in typeIds)
        {
            layout.Tiles.Add(new()
            {
                Id = typeId,
                TypeId = typeId,
                Order = layout.Tiles.Count,
                Row = layout.Tiles.Count,
                Column = 0,
                RowSpan = 1,
                ColumnSpan = 1,
                IsVisible = true,
            });
        }

        return layout;
    }
}
