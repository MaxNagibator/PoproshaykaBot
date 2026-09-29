using KeepShell.Bootstrap;
using KeepShell.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.Views;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PoproshaykaBot.Wpf.Tests.Streams;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class StreamHistoryTrendGeometryTests
{
    private static readonly DateTimeOffset Start = new(new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Local));
    private static readonly Size Area = new(1024, 640);
    private static readonly Size Wide = new(1360, 800);
    private static readonly Size NarrowSideBySide = new(StreamHistoryPageView.SideBySideWidth, 800);
    private static readonly Size Tall = new(1024, 1000);

    private const int HiddenMenuItemIndex = 1;
    private const int TrendBarCount = 40;
    private const int FourDigitPeak = 2543;
    private const int FiveDigitPeak = 12345;
    private const int EdgePeak = 12317;
    private const int LongTrend = 80;
    private const double Tolerance = 0.5;
    private const int LongHistory = 200;
    private const double CardBorder = 1;
    private const double CompactCoverHeight = 32;
    private const double WideCoverHeight = 72;
    private const double SplitterStep = 250;
    private const double Shift = 200;

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

    private string _directory = null!;

    [OneTimeSetUp]
    public void EnsureApplication()
    {
        TestResources.Ensure(Dictionaries);
    }

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "poproshayka-trend-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Скрытая_полоса_отдаёт_таблице_всю_свою_высоту()
    {
        var page = CreatePage();
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view);

        var withStrip = TableTop(view);

        page.ToggleTrendCommand.Execute(null);
        Arrange(view);

        var withoutStrip = TableTop(view);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withStrip - withoutStrip, Is.GreaterThanOrEqualTo(StreamHistoryPageView.TrendStripCompactHeight));
            Assert.That(withoutStrip, Is.LessThanOrEqualTo(CardBorder),
                "Ни столбиков, ни подписей, ни черты, ни полей обёртки – таблица начинается у рамки карточки");
        }
    }

    [Test]
    public void Компактная_карточка_идёт_малым_слотом_без_полосы_долей()
    {
        var page = CreatePage();
        page.IsCardsView = true;

        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view);

        var compact = Card(view);

        Arrange(view, Wide);

        var wide = Card(view);

        Arrange(view);

        var again = Card(view);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.IsCompactLayout, Is.True, "На 1024 DIP страница идёт стопкой – без этого мерить нечего");
            Assert.That(compact.Height, Is.LessThanOrEqualTo(StreamHistoryPageView.CardCompactHeight),
                $"Компактная карточка выросла до {compact.Height:F1} DIP при потолке {StreamHistoryPageView.CardCompactHeight}");
            Assert.That(compact.CoverHeight, Is.EqualTo(CompactCoverHeight), "В компактной карточке обложка идёт малым слотом");
            Assert.That(compact.Stripes, Is.EqualTo(Visibility.Collapsed), "Полоса долей категорий набирает высоту, которой в 72 DIP нет");
            Assert.That(wide.Height, Is.GreaterThanOrEqualTo(StreamHistoryPageView.CardHeight), "Широкая раскладка оставляет карточку прежней");
            Assert.That(wide.CoverHeight, Is.EqualTo(WideCoverHeight));
            Assert.That(wide.Stripes, Is.EqualTo(Visibility.Visible));
            Assert.That(again.Height, Is.EqualTo(compact.Height), "Возврат в узкую раскладку возвращает компактную карточку – признак не залипает");
            Assert.That(again.Stripes, Is.EqualTo(Visibility.Collapsed));
        }
    }

    [TestCase(1.0, 1680)]
    [TestCase(1.6, 2200)]
    public void Инспектор_стоит_на_полу_с_выбором_и_без_а_таблица_забирает_остаток(double scale, double width)
    {
        var area = new Size(width, 900);

        try
        {
            FontScaleManager.Apply(scale);

            var page = CreatePage();
            var view = new StreamHistoryPageView { DataContext = page };

            Arrange(view, area);

            var empty = PageColumns(view);

            page.TrySelectAt(0);
            Arrange(view, area);

            var selected = PageColumns(view);

            page.TrySelectAt(1);
            Arrange(view, area);

            var other = PageColumns(view);

            page.SelectedRow = null;
            Arrange(view, area);

            var cleared = PageColumns(view);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(empty.Detail, Is.EqualTo(StreamHistoryPageView.DetailMinWidth * scale).Within(Tolerance),
                    "Заглушка «Выберите стрим» стоит на своём полу, остальное отдано таблице");
                Assert.That(selected, Is.EqualTo(empty), "Выбор стрима не расширяет инспектор и не сужает таблицу");
                Assert.That(other, Is.EqualTo(selected), "Выбор другой строки раскладку не двигает");
                Assert.That(cleared, Is.EqualTo(empty), "Снятый выбор раскладку не двигает");
            }
        }
        finally
        {
            FontScaleManager.Apply(FontScaleManager.DefaultScale);
        }
    }

    [Test]
    public void Стопка_не_меняет_раскладку_от_выбора()
    {
        var page = CreatePage();
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view);

        var empty = PageRows(view);

        page.TrySelectAt(0);
        Arrange(view);

        var selected = PageRows(view);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(((Grid)view.FindName("PageGrid")!).ColumnDefinitions, Is.Empty);
            Assert.That(selected, Is.EqualTo(empty).AsCollection);
        }
    }

    [TestCase(1.0, 1660, 900, false)]
    [TestCase(1.0, 1660, 900, true)]
    [TestCase(1.6, 2200, 900, false)]
    [TestCase(1.6, 2200, 900, true)]
    [TestCase(1.0, 1024, 640, false)]
    [TestCase(1.6, 1024, 640, true)]
    public void Разделитель_упирается_в_пол_соседа_и_не_растит_страницу(double scale, double width, double height, bool keyboard)
    {
        var area = new Size(width, height);

        try
        {
            FontScaleManager.Apply(scale);

            var page = CreatePage();
            page.TrySelectAt(0);

            var view = new StreamHistoryPageView { DataContext = page };

            Arrange(view, area);

            var before = SplitOf(view);

            MoveSplitter(view, area, -Math.Max(width, height), keyboard);

            var detailWidest = SplitOf(view);

            MoveSplitter(view, area, Math.Max(width, height), keyboard);

            var tableWidest = SplitOf(view);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(detailWidest.Total, Is.EqualTo(before.Total).Within(Tolerance),
                    $"Разделитель только делит место: инспектор ушёл за край на {detailWidest.Total - before.Total:F1} DIP");
                Assert.That(detailWidest.Table, Is.EqualTo(detailWidest.TableFloor).Within(Tolerance), "Ход к таблице кончается на её полу");
                Assert.That(tableWidest.Total, Is.EqualTo(before.Total).Within(Tolerance));
                Assert.That(tableWidest.Detail, Is.EqualTo(tableWidest.DetailFloor).Within(Tolerance), "Обратный ход кончается на полу инспектора");
            }
        }
        finally
        {
            FontScaleManager.Apply(FontScaleManager.DefaultScale);
        }
    }

    [Test]
    public void Сужение_страницы_после_сдвига_поджимает_инспектор_а_расширение_возвращает()
    {
        var wide = new Size(1660, 900);
        var narrow = new Size(StreamHistoryPageView.SideBySideWidth, 900);

        var page = CreatePage();
        page.TrySelectAt(0);

        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view, wide);
        MoveSplitter(view, wide, -2 * Shift, keyboard: false);

        var dragged = SplitOf(view);

        Arrange(view, narrow);

        var squeezed = SplitOf(view);

        Arrange(view, wide);

        var restored = SplitOf(view);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dragged.Detail, Is.EqualTo(StreamHistoryPageView.DetailMinWidth + 2 * Shift).Within(Tolerance));
            Assert.That(squeezed.Total, Is.EqualTo(narrow.Width).Within(Tolerance), "Узкая страница не выносит инспектор за край");
            Assert.That(squeezed.Table, Is.EqualTo(StreamHistoryPageView.TableMinWidth).Within(Tolerance));
            Assert.That(restored.Detail, Is.EqualTo(dragged.Detail).Within(Tolerance), "Расширение возвращает ширину, заданную разделителем");
        }
    }

    [TestCase(1.0, 1660)]
    [TestCase(1.6, 2200)]
    public void Ширина_инспектора_от_разделителя_переживает_смену_вида_и_стопку(double scale, double width)
    {
        var area = new Size(width, 900);

        try
        {
            FontScaleManager.Apply(scale);

            var page = CreatePage();
            page.TrySelectAt(0);

            var view = new StreamHistoryPageView { DataContext = page };

            Arrange(view, area);
            MoveSplitter(view, area, -Shift, keyboard: false);

            var dragged = SplitOf(view).Detail;

            page.IsCardsView = true;
            Arrange(view, area);

            var cards = SplitOf(view).Detail;

            page.IsCardsView = false;
            Arrange(view, area);

            var table = SplitOf(view).Detail;

            Arrange(view);
            Arrange(view, area);

            var unstacked = SplitOf(view).Detail;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(dragged, Is.EqualTo(StreamHistoryPageView.DetailMinWidth * scale + Shift).Within(Tolerance));
                Assert.That(cards, Is.EqualTo(dragged).Within(Tolerance), "Вид карточками сохраняет ширину инспектора");
                Assert.That(table, Is.EqualTo(dragged).Within(Tolerance), "Возврат к таблице сохраняет ширину инспектора");
                Assert.That(unstacked, Is.EqualTo(dragged).Within(Tolerance), "Стопка и обратно сохраняет ширину инспектора");
            }
        }
        finally
        {
            FontScaleManager.Apply(FontScaleManager.DefaultScale);
        }
    }

    [Test]
    public void Ширина_инспектора_от_разделителя_едет_с_масштабом_шрифта()
    {
        var area = new Size(2200, 900);
        var page = CreatePage();
        page.TrySelectAt(0);

        var view = new StreamHistoryPageView { DataContext = page };

        try
        {
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Arrange(view, area);
            MoveSplitter(view, area, -Shift, keyboard: false);

            var dragged = SplitOf(view).Detail;

            FontScaleManager.Apply(1.6);
            Arrange(view, area);

            var scaled = SplitOf(view).Detail;

            FontScaleManager.Apply(FontScaleManager.DefaultScale);
            Arrange(view, area);

            var back = SplitOf(view).Detail;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(scaled, Is.EqualTo(dragged * 1.6).Within(Tolerance), "Ширина от разделителя масштабируется, как пол инспектора");
                Assert.That(back, Is.EqualTo(dragged).Within(Tolerance));
            }
        }
        finally
        {
            FontScaleManager.Apply(FontScaleManager.DefaultScale);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        }
    }

    [Test]
    public void Доля_карточки_стрима_от_разделителя_в_стопке_переживает_смену_вида()
    {
        var page = CreatePage();
        page.TrySelectAt(0);

        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view, Tall);

        var initial = SplitOf(view);

        MoveSplitter(view, Tall, -Shift / 2, keyboard: false);

        var dragged = SplitOf(view);

        page.IsCardsView = true;
        Arrange(view, Tall);

        var cards = SplitOf(view);

        page.IsCardsView = false;
        Arrange(view, Tall);

        var table = SplitOf(view);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dragged.Detail, Is.GreaterThan(initial.Detail), "Разделитель поднят – карточка стрима выросла");
            Assert.That(cards.Share, Is.EqualTo(dragged.Share).Within(0.01), "Вид карточками сохраняет долю, заданную разделителем");
            Assert.That(table.Share, Is.EqualTo(dragged.Share).Within(0.01), "Возврат к таблице сохраняет долю, заданную разделителем");
        }
    }

    [TestCase(1.0)]
    [TestCase(1.6)]
    public void Пол_карточки_рекорда_и_колонки_рекордов_едет_с_масштабом_шрифта(double scale)
    {
        try
        {
            FontScaleManager.Apply(scale);

            var page = CreatePage();
            var view = new StreamHistoryPageView { DataContext = page };

            Arrange(view, new(2200, 900));

            var style = (Style)view.FindResource("RecordCard");
            var cards = Descendants<Button>(view).Where(button => ReferenceEquals(button.Style, style)).ToList();
            var records = cards.Count > 0 ? ColumnOf(cards[0]) : null;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(cards, Is.Not.Empty, "На четырёх сессиях сводка показывает рекорды");
                Assert.That(cards.Select(card => card.MinWidth),
                    Is.All.EqualTo(StreamHistoryPageView.RecordCardMinWidth * scale).Within(Tolerance));
                Assert.That(records?.MinWidth, Is.EqualTo(StreamHistoryPageView.RecordsPaneMinWidth * scale).Within(Tolerance));
            }
        }
        finally
        {
            FontScaleManager.Apply(FontScaleManager.DefaultScale);
        }
    }

    private static ColumnDefinition? ColumnOf(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (VisualTreeHelper.GetParent(current) is Grid { ColumnDefinitions.Count: > 1 } grid
                && grid.ColumnDefinitions.Any(column => column.Style is not null))
            {
                return grid.ColumnDefinitions[Grid.GetColumn((UIElement)current)];
            }
        }

        return null;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static (double Table, double Detail) PageColumns(StreamHistoryPageView view)
    {
        var columns = ((Grid)view.FindName("PageGrid")!).ColumnDefinitions;

        Assert.That(columns, Has.Count.EqualTo(3), "Раскладка рядом");

        return (Math.Round(columns[0].ActualWidth, 2), Math.Round(columns[2].ActualWidth, 2));
    }

    private static (double Table, double Detail, double Total, double TableFloor, double DetailFloor, double Share) SplitOf(StreamHistoryPageView view)
    {
        var grid = (Grid)view.FindName("PageGrid")!;

        if (grid.ColumnDefinitions.Count == 3)
        {
            var columns = grid.ColumnDefinitions;

            return (columns[0].ActualWidth, columns[2].ActualWidth, columns.Sum(column => column.ActualWidth),
                columns[0].MinWidth, columns[2].MinWidth, columns[2].ActualWidth / (columns[0].ActualWidth + columns[2].ActualWidth));
        }

        var rows = grid.RowDefinitions;

        return (rows[1].ActualHeight, rows[3].ActualHeight, rows.Sum(row => row.ActualHeight),
            rows[1].MinHeight, rows[3].MinHeight, rows[3].ActualHeight / (rows[1].ActualHeight + rows[3].ActualHeight));
    }

    private static void MoveSplitter(StreamHistoryPageView view, Size area, double change, bool keyboard)
    {
        var splitter = (GridSplitter)view.FindName("LayoutSplitter")!;
        var columns = splitter.ResizeDirection == GridResizeDirection.Columns;

        if (keyboard)
        {
            using var source = new HwndSource(new HwndSourceParameters("splitter-keyboard") { WindowStyle = 0 });
            var key = columns
                ? change < 0 ? Key.Left : Key.Right
                : change < 0 ? Key.Up : Key.Down;

            for (var remaining = Math.Abs(change); remaining > 0; remaining -= SplitterStep)
            {
                splitter.KeyboardIncrement = Math.Min(remaining, SplitterStep);
                splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
                Arrange(view, area);
            }

            return;
        }

        splitter.RaiseEvent(new DragStartedEventArgs(0, 0));

        for (var remaining = Math.Abs(change); remaining > 0; remaining -= SplitterStep)
        {
            var step = Math.Sign(change) * Math.Min(remaining, SplitterStep);

            splitter.RaiseEvent(columns ? new DragDeltaEventArgs(step, 0) : new DragDeltaEventArgs(0, step));
            Arrange(view, area);
        }

        splitter.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
    }

    private static double[] PageRows(StreamHistoryPageView view)
    {
        return ((Grid)view.FindName("PageGrid")!).RowDefinitions.Select(row => Math.Round(row.ActualHeight, 2)).ToArray();
    }

    private static (double Height, double CoverHeight, Visibility Stripes) Card(StreamHistoryPageView view)
    {
        var list = (ListBox)view.FindName("SessionCards")!;
        var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0)!;
        var presenter = FindChild<ContentPresenter>(container)!;
        var template = container.ContentTemplate;

        var cover = (ContentPresenter)template.FindName("CardCover", presenter)!;
        var stripes = (UIElement)template.FindName("CardStripes", presenter)!;
        var slot = FindChild<Border>(cover)!;

        return (container.ActualHeight, slot.Height, stripes.Visibility);
    }

    [Test]
    public void Подпись_крайнего_столбика_не_выходит_за_карточку_таблицы()
    {
        var page = CreatePage(sessions: LongTrend, peak: FiveDigitPeak, firstPeak: EdgePeak, trendLength: LongTrend);
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view, NarrowSideBySide);

        var card = (FrameworkElement)view.FindName("TableCard")!;

        page.TrySelectAt(0);
        Pump(view);

        var right = LabelBounds(card, TrendLabel(view, LongTrend - 1));

        page.TrySelectAt(LongTrend - 1);
        Pump(view);

        var left = LabelBounds(card, TrendLabel(view, 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(right.Right, Is.LessThanOrEqualTo(card.ActualWidth + Tolerance),
                $"Подпись «{right.Text}» крайнего правого столбика вышла за правую границу карточки на {right.Right - card.ActualWidth:F1} DIP");
            Assert.That(left.Left, Is.GreaterThanOrEqualTo(-Tolerance),
                $"Подпись «{left.Text}» крайнего левого столбика вышла за левую границу карточки на {-left.Left:F1} DIP");
            Assert.That(right.Clipped, Is.False, "Подпись правого края обрезана – запас в поле не сработал");
            Assert.That(left.Clipped, Is.False, "Подпись левого края обрезана – запас в поле не сработал");
        }
    }

    private static (double Left, double Right, bool Clipped, string Text) LabelBounds(FrameworkElement card, TextBlock label)
    {
        var left = label.TransformToAncestor(card).Transform(default).X;

        return (left, left + label.ActualWidth, LayoutInformation.GetLayoutClip(label) is not null, label.Text);
    }

    private static TextBlock TrendLabel(StreamHistoryPageView view, int index)
    {
        var strip = (ItemsControl)view.FindName("TrendStrip")!;
        var presenter = (ContentPresenter)strip.ItemContainerGenerator.ContainerFromIndex(index)!;
        var button = FindChild<Button>(presenter)!;

        return (TextBlock)button.Template.FindName("Value", button)!;
    }

    [Test]
    public void Подпись_четырёхзначного_пика_не_режется_шириной_столбика()
    {
        var page = CreatePage(sessions: TrendBarCount, peak: FourDigitPeak);
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view, Wide);

        var label = PeakLabel(view);
        var text = label.Text;
        var visibility = label.Visibility;
        var clip = LayoutInformation.GetLayoutClip(label);

        Arrange(view);

        var compact = PeakLabel(view).Visibility;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(visibility, Is.EqualTo(Visibility.Visible), "Подпись пика видна в широкой раскладке");
            Assert.That(text, Is.EqualTo(StreamHistoryPageViewModel.FormatNumber(FourDigitPeak)));
            Assert.That(clip, Is.Null,
                $"Подпись «{text}» обрезана шириной своего столбика – запас в {StreamHistoryPageView.TrendValueMargin.Left:F0} DIP не сработал");
            Assert.That(compact, Is.EqualTo(Visibility.Collapsed), "В компактной раскладке подпись по-прежнему скрыта");
        }
    }

    [Test]
    public void Столбик_тренда_назван_для_диктора_датой_и_значением_а_кнопка_действием()
    {
        var page = CreatePage(sessions: TrendBarCount, peak: FourDigitPeak);
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view, Wide);

        var strip = (ItemsControl)view.FindName("TrendStrip")!;
        var bar = strip.Items.Cast<StreamTrendBarViewModel>().Single(item => item.IsPeak);
        var index = strip.Items.IndexOf(bar);
        var items = UIElementAutomationPeer.CreatePeerForElement(strip).GetChildren();
        var presenter = (ContentPresenter)strip.ItemContainerGenerator.ContainerFromIndex(index)!;
        var button = UIElementAutomationPeer.CreatePeerForElement(FindChild<Button>(presenter)!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(items, Has.Count.EqualTo(strip.Items.Count), "У каждого столбика свой элемент в дереве автоматизации");
            Assert.That(items[index].GetName(), Is.EqualTo(bar.Label), "Контейнер столбика звучит датой и значением, а не именем типа");
            Assert.That(button.GetName(), Is.Not.Empty.And.Not.EqualTo(bar.Label),
                "Кнопка внутри называет действие и не повторяет имя контейнера");
        }
    }

    [Test]
    public void Выделение_доводится_пикселями_и_не_двигает_уже_видимую_строку()
    {
        var page = CreatePage(sessions: LongHistory);
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view, Wide);

        var list = (ListBox)view.FindName("SessionsList")!;
        var viewer = FindChild<ScrollViewer>(list)!;
        var panel = FindChild<VirtualizingStackPanel>(list);

        var cut = LastPartiallyVisible(list, viewer);
        var overflow = Bounds(list, viewer, cut).Bottom - viewer.ViewportHeight;

        page.TrySelectAt(cut);
        Pump(view);

        var revealed = Bounds(list, viewer, cut);
        var settled = viewer.VerticalOffset;

        page.TrySelectAt(cut - 1);
        Pump(view);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(VirtualizingPanel.GetScrollUnit(list), Is.EqualTo(ScrollUnit.Pixel),
                "Список прокручивается пикселями, а не целыми строками");
            Assert.That(panel, Is.Not.Null, "Виртуализация списка сохранена – панель осталась VirtualizingStackPanel");
            Assert.That(panel!.Children, Has.Count.LessThan(LongHistory / 4),
                "Пиксельная прокрутка не должна материализовать всю историю");
            Assert.That(overflow, Is.GreaterThan(0.5), "Тест мерит строку, которая не влезла в окно целиком");
            Assert.That(revealed.Bottom, Is.EqualTo(viewer.ViewportHeight).Within(1),
                "Строка доведена ровно на недостающие пиксели, а не на целую строку");
            Assert.That(viewer.VerticalOffset, Is.EqualTo(settled).Within(0.5),
                "Выбор строки, видимой целиком, список не двигает");
        }
    }

    private static TextBlock PeakLabel(StreamHistoryPageView view)
    {
        var strip = (ItemsControl)view.FindName("TrendStrip")!;
        var bar = strip.Items.Cast<StreamTrendBarViewModel>().Single(item => item.IsPeak);
        var presenter = (ContentPresenter)strip.ItemContainerGenerator.ContainerFromItem(bar)!;
        var button = FindChild<Button>(presenter)!;

        return (TextBlock)button.Template.FindName("Value", button)!;
    }

    private static (double Top, double Bottom) Bounds(ItemsControl list, ScrollViewer viewer, int index)
    {
        var container = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(index)!;
        var top = container.TransformToAncestor(viewer).Transform(default).Y;

        return (top, top + container.ActualHeight);
    }

    private static int LastPartiallyVisible(ItemsControl list, ScrollViewer viewer)
    {
        for (var index = 0; index < list.Items.Count; index++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is null)
            {
                break;
            }

            var bounds = Bounds(list, viewer, index);

            if (bounds.Top < viewer.ViewportHeight && bounds.Bottom > viewer.ViewportHeight + 0.5)
            {
                return index;
            }
        }

        throw new InvalidOperationException("В списке нет строки, обрезанной нижним краем окна");
    }

    private static void Pump(FrameworkElement view)
    {
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        view.UpdateLayout();
    }

    [Test]
    public void Меню_фильтра_висит_на_строке_сегмента_и_достижимо_с_клавиатуры()
    {
        var page = CreatePage();
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view);

        page.TrySelectAt(0);
        Arrange(view);

        var list = (ListBox)view.FindName("SegmentCards")!;
        var card = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Segments[0].CanFilter, Is.True);
            Assert.That(ContextMenuService.GetContextMenu(card), Is.Not.Null);
            Assert.That(card.Focusable, Is.True, "Фокус клавиатуры стоит на самой карточке – с неё и поднимается ContextMenuOpening");
            Assert.That(OwnerOfMenu(card), Is.SameAs(card));
            Assert.That(card.Cursor, Is.EqualTo(Cursors.Hand),
                "Карточка выбирается кликом, и курсор у неё тот же, что у строк списков каркаса");
        }
    }

    [Test]
    public void Меню_строки_видит_вьюмодель_той_строки_над_которой_открыто()
    {
        var page = CreatePage();
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view);

        page.TrySelectAt(0);
        Arrange(view);

        var sessions = (ListBox)view.FindName("SessionsList")!;
        var segments = (ListBox)view.FindName("SegmentCards")!;

        var firstRow = (ListBoxItem)sessions.ItemContainerGenerator.ContainerFromIndex(0)!;
        var secondRow = (ListBoxItem)sessions.ItemContainerGenerator.ContainerFromIndex(1)!;
        var segmentRow = (ListBoxItem)segments.ItemContainerGenerator.ContainerFromIndex(0)!;

        var first = MenuItemState(firstRow, HiddenMenuItemIndex);
        var second = MenuItemState(secondRow, HiddenMenuItemIndex);
        var segment = MenuItemState(segmentRow);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.DataContext, Is.SameAs(firstRow.DataContext), "Обработчик берёт строку из DataContext пункта меню");
            Assert.That(first.Header, Is.EqualTo(((StreamSessionRowViewModel)firstRow.DataContext).HiddenMenuCaption));
            Assert.That(second.DataContext, Is.SameAs(secondRow.DataContext), "Меню общее на все строки – оно обязано смотреть на ту, над которой открыто");
            Assert.That(segment.DataContext, Is.SameAs(segmentRow.DataContext));
            Assert.That(segment.Header, Is.EqualTo(((StreamSessionSegmentRowViewModel)segmentRow.DataContext).FilterCaption));
        }
    }

    private static (object? DataContext, object? Header) MenuItemState(FrameworkElement owner, int index = 0)
    {
        var menu = ContextMenuService.GetContextMenu(owner)
            ?? throw new InvalidOperationException("У строки нет контекстного меню");

        menu.PlacementTarget = owner;
        menu.IsOpen = true;

        try
        {
            var item = (MenuItem)menu.Items[index];
            item.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            return (item.DataContext, item.Header);
        }
        finally
        {
            menu.IsOpen = false;
        }
    }

    private static DependencyObject? OwnerOfMenu(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement && ContextMenuService.GetContextMenu(current) is not null)
            {
                return current;
            }
        }

        return null;
    }

    private static TChild? FindChild<TChild>(DependencyObject parent)
        where TChild : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if (child is TChild found)
            {
                return found;
            }

            if (FindChild<TChild>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static void Arrange(FrameworkElement view)
    {
        Arrange(view, Area);
    }

    private static void Arrange(FrameworkElement view, Size area)
    {
        view.Measure(area);
        view.Arrange(new(area));
        view.UpdateLayout();
    }

    private static double TableTop(StreamHistoryPageView view)
    {
        var card = (FrameworkElement)view.FindName("TableCard")!;
        var table = (FrameworkElement)view.FindName("SessionsTable")!;

        return table.TransformToAncestor(card).Transform(default).Y;
    }

    private StreamHistoryPageViewModel CreatePage(int sessions = 4, int peak = 0, int firstPeak = 0, int trendLength = StreamHistoryPageViewModel.DefaultTrendLength)
    {
        var store = new StreamSessionHistoryStore(filePath: Path.Combine(_directory, "sessions.json"));

        for (var index = 0; index < sessions; index++)
        {
            var value = index == sessions - 1 ? peak : index == 0 ? firstPeak : 0;

            store.Append(Session(index, value));
        }

        var users = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);
        var boxArt = new GameBoxArtProvider(new FakeBoxArtCache(), NullLogger<GameBoxArtProvider>.Instance);
        ISettingsStore settings = new MemorySettings();

        settings.SetInt(SettingsKeys.StreamTrendLength, trendLength);

        return new(
            store,
            users,
            settings,
            boxArt,
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance));
    }

    private static StreamSessionRecord Session(int index, int peak = 0)
    {
        var started = Start.AddDays(index);

        return new()
        {
            Channel = "bobito217",
            StartedAt = started,
            EndedAt = started.AddHours(3),
            Title = "эфир",
            Game = index % 2 == 0 ? "Just Chatting" : "Minecraft",
            MessageCount = 100 + index,
            ChatterCount = 1,
            PeakViewers = peak > 0 ? peak : 10 + index,
            AverageViewers = 5,
        };
    }
}
