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
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace PoproshaykaBot.Wpf.Tests.Users;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
[Explicit("Замер скорости страниц статистики: печатает миллисекунды, ничего не утверждает")]
public class StatisticsPagesPerformanceTests
{
    private const string ProfileVariable = "POPROSHAYKA_PERF_PROFILE";
    private const int Repeats = 5;

    private static readonly Size Area = new(1920, 1080);
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

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
        _directory = Path.Combine(Path.GetTempPath(), "poproshayka-perf-" + Guid.NewGuid().ToString("N"));
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
    public void История_стримов()
    {
        var profile = Profile();
        var sessionsPath = Path.Combine(_directory, "stream_sessions.json");
        File.Copy(Path.Combine(profile, "stream_sessions.json"), sessionsPath);

        var cache = new FakeBoxArtCache();
        var images = BoxArtImages(profile);
        var store = new StreamSessionHistoryStore(filePath: sessionsPath);

        var games = store.Load().Sessions
            .SelectMany(session => session.Segments.Select(segment => segment.Game).Append(session.Game))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < games.Count && images.Count > 0; index++)
        {
            cache.Cached[games[index]] = images[index % images.Count];
        }

        StreamHistoryPageViewModel page = null!;
        StreamHistoryPageView view = null!;

        var open = Time(null, () =>
        {
            page = new(
                store,
                new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance),
                new MemorySettings(),
                new GameBoxArtProvider(cache, NullLogger<GameBoxArtProvider>.Instance),
                new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance));

            view = new() { DataContext = page };
            Arrange(view);
        });

        var events = 0;
        page.Sessions.CollectionChanged += (_, _) => events++;
        page.SortedSessions.CollectionChanged += (_, _) => events++;

        var category = page.TopCategories[0];

        var filter = Median(() => Time(view, () => page.FilterByGameCommand.Execute(category)),
            () => Time(view, () => page.ClearGameFilterCommand.Execute(null)));

        var eventsPerRebuild = events / (2.0 * (Repeats + 1));
        events = 0;

        var sort = Median(() => Time(view, () => page.SortByColumnCommand.Execute(StreamSortKey.Duration)),
            () => Time(view, () => page.SortByColumnCommand.Execute(StreamSortKey.StartedAt)));

        var eventsPerSort = events / (2.0 * (Repeats + 1));

        var select = Median(() => Time(view, () => page.TrySelectAt(1)), () => Time(view, () => page.TrySelectAt(2)));

        view.DataContext = null;

        var modelFilter = Median(() => Time(null, () => page.FilterByGameCommand.Execute(category)),
            () => Time(null, () => page.ClearGameFilterCommand.Execute(null)));

        var modelSort = Median(() => Time(null, () => page.SortByColumnCommand.Execute(StreamSortKey.Duration)),
            () => Time(null, () => page.SortByColumnCommand.Execute(StreamSortKey.StartedAt)));

        Report("История стримов", page.Sessions.Count, open, filter, sort, select, eventsPerRebuild, eventsPerSort, modelFilter, modelSort);
    }

    [Test]
    public void Пользователи()
    {
        var profile = Profile();
        var records = JsonSerializer.Deserialize<List<UserStatistics>>(
                          File.ReadAllText(Path.Combine(profile, "users_statistics.json")),
                          WebJson)
                      ?? [];

        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_directory, "settings.json"));
        var statistics = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);
        var eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);

        statistics.ReplaceAll(records);

        UserStatisticsPageViewModel page = null!;
        UserStatisticsPageView view = null!;

        var open = Time(null, () =>
        {
            page = new(
                statistics,
                null!,
                new UserRankService(settingsManager),
                new UserPointsManagementService(statistics, settingsManager, eventBus),
                new FakeChannelProvider(),
                settingsManager,
                null!,
                eventBus,
                new MemorySettings());

            view = new() { DataContext = page };
            Arrange(view);
        });

        var events = 0;
        page.Users.CollectionChanged += (_, _) => events++;

        var filter = Median(() => Time(view, () => page.FilterText = "a"),
            () => Time(view, () => page.FilterText = string.Empty));

        var eventsPerRebuild = events / (2.0 * (Repeats + 1));
        events = 0;

        var sort = Median(() => Time(view, () => page.SortByCommand.Execute(UserStatisticsSortKey.Name)),
            () => Time(view, () => page.SortByCommand.Execute(UserStatisticsSortKey.Points)));

        var eventsPerSort = events / (2.0 * (Repeats + 1));

        var select = Median(() => Time(view, () => page.TrySelectAt(1)), () => Time(view, () => page.TrySelectAt(2)));

        view.DataContext = null;

        var modelFilter = Median(() => Time(null, () => page.FilterText = "a"),
            () => Time(null, () => page.FilterText = string.Empty));

        var modelSort = Median(() => Time(null, () => page.SortByCommand.Execute(UserStatisticsSortKey.Name)),
            () => Time(null, () => page.SortByCommand.Execute(UserStatisticsSortKey.Points)));

        Report("Пользователи", page.Users.Count, open, filter, sort, select, eventsPerRebuild, eventsPerSort, modelFilter, modelSort);
    }

    private static string Profile()
    {
        var profile = Environment.GetEnvironmentVariable(ProfileVariable);

        if (profile is not { Length: > 0 } || !Directory.Exists(profile))
        {
            Assert.Ignore($"Замеру нужен реальный профиль: {ProfileVariable}=<каталог с stream_sessions.json и users_statistics.json>");
        }

        return profile!;
    }

    private static List<string> BoxArtImages(string profile)
    {
        var directory = Path.Combine(profile, "cache", "box-art");

        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.jpg").ToList()
            : [];
    }

    private static double Median(Func<double> first, Func<double> second)
    {
        first();
        second();

        var samples = new List<double>();

        for (var index = 0; index < Repeats; index++)
        {
            samples.Add(first());
            samples.Add(second());
        }

        samples.Sort();

        return samples[samples.Count / 2];
    }

    private static double Time(FrameworkElement? view, Action action)
    {
        var stopwatch = Stopwatch.StartNew();

        action();

        if (view is not null)
        {
            view.UpdateLayout();
        }

        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        view?.UpdateLayout();

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static void Arrange(FrameworkElement view)
    {
        view.Measure(Area);
        view.Arrange(new(Area));
        view.UpdateLayout();
    }

    private static void Report(
        string page,
        int rows,
        double open,
        double filter,
        double sort,
        double select,
        double eventsPerRebuild,
        double eventsPerSort,
        double modelFilter,
        double modelSort)
    {
        TestContext.Progress.WriteLine(
            $"{page}: строк {rows}; открытие {open:F0} мс; фильтр {filter:F1} мс; сортировка {sort:F1} мс; выбор {select:F1} мс; "
            + $"CollectionChanged на пересборку {eventsPerRebuild:F0}, на сортировку {eventsPerSort:F0}; "
            + $"без вью: фильтр {modelFilter:F1} мс, сортировка {modelSort:F1} мс");
    }

    private sealed class FakeChannelProvider : IChannelProvider
    {
        public string? Channel => "test";
    }
}
