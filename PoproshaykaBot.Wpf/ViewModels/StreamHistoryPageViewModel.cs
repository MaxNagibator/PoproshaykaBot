using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepShell.ViewModels;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Statistics;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Users;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Collections.ObjectModel;
using System.Windows;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class StreamHistoryPageViewModel : ObservableObject, IPageHeader, IDisposable
{
    public const int DefaultTrendLength = 40;
    public const double FlatDeltaPercent = 5;

    public static readonly int[] TrendLengths = [20, 40, 80];

    public static readonly StreamTrendMetric[] TrendMetrics =
    [
        StreamTrendMetric.PeakViewers,
        StreamTrendMetric.AverageViewers,
        StreamTrendMetric.Messages,
        StreamTrendMetric.Chatters,
    ];

    public static readonly StreamSortKey[] SortKeys =
    [
        StreamSortKey.StartedAt,
        StreamSortKey.Duration,
        StreamSortKey.Title,
        StreamSortKey.Game,
        StreamSortKey.Messages,
        StreamSortKey.Chatters,
        StreamSortKey.PeakViewers,
        StreamSortKey.AverageViewers,
    ];

    private const string Placeholder = "–";

    private static readonly PointTerm SessionTerm = new()
    {
        Singular = "стрим",
        Few = "стрима",
        Many = "стримов",
    };

    private static readonly PointTerm TitleTerm = new()
    {
        Singular = "название",
        Few = "названия",
        Many = "названий",
    };

    private static readonly PointTerm CategoryTerm = new()
    {
        Singular = "категория",
        Few = "категории",
        Many = "категорий",
    };

    private readonly StreamSessionHistoryStore _historyStore;
    private readonly IUserStatisticsRepository _userStatistics;
    private readonly ISettingsStore _settings;
    private readonly GameBoxArtProvider _boxArt;
    private readonly List<IDisposable> _subs = [];
    private readonly List<StreamSessionRowViewModel> _allSessions = [];
    private readonly List<StreamSessionRowViewModel> _visibleSessions = [];
    private readonly RangeObservableCollection<StreamSessionRowViewModel> _sessions = [];
    private readonly ObservableCollection<StreamSessionRowViewModel> _sortedSessions = [];

    private CancellationTokenSource? _boxArtCancellation;

    private double _averageMessages;
    private double _averageChatters;
    private double _averagePeakViewers;
    private double _averageViewers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private StreamSessionRowViewModel? _selectedRow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryLine))]
    private string _totalSessionsText = "0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryLine))]
    private string _totalSessionsLabel = "стримов";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryLine))]
    private string _totalAirTimeText = Placeholder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryLine))]
    private string _lastStreamText = Placeholder;

    [ObservableProperty]
    private string _detailTitle = Placeholder;

    [ObservableProperty]
    private bool _isDetailTitleMissing;

    [ObservableProperty]
    private string _detailGame = Placeholder;

    [ObservableProperty]
    private GameBoxArtViewModel _detailBoxArt = GameBoxArtViewModel.None;

    [ObservableProperty]
    private string _detailComposition = string.Empty;

    [ObservableProperty]
    private string _detailPeriodText = Placeholder;

    [ObservableProperty]
    private string _detailTrackingText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetailDeltas))]
    private string _detailMessagesDelta = string.Empty;

    [ObservableProperty]
    private TrendTone _detailMessagesDeltaTone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetailDeltas))]
    private string _detailChattersDelta = string.Empty;

    [ObservableProperty]
    private TrendTone _detailChattersDeltaTone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetailDeltas))]
    private string _detailPeakViewersDelta = string.Empty;

    [ObservableProperty]
    private TrendTone _detailPeakViewersDeltaTone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetailDeltas))]
    private string _detailAverageViewersDelta = string.Empty;

    [ObservableProperty]
    private TrendTone _detailAverageViewersDeltaTone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGameFilter))]
    [NotifyPropertyChangedFor(nameof(GameFilterText))]
    private string? _gameFilter;

    [ObservableProperty]
    private string? _notice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HiddenToggleHint))]
    private bool _showHidden;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrendVisibilityCaption))]
    private bool _isTrendVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSummaryOpen))]
    [NotifyPropertyChangedFor(nameof(SummaryToggleCaption))]
    private bool _isSummaryExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrendLabels))]
    [NotifyPropertyChangedFor(nameof(HasTrendBaseline))]
    [NotifyPropertyChangedFor(nameof(HasWideSummary))]
    private bool _isCompactLayout;

    [ObservableProperty]
    private string _trendFirstDate = string.Empty;

    [ObservableProperty]
    private string _trendLastDate = string.Empty;

    private StreamTrendMetric _trendMetric;
    private int _trendLength;
    private StreamListView _listView;
    private StreamSortKey _sortKey;
    private bool _sortDescending;

    private bool _rebuildingSortedView;

    public StreamHistoryPageViewModel(
        StreamSessionHistoryStore historyStore,
        IUserStatisticsRepository userStatistics,
        ISettingsStore settings,
        GameBoxArtProvider boxArt,
        IEventBus eventBus)
    {
        _historyStore = historyStore;
        _userStatistics = userStatistics;
        _settings = settings;
        _boxArt = boxArt;

        _trendMetric = NormalizeMetric(settings.GetEnum(SettingsKeys.StreamTrendMetric, StreamTrendMetric.PeakViewers));
        _trendLength = NormalizeLength(settings.GetInt(SettingsKeys.StreamTrendLength, DefaultTrendLength));
        _isTrendVisible = settings.GetBool(SettingsKeys.StreamTrendVisible, true);
        _isSummaryExpanded = settings.GetBool(SettingsKeys.StreamSummaryExpanded, true);
        _listView = NormalizeListView(settings.GetEnum(SettingsKeys.StreamListView, StreamListView.Table));
        _sortKey = NormalizeSortKey(settings.GetEnum(SettingsKeys.StreamSortKey, StreamSortKey.StartedAt));
        _sortDescending = settings.GetBool(SettingsKeys.StreamSortDescending, true);

        foreach (var key in SortKeys)
        {
            SortOptions.Add(new(SortName(key), key == _sortKey, new RelayCommand(() => SetSortKey(key))));
        }

        foreach (var descending in new[] { true, false })
        {
            SortDirectionOptions.Add(new(
                DirectionName(descending),
                descending == _sortDescending,
                new RelayCommand(() => SetSortDirection(descending))));
        }

        foreach (var metric in TrendMetrics)
        {
            MetricOptions.Add(new(MetricName(metric), metric == _trendMetric, new RelayCommand(() => SetMetric(metric))));
        }

        foreach (var length in TrendLengths)
        {
            LengthOptions.Add(new(
                string.Create(UiCulture.Russian, $"{length:N0} {SessionTerm.ForCount(length)}"),
                length == _trendLength,
                new RelayCommand(() => SetLength(length))));
        }

        _subs.Add(eventBus.SubscribeOnUi<StreamSessionCompleted>(_ => RefreshCommand.Execute(null)));

        Refresh();
    }

    public event EventHandler<string>? UserRequested;

    public string PageTitle => "История стримов";
    public string? PageDescription => "Сессии стримов, их сегменты и чаттеры";

    public Task BoxArtLoading { get; private set; } = Task.CompletedTask;

    public ObservableCollection<StreamSessionRowViewModel> Sessions => _sessions;
    public ObservableCollection<StreamSessionRowViewModel> SortedSessions => _sortedSessions;
    public RangeObservableCollection<StreamTrendBarViewModel> Trend { get; } = [];
    public RangeObservableCollection<StreamSessionSegmentRowViewModel> Segments { get; } = [];
    public ObservableCollection<StreamSessionChatterRowViewModel> Chatters { get; } = [];
    public RangeObservableCollection<StreamCategoryRowViewModel> TopCategories { get; } = [];
    public RangeObservableCollection<StreamRecordCardViewModel> Records { get; } = [];
    public ObservableCollection<StreamTrendOptionViewModel> MetricOptions { get; } = [];
    public ObservableCollection<StreamTrendOptionViewModel> LengthOptions { get; } = [];
    public ObservableCollection<StreamTrendOptionViewModel> SortOptions { get; } = [];
    public ObservableCollection<StreamTrendOptionViewModel> SortDirectionOptions { get; } = [];

    public bool IsTableView
    {
        get => _listView != StreamListView.Cards;
        set
        {
            if (value)
            {
                SetListView(StreamListView.Table);
            }
        }
    }

    public bool IsCardsView
    {
        get => _listView == StreamListView.Cards;
        set
        {
            if (value)
            {
                SetListView(StreamListView.Cards);
            }
        }
    }

    public string SortCaption => string.Create(
        UiCulture.Russian,
        $"Сортировка: {SortName(_sortKey)}, {DirectionName(_sortDescending).ToLower(UiCulture.Russian)}");

    public TableSortIndicator StartedAtSortIndicator => IndicatorFor(StreamSortKey.StartedAt);
    public TableSortIndicator DurationSortIndicator => IndicatorFor(StreamSortKey.Duration);
    public TableSortIndicator TitleSortIndicator => IndicatorFor(StreamSortKey.Title);
    public TableSortIndicator GameSortIndicator => IndicatorFor(StreamSortKey.Game);
    public TableSortIndicator MessagesSortIndicator => IndicatorFor(StreamSortKey.Messages);
    public TableSortIndicator ChattersSortIndicator => IndicatorFor(StreamSortKey.Chatters);
    public TableSortIndicator PeakViewersSortIndicator => IndicatorFor(StreamSortKey.PeakViewers);
    public TableSortIndicator AverageViewersSortIndicator => IndicatorFor(StreamSortKey.AverageViewers);

    public bool ShowDetailDeltas => IsTableView
                                    && (DetailMessagesDelta.Length > 0
                                        || DetailChattersDelta.Length > 0
                                        || DetailPeakViewersDelta.Length > 0
                                        || DetailAverageViewersDelta.Length > 0);

    public bool HasSelection => SelectedRow is not null;

    public bool HasSessions => _allSessions.Count > 0 && !IsEverythingHidden;
    public bool IsEverythingHidden => _allSessions.Count > 0 && !ShowHidden && HiddenCount == _allSessions.Count;

    public string EmptyHeading => IsEverythingHidden
        ? "Все стримы убраны из статистики"
        : "Стримов пока нет";

    public string EmptyDescription => IsEverythingHidden
        ? "Список пуст: каждый стрим убран вручную. Кнопка со счётчиком убранных в панели сверху вернёт их на экран."
        : "Сессия появится здесь после первого завершённого стрима.";

    public int HiddenCount { get; private set; }
    public bool CanShowTrend => Trend.Count > 1;
    public bool HasTrend => IsTrendVisible && CanShowTrend;
    public bool HasTrendLabels => HasTrend && !IsCompactLayout;
    public bool HasTrendBaseline => HasTrendLabels;
    public bool HasWideSummary => !IsCompactLayout;
    public bool HasSegments => Segments.Count > 0;
    public bool HasSegmentTimeline => Segments.Count > 1;
    public bool HasChatters => Chatters.Count > 0;

    public bool HasGameFilter => GameFilter is { Length: > 0 };

    public bool HasHiddenSessions => HiddenCount > 0;

    public string HiddenToggleCaption => string.Create(UiCulture.Russian, $"Убранные: {HiddenCount:N0}");

    public string HiddenToggleHint => ShowHidden
        ? "Не показывать убранные стримы в списке"
        : "Показать убранные стримы в списке";

    public bool HasTopCategories => TopCategories.Count > 0;
    public bool HasRecords => Records.Count > 0;
    public bool HasSummaryStrip => HasTopCategories || HasRecords;
    public bool IsSummaryOpen => IsSummaryExpanded && HasSummaryStrip;

    public string SummaryToggleCaption => IsSummaryExpanded ? "Скрыть сводку" : "Показать сводку";

    public string TopCategoriesHeading => string.Create(UiCulture.Russian, $"Топ категорий ({TopCategories.Count:N0})");

    public string SummaryLine => string.Create(
        UiCulture.Russian,
        $"{TotalSessionsText} {TotalSessionsLabel} · {TotalAirTimeText} в эфире · {LastStreamText}");

    public string GameFilterText => HasGameFilter
        ? string.Create(UiCulture.Russian, $"Игра: {GameFilter}")
        : string.Empty;

    public string TrendCaption => string.Create(
        UiCulture.Russian,
        $"{MetricName(_trendMetric)}, последние {Trend.Count:N0} {SessionTerm.ForCount(Trend.Count)}");

    public string TrendVisibilityCaption => IsTrendVisible ? "Скрыть полосу" : "Показать полосу";

    public string SegmentsHeading => string.Create(UiCulture.Russian, $"Сегменты ({Segments.Count:N0})");
    public string ChattersHeading => string.Create(UiCulture.Russian, $"Чаттеры ({Chatters.Count:N0})");

    public static string MetricName(StreamTrendMetric metric)
    {
        return metric switch
        {
            StreamTrendMetric.AverageViewers => "Средние зрители",
            StreamTrendMetric.Messages => "Сообщения",
            StreamTrendMetric.Chatters => "Чаттеры",
            _ => "Пик зрителей",
        };
    }

    public static string SortName(StreamSortKey key)
    {
        return key switch
        {
            StreamSortKey.Duration => "Эфир",
            StreamSortKey.Title => "Название",
            StreamSortKey.Game => "Игра",
            StreamSortKey.Messages => "Сообщения",
            StreamSortKey.Chatters => "Чаттеры",
            StreamSortKey.PeakViewers => "Пик зрителей",
            StreamSortKey.AverageViewers => "Средние зрители",
            _ => "Начало",
        };
    }

    public static string DirectionName(bool descending)
    {
        return descending ? "По убыванию" : "По возрастанию";
    }

    public static string MetricLabel(StreamTrendMetric metric)
    {
        return metric switch
        {
            StreamTrendMetric.AverageViewers => "средние зрители",
            StreamTrendMetric.Messages => "сообщений",
            StreamTrendMetric.Chatters => "чаттеров",
            _ => "пик зрителей",
        };
    }

    public static long MetricValue(StreamSessionRowViewModel row, StreamTrendMetric metric)
    {
        return metric switch
        {
            StreamTrendMetric.AverageViewers => row.AverageViewers,
            StreamTrendMetric.Messages => row.MessageCount,
            StreamTrendMetric.Chatters => row.ChatterCount,
            _ => row.PeakViewers,
        };
    }

    public static string FormatDateTime(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", UiCulture.Russian);
    }

    public static string FormatDuration(TimeSpan span)
    {
        if (span.TotalDays >= 1)
        {
            return string.Create(UiCulture.Russian, $"{(int)span.TotalDays} д {span.Hours} ч {span.Minutes} мин");
        }

        if (span.TotalHours >= 1)
        {
            return string.Create(UiCulture.Russian, $"{span.Hours} ч {span.Minutes} мин");
        }

        if (span.TotalMinutes >= 1)
        {
            return string.Create(UiCulture.Russian, $"{span.Minutes} мин");
        }

        return string.Create(UiCulture.Russian, $"{span.Seconds} с");
    }

    public static string FormatAirTime(TimeSpan span)
    {
        return string.Create(UiCulture.Russian, $"{(int)span.TotalHours:N0} ч {span.Minutes} мин");
    }

    public static string FormatNumber(long value) => value.ToString("N0", UiCulture.Russian);

    public static string FormatSessions(int count)
    {
        return string.Create(UiCulture.Russian, $"{count:N0} {SessionTerm.ForCount(count)}");
    }

    public static string FormatShare(double share)
    {
        return string.Create(UiCulture.Russian, $"{share * 100:N0} %");
    }

    public static string RecordCaption(StreamRecordKind kind)
    {
        return kind switch
        {
            StreamRecordKind.PeakViewers => "рекорд зрителей",
            StreamRecordKind.Messages => "больше всего сообщений",
            StreamRecordKind.Chatters => "больше всего чаттеров",
            _ => "самый долгий стрим",
        };
    }

    public static string FormatPeriod(DateTimeOffset started, DateTimeOffset ended)
    {
        var from = started.ToLocalTime();
        var to = ended.ToLocalTime();

        return from.Date == to.Date
            ? string.Create(UiCulture.Russian, $"{from:dd.MM.yyyy}, {from:HH:mm} – {to:HH:mm}")
            : string.Create(UiCulture.Russian, $"{from:dd.MM.yyyy}, {from:HH:mm} – {to:dd.MM.yyyy}, {to:HH:mm}");
    }

    public static (string Text, TrendTone Tone) DescribeDelta(double value, double average, int sessionCount)
    {
        if (sessionCount < 2 || average <= 0)
        {
            return (string.Empty, TrendTone.None);
        }

        var percent = (value - average) / average * 100;

        if (Math.Abs(percent) < FlatDeltaPercent)
        {
            return ("как обычно", TrendTone.Flat);
        }

        var rounded = (int)Math.Round(percent, MidpointRounding.AwayFromZero);

        return rounded >= 0
            ? (string.Create(UiCulture.Russian, $"+{rounded:N0} % к обычному"), TrendTone.Up)
            : (string.Create(UiCulture.Russian, $"−{-rounded:N0} % к обычному"), TrendTone.Down);
    }

    public static string DescribeTracking(StreamSessionRecord session)
    {
        if (session.TrackedDuration is not { } tracked)
        {
            return string.Empty;
        }

        var duration = session.Duration;

        if (tracked <= TimeSpan.Zero)
        {
            return "Учёт не вёлся: бот не был подключён к чату";
        }

        if (tracked >= duration)
        {
            return "Учёт вёлся весь эфир";
        }

        var trackedText = FormatDuration(tracked);
        var durationText = FormatDuration(duration);

        return trackedText == durationText
            ? "Учёт вёлся почти весь эфир"
            : string.Create(UiCulture.Russian, $"Учёт вёлся {trackedText} из {durationText}");
    }

    public static string DescribeComposition(StreamSessionRecord session)
    {
        var titles = session.Segments
            .Select(segment => segment.Title)
            .Where(title => title is { Length: > 0 })
            .Distinct(StringComparer.Ordinal)
            .Count();

        var games = session.Segments
            .Select(segment => segment.Game)
            .Where(game => game is { Length: > 0 })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var parts = new List<string>(2);

        if (titles > 1)
        {
            parts.Add(string.Create(UiCulture.Russian, $"{titles:N0} {TitleTerm.ForCount(titles)}"));
        }

        if (games > 1)
        {
            parts.Add(string.Create(UiCulture.Russian, $"{games:N0} {CategoryTerm.ForCount(games)}"));
        }

        return string.Join(" · ", parts);
    }

    public void Dispose()
    {
        foreach (var sub in _subs)
        {
            sub.Dispose();
        }

        _subs.Clear();

        CancelBoxArt();
    }

    public void OnEnter()
    {
        Notice = null;
        ShowHidden = false;
    }

    public bool TrySelectAt(int index)
    {
        if (index < 0 || index >= _sessions.Count)
        {
            return false;
        }

        SelectedRow = _sessions[index];

        return true;
    }

    partial void OnSelectedRowChanged(StreamSessionRowViewModel? value)
    {
        if (_rebuildingSortedView && value is null)
        {
            return;
        }

        Segments.ReplaceAll(value?.Source is { } segmentsOf ? BuildSegments(segmentsOf) : []);
        Chatters.SyncTo(value?.Source is { } chattersOf ? BuildChatters(chattersOf) : []);
        RefreshSegmentFilterState();

        foreach (var bar in Trend)
        {
            bar.IsSelected = value is not null && ReferenceEquals(bar.Row, value);
        }

        UpdateDetails(value);

        OnPropertyChanged(nameof(HasSegments));
        OnPropertyChanged(nameof(HasSegmentTimeline));
        OnPropertyChanged(nameof(HasChatters));
        OnPropertyChanged(nameof(SegmentsHeading));
        OnPropertyChanged(nameof(ChattersHeading));
    }

    partial void OnIsSummaryExpandedChanged(bool value)
    {
        _settings.SetBool(SettingsKeys.StreamSummaryExpanded, value);
    }

    partial void OnShowHiddenChanged(bool value)
    {
        RebuildView();
    }

    partial void OnIsTrendVisibleChanged(bool value)
    {
        _settings.SetBool(SettingsKeys.StreamTrendVisible, value);
        OnPropertyChanged(nameof(HasTrend));
        OnPropertyChanged(nameof(HasTrendLabels));
        OnPropertyChanged(nameof(HasTrendBaseline));
    }

    [RelayCommand]
    private void SelectSession(StreamSessionRowViewModel? row)
    {
        if (row is not null)
        {
            SelectedRow = row;
        }
    }

    [RelayCommand]
    private void SortByColumn(StreamSortKey key)
    {
        if (key == StreamSortKey.None)
        {
            return;
        }

        if (_sortKey == key)
        {
            ApplySort(key, !_sortDescending);

            return;
        }

        ApplySort(key, key is not (StreamSortKey.Title or StreamSortKey.Game));
    }

    [RelayCommand]
    private void ToggleTrend()
    {
        IsTrendVisible = !IsTrendVisible;
    }

    [RelayCommand]
    private void ToggleSummary()
    {
        IsSummaryExpanded = !IsSummaryExpanded;
    }

    [RelayCommand]
    private void FilterByGame(object? source)
    {
        var key = source switch
        {
            StreamSessionSegmentRowViewModel segment => segment.GameKey,
            StreamCategoryRowViewModel category => category.Game,
            _ => null,
        };

        if (key is not { Length: > 0 } game)
        {
            return;
        }

        GameFilter = string.Equals(GameFilter, game, StringComparison.OrdinalIgnoreCase) ? null : game;
        RebuildView();
    }

    [RelayCommand]
    private void SetSessionHidden(StreamSessionRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var hide = !row.IsHidden;

        if (!_historyStore.TrySetHidden(row.Source.Id, hide))
        {
            Notice = hide
                ? "Стрим не удалось убрать из статистики. История осталась прежней, подробности в журнале."
                : "Стрим не удалось вернуть в статистику. История осталась прежней, подробности в журнале.";

            return;
        }

        Notice = null;

        Refresh();
    }

    [RelayCommand]
    private void ClearGameFilter()
    {
        if (!HasGameFilter)
        {
            return;
        }

        GameFilter = null;
        RebuildView();
    }

    [RelayCommand]
    private void OpenChatter(StreamSessionChatterRowViewModel? chatter)
    {
        if (chatter is { CanOpen: true })
        {
            UserRequested?.Invoke(this, chatter.UserId);
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        var history = _historyStore.Load();

        _allSessions.Clear();

        foreach (var record in history.Sessions.OrderByDescending(record => record.StartedAt))
        {
            _allSessions.Add(new(record) { BoxArt = _boxArt.For(record.Game) });
        }

        RebuildView();
    }

    private static StreamTrendMetric NormalizeMetric(StreamTrendMetric metric)
    {
        return metric == StreamTrendMetric.None ? StreamTrendMetric.PeakViewers : metric;
    }

    private static int NormalizeLength(int length)
    {
        return TrendLengths.Contains(length) ? length : DefaultTrendLength;
    }

    private static StreamListView NormalizeListView(StreamListView view)
    {
        return view == StreamListView.Cards ? StreamListView.Cards : StreamListView.Table;
    }

    private static StreamSortKey NormalizeSortKey(StreamSortKey key)
    {
        return SortKeys.Contains(key) ? key : StreamSortKey.StartedAt;
    }

    private static bool Matches(StreamSessionRowViewModel row, string game)
    {
        foreach (var segment in row.Source.Segments)
        {
            if (string.Equals(segment.Game, game, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return row.Source.Segments.Count == 0
               && string.Equals(row.Source.Game, game, StringComparison.OrdinalIgnoreCase);
    }

    private void SetMetric(StreamTrendMetric metric)
    {
        _trendMetric = NormalizeMetric(metric);
        _settings.SetEnum(SettingsKeys.StreamTrendMetric, _trendMetric);

        for (var index = 0; index < MetricOptions.Count; index++)
        {
            MetricOptions[index].IsChecked = TrendMetrics[index] == _trendMetric;
        }

        IsTrendVisible = true;
        BuildTrend();
    }

    private void SetListView(StreamListView view)
    {
        if (_listView == view)
        {
            return;
        }

        _listView = view;
        _settings.SetEnum(SettingsKeys.StreamListView, view);

        OnPropertyChanged(nameof(IsTableView));
        OnPropertyChanged(nameof(IsCardsView));
        OnPropertyChanged(nameof(ShowDetailDeltas));
    }

    private void SetSortKey(StreamSortKey key)
    {
        ApplySort(key, _sortDescending);
    }

    private void SetSortDirection(bool descending)
    {
        ApplySort(_sortKey, descending);
    }

    private void ApplySort(StreamSortKey key, bool descending)
    {
        _sortKey = NormalizeSortKey(key);
        _sortDescending = descending;

        _settings.SetEnum(SettingsKeys.StreamSortKey, _sortKey);
        _settings.SetBool(SettingsKeys.StreamSortDescending, _sortDescending);

        for (var index = 0; index < SortOptions.Count; index++)
        {
            SortOptions[index].IsChecked = SortKeys[index] == _sortKey;
        }

        foreach (var option in SortDirectionOptions)
        {
            option.IsChecked = string.Equals(option.Caption, DirectionName(_sortDescending), StringComparison.Ordinal);
        }

        RebuildSortedSessions();
        OnPropertyChanged(nameof(SortCaption));
        NotifySortIndicators();
    }

    private TableSortIndicator IndicatorFor(StreamSortKey key)
    {
        if (_sortKey != key)
        {
            return TableSortIndicator.None;
        }

        return _sortDescending ? TableSortIndicator.Descending : TableSortIndicator.Ascending;
    }

    private void NotifySortIndicators()
    {
        OnPropertyChanged(nameof(StartedAtSortIndicator));
        OnPropertyChanged(nameof(DurationSortIndicator));
        OnPropertyChanged(nameof(TitleSortIndicator));
        OnPropertyChanged(nameof(GameSortIndicator));
        OnPropertyChanged(nameof(MessagesSortIndicator));
        OnPropertyChanged(nameof(ChattersSortIndicator));
        OnPropertyChanged(nameof(PeakViewersSortIndicator));
        OnPropertyChanged(nameof(AverageViewersSortIndicator));
    }

    private void SetLength(int length)
    {
        _trendLength = NormalizeLength(length);
        _settings.SetInt(SettingsKeys.StreamTrendLength, _trendLength);

        for (var index = 0; index < LengthOptions.Count; index++)
        {
            LengthOptions[index].IsChecked = TrendLengths[index] == _trendLength;
        }

        IsTrendVisible = true;
        BuildTrend();
    }

    private void RebuildView()
    {
        var selectedId = SelectedRow?.Source.Id;
        var sessions = new List<StreamSessionRowViewModel>(_allSessions.Count);

        _visibleSessions.Clear();

        var hidden = _allSessions.Count(static row => row.IsHidden);

        foreach (var row in _allSessions)
        {
            if (HasGameFilter && !Matches(row, GameFilter!))
            {
                continue;
            }

            if (row.IsHidden)
            {
                if (ShowHidden)
                {
                    sessions.Add(row);
                }

                continue;
            }

            _visibleSessions.Add(row);
            sessions.Add(row);
        }

        _sessions.ReplaceAll(sessions);

        HiddenCount = hidden;

        OnPropertyChanged(nameof(HiddenCount));
        OnPropertyChanged(nameof(HasHiddenSessions));
        OnPropertyChanged(nameof(HiddenToggleCaption));

        UpdateAverages();
        UpdateRowDeltas();
        UpdateSummary();
        UpdateInsights();
        BuildTrend();
        RefreshBoxArt();
        RebuildSortedSessions();

        OnPropertyChanged(nameof(HasSessions));
        OnPropertyChanged(nameof(IsEverythingHidden));
        OnPropertyChanged(nameof(EmptyHeading));
        OnPropertyChanged(nameof(EmptyDescription));

        SelectedRow = selectedId.HasValue
            ? _sessions.FirstOrDefault(row => row.Source.Id == selectedId)
            : null;

        RefreshSegmentFilterState();
    }

    private void RebuildSortedSessions()
    {
        var selected = SelectedRow;

        _rebuildingSortedView = true;

        try
        {
            _sortedSessions.SyncTo(SortRows(_sessions).ToList());
        }
        finally
        {
            _rebuildingSortedView = false;
        }

        if (selected is not null && _sortedSessions.Contains(selected))
        {
            SelectedRow = selected;
        }
        else if (SelectedRow is null)
        {
            OnSelectedRowChanged(null);
        }
    }

    private IEnumerable<StreamSessionRowViewModel> SortRows(IEnumerable<StreamSessionRowViewModel> rows)
    {
        return _sortKey switch
        {
            StreamSortKey.Duration => Order(rows, row => row.Duration),
            StreamSortKey.Title => Order(rows, row => row.TitleFormatted, StringComparer.CurrentCultureIgnoreCase),
            StreamSortKey.Game => Order(rows, row => row.GameFormatted, StringComparer.CurrentCultureIgnoreCase),
            StreamSortKey.Messages => Order(rows, row => row.MessageCount),
            StreamSortKey.Chatters => Order(rows, row => row.ChatterCount),
            StreamSortKey.PeakViewers => Order(rows, row => row.PeakViewers),
            StreamSortKey.AverageViewers => Order(rows, row => row.AverageViewers),
            _ => Order(rows, row => row.StartedAt),
        };
    }

    private IOrderedEnumerable<StreamSessionRowViewModel> Order<TKey>(
        IEnumerable<StreamSessionRowViewModel> rows,
        Func<StreamSessionRowViewModel, TKey> selector,
        IComparer<TKey>? comparer = null)
    {
        return _sortDescending
            ? rows.OrderByDescending(selector, comparer)
            : rows.OrderBy(selector, comparer);
    }

    private void UpdateRowDeltas()
    {
        foreach (var row in _sessions)
        {
            row.ApplyDeltas(
                DescribeDelta(row.MessageCount, _averageMessages, _visibleSessions.Count),
                DescribeDelta(row.ChatterCount, _averageChatters, _visibleSessions.Count),
                DescribeDelta(row.PeakViewers, _averagePeakViewers, _visibleSessions.Count),
                DescribeDelta(row.AverageViewers, _averageViewers, _visibleSessions.Count));
        }
    }

    private void RefreshBoxArt()
    {
        CancelBoxArt();

        var games = CollectGames();

        if (games.Count == 0)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _boxArtCancellation = cancellation;

        BoxArtLoading = _boxArt.LoadAsync(games, cancellation.Token);
    }

    private void CancelBoxArt()
    {
        if (_boxArtCancellation is not { } cancellation)
        {
            return;
        }

        _boxArtCancellation = null;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private List<string> CollectGames()
    {
        var games = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in _sessions)
        {
            if (row.Source.Game is { Length: > 0 } game)
            {
                games.Add(game);
            }

            foreach (var segment in row.Source.Segments)
            {
                if (segment.Game is { Length: > 0 } segmentGame)
                {
                    games.Add(segmentGame);
                }
            }
        }

        return [.. games];
    }

    private List<StreamSessionSegmentRowViewModel> BuildSegments(StreamSessionRecord session)
    {
        var longest = session.Segments.Aggregate(TimeSpan.Zero, (max, segment) => segment.Duration > max ? segment.Duration : max);
        var segments = new List<StreamSessionSegmentRowViewModel>(session.Segments.Count);
        StreamSessionSegment? previous = null;

        foreach (var segment in session.Segments)
        {
            var share = longest > TimeSpan.Zero ? segment.Duration / longest : 1;

            var continuesGame = previous is not null
                                && segment.Game is { Length: > 0 }
                                && string.Equals(previous.Game, segment.Game, StringComparison.OrdinalIgnoreCase);

            segments.Add(new(segment, share, continuesGame) { BoxArt = _boxArt.For(segment.Game) });
            previous = segment;
        }

        return segments;
    }

    private void RefreshSegmentFilterState()
    {
        foreach (var segment in Segments)
        {
            segment.IsFiltered = HasGameFilter
                                 && segment.GameKey is { } game
                                 && string.Equals(game, GameFilter, StringComparison.OrdinalIgnoreCase);
        }
    }

    private List<StreamSessionChatterRowViewModel> BuildChatters(StreamSessionRecord session)
    {
        var ordered = session.Chatters
            .OrderByDescending(chatter => chatter.MessageCount)
            .ThenBy(chatter => chatter.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var leader = ordered.Count > 0 ? ordered[0].MessageCount : 0;
        var chatters = new List<StreamSessionChatterRowViewModel>(ordered.Count);

        for (var index = 0; index < ordered.Count; index++)
        {
            var known = ordered[index].UserId is { Length: > 0 } userId && _userStatistics.GetById(userId) is not null;

            chatters.Add(new(ordered[index], index + 1, leader, known));
        }

        return chatters;
    }

    private void UpdateDetails(StreamSessionRowViewModel? row)
    {
        if (row is null)
        {
            DetailTitle = Placeholder;
            IsDetailTitleMissing = false;
            DetailGame = Placeholder;
            DetailBoxArt = GameBoxArtViewModel.None;
            DetailComposition = string.Empty;
            DetailPeriodText = Placeholder;
            DetailTrackingText = string.Empty;
            DetailMessagesDelta = string.Empty;
            DetailChattersDelta = string.Empty;
            DetailPeakViewersDelta = string.Empty;
            DetailAverageViewersDelta = string.Empty;
            DetailMessagesDeltaTone = TrendTone.None;
            DetailChattersDeltaTone = TrendTone.None;
            DetailPeakViewersDeltaTone = TrendTone.None;
            DetailAverageViewersDeltaTone = TrendTone.None;
            return;
        }

        DetailTitle = row.TitleFormatted;
        IsDetailTitleMissing = !row.HasTitle;
        DetailGame = row.Source.Game is { Length: > 0 } game ? game : Placeholder;
        DetailBoxArt = row.BoxArt;
        DetailComposition = DescribeComposition(row.Source);
        DetailPeriodText = FormatPeriod(row.StartedAt, row.Source.EndedAt);
        DetailTrackingText = DescribeTracking(row.Source);

        (DetailMessagesDelta, DetailMessagesDeltaTone) = DescribeDelta(row.MessageCount, _averageMessages, _visibleSessions.Count);
        (DetailChattersDelta, DetailChattersDeltaTone) = DescribeDelta(row.ChatterCount, _averageChatters, _visibleSessions.Count);
        (DetailPeakViewersDelta, DetailPeakViewersDeltaTone) = DescribeDelta(row.PeakViewers, _averagePeakViewers, _visibleSessions.Count);
        (DetailAverageViewersDelta, DetailAverageViewersDeltaTone) = DescribeDelta(row.AverageViewers, _averageViewers, _visibleSessions.Count);
    }

    private void UpdateAverages()
    {
        if (_visibleSessions.Count == 0)
        {
            _averageMessages = 0;
            _averageChatters = 0;
            _averagePeakViewers = 0;
            _averageViewers = 0;
            return;
        }

        _averageMessages = _visibleSessions.Average(row => (double)row.MessageCount);
        _averageChatters = _visibleSessions.Average(row => (double)row.ChatterCount);
        _averagePeakViewers = _visibleSessions.Average(row => (double)row.PeakViewers);
        _averageViewers = _visibleSessions.Average(row => (double)row.AverageViewers);
    }

    private void UpdateSummary()
    {
        var totalDuration = _visibleSessions.Aggregate(TimeSpan.Zero, (sum, row) => sum + row.Duration);

        TotalSessionsText = FormatNumber(_visibleSessions.Count);
        TotalSessionsLabel = SessionTerm.ForCount(_visibleSessions.Count);
        TotalAirTimeText = _visibleSessions.Count > 0 ? FormatAirTime(totalDuration) : Placeholder;
        LastStreamText = _visibleSessions.Count > 0
            ? RelativeTime.Describe(_visibleSessions[0].StartedAt, DateTimeOffset.Now)
            : Placeholder;
    }

    private void UpdateInsights()
    {
        var summary = StreamHistorySummary.Build(_visibleSessions.Select(row => row.Source).ToList());

        var leaderAirTime = summary.Categories.Count > 0 ? summary.Categories[0].AirTime : TimeSpan.Zero;

        TopCategories.ReplaceAll(summary.Categories
            .Select((stat, index) => new StreamCategoryRowViewModel(stat, index + 1, leaderAirTime) { BoxArt = _boxArt.For(stat.Game) })
            .ToList());

        Records.ReplaceAll(summary.Records
            .Select(stat => new StreamRecordCardViewModel(stat, _visibleSessions.FirstOrDefault(row => row.Source.Id == stat.Session.Id)))
            .ToList());

        OnPropertyChanged(nameof(HasTopCategories));
        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(HasSummaryStrip));
        OnPropertyChanged(nameof(IsSummaryOpen));
        OnPropertyChanged(nameof(TopCategoriesHeading));
    }

    private void BuildTrend()
    {
        var recent = _visibleSessions.Take(_trendLength).Reverse().ToList();
        var leader = recent.Count > 0 ? recent.Max(row => MetricValue(row, _trendMetric)) : 0;
        var scale = StreamTrendBarViewModel.ScaleOf(recent.Select(row => MetricValue(row, _trendMetric)));
        var bars = new List<StreamTrendBarViewModel>(recent.Count);

        for (var index = 0; index < recent.Count; index++)
        {
            var bar = new StreamTrendBarViewModel(recent[index], scale, _trendMetric)
            {
                IsFirst = index == 0,
                IsLast = index == recent.Count - 1,
            };

            bar.IsPeak = leader > 0 && bar.Value == leader;
            bar.IsSelected = SelectedRow is not null && ReferenceEquals(bar.Row, SelectedRow);
            bars.Add(bar);
        }

        Trend.ReplaceAll(bars);

        TrendFirstDate = recent.Count > 0 ? RelativeTime.FormatDate(recent[0].StartedAt) : string.Empty;
        TrendLastDate = recent.Count > 0 ? RelativeTime.FormatDate(recent[^1].StartedAt) : string.Empty;

        OnPropertyChanged(nameof(CanShowTrend));
        OnPropertyChanged(nameof(HasTrend));
        OnPropertyChanged(nameof(HasTrendLabels));
        OnPropertyChanged(nameof(HasTrendBaseline));
        OnPropertyChanged(nameof(TrendCaption));
    }
}

public sealed partial class StreamTrendOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isChecked;

    public StreamTrendOptionViewModel(string caption, bool isChecked, IRelayCommand command)
    {
        Caption = caption;
        _isChecked = isChecked;
        Command = command;
    }

    public string Caption { get; }
    public IRelayCommand Command { get; }
}

public sealed partial class StreamSessionRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _messagesDelta = string.Empty;

    [ObservableProperty]
    private TrendTone _messagesDeltaTone;

    [ObservableProperty]
    private string _chattersDelta = string.Empty;

    [ObservableProperty]
    private TrendTone _chattersDeltaTone;

    [ObservableProperty]
    private string _peakViewersDelta = string.Empty;

    [ObservableProperty]
    private TrendTone _peakViewersDeltaTone;

    [ObservableProperty]
    private string _averageViewersDelta = string.Empty;

    [ObservableProperty]
    private TrendTone _averageViewersDeltaTone;

    public StreamSessionRecord Source { get; }

    public DateTimeOffset StartedAt => Source.StartedAt;
    public bool IsHidden => Source.IsHidden;
    public TimeSpan Duration => Source.Duration;
    public long MessageCount => Source.MessageCount;
    public int ChatterCount => Source.ChatterCount;
    public int PeakViewers => Source.PeakViewers;
    public int AverageViewers => Source.AverageViewers;

    public GameBoxArtViewModel BoxArt { get; init; } = GameBoxArtViewModel.None;

    public string HiddenMenuCaption => IsHidden ? "Вернуть в статистику" : "Убрать из статистики";

    public string HiddenHint => "Стрим убран из статистики. В сводке, средних и полосе зрителей его нет.";

    public bool HasTitle { get; }
    public string TitleFormatted { get; }
    public string StartedAtFormatted { get; }
    public string DurationFormatted { get; }
    public string GameFormatted { get; }
    public string GameCellText => GameFormatted;
    public string MessageCountFormatted { get; }
    public string ChatterCountFormatted { get; }
    public string PeakViewersFormatted { get; }
    public string AverageViewersFormatted { get; }

    public IReadOnlyList<StreamSessionStripeViewModel> Stripes { get; }

    public string CardMetaText { get; }
    public string CardSummary { get; }

    public StreamSessionRowViewModel(StreamSessionRecord source)
    {
        Source = source;
        HasTitle = source.Title is { Length: > 0 };
        TitleFormatted = HasTitle ? source.Title! : "Без названия";
        StartedAtFormatted = StreamHistoryPageViewModel.FormatDateTime(source.StartedAt);
        DurationFormatted = StreamHistoryPageViewModel.FormatDuration(source.Duration);
        GameFormatted = BuildGameDisplay(source);
        MessageCountFormatted = StreamHistoryPageViewModel.FormatNumber(source.MessageCount);
        ChatterCountFormatted = StreamHistoryPageViewModel.FormatNumber(source.ChatterCount);
        PeakViewersFormatted = StreamHistoryPageViewModel.FormatNumber(source.PeakViewers);
        AverageViewersFormatted = StreamHistoryPageViewModel.FormatNumber(source.AverageViewers);
        Stripes = BuildStripes(source);

        CardMetaText = string.Create(UiCulture.Russian, $"{StartedAtFormatted} · {DurationFormatted}");

        CardSummary = string.Create(
            UiCulture.Russian,
            $"{TitleFormatted}, {GameFormatted}, {StartedAtFormatted}, эфир {DurationFormatted}, "
            + $"сообщений {MessageCountFormatted}, чаттеров {ChatterCountFormatted}, "
            + $"пик {PeakViewersFormatted}, средние {AverageViewersFormatted}");
    }

    public void ApplyDeltas(
        (string Text, TrendTone Tone) messages,
        (string Text, TrendTone Tone) chatters,
        (string Text, TrendTone Tone) peakViewers,
        (string Text, TrendTone Tone) averageViewers)
    {
        (MessagesDelta, MessagesDeltaTone) = messages;
        (ChattersDelta, ChattersDeltaTone) = chatters;
        (PeakViewersDelta, PeakViewersDeltaTone) = peakViewers;
        (AverageViewersDelta, AverageViewersDeltaTone) = averageViewers;
    }

    private static IReadOnlyList<StreamSessionStripeViewModel> BuildStripes(StreamSessionRecord source)
    {
        var total = source.Segments.Aggregate(TimeSpan.Zero, (sum, segment) => sum + segment.Duration);

        if (source.Segments.Count == 0 || total <= TimeSpan.Zero)
        {
            return [new(source.Game, 1)];
        }

        var stripes = new List<StreamSessionStripeViewModel>(source.Segments.Count);

        foreach (var segment in source.Segments)
        {
            stripes.Add(new(segment.Game, segment.Duration / total));
        }

        return stripes;
    }

    private static string BuildGameDisplay(StreamSessionRecord source)
    {
        var game = string.IsNullOrEmpty(source.Game) ? "–" : source.Game;

        var extra = source.Segments
            .Select(segment => segment.Game)
            .Where(value => value is { Length: > 0 })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() - 1;

        return extra > 0 ? string.Create(UiCulture.Russian, $"{game} (+{extra:N0})") : game;
    }
}

public sealed class StreamSessionStripeViewModel
{
    public double Share { get; }
    public int PaletteIndex { get; }

    public StreamSessionStripeViewModel(string? game, double share)
    {
        PaletteIndex = StreamSegmentPalette.IndexOf(game);
        Share = share;
    }
}

public sealed partial class StreamTrendBarViewModel : ObservableObject
{
    public const double MinimumShare = 0.06;

    private const int ScalePercentile = 90;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isPeak;

    public StreamTrendBarViewModel(StreamSessionRowViewModel row, long scale, StreamTrendMetric metric)
    {
        Row = row;
        Value = StreamHistoryPageViewModel.MetricValue(row, metric);
        Share = scale > 0
            ? Math.Clamp((double)Value / scale, MinimumShare, 1)
            : MinimumShare;
        IsClipped = scale > 0 && Value > scale;
        FillTrack = new(Share, GridUnitType.Star);
        RestTrack = new(1 - Share, GridUnitType.Star);
        ValueText = StreamHistoryPageViewModel.FormatNumber(Value);
        Label = string.Create(
            UiCulture.Russian,
            $"{RelativeTime.FormatDate(row.StartedAt)}, {StreamHistoryPageViewModel.MetricLabel(metric)} {ValueText}{(IsClipped ? ", выше шкалы полосы" : string.Empty)}");
    }

    public StreamSessionRowViewModel Row { get; }
    public bool IsFirst { get; init; }
    public bool IsLast { get; init; }
    public long Value { get; }
    public double Share { get; }
    public bool IsClipped { get; }
    public GridLength FillTrack { get; }
    public GridLength RestTrack { get; }
    public string ValueText { get; }
    public string Label { get; }

    public static long ScaleOf(IEnumerable<long> values)
    {
        var positive = values.Where(value => value > 0).Order().ToList();

        if (positive.Count == 0)
        {
            return 0;
        }

        var rank = (positive.Count * ScalePercentile + 99) / 100;
        return positive[rank - 1];
    }
}

public sealed partial class StreamSessionSegmentRowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterCaption))]
    private bool _isFiltered;

    public GameBoxArtViewModel BoxArt { get; init; } = GameBoxArtViewModel.None;

    public string Game { get; }
    public string GameCellText => Game;
    public string Title { get; }
    public string? GameKey { get; }
    public string Duration { get; }
    public string MessagesText { get; }
    public string PeakViewersText { get; }
    public string AverageViewersText { get; }
    public double Share { get; }
    public int PaletteIndex { get; }
    public bool ContinuesGame { get; }
    public bool CanFilter { get; }
    public string Caption { get; }
    public string TimelineText { get; }

    public string FilterCaption => IsFiltered
        ? string.Create(UiCulture.Russian, $"Снять фильтр по игре «{Game}»")
        : string.Create(UiCulture.Russian, $"Фильтровать по игре «{Game}»");

    public StreamSessionSegmentRowViewModel(StreamSessionSegment segment, double share, bool continuesGame)
    {
        GameKey = segment.Game is { Length: > 0 } key ? key : null;
        Game = GameKey ?? "–";
        Title = segment.Title is { Length: > 0 } title ? title : "Без названия";
        Duration = StreamHistoryPageViewModel.FormatDuration(segment.Duration);

        var messages = StreamHistoryPageViewModel.FormatNumber(segment.MessageCount);
        var peakViewers = StreamHistoryPageViewModel.FormatNumber(segment.PeakViewers);
        var averageViewers = StreamHistoryPageViewModel.FormatNumber(segment.AverageViewers);

        MessagesText = string.Create(UiCulture.Russian, $"{messages} сообщ.");
        PeakViewersText = string.Create(UiCulture.Russian, $"пик {peakViewers}");
        AverageViewersText = string.Create(UiCulture.Russian, $"средн. {averageViewers}");
        Share = share;
        PaletteIndex = StreamSegmentPalette.IndexOf(GameKey);
        ContinuesGame = continuesGame;
        CanFilter = GameKey is not null;
        Caption = continuesGame ? Title : Game;
        TimelineText = string.Create(
            UiCulture.Russian,
            $"{Game} · {Title}, {Duration} · {MessagesText} · {PeakViewersText}");
    }
}

