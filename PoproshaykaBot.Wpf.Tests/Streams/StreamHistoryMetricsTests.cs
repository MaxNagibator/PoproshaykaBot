using KeepShell.Testing;
using PoproshaykaBot.Wpf.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Tests.Streams;

[TestFixture]
public class StreamHistoryMetricsTests
{
    private const double GameColumnMinWidth = 72;
    private const double TitleColumnMinWidth = 72;
    private const double SmallFontScale = 0.8;
    private const double SegmentCaptionWidth = 56;
    private const double SegmentPadding = 12;
    private const double SplitterWidth = 12;
    private const double TableShare = 3d / 5;
    private const double SortHeaderChrome = 2 + 8 + 6 + 8 + 8;
    private const double TableEdges = 12 + 12 + 17;
    private const double CellMargin = 10 + 10;
    private const double SegmentCardChrome = 16 + 2 + 3 + 8 + 24 + 8 + 8;
    private const double SegmentCardGameMinWidth = 50;
    private const double SegmentCardDurationGap = 8;
    private const double WindowMinHeight = 640;
    private const double TitleBarHeight = 37;
    private const double StatusBarHeight = 30;
    private const double PageHeaderHeight = 40 + 40 + 48;
    private const double SplitterHeight = 8;

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Подпись_сегмента_показывает_начало_названия_на_пороге()
    {
        var root = Themed();
        var caption = MeasureText(root, "Mine…", (double)root.FindResource("Font.Size.Caption"), null);

        Assert.That(SegmentCaptionWidth - SegmentPadding, Is.GreaterThanOrEqualTo(caption),
            $"На пороге {SegmentCaptionWidth} DIP подписи остаётся {SegmentCaptionWidth - SegmentPadding:F1} DIP, а четырём буквам с многоточием нужно {caption:F1} – подпись выродится в одно многоточие");
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Таблица_влезает_в_свою_колонку_на_пороге_раскладки()
    {
        var root = Themed();

        var required = Required(root, 1);
        var small = Required(root, SmallFontScale);

        var available = (StreamHistoryPageView.SideBySideWidth - SplitterWidth) * TableShare;

        Assert.Multiple(() =>
        {
            Assert.That(available, Is.GreaterThanOrEqualTo(required),
                $"На пороге {StreamHistoryPageView.SideBySideWidth} DIP таблица получает {available:F1} DIP, а её колонкам на минимуме нужно {required:F1} – рядом с карточкой у последней колонки отрежет заголовок");
            Assert.That(StreamHistoryPageView.TableMinWidth, Is.GreaterThanOrEqualTo(required),
                $"Пол колонки таблицы ({StreamHistoryPageView.TableMinWidth} DIP) ниже нужных ей {required:F1} – разделителем её можно будет сузить так, что последняя колонка обрежется");
            Assert.That(StreamHistoryPageView.TableMinWidth * SmallFontScale, Is.GreaterThanOrEqualTo(small),
                $"Пол колонки едет с масштабом шрифта целиком, а поля кнопок шапки, стрелка, полы колонок и полоса прокрутки – нет: при {SmallFontScale} таблице остаётся {StreamHistoryPageView.TableMinWidth * SmallFontScale:F1} DIP при нужных {small:F1}");
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Карточка_сегмента_влезает_в_самую_узкую_панель()
    {
        var root = Themed();

        var caption = (double)root.FindResource("Font.Size.Caption");
        var strong = (double)root.FindResource("Font.Size.S");
        var mono = (FontFamily)root.FindResource("Font.Mono");

        var head = SegmentCardChrome
                   + SegmentCardGameMinWidth
                   + SegmentCardDurationGap
                   + MeasureText(root, "1 ч 25 мин", strong, mono);

        var metrics = SegmentCardChrome
                      + MeasureText(root, "1 234 сообщ.", caption, null)
                      + MeasureText(root, "пик 175", caption, null)
                      + MeasureText(root, "средн. 132", caption, null)
                      + (12 * 2);

        Assert.Multiple(() =>
        {
            Assert.That(StreamHistoryPageView.SegmentsPaneMinWidth, Is.GreaterThanOrEqualTo(head),
                $"Пол панели сегментов {StreamHistoryPageView.SegmentsPaneMinWidth} DIP ниже нужных карточке {head:F1} – название игры схлопнется в многоточие рядом с эфиром");
            Assert.That(StreamHistoryPageView.SegmentCardMinWidth, Is.GreaterThanOrEqualTo(metrics),
                $"Порог второй колонки {StreamHistoryPageView.SegmentCardMinWidth} DIP ниже строки показателей {metrics:F1} – в двух колонках она переносится на вторую строку");
            Assert.That(StreamHistoryPageView.DetailSplitWidth - SplitterWidth - StreamHistoryPageView.ChattersMinWidth,
                Is.GreaterThanOrEqualTo(StreamHistoryPageView.SegmentsPaneMinWidth),
                "На пороге разъезда панели сегментов остаётся меньше её собственного пола – сетка пересилит минимум сразу после переключения");
        });
    }

    [Test]
    public void Пороги_идут_с_гистерезисом()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StreamHistoryPageView.StackedWidth, Is.LessThan(StreamHistoryPageView.SideBySideWidth),
                "без гистерезиса раскладка страницы дрожит на одном пикселе");
            Assert.That(StreamHistoryPageView.DetailStackWidth, Is.LessThan(StreamHistoryPageView.DetailSplitWidth),
                "без гистерезиса раскладка карточки дрожит на одном пикселе");
            Assert.That(StreamHistoryPageView.TableMinWidth + StreamHistoryPageView.DetailMinWidth,
                Is.LessThan(StreamHistoryPageView.SideBySideWidth),
                "полы колонок не должны превышать порог, иначе сетка пересилит их сразу после переключения");
        });
    }

