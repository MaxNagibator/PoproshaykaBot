using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Moderation;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Users;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class UserStatisticsPageViewModel : ObservableObject, IPageHeader, IDisposable
{
    public const long MaxAdjustment = 1_000_000;

    private readonly IUserStatisticsRepository _userStatistics;
    private readonly StatisticsAutoSaver _statisticsAutoSaver;
    private readonly UserRankService _userRankService;
    private readonly UserPointsManagementService _pointsManagement;
    private readonly IChannelProvider _channelProvider;
    private readonly SettingsManager _settingsManager;
    private readonly IDialogService _dialogService;
    private readonly ISettingsStore _settings;
    private readonly List<UserStatisticsRowViewModel> _allRows = [];
    private readonly ObservableCollection<UserStatisticsRowViewModel> _rows = [];
    private readonly List<IDisposable> _subscriptions = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedRow))]
    [NotifyPropertyChangedFor(nameof(SelectedPlaceText))]
    [NotifyCanExecuteChangedFor(nameof(ApplyAdjustmentCommand))]
    [NotifyPropertyChangedFor(nameof(RankLadderSteps))]
    [NotifyPropertyChangedFor(nameof(RankLadderToggleText))]
    private UserStatisticsRowViewModel? _selectedRow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RankLadderSteps))]
    [NotifyPropertyChangedFor(nameof(RankLadderToggleText))]
    private bool _isRankLadderExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BalanceToggleCaption))]
    private bool _isBalanceExpanded;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyActionCommand))]
    private bool _isFilterActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PointsSortIndicator))]
    [NotifyPropertyChangedFor(nameof(MessagesSortIndicator))]
    [NotifyPropertyChangedFor(nameof(NameSortIndicator))]
    [NotifyPropertyChangedFor(nameof(RankSortIndicator))]
    private UserStatisticsSortKey _sortKey = UserStatisticsSortKey.Points;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PointsSortIndicator))]
    [NotifyPropertyChangedFor(nameof(MessagesSortIndicator))]
    [NotifyPropertyChangedFor(nameof(NameSortIndicator))]
    [NotifyPropertyChangedFor(nameof(RankSortIndicator))]
    private bool _sortDescending = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AdjustmentAmount))]
    [NotifyPropertyChangedFor(nameof(AdjustmentError))]
    [NotifyPropertyChangedFor(nameof(HasAdjustmentError))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    [NotifyCanExecuteChangedFor(nameof(ApplyAdjustmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(IncreaseAdjustmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseAdjustmentCommand))]
    private string _adjustmentText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyAdjustmentCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _totalUsers = "0";

    [ObservableProperty]
    private string _totalMessages = "0";

    [ObservableProperty]
    private string _totalPoints = "0";

    [ObservableProperty]
    private string _totalBonus = "0";

    [ObservableProperty]
    private string _totalPenalty = "0";

    [ObservableProperty]
    private bool _hasTotalBonus;

    [ObservableProperty]
    private bool _hasTotalPenalty;

    public UserStatisticsPageViewModel(
        IUserStatisticsRepository userStatistics,
        StatisticsAutoSaver statisticsAutoSaver,
        UserRankService userRankService,
        UserPointsManagementService pointsManagement,
        IChannelProvider channelProvider,
        SettingsManager settingsManager,
        IDialogService dialogService,
        IEventBus eventBus,
        ISettingsStore settings)
    {
        _userStatistics = userStatistics;
        _statisticsAutoSaver = statisticsAutoSaver;
        _userRankService = userRankService;
        _pointsManagement = pointsManagement;
        _channelProvider = channelProvider;
        _settingsManager = settingsManager;
        _dialogService = dialogService;
        _settings = settings;
        _isBalanceExpanded = settings.GetBool(SettingsKeys.UserBalanceExpanded, true);

        _subscriptions.Add(eventBus.SubscribeOnUi<UserPunished>(_ => Reload()));
        _subscriptions.Add(eventBus.SubscribeOnUi<UserRewarded>(_ => Reload()));

        Reload();
    }

    public string PageTitle => "Пользователи";

    public string? PageDescription => "Статистика сообщений, баллов и рангов";

    public ObservableCollection<UserStatisticsRowViewModel> Users => _rows;

    public bool HasUsers => _rows.Count > 0;

    public string EmptyHeading => IsFilterActive ? "Ничего не найдено" : "Статистики пока нет";

    public string EmptyDescription => IsFilterActive
        ? "Ни один пользователь не подходит под фильтр. Очистите поиск, чтобы увидеть всех."
        : "Пользователи появятся здесь, когда бот начнёт собирать сообщения чата.";

    public bool HasSelectedRow => SelectedRow is not null;

    public string? EmptyActionText => "Сбросить поиск";

    public IRelayCommand? EmptyActionCommand => IsFilterActive ? ClearFilterCommand : null;

    public TableSortIndicator PointsSortIndicator => IndicatorFor(UserStatisticsSortKey.Points);

    public TableSortIndicator MessagesSortIndicator => IndicatorFor(UserStatisticsSortKey.Messages);

    public TableSortIndicator NameSortIndicator => IndicatorFor(UserStatisticsSortKey.Name);

    public TableSortIndicator RankSortIndicator => IndicatorFor(UserStatisticsSortKey.Rank);

    public string VisibleCountText => IsFilterActive
        ? UserStatisticsRanking.DescribeVisibleCount(_rows.Count, _allRows.Count)
        : string.Empty;

    public string SelectedPlaceText => SelectedRow is { } row
        ? UserStatisticsRanking.DescribePlace(row.Position, _rows.Count)
        : string.Empty;

    public IReadOnlyList<UserRankLadderStep> RankLadderSteps => SelectedRow?.RankLadder?.Arrange(IsRankLadderExpanded) ?? [];

    public string RankLadderToggleText => IsRankLadderExpanded
        ? "Свернуть"
        : SelectedRow?.RankLadder?.ExpandText ?? string.Empty;

    public string BalanceToggleCaption => IsBalanceExpanded ? "Свернуть блок «Баланс»" : "Развернуть блок «Баланс»";

    public long AdjustmentAmount => ParseAdjustment(AdjustmentText).Amount;

    public string? AdjustmentError => ParseAdjustment(AdjustmentText).Error;

    public bool HasAdjustmentError => AdjustmentError is not null;

    public string ActionButtonText
    {
        get
        {
            var delta = AdjustmentAmount;
            var term = _userRankService.PointTerm;
            return delta switch
            {
                > 0 => $"Добавить {delta} {term.ForCount(delta)}",
                < 0 => $"Убрать {-delta} {term.ForCount(-delta)}",
                _ => "Изменить баланс",
            };
        }
    }

    public bool TrySelectAt(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return false;
        }

        SelectedRow = _rows[index];

        return true;
    }

    public bool TrySelect(string? userId)
    {
        if (userId is not { Length: > 0 })
        {
            return false;
        }

        if (_allRows.All(row => !string.Equals(row.UserId, userId, StringComparison.Ordinal)))
        {
            return false;
        }

        if (_rows.All(row => !string.Equals(row.UserId, userId, StringComparison.Ordinal)))
        {
            FilterText = string.Empty;
        }

        SelectedRow = _rows.FirstOrDefault(row => string.Equals(row.UserId, userId, StringComparison.Ordinal));

        return SelectedRow is not null;
    }

    partial void OnFilterTextChanged(string value)
    {
        RebuildView();
    }

    private void RebuildView()
    {
        var selectedId = SelectedRow?.UserId;
        var trimmed = FilterText.Trim();
        var hasFilter = trimmed.Length > 0;
        var visible = new List<UserStatisticsRowViewModel>(_allRows.Count);

        foreach (var row in _allRows)
        {
            if (hasFilter
                && !row.Name.Contains(trimmed, StringComparison.InvariantCultureIgnoreCase)
                && !row.UserId.Contains(trimmed, StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            visible.Add(row);
        }

        _rows.SyncTo(UserStatisticsRanking.Arrange(visible, SortKey, SortDescending).ToList());

        SelectedRow = selectedId is not null
            ? _rows.FirstOrDefault(row => row.UserId == selectedId)
            : null;

        IsFilterActive = hasFilter;
        OnPropertyChanged(nameof(HasUsers));
        OnPropertyChanged(nameof(EmptyHeading));
        OnPropertyChanged(nameof(EmptyDescription));
        OnPropertyChanged(nameof(VisibleCountText));
        OnPropertyChanged(nameof(SelectedPlaceText));
    }

    private TableSortIndicator IndicatorFor(UserStatisticsSortKey key)
    {
        if (SortKey != key)
        {
            return TableSortIndicator.None;
        }

        return SortDescending ? TableSortIndicator.Descending : TableSortIndicator.Ascending;
    }

    [RelayCommand]
    private void SortBy(UserStatisticsSortKey key)
    {
        if (key == UserStatisticsSortKey.None)
        {
            return;
        }

        if (SortKey == key)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortKey = key;
            SortDescending = key != UserStatisticsSortKey.Name;
        }

        OnPropertyChanged(nameof(SortKey));
        RebuildView();
    }

    [RelayCommand]
    private void ClearFilter() => FilterText = string.Empty;

    [RelayCommand]
    private void ToggleRankLadder() => IsRankLadderExpanded = !IsRankLadderExpanded;

    [RelayCommand]
    private void ToggleBalance() => IsBalanceExpanded = !IsBalanceExpanded;

    partial void OnIsBalanceExpandedChanged(bool value)
    {
        _settings.SetBool(SettingsKeys.UserBalanceExpanded, value);
    }

    [RelayCommand]
    private void SetAdjustment(double amount) => SetAdjustmentAmount((long)amount);

    [RelayCommand]
    private void Refresh() => Reload();

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private async Task ApplyAdjustmentAsync()
    {
        if (SelectedRow is null)
        {
            return;
        }

        var row = SelectedRow;
        var (delta, error) = ParseAdjustment(AdjustmentText);

        if (delta == 0 || error is not null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            bool updated;

            if (delta < 0)
            {
                var amount = (ulong)-delta;
                var notification = _pointsManagement.GetPunishmentNotification(row.Name, amount);
                _dialogService.Info("Наказание", notification);
                updated = await _pointsManagement.PunishUserAsync(row.UserId, row.Name, amount, _channelProvider.Channel);
            }
            else
            {
                var amount = (ulong)delta;
                var notification = _pointsManagement.GetRewardNotification(row.Name, amount);
                _dialogService.Info("Поощрение", notification);
                updated = await _pointsManagement.RewardUserAsync(row.UserId, row.Name, amount, _channelProvider.Channel);
            }

            if (!updated)
            {
                _dialogService.Warning("Ошибка", "Пользователь не найден в статистике.");
                return;
            }

            Reload();

            try
            {
                await _statisticsAutoSaver.SaveNowAsync();
            }
            catch (Exception ex)
            {
                _dialogService.Error("Ошибка сохранения", $"Не удалось сохранить статистику: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            var verb = delta < 0 ? "наказать" : "поощрить";
            _dialogService.Error("Ошибка", $"Не удалось {verb} пользователя: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanApplyAdjustment() => SelectedRow is not null && AdjustmentAmount != 0 && !HasAdjustmentError && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanIncreaseAdjustment))]
    private void IncreaseAdjustment() => SetAdjustmentAmount(AdjustmentAmount + 1);

    private bool CanIncreaseAdjustment() => !HasAdjustmentError && AdjustmentAmount < MaxAdjustment;

    [RelayCommand(CanExecute = nameof(CanDecreaseAdjustment))]
    private void DecreaseAdjustment() => SetAdjustmentAmount(AdjustmentAmount - 1);

    private bool CanDecreaseAdjustment() => !HasAdjustmentError && AdjustmentAmount > -MaxAdjustment;

    private void SetAdjustmentAmount(long amount)
    {
        AdjustmentText = amount == 0
            ? string.Empty
            : amount.ToString(CultureInfo.InvariantCulture);
    }

    private static (long Amount, string? Error) ParseAdjustment(string? text)
    {
        var normalized = new StringBuilder(text?.Length ?? 0);

        foreach (var symbol in text ?? string.Empty)
        {
            if (char.IsWhiteSpace(symbol))
            {
                continue;
            }

            normalized.Append(symbol == '−' ? '-' : symbol);
        }

        if (normalized.Length == 0)
        {
            return (0, null);
        }

        var candidate = normalized.ToString();
        var digits = candidate.TrimStart('+', '-');

        if (candidate is "+" or "-")
        {
            return (0, null);
        }

        var isInteger = candidate.Length - digits.Length <= 1 && digits.Length > 0 && digits.All(char.IsAsciiDigit);

        if (!isInteger)
        {
            return (0, "Введите целое число, например 50 или −20.");
        }

        if (!long.TryParse(candidate, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)
            || amount is > MaxAdjustment or < -MaxAdjustment)
        {
            return (0, "За один раз можно изменить баланс не больше чем на 1 000 000.");
        }

        return (amount, null);
    }

    [RelayCommand]
    private async Task EditPointTermAsync()
    {
        var dialog = new PointTermDialogViewModel(_settingsManager.Current.Ranks.PointTerm);

        if (!await _dialogService.ShowAsync(dialog))
        {
            return;
        }

        try
        {
            var written = _settingsManager.Mutate(settings => settings.Ranks.PointTerm = dialog.BuildResult());
            OnPropertyChanged(nameof(ActionButtonText));
            Reload();

            if (!written)
            {
                _dialogService.Warning("Названия баллов",
                    "Названия применены и работают до перезапуска. В файл они не записаны: туда только что перенесены данные предыдущей версии. Перезапустите приложение и задайте названия ещё раз.");
            }
        }
        catch (Exception ex)
        {
            _dialogService.Error("Ошибка сохранения", $"Не удалось сохранить названия баллов: {ex.Message}");
        }
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }

        _subscriptions.Clear();
    }

    private void Reload()
    {
        var all = _userStatistics.GetAll();

        _allRows.Clear();

        long totalMessages = 0;
        long totalPoints = 0;
        long totalBonus = 0;
        long totalPenalty = 0;

        var pointTerm = _userRankService.PointTerm;
        var ranks = _settingsManager.Current.Ranks.Ranks.ToArray();

        foreach (var user in all)
        {
            var rank = _userRankService.GetRank(user.Points);
            var standing = UserRankStanding.Create(user.Points, _userRankService.GetRankDisplay(user.Points), rank, ranks);
            _allRows.Add(new UserStatisticsRowViewModel(user, standing, pointTerm));

            totalMessages += (long)user.MessageCount;
            totalPoints += user.Points;
            totalBonus += (long)user.BonusPoints;
            totalPenalty += (long)user.PenaltyPoints;
        }

        TotalUsers = all.Count.ToString("N0", UiCulture.Russian);
        TotalMessages = totalMessages.ToString("N0", UiCulture.Russian);
        TotalPoints = totalPoints.ToString("N0", UiCulture.Russian);
        TotalBonus = totalBonus > 0 ? string.Create(UiCulture.Russian, $"+{totalBonus:N0}") : "0";
        TotalPenalty = totalPenalty > 0 ? string.Create(UiCulture.Russian, $"−{totalPenalty:N0}") : "0";
        HasTotalBonus = totalBonus > 0;
        HasTotalPenalty = totalPenalty > 0;

        RebuildView();
    }
}