public sealed class StreamCategoryRowViewModel
{
    public GameBoxArtViewModel BoxArt { get; init; } = GameBoxArtViewModel.None;

    public string Game { get; }
    public int Position { get; }
    public bool IsTopThree { get; }
    public double Share { get; }
    public string AirTimeText { get; }
    public string SessionsText { get; }
    public string ShareText { get; }
    public string Summary { get; }

    public StreamCategoryRowViewModel(StreamCategoryStat stat, int position, TimeSpan leaderAirTime)
    {
        Game = stat.Game;
        Position = position;
        IsTopThree = position is > 0 and <= 3;
        Share = leaderAirTime > TimeSpan.Zero
            ? Math.Clamp(stat.AirTime / leaderAirTime, UserStatisticsRanking.MinimumShare, 1)
            : UserStatisticsRanking.MinimumShare;
        AirTimeText = StreamHistoryPageViewModel.FormatAirTime(stat.AirTime);
        SessionsText = StreamHistoryPageViewModel.FormatSessions(stat.SessionCount);
        ShareText = StreamHistoryPageViewModel.FormatShare(stat.Share);
        Summary = string.Create(
            UiCulture.Russian,
            $"{position:N0}. {Game}, {AirTimeText} в эфире, {SessionsText}, {ShareText} эфира. Отфильтровать историю по этой игре");
    }
}

