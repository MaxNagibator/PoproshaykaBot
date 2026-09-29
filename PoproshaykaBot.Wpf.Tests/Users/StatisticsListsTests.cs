using KeepShell.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Users;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Tests.Users;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class StatisticsListsTests
{
    private const double Tolerance = 0.5;
    private const double StripeWidth = 3;

    private static readonly DateTimeOffset Start = new(new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Local));
    private static readonly Size FullHd = new(1660, 1000);

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
        _directory = Path.Combine(Path.GetTempPath(), "poproshayka-lists-" + Guid.NewGuid().ToString("N"));
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
    public void Сортировка_рейтинга_переиспользует_контейнеры_строк()
    {
        var page = CreateUsersPage(80);
        var view = new UserStatisticsPageView { DataContext = page };

        Arrange(view);

        var list = (ListBox)view.FindName("RatingList")!;
        var before = Containers(list);

        page.SortByCommand.Execute(UserStatisticsSortKey.Name);
        Arrange(view);

        var after = Containers(list);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.Users[0].Name, Is.EqualTo("user-00"), "Сортировка по имени действительно переставила строки");
            Assert.That(before, Is.Not.Empty);
            Assert.That(after.Where(container => !before.Contains(container)), Is.Empty,
                "Сброс коллекции заново строил бы каждую видимую строку – это и было главной ценой сортировки");
        }
    }

    [Test]
    public void Сортировка_истории_переиспользует_контейнеры_строк()
    {
        var page = CreateStreamsPage(Enumerable.Range(0, 60).Select(index => Session(index, segments: 1)).ToArray());
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view);

        var list = (ListBox)view.FindName("SessionsList")!;
        var before = Containers(list);

        page.SortByColumnCommand.Execute(StreamSortKey.Duration);
        Arrange(view);

        var after = Containers(list);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before, Is.Not.Empty);
            Assert.That(after.Where(container => !before.Contains(container)), Is.Empty);
        }
    }

    [TestCase(nameof(UserStatisticsPageView))]
    [TestCase(nameof(StreamHistoryPageView))]
    public void Каждый_список_страницы_прокручивается_пикселями(string viewName)
    {
        FrameworkElement view = string.Equals(viewName, nameof(UserStatisticsPageView), StringComparison.Ordinal)
            ? new UserStatisticsPageView { DataContext = CreateUsersPage(10) }
            : new StreamHistoryPageView { DataContext = CreateStreamsPage(Session(0, segments: 2), Session(1, segments: 1)) };

        Arrange(view);

        var lists = Descendants<ListBox>(view).ToList();

        Assert.That(lists, Is.Not.Empty);
        Assert.That(lists.Where(list => ScrollViewer.GetCanContentScroll(list) && VirtualizingPanel.GetScrollUnit(list) != ScrollUnit.Pixel)
                .Select(list => list.Name),
            Is.Empty,
            "Список, который прокручивает содержимое сам, обязан идти пикселями – иначе колесо двигает его целыми строками");
    }

    [TestCase(1)]
    [TestCase(12)]
    public void Карточки_сегментов_идут_по_высоте_содержимого(int segments)
    {
        var page = CreateStreamsPage(Session(0, segments));
        var view = new StreamHistoryPageView { DataContext = page };

        Arrange(view, FullHd);
        page.TrySelectAt(0);
        Arrange(view, FullHd);

        var list = (ListBox)view.FindName("SegmentCards")!;
        var card = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0)!;
        var stripe = Descendants<Border>(card).First(border => Math.Abs(border.Width - StripeWidth) < Tolerance);
        var viewer = Descendants<ScrollViewer>(list).First();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.ActualHeight, Is.EqualTo(card.DesiredSize.Height - card.Margin.Top - card.Margin.Bottom).Within(Tolerance),
                "Карточка сегмента не тянется на высоту списка");
            Assert.That(stripe.ActualHeight, Is.LessThan(card.ActualHeight), "Полоса слева – в пределах своей карточки");

            if (segments == 1)
            {
                Assert.That(card.ActualHeight, Is.LessThan(list.ActualHeight / 2), "Один сегмент – карточка, а не пустой растянутый блок");
            }
            else
            {
                Assert.That(viewer.ExtentHeight, Is.GreaterThan(viewer.ViewportHeight), "Много сегментов по-прежнему прокручиваются");
            }
        }
    }

    private static HashSet<ListBoxItem> Containers(ListBox list)
    {
        var containers = new HashSet<ListBoxItem>(ReferenceEqualityComparer.Instance);

        for (var index = 0; index < list.Items.Count; index++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container)
            {
                containers.Add(container);
            }
        }

        return containers;
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

    private static void Arrange(FrameworkElement view)
    {
        Arrange(view, new(1024, 640));
    }

    private static void Arrange(FrameworkElement view, Size area)
    {
        view.Measure(area);
        view.Arrange(new(area));
        view.UpdateLayout();
    }

    private UserStatisticsPageViewModel CreateUsersPage(int count)
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_directory, "settings.json"));
        var statistics = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);
        var eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);

        statistics.ReplaceAll(Enumerable.Range(0, count)
            .Select(index => new UserStatistics
            {
                UserId = index.ToString(UiCulture.Russian),
                Name = $"user-{(count - 1 - index):D2}",
                MessageCount = (ulong)(1000 - index),
            }));

        return new(
            statistics,
            null!,
            new UserRankService(settingsManager),
            new UserPointsManagementService(statistics, settingsManager, eventBus),
            new FakeChannelProvider(),
            settingsManager,
            null!,
            eventBus,
            new MemorySettings());
    }

    private StreamHistoryPageViewModel CreateStreamsPage(params StreamSessionRecord[] sessions)
    {
        var store = new StreamSessionHistoryStore(filePath: Path.Combine(_directory, "sessions.json"));

        foreach (var session in sessions)
        {
            store.Append(session);
        }

        return new(
            store,
            new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance),
            new MemorySettings(),
            new GameBoxArtProvider(new FakeBoxArtCache(), NullLogger<GameBoxArtProvider>.Instance),
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance));
    }

    private static StreamSessionRecord Session(int index, int segments)
    {
        var started = Start.AddDays(index);
        var length = TimeSpan.FromMinutes(30 + (index * 37 % 200));
        var parts = Enumerable.Range(0, segments)
            .Select(part => new StreamSessionSegment
            {
                StartedAt = started + (length * part / segments),
                EndedAt = started + (length * (part + 1) / segments),
                Title = "эфир",
                Game = part % 2 == 0 ? "Just Chatting" : "Minecraft",
                MessageCount = 10,
                PeakViewers = 5,
                AverageViewers = 3,
            })
            .ToList();

        return new()
        {
            Channel = "bobito217",
            StartedAt = started,
            EndedAt = started + length,
            Title = "эфир",
            Game = "Just Chatting",
            MessageCount = 100 + index,
            ChatterCount = 1,
            PeakViewers = 10 + index,
            AverageViewers = 5,
            Segments = parts,
        };
    }

    private sealed class FakeChannelProvider : IChannelProvider
    {
        public string? Channel => "test";
    }
}
