using KeepShell.Bootstrap;
using KeepShell.Services.Platform;
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
using System.Windows.Controls;
using System.Windows.Input;

namespace PoproshaykaBot.Wpf.Tests.Streams;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class StreamHistoryPageTests
{
    private static readonly DateTimeOffset Start = new(new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Local));

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
    private StreamSessionHistoryStore _store = null!;

    [OneTimeSetUp]
    public void EnsureApplication()
    {
        TestResources.Ensure(Dictionaries);
    }

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "poproshayka-streams-" + Guid.NewGuid().ToString("N"));
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
    public void Фильтр_по_игре_пересчитывает_список_сводку_и_тренд()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"), Session(1, "Minecraft"), Session(2, "Just Chatting"));

        page.TrySelectAt(0);
        page.FilterByGameCommand.Execute(page.Segments[0]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Sessions, Has.Count.EqualTo(2));
            Assert.That(page.HasGameFilter, Is.True);
            Assert.That(page.GameFilterText, Is.EqualTo("Игра: Just Chatting"));
            Assert.That(page.TotalSessionsText, Is.EqualTo("2"));
            Assert.That(page.Trend, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void Чип_снимает_фильтр_и_возвращает_все_стримы()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"), Session(1, "Minecraft"));

        page.TrySelectAt(0);
        page.FilterByGameCommand.Execute(page.Segments[0]);
        page.ClearGameFilterCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Sessions, Has.Count.EqualTo(2));
            Assert.That(page.HasGameFilter, Is.False);
        }
    }

    [Test]
    public void Повторный_выбор_той_же_игры_снимает_фильтр()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"), Session(1, "Minecraft"));

        page.TrySelectAt(0);
        page.FilterByGameCommand.Execute(page.Segments[0]);
        page.FilterByGameCommand.Execute(page.Segments[0]);

        Assert.That(page.HasGameFilter, Is.False);
    }

    [Test]
    public void Сегмент_без_категории_фильтра_не_ставит()
    {
        var page = Create(new MemorySettings(), Session(0, null));

        page.TrySelectAt(0);
        page.FilterByGameCommand.Execute(page.Segments[0]);

        Assert.That(page.HasGameFilter, Is.False);
    }

    [Test]
    public void Строка_учёта_есть_только_у_записи_с_интервалами()
    {
        var legacy = Session(0, "Just Chatting");
        var tracked = Session(1, "Minecraft");
        tracked.TrackedIntervals = [new() { StartedAt = tracked.StartedAt.AddMinutes(5), EndedAt = tracked.EndedAt }];

        var page = Create(new MemorySettings(), legacy, tracked);
        var texts = new Dictionary<string, string>();

        for (var index = 0; index < 2; index++)
        {
            page.TrySelectAt(index);
            texts[page.SelectedRow!.Source.Game!] = page.DetailTrackingText;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(texts["Just Chatting"], Is.Empty);
            Assert.That(texts["Minecraft"], Is.EqualTo("Учёт вёлся 2 ч 55 мин из 3 ч 0 мин"));
        }
    }

    [TestCase(-1, "")]
    [TestCase(0, "Учёт не вёлся: бот не был подключён к чату")]
    [TestCase(181, "Учёт вёлся весь эфир")]
    [TestCase(179.5, "Учёт вёлся 2 ч 59 мин из 3 ч 0 мин")]
    [TestCase(180.2, "Учёт вёлся почти весь эфир")]
    [TestCase(74, "Учёт вёлся 1 ч 14 мин из 3 ч 0 мин")]
    public void Строка_учёта_называет_долю_эфира_без_лжи_про_неизвестное(double trackedMinutes, string expected)
    {
        var session = Session(0, "Just Chatting");
        session.EndedAt += TimeSpan.FromSeconds(30);

        if (trackedMinutes >= 0)
        {
            session.TrackedIntervals = trackedMinutes > 0
                ? [new() { StartedAt = session.StartedAt, EndedAt = session.StartedAt.AddMinutes(trackedMinutes) }]
                : [];
        }

        Assert.That(StreamHistoryPageViewModel.DescribeTracking(session), Is.EqualTo(expected));
    }

    [TestCase(20, 20)]
    [TestCase(40, 30)]
    public void Глубина_ограничивает_число_столбиков(int length, int expected)
    {
        var page = Create(new MemorySettings(), Enumerable.Range(0, 30).Select(index => Session(index, "Just Chatting")).ToArray());

        page.LengthOptions[Array.IndexOf(StreamHistoryPageViewModel.TrendLengths, length)].Command.Execute(null);

        Assert.That(page.Trend, Has.Count.EqualTo(expected));
    }

    [Test]
    public void Метрика_и_глубина_переживают_перезапуск()
    {
        var settings = new MemorySettings();
        var page = Create(settings, Session(0, "Just Chatting"), Session(1, "Minecraft"));

        page.MetricOptions[Array.IndexOf(StreamHistoryPageViewModel.TrendMetrics, StreamTrendMetric.Messages)].Command.Execute(null);
        page.LengthOptions[Array.IndexOf(StreamHistoryPageViewModel.TrendLengths, 20)].Command.Execute(null);

        var restarted = Create(settings, Session(0, "Just Chatting"), Session(1, "Minecraft"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restarted.TrendCaption, Does.StartWith("Сообщения"));
            Assert.That(restarted.MetricOptions[2].IsChecked, Is.True);
            Assert.That(restarted.LengthOptions[0].IsChecked, Is.True);
        }
    }

    [Test]
    public void Столбик_считает_выбранную_метрику_и_подписывает_максимум()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting", messages: 10), Session(1, "Minecraft", messages: 40));

        page.MetricOptions[Array.IndexOf(StreamHistoryPageViewModel.TrendMetrics, StreamTrendMetric.Messages)].Command.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Trend[0].Value, Is.EqualTo(10));
            Assert.That(page.Trend[1].Value, Is.EqualTo(40));
            Assert.That(page.Trend[0].IsPeak, Is.False);
            Assert.That(page.Trend[1].IsPeak, Is.True);
            Assert.That(page.Trend[1].Label, Does.Contain("сообщений 40"));
        }
    }

    [Test]
    public void Скрытая_полоса_уносит_подписи_и_черту_а_меню_остаётся_на_месте()
    {
        ISettingsStore settings = new MemorySettings();
        var page = Create(settings, Session(0, "Just Chatting"), Session(1, "Minecraft"));

        page.ToggleTrendCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HasTrend, Is.False);
            Assert.That(page.HasTrendLabels, Is.False);
            Assert.That(page.HasTrendBaseline, Is.False);
            Assert.That(page.CanShowTrend, Is.True, "Меню полосы живёт по этому признаку и остаётся в тулбаре");
            Assert.That(page.TrendVisibilityCaption, Is.EqualTo("Показать полосу"));
            Assert.That(settings.GetBool(SettingsKeys.StreamTrendVisible, true), Is.False);
        }

        page.ToggleTrendCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HasTrend, Is.True);
            Assert.That(page.HasTrendLabels, Is.True);
            Assert.That(page.HasTrendBaseline, Is.True);
        }
    }

    [Test]
    public void Топ_категорий_считается_по_отфильтрованному_набору()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"), Session(1, "Minecraft"), Session(2, "Just Chatting"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.TopCategories.Select(category => category.Game), Is.EqualTo(new[] { "Just Chatting", "Minecraft" }));
            Assert.That(page.TopCategories[0].SessionsText, Is.EqualTo("2 стрима"));
            Assert.That(page.HasSummaryStrip, Is.True);
        }

        page.FilterByGameCommand.Execute(page.TopCategories[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.GameFilter, Is.EqualTo("Minecraft"));
            Assert.That(page.TopCategories.Select(category => category.Game), Is.EqualTo(new[] { "Minecraft" }));
            Assert.That(page.TopCategories[0].ShareText, Is.EqualTo("100 %"));
            Assert.That(page.Records, Is.Empty);
        }
    }

    [Test]
    public void Повторный_клик_по_той_же_категории_снимает_фильтр()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"), Session(1, "Minecraft"));

        page.FilterByGameCommand.Execute(page.TopCategories[0]);
        page.FilterByGameCommand.Execute(page.TopCategories[0]);

        Assert.That(page.HasGameFilter, Is.False);
    }

    [Test]
    public void Карточка_рекорда_выбирает_свою_сессию()
    {
        var page = Create(
            new MemorySettings(),
            Session(0, "Just Chatting", messages: 10),
            Session(1, "Minecraft", messages: 900));

        var record = page.Records.Single(card => card.Kind == StreamRecordKind.Messages);

        page.SelectSessionCommand.Execute(record.Row);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.CanSelect, Is.True);
            Assert.That(record.ValueText, Is.EqualTo("900"));
            Assert.That(page.SelectedRow?.MessageCount, Is.EqualTo(900));
        }
    }

    [Test]
    public void Сводка_открыта_при_первом_заходе_и_свёртка_переживает_перезапуск()
    {
        ISettingsStore settings = new MemorySettings();
        var page = Create(settings, Session(0, "Just Chatting"), Session(1, "Minecraft"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.IsSummaryExpanded, Is.True);
            Assert.That(page.IsSummaryOpen, Is.True);
        }

        page.ToggleSummaryCommand.Execute(null);

        var restarted = Create(settings, Session(0, "Just Chatting"), Session(1, "Minecraft"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.GetBool(SettingsKeys.StreamSummaryExpanded, true), Is.False);
            Assert.That(restarted.IsSummaryExpanded, Is.False);
            Assert.That(restarted.IsSummaryOpen, Is.False);
            Assert.That(restarted.HasSummaryStrip, Is.True);
        }

        restarted.ToggleSummaryCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restarted.IsSummaryOpen, Is.True);
            Assert.That(Create(settings, Session(0, "Just Chatting")).IsSummaryExpanded, Is.True);
        }
    }

    [Test]
    public void Числа_сводки_живут_и_без_категорий_с_рекордами()
    {
        var page = Create(new MemorySettings(), Session(0, game: null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HasSessions, Is.True, "Шапка сводки с числами держится на этом признаке");
            Assert.That(page.HasSummaryStrip, Is.False, "Ни категорий, ни рекордов – раскрывающейся части и шеврона нет");
            Assert.That(page.IsSummaryOpen, Is.False);
            Assert.That(page.TotalSessionsText, Is.EqualTo("1"));
            Assert.That(page.SummaryLine, Does.Contain("в эфире"));
        }
    }

    [TestCase(false, "Фильтровать по игре «Just Chatting»")]
    [TestCase(true, "Снять фильтр по игре «Just Chatting»")]
    public void Меню_сегмента_называет_действие_по_текущему_фильтру(bool filtered, string expected)
    {
        var page = Create(new MemorySettings(), Session(0, "Minecraft"), Session(1, "Just Chatting"));

        page.TrySelectAt(0);

        if (filtered)
        {
            page.FilterByGameCommand.Execute(page.Segments[0]);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Segments[0].CanFilter, Is.True);
            Assert.That(page.Segments[0].IsFiltered, Is.EqualTo(filtered));
            Assert.That(page.Segments[0].FilterCaption, Is.EqualTo(expected));
        }
    }

    [Test]
    public void Клик_по_известному_чаттеру_просит_открыть_пользователя()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting", chatterId: "42"));
        var requested = new List<string>();

        page.UserRequested += (_, userId) => requested.Add(userId);
        page.TrySelectAt(0);
        page.OpenChatterCommand.Execute(page.Chatters[0]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Chatters[0].CanOpen, Is.True);
            Assert.That(requested, Is.EqualTo(new[] { "42" }));
        }
    }

    [Test]
    public void Чаттера_которого_нет_в_статистике_страница_не_открывает()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting", chatterId: "777"));
        var requested = new List<string>();

        page.UserRequested += (_, userId) => requested.Add(userId);
        page.TrySelectAt(0);
        page.OpenChatterCommand.Execute(page.Chatters[0]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Chatters[0].CanOpen, Is.False);
            Assert.That(page.Chatters[0].Hint, Does.Contain("нет"));
            Assert.That(requested, Is.Empty);
        }
    }

    [Test]
    public void Вид_карточками_переживает_перезапуск()
    {
        ISettingsStore settings = new MemorySettings();
        var page = Create(settings, Session(0, "Just Chatting"), Session(1, "Minecraft"));

        Assert.That(page.IsTableView, Is.True);

        page.IsCardsView = true;

        var restarted = Create(settings, Session(0, "Just Chatting"), Session(1, "Minecraft"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restarted.IsCardsView, Is.True);
            Assert.That(restarted.IsTableView, Is.False);
            Assert.That(restarted.SortedSessions, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void Сортировка_не_трогает_порядок_тренда()
    {
        ISettingsStore settings = new MemorySettings();
        var page = Create(settings, Session(0, "Just Chatting", messages: 900), Session(1, "Minecraft", messages: 10));

        page.SortOptions[Array.IndexOf(StreamHistoryPageViewModel.SortKeys, StreamSortKey.Messages)].Command.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.SortedSessions.Select(row => row.MessageCount), Is.EqualTo(new long[] { 900, 10 }));
            Assert.That(page.Sessions.Select(row => row.MessageCount), Is.EqualTo(new long[] { 10, 900 }),
                "Тренд строится по этому списку и остаётся в порядке по дате – сортировка живёт только в списке стримов");
            Assert.That(page.Trend.Select(bar => bar.Row.MessageCount), Is.EqualTo(new long[] { 900, 10 }));
            Assert.That(page.SortCaption, Is.EqualTo("Сортировка: Сообщения, по убыванию"));
        }

        page.SortDirectionOptions[1].Command.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.SortedSessions.Select(row => row.MessageCount), Is.EqualTo(new long[] { 10, 900 }));
            Assert.That(page.SortCaption, Is.EqualTo("Сортировка: Сообщения, по возрастанию"));
        }

        var restarted = Create(settings, Session(0, "Just Chatting", messages: 900), Session(1, "Minecraft", messages: 10));

        Assert.That(restarted.SortedSessions.Select(row => row.MessageCount), Is.EqualTo(new long[] { 10, 900 }));
    }

    [Test]
    public void Заголовок_колонки_переворачивает_направление_и_показывает_стрелку()
    {
        ISettingsStore settings = new MemorySettings();
        var page = Create(settings, Session(0, "Just Chatting", messages: 900), Session(1, "Minecraft", messages: 10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.StartedAtSortIndicator, Is.EqualTo(TableSortIndicator.Descending),
                "стартовое направление – по началу убыванием");
            Assert.That(page.MessagesSortIndicator, Is.EqualTo(TableSortIndicator.None));
        }

        page.SortByColumnCommand.Execute(StreamSortKey.Messages);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.SortedSessions.Select(row => row.MessageCount), Is.EqualTo(new long[] { 900, 10 }));
            Assert.That(page.MessagesSortIndicator, Is.EqualTo(TableSortIndicator.Descending));
            Assert.That(page.StartedAtSortIndicator, Is.EqualTo(TableSortIndicator.None));
        }

        page.SortByColumnCommand.Execute(StreamSortKey.Messages);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.SortedSessions.Select(row => row.MessageCount), Is.EqualTo(new long[] { 10, 900 }));
            Assert.That(page.MessagesSortIndicator, Is.EqualTo(TableSortIndicator.Ascending));
            Assert.That(page.SortOptions[Array.IndexOf(StreamHistoryPageViewModel.SortKeys, StreamSortKey.Messages)].IsChecked, Is.True,
                "меню сортировки карточек и шапка таблицы делят одно состояние");
            Assert.That(page.SortDirectionOptions[1].IsChecked, Is.True);
        }

        page.SortByColumnCommand.Execute(StreamSortKey.Title);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.TitleSortIndicator, Is.EqualTo(TableSortIndicator.Ascending),
                "текстовая колонка открывается по возрастанию, числовая – по убыванию");
            Assert.That(page.SortedSessions.Select(row => row.TitleFormatted), Is.Ordered);
        }

        var restarted = Create(settings, Session(0, "Just Chatting", messages: 900), Session(1, "Minecraft", messages: 10));

        Assert.That(restarted.TitleSortIndicator, Is.EqualTo(TableSortIndicator.Ascending));
    }

    [Test]
    public void Инспектор_показывает_отклонения_только_там_где_их_нет_в_списке()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting", messages: 900), Session(1, "Minecraft", messages: 10));

        page.TrySelectAt(0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.ShowDetailDeltas, Is.True, "В таблице отклонений не видно – инспектор их и показывает");
            Assert.That(page.DetailMessagesDelta, Is.Not.Empty);
        }

        page.IsCardsView = true;

        Assert.That(page.ShowDetailDeltas, Is.False, "Карточка стрима несёт те же отклонения – в инспекторе они были бы дублем");
    }

    [Test]
    public void Смена_сортировки_сохраняет_выбранную_сессию()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting", messages: 900), Session(1, "Minecraft", messages: 10));

        page.TrySelectAt(0);

        var selected = page.SelectedRow;

        page.SortOptions[Array.IndexOf(StreamHistoryPageViewModel.SortKeys, StreamSortKey.Messages)].Command.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.SelectedRow, Is.SameAs(selected));
            Assert.That(page.SortedSessions, Does.Contain(selected));
        }
    }

    [Test]
    public void Карточка_получает_отклонения_и_полосу_сегментов()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting", messages: 100), Session(1, "Minecraft", messages: 900));

        var quiet = page.SortedSessions.Single(card => card.MessageCount == 100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(quiet.MessagesDelta, Is.EqualTo("−80 % к обычному"));
            Assert.That(quiet.MessagesDeltaTone, Is.EqualTo(TrendTone.Down));
            Assert.That(quiet.Stripes.Sum(stripe => stripe.Share), Is.EqualTo(1).Within(0.001));
            Assert.That(quiet.CardSummary, Does.Contain("Just Chatting"));
        }
    }

    [TestCase(false, 2)]
    [TestCase(true, 3)]
    public void Убранный_стрим_не_идёт_в_агрегаты_ни_в_одном_режиме(bool showHidden, int expectedRows)
    {
        var page = Create(
            new MemorySettings(),
            Session(0, "Just Chatting"),
            Session(1, "Minecraft", messages: 1000, hidden: true),
            Session(2, "Just Chatting"));

        page.ShowHidden = showHidden;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Sessions, Has.Count.EqualTo(expectedRows));
            Assert.That(page.Sessions.Any(row => row.IsHidden), Is.EqualTo(showHidden));
            Assert.That(page.TotalSessionsText, Is.EqualTo("2"));
            Assert.That(page.TotalAirTimeText, Is.EqualTo("6 ч 0 мин"));
            Assert.That(page.Trend, Has.Count.EqualTo(2));
            Assert.That(page.Trend.All(bar => !bar.Row.IsHidden), Is.True);
            Assert.That(page.Sessions.First(row => !row.IsHidden).MessagesDelta, Is.EqualTo("как обычно"));
            Assert.That(page.HasHiddenSessions, Is.True);
            Assert.That(page.HiddenToggleCaption, Is.EqualTo("Убранные: 1"));
        }
    }

    [Test]
    public void Стрим_убирается_из_статистики_и_возвращается_обратно()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"), Session(1, "Minecraft"));

        var target = page.Sessions.First(row => row.Source.Game == "Minecraft");

        page.SetSessionHiddenCommand.Execute(target);

        var afterHide = _store.Load().Sessions.Single(session => session.Id == target.Source.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterHide.IsHidden, Is.True);
            Assert.That(page.Sessions, Has.Count.EqualTo(1));
            Assert.That(page.HiddenCount, Is.EqualTo(1));
            Assert.That(page.Notice, Is.Null);
        }

        page.ShowHidden = true;

        var hiddenRow = page.Sessions.Single(row => row.IsHidden);

        page.SetSessionHiddenCommand.Execute(hiddenRow);

        var afterRestore = _store.Load().Sessions.Single(session => session.Id == target.Source.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterRestore.IsHidden, Is.False);
            Assert.That(page.Sessions, Has.Count.EqualTo(2));
            Assert.That(page.HasHiddenSessions, Is.False);
        }
    }

    [Test]
    public void Вход_на_страницу_гасит_показ_убранных()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"), Session(1, "Minecraft", hidden: true));

        page.ShowHidden = true;
        page.OnEnter();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.ShowHidden, Is.False);
            Assert.That(page.Sessions, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Фильтр_по_игре_не_уносит_доступ_к_убранным()
    {
        var page = Create(
            new MemorySettings(),
            Session(0, "Just Chatting"),
            Session(1, "Minecraft", hidden: true),
            Session(2, "Just Chatting"));

        page.TrySelectAt(0);
        page.FilterByGameCommand.Execute(page.Segments[0]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HasGameFilter, Is.True);
            Assert.That(page.Sessions.Any(row => row.IsHidden), Is.False);
            Assert.That(page.HasHiddenSessions, Is.True, "Убранный стрим вне фильтра – не повод терять кнопку, которой его возвращают");
            Assert.That(page.HiddenToggleCaption, Is.EqualTo("Убранные: 1"));
        }
    }

    [Test]
    public void История_из_одних_убранных_объясняет_пустой_список()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting", hidden: true), Session(1, "Minecraft", hidden: true));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Sessions, Is.Empty);
            Assert.That(page.HasSessions, Is.False, "Иначе на экране пустая таблица без единого слова");
            Assert.That(page.IsEverythingHidden, Is.True);
            Assert.That(page.EmptyHeading, Is.EqualTo("Все стримы убраны из статистики"));
            Assert.That(page.EmptyDescription, Does.Contain("убранных"), "Текст ведёт к кнопке в панели сверху");
            Assert.That(page.HasHiddenSessions, Is.True, "Кнопка возврата на месте – доступ не потерян");
        }

        page.ShowHidden = true;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Sessions, Has.Count.EqualTo(2));
            Assert.That(page.HasSessions, Is.True);
            Assert.That(page.EmptyHeading, Is.EqualTo("Стримов пока нет"));
        }
    }

    [Test]
    public void Фильтр_без_единой_строки_пустое_состояние_не_подменяет()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"), Session(1, "Minecraft"));

        page.TrySelectAt(0);
        page.FilterByGameCommand.Execute(page.Segments[0]);
        page.SetSessionHiddenCommand.Execute(page.Sessions.Single());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Sessions, Is.Empty);
            Assert.That(page.HasGameFilter, Is.True);
            Assert.That(page.IsEverythingHidden, Is.False, "Убран не весь список – это случай фильтра, а не пустой истории");
            Assert.That(page.HasSessions, Is.True, "Страница остаётся такой же, какой была до правки");
            Assert.That(page.EmptyHeading, Is.EqualTo("Стримов пока нет"));
        }
    }

    [Test]
    [TestCase("SessionsList")]
    [TestCase("SessionCards")]
    public void Копирование_строки_кладёт_колонки_в_буфер_через_табуляцию(string listName)
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"));
        var clipboard = new FakeClipboard();
        var list = (ListBox)CreateView(page, clipboard).FindName(listName)!;

        var idle = ApplicationCommands.Copy.CanExecute(null, list);

        page.TrySelectAt(0);

        var ready = ApplicationCommands.Copy.CanExecute(null, list);

        ApplicationCommands.Copy.Execute(null, list);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(idle, Is.False, "Без выбранной строки копировать нечего – пункт меню и Ctrl+C неактивны");
            Assert.That(ready, Is.True);
            Assert.That(clipboard.Text,
                Is.EqualTo("01.09.2026 18:00\t3 ч 0 мин\tэфир\tJust Chatting\t100\t1\t10\t5"),
                "Порядок колонок таблицы и разделитель-табуляция – так отдавал строку DataGrid до переезда на список");
        }
    }

    [Test]
    public void Копирование_берёт_строку_под_курсором_а_не_выбранную()
    {
        var page = Create(new MemorySettings(), Session(0, "Just Chatting"), Session(1, "Minecraft"));
        var clipboard = new FakeClipboard();
        var list = (ListBox)CreateView(page, clipboard).FindName("SessionsList")!;

        page.TrySelectAt(0);

        var other = page.Sessions.Single(row => row.GameCellText == "Minecraft");

        ApplicationCommands.Copy.Execute(other, list);

        Assert.That(clipboard.Text, Does.Contain("Minecraft"),
            "Правый клик по строке её не выбирает, поэтому меню передаёт свою строку параметром");
    }

    private static StreamHistoryPageView CreateView(StreamHistoryPageViewModel page, IClipboardService clipboard)
    {
        return new(clipboard) { DataContext = page };
    }

    private static StreamSessionRecord Session(
        int index,
        string? game,
        long messages = 100,
        string chatterId = "42",
        bool hidden = false)
    {
        var started = Start.AddDays(index);

        return new()
        {
            Channel = "bobito217",
            StartedAt = started,
            EndedAt = started.AddHours(3),
            Title = "эфир",
            Game = game,
            MessageCount = messages,
            ChatterCount = 1,
            PeakViewers = 10 + index,
            AverageViewers = 5,
            IsHidden = hidden,
            Chatters =
            [
                new() { UserId = chatterId, DisplayName = "qp_illson", MessageCount = messages },
            ],
            Segments =
            [
                new()
                {
                    StartedAt = started,
                    EndedAt = started.AddHours(3),
                    Title = "эфир",
                    Game = game,
                    MessageCount = messages,
                    PeakViewers = 10 + index,
                    AverageViewers = 5,
                },
            ],
        };
    }

    private StreamHistoryPageViewModel Create(ISettingsStore settings, params StreamSessionRecord[] sessions)
    {
        var store = new StreamSessionHistoryStore(filePath: Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".json"));

        foreach (var session in sessions)
        {
            store.Append(session);
        }

        _store = store;

        var users = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);
        users.ReplaceAll([new() { UserId = "42", Name = "qp_illson" }]);

        var boxArt = new GameBoxArtProvider(new FakeBoxArtCache(), NullLogger<GameBoxArtProvider>.Instance);

        return new(store, users, settings, boxArt, new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance));
    }

    private sealed class FakeClipboard : IClipboardService
    {
        public string? Text { get; private set; }

        public bool TrySetText(string? text)
        {
            Text = text;
            return true;
        }
    }
}