public sealed class StreamRecordCardViewModel
{
    public StreamRecordKind Kind { get; }
    public StreamSessionRowViewModel? Row { get; }
    public bool CanSelect { get; }
    public string Caption { get; }
    public string ValueText { get; }
    public string DateText { get; }
    public string TitleText { get; }
    public string Summary { get; }

    public StreamRecordCardViewModel(StreamRecordStat stat, StreamSessionRowViewModel? row)
    {
        Kind = stat.Kind;
        Row = row;
        CanSelect = row is not null;
        Caption = StreamHistoryPageViewModel.RecordCaption(stat.Kind);
        ValueText = stat.Kind == StreamRecordKind.Duration
            ? StreamHistoryPageViewModel.FormatDuration(stat.Session.Duration)
            : StreamHistoryPageViewModel.FormatNumber(stat.Value);
        DateText = RelativeTime.FormatDate(stat.Session.StartedAt);
        TitleText = stat.Session.Title is { Length: > 0 } title ? title : "Без названия";

        var summary = string.Create(UiCulture.Russian, $"{Caption}: {ValueText}. {DateText}, {TitleText}");

        Summary = CanSelect
            ? string.Create(UiCulture.Russian, $"{summary}. Выбрать этот стрим в таблице")
            : summary;
    }
}

