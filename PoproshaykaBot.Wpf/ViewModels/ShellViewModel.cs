using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using System.ComponentModel;
using System.Windows;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class ShellViewModel : ShellViewModelBase, IDisposable
{
    private readonly BotConnectionManager _connectionManager;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly ShellPreferences _preferences;
    private readonly IDialogService _dialogService;
    private readonly List<IDisposable> _subscriptions = [];
    private readonly NavigationItem _settingsSection;
    private readonly SettingsPageViewModel _settingsPage;
    private readonly ChatHistoryManager _chatHistory;
    private readonly ISettingsStore _uiSettings;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly NavigationItem _statisticsSection;
    private readonly NavigationItem _streamHistorySection;
    private readonly NavigationItem _commandsSection;
    private readonly UserStatisticsPageViewModel _statisticsPage;
    private readonly StreamHistoryPageViewModel _streamHistoryPage;
    private readonly NavigationItem _overviewSection;
    private readonly IUnsavedChangesPrompt _unsavedChangesPrompt;
    private NavigationItem? _current;
    private bool _returningToPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleConnectCommand))]
    private BotLifecyclePhase _phase;

    public ShellViewModel(
        ModalHostViewModel modal,
        DashboardViewModel overview,
        SettingsPageViewModel settingsPage,
        UserStatisticsPageViewModel statisticsPage,
        StreamHistoryPageViewModel streamHistoryPage,
        CommandsPageViewModel commandsPage,
        LogsViewModel logsPage,
        DiagnosticsViewModel diagnosticsPage,
        ThemeViewModel theme,
        ShellPreferences preferences,
        BotConnectionManager connectionManager,
        IEventBus eventBus,
        ILogger<ShellViewModel> logger,
        IDialogService dialogService,
        UpdateBannerViewModel updateBanner,
        OnboardingBannerViewModel onboardingBanner,
        DebugBannerViewModel debugBanner,
        StreamMonitoringViewModel streamMonitoring,
        ChatHistoryManager chatHistory,
        ISettingsStore uiSettings,
        IUiDispatcher uiDispatcher,
        IUnsavedChangesPrompt unsavedChangesPrompt)
        : base(modal)
    {
        _unsavedChangesPrompt = unsavedChangesPrompt;
        _connectionManager = connectionManager;
        _logger = logger;
        _preferences = preferences;
        _preferences.PropertyChanged += OnPreferencesPropertyChanged;
        _dialogService = dialogService;
        Theme = theme;
        UpdateBanner = updateBanner;
        OnboardingBanner = onboardingBanner;
        DebugBanner = debugBanner;
        StreamMonitoring = streamMonitoring;

        Overview = overview;

        _settingsPage = settingsPage;
        _settingsPage.SettingsSaved += OnSettingsSaved;
        _chatHistory = chatHistory;
        _uiSettings = uiSettings;
        _uiDispatcher = uiDispatcher;
        _uiSettings.WriteFailed += OnUiSettingsWriteFailed;
        _settingsSection = new("Настройки", PackIconLucideKind.Settings, settingsPage, activate: GuardReturn(settingsPage.OnEnter), key: SectionKeys.Settings);

        _statisticsPage = statisticsPage;
        _statisticsSection = new("Пользователи", PackIconLucideKind.Users, statisticsPage, key: SectionKeys.Users) { StartsGroup = true };
        _streamHistorySection = new(
            "История стримов",
            PackIconLucideKind.History,
            streamHistoryPage,
            activate: streamHistoryPage.OnEnter,
            key: SectionKeys.Streams);
        _commandsSection = new("Команды", PackIconLucideKind.Terminal, commandsPage, activate: GuardReturn(commandsPage.OnEnter), key: SectionKeys.Commands);

        _streamHistoryPage = streamHistoryPage;
        _streamHistoryPage.UserRequested += OnUserRequested;

        _overviewSection = new("Обзор", PackIconLucideKind.LayoutDashboard, overview, activate: overview.OnEnter, key: SectionKeys.Overview)
        {
            KeepAlive = true,
        };

        Sections.Add(_overviewSection);
        Sections.Add(_statisticsSection);
        Sections.Add(_streamHistorySection);
        Sections.Add(_commandsSection);
        Sections.Add(new("Логи", PackIconLucideKind.ScrollText, logsPage, key: SectionKeys.Logs) { StartsGroup = true });
        Sections.Add(new("Диагностика", PackIconLucideKind.Gauge, diagnosticsPage, key: SectionKeys.Diagnostics));

        IsNavCollapsed = _preferences.NavCollapsed;

        Selected = FindSectionByKey(_preferences.LastPage) ?? Sections[0];

        _subscriptions.Add(eventBus.SubscribeOnUi<BotLifecyclePhaseChanged>(OnLifecyclePhaseChanged));
        _subscriptions.Add(eventBus.SubscribeOnUi<BotConnectionStatusUpdated>(statusEvent => SetStatus(statusEvent.Message, StatusSeverity.Info)));

        ApplyPhase(_connectionManager.CurrentPhase);
    }

    public ThemeViewModel Theme { get; }

    public UpdateBannerViewModel UpdateBanner { get; }

    public OnboardingBannerViewModel OnboardingBanner { get; }

    public DebugBannerViewModel DebugBanner { get; }

    public StreamMonitoringViewModel StreamMonitoring { get; }

    public DashboardViewModel Overview { get; }

    public string BuildLabel => AppBuildBadge.Current.Value;

    public string BuildTooltip => AppBuildBadge.Current.Tooltip;

    public bool IsOverviewSelected => ReferenceEquals(Selected, _overviewSection);

    public override IPageHeader? EffectivePageHeader => _preferences.ShowPageHeader ? CurrentPageHeader : null;

    public new Thickness ContentMargin => ReferenceEquals(Selected, _overviewSection) ? default : base.ContentMargin;

    public string ConnectButtonText => Phase switch
    {
        BotLifecyclePhase.Connecting => "Отменить",
        BotLifecyclePhase.Connected => "Отключить",
        BotLifecyclePhase.Disconnecting => "Отключение...",
        _ => "Подключить",
    };

    public override async Task<bool> RequestCloseAsync()
    {
        if (!await ConfirmDirtyPagesOnCloseAsync())
        {
            UpdateBanner.ResetInstallState();

            if (App.IsRestartRequested)
            {
                App.CancelRestart();
                _logger.RestartCancelled();
            }

            return false;
        }

        try
        {
            await _connectionManager.ShutdownAsync(BotStopMode.Forced);
        }
        catch (Exception exception)
        {
            _logger.ShutdownOnCloseFailed(exception);
        }

        return true;
    }

    public void Dispose()
    {
        _uiSettings.WriteFailed -= OnUiSettingsWriteFailed;
        _settingsPage.SettingsSaved -= OnSettingsSaved;
        _streamHistoryPage.UserRequested -= OnUserRequested;
        _preferences.PropertyChanged -= OnPreferencesPropertyChanged;

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }

    protected override void NavigateToSettings()
    {
        Selected = _settingsSection;
    }

    [RelayCommand]
    private void NavigateToStatistics()
    {
        Selected = _statisticsSection;
    }

    private void OnUserRequested(object? sender, string userId)
    {
        if (!_statisticsPage.TrySelect(userId))
        {
            SetStatus("Этого пользователя нет в статистике", StatusSeverity.Info);
            return;
        }

        Selected = _statisticsSection;
    }

    [RelayCommand]
    private void NavigateToStreamHistory()
    {
        Selected = _streamHistorySection;
    }

    [RelayCommand]
    private void ClearChatHistory()
    {
        if (!_dialogService.Confirm("Очистка истории чата",
                "Вы уверены, что хотите очистить всю историю сообщений чата?\n\nЭто действие нельзя отменить."))
        {
            return;
        }

        _chatHistory.ClearHistory();
        _logger.ChatHistoryCleared();
    }

    public NavigationItem? FindSectionByKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (SectionKeys.LegacyTitles.TryGetValue(key, out var migrated))
        {
            key = migrated;
        }

        if (string.Equals(_settingsSection.Key, key, StringComparison.OrdinalIgnoreCase))
        {
            return _settingsSection;
        }

        return Sections.FirstOrDefault(section => section.Key.Length > 0
            && string.Equals(section.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    protected override void OnNavCollapsedChanged(bool value)
    {
        _preferences.NavCollapsed = value;
    }

    protected override void OnSelectionChanged(NavigationItem? value)
    {
        if (!_returningToPage && !ReferenceEquals(value, _current) && !TryLeaveDirtyPage(value))
        {
            return;
        }

        base.OnSelectionChanged(value);

        OnPropertyChanged(nameof(ContentMargin));
        OnPropertyChanged(nameof(IsOverviewSelected));

        _current = value;

        if (value is not null)
        {
            _preferences.LastPage = value.Key;
        }
    }

    private Action GuardReturn(Action activate)
    {
        return () =>
        {
            if (_returningToPage)
            {
                return;
            }

            activate();
        };
    }

    private static IUnsavedChangesPage? DirtyPageOf(NavigationItem? section)
    {
        return section?.Content is IUnsavedChangesPage { HasUnsavedChanges: true } page ? page : null;
    }

    private IEnumerable<NavigationItem> GatedSections()
    {
        yield return _settingsSection;

        foreach (var section in Sections)
        {
            yield return section;
        }
    }

    private async Task<bool> ConfirmDirtyPagesOnCloseAsync()
    {
        foreach (var section in GatedSections())
        {
            if (DirtyPageOf(section) is not { } page)
            {
                continue;
            }

            switch (_unsavedChangesPrompt.Ask(page.UnsavedChangesSubject, "закрытием"))
            {
                case UnsavedChangesDecision.Save:
                    await page.TrySaveUnsavedChangesAsync();

                    if (page.HasUnsavedChanges)
                    {
                        ReturnToPage(section);
                        return false;
                    }

                    break;

                case UnsavedChangesDecision.Discard:
                    page.DiscardUnsavedChanges();
                    break;

                default:
                    ReturnToPage(section);
                    return false;
            }
        }

        return true;
    }

    private bool TryLeaveDirtyPage(NavigationItem? target)
    {
        if (_current is not { } section || DirtyPageOf(section) is not { } page)
        {
            return true;
        }

        switch (_unsavedChangesPrompt.Ask(page.UnsavedChangesSubject, "переходом"))
        {
            case UnsavedChangesDecision.Save:
                ReturnToPage(section);
                _ = SaveThenNavigateAsync(page, target);
                return false;

            case UnsavedChangesDecision.Discard:
                page.DiscardUnsavedChanges();
                return true;

            default:
                ReturnToPage(section);
                return false;
        }
    }

    private void ReturnToPage(NavigationItem section)
    {
        _returningToPage = true;

        try
        {
            Selected = section;
        }
        finally
        {
            _returningToPage = false;
        }
    }

    private async Task SaveThenNavigateAsync(IUnsavedChangesPage page, NavigationItem? target)
    {
        await page.TrySaveUnsavedChangesAsync();

        if (page.HasUnsavedChanges || target is null)
        {
            return;
        }

        Selected = target;
    }

    [RelayCommand(CanExecute = nameof(CanToggleConnect))]
    private async Task ToggleConnectAsync()
    {
        switch (Phase)
        {
            case BotLifecyclePhase.Connecting:
                _connectionManager.CancelConnection();
                break;

            case BotLifecyclePhase.Connected:
                _logger.BotDisconnectRequested();

                try
                {
                    await _connectionManager.StopAsync();
                }
                catch (Exception exception)
                {
                    _logger.BotDisconnectFailed(exception);
                }

                break;

            case BotLifecyclePhase.Disconnecting:
                break;

            default:
                _logger.BotConnectRequested();

                try
                {
                    _connectionManager.StartConnection();
                }
                catch (InvalidOperationException exception)
                {
                    _logger.BotStartConnectionFailed(exception);
                }

                break;
        }
    }

    private bool CanToggleConnect()
    {
        return Phase != BotLifecyclePhase.Disconnecting;
    }

    [RelayCommand]
    private void Restart()
    {
        _logger.RestartRequested();

        if (App.IsHeadless)
        {
            _logger.RestartUnavailableHeadless();
            return;
        }

        if (!App.TryRequestRestart())
        {
            return;
        }

        Application.Current.MainWindow?.Close();
    }

    [RelayCommand]
    private void RestartForced()
    {
        _logger.ForcedRestartRequested();

        if (App.IsHeadless)
        {
            _logger.RestartUnavailableHeadless();
            return;
        }

        if (!_dialogService.ConfirmWarning("Принудительный перезапуск",
                "Приложение закроется сразу, без штатной остановки бота.\n\n"
                + "Несохранённые изменения настроек будут потеряны, прощание в чат не отправится, "
                + "положение окна не запомнится.\n\nПерезапустить принудительно?"))
        {
            _logger.ForcedRestartDeclined();
            return;
        }

        if (!App.TryRestartForced())
        {
            _dialogService.Error("Перезапуск не удался",
                "Не получилось запустить приложение заново, поэтому текущее окно осталось открытым. Подробности – в журнале.");
        }
    }

    private void OnPreferencesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(ShellPreferences.ShowPageHeader))
        {
            return;
        }

        OnPropertyChanged(nameof(EffectivePageHeader));
        OnPropertyChanged(nameof(ContentMargin));
    }

    private void OnUiSettingsWriteFailed(object? sender, SettingsWriteFailedEventArgs e)
    {
        _logger.UiSettingsWriteFailed(e.Exception, e.FilePath);

        _uiDispatcher.Invoke(
            () => SetStatus("Настройки оформления не сохранены на диск, правки применены только в этом сеансе", StatusSeverity.Error));
    }

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        OnboardingBanner.Refresh();
        DebugBanner.Refresh();
    }

    private void OnLifecyclePhaseChanged(BotLifecyclePhaseChanged phaseEvent)
    {
        ApplyPhase(phaseEvent.Phase);

        if (phaseEvent.Phase == BotLifecyclePhase.Failed)
        {
            _logger.BotConnectFailed(phaseEvent.Exception);

            _dialogService.Error("Ошибка", $"Ошибка подключения бота: {phaseEvent.Exception?.Message}");
        }
    }

    private void ApplyPhase(BotLifecyclePhase phase)
    {
        Phase = phase;

        var (text, severity) = phase switch
        {
            BotLifecyclePhase.Connecting => ("Подключение...", StatusSeverity.Info),
            BotLifecyclePhase.Connected => ("Бот подключён", StatusSeverity.Success),
            BotLifecyclePhase.Disconnecting => ("Отключение...", StatusSeverity.Info),
            BotLifecyclePhase.Cancelled => ("Подключение отменено", StatusSeverity.Warning),
            BotLifecyclePhase.Failed => ("Ошибка подключения", StatusSeverity.Error),
            _ => ("Бот отключён", StatusSeverity.None),
        };

        SetStatus(text, severity);
    }

    private void SetStatus(string text, StatusSeverity severity)
    {
        StatusText = text;
        StatusSeverity = severity;
    }
}