    [Test]
    public void Список_карточек_на_минимальном_окне_не_вырождается_в_одну_карточку()
    {
        var list = CardListBudget(StreamHistoryPageView.TrendStripHeight);
        var cards = list / StreamHistoryPageView.CardHeight;

        Assert.Multiple(() =>
        {
            Assert.That(StreamHistoryPageView.DetailCardsRowShare, Is.LessThan(StreamHistoryPageView.DetailRowShare),
                "в виде карточками карточка стрима дублирует цифры списка, поэтому её доля меньше табличной – иначе списку не остаётся высоты");
            Assert.That(StreamHistoryPageView.DetailCardsRowMinHeight, Is.LessThan(StreamHistoryPageView.DetailRowMinHeight),
                "пол карточки стрима в виде карточками тоже ниже: доля без пола на высоком окне ужала бы её до нечитаемой");
            Assert.That(cards, Is.GreaterThanOrEqualTo(2.5),
                $"при окне {WindowMinHeight} DIP списку карточек остаётся {list:F1} DIP, то есть {cards:F1} карточки по {StreamHistoryPageView.CardHeight} – вид карточками вырождается в одну строку с обрезком");
        });
    }

    [Test]
    public void Компактная_карточка_укладывается_трижды_в_список_минимального_окна()
    {
        var list = CardListBudget(StreamHistoryPageView.TrendStripCompactHeight);
        var cards = list / StreamHistoryPageView.CardCompactHeight;

        Assert.Multiple(() =>
        {
            Assert.That(StreamHistoryPageView.CardCompactHeight, Is.LessThan(StreamHistoryPageView.CardHeight),
                "компактная карточка тем и компактна, что ниже обычной");
            Assert.That(cards, Is.GreaterThanOrEqualTo(3),
                $"при окне {WindowMinHeight} DIP списку остаётся {list:F1} DIP, то есть {cards:F1} карточки по {StreamHistoryPageView.CardCompactHeight} – приёмочных трёх целых не видно");
        });
    }

    private static double CardListBudget(double trendStripHeight)
    {
        var content = WindowMinHeight - TitleBarHeight - StatusBarHeight;
        var shared = content - PageHeaderHeight - SplitterHeight;
        var share = StreamHistoryPageView.DetailCardsRowShare
                    / (StreamHistoryPageView.ListRowShare + StreamHistoryPageView.DetailCardsRowShare);
        var detail = Math.Max(shared * share, StreamHistoryPageView.DetailCardsRowMinHeight);

        return shared - detail - trendStripHeight;
    }

    private static Grid Themed()
    {
        PackScheme.Ensure();

        var root = new Grid();

        foreach (var source in new[]
                 {
                     "pack://application:,,,/KeepShell;component/Resources/Themes/Light.xaml",
                     "pack://application:,,,/KeepShell;component/Resources/Themes/Tokens.xaml",
                     "pack://application:,,,/KeepShell;component/Resources/Styles/Controls.xaml",
                 })
        {
            root.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source) });
        }

        return root;
    }

    private static double Required(Grid root, double fontScale)
    {
        return TableEdges
               + TitleColumnMinWidth
               + GameColumnMinWidth
               + Column(root, "Начало", "06.09.2026 08:34", fontScale, mono: true)
               + Column(root, "Эфир", "2 ч 54 мин", fontScale)
               + Column(root, "Сообщ.", "175", fontScale)
               + Column(root, "Чаттеры", "9", fontScale)
               + Column(root, "Пик", "13", fontScale)
               + Column(root, "Средн.", "12", fontScale);
    }

    private static double Column(Grid root, string header, string cell, double fontScale, bool mono = false)
    {
        var headerSize = (double)root.FindResource("Font.Size.Caption") * fontScale;
        var cellSize = (double)root.FindResource("Font.Size.Body") * fontScale;
        var family = mono ? (FontFamily)root.FindResource("Font.Mono") : null;

        return Math.Max(
            MeasureText(root, header, headerSize, null) + SortHeaderChrome,
            MeasureText(root, cell, cellSize, family) + CellMargin);
    }

    private static double MeasureText(Grid root, string text, double fontSize, FontFamily? family)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            TextWrapping = TextWrapping.NoWrap,
        };

        if (family is not null)
        {
            block.FontFamily = family;
        }

        root.Children.Clear();
        root.Children.Add(block);
        root.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
        root.UpdateLayout();

        return block.DesiredSize.Width;
    }
}