public sealed class StreamSessionChatterRowViewModel
{
    public string UserId { get; }
    public string DisplayName { get; }
    public long MessageCount { get; }
    public string MessageCountFormatted { get; }
    public int Position { get; }
    public double Share { get; }
    public bool IsTopThree { get; }
    public bool CanOpen { get; }
    public string Summary { get; }
    public string Hint { get; }

    public StreamSessionChatterRowViewModel(
        StreamSessionChatter chatter,
        int position,
        long leaderMessageCount,
        bool canOpen)
    {
        UserId = chatter.UserId;
        DisplayName = chatter.DisplayName;
        MessageCount = chatter.MessageCount;
        MessageCountFormatted = StreamHistoryPageViewModel.FormatNumber(chatter.MessageCount);
        Position = position;
        IsTopThree = position is > 0 and <= 3;
        CanOpen = canOpen;
        Share = leaderMessageCount > 0
            ? Math.Clamp((double)chatter.MessageCount / leaderMessageCount, UserStatisticsRanking.MinimumShare, 1)
            : UserStatisticsRanking.MinimumShare;
        Summary = string.Create(
            UiCulture.Russian,
            $"{position:N0}. {DisplayName}, {MessageCountFormatted} {ChatMessageTerm.Instance.ForCount(chatter.MessageCount)}");
        Hint = canOpen
            ? string.Create(UiCulture.Russian, $"{Summary}. Открыть в разделе «Пользователи»")
            : string.Create(UiCulture.Russian, $"{Summary}. В статистике пользователей этой записи нет");
    }
}
