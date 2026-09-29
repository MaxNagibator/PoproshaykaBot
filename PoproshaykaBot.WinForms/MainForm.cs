using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Infrastructure.Events.Update;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Onboarding;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Update;
using PoproshaykaBot.WinForms.Forms.Onboarding;
using PoproshaykaBot.WinForms.Forms.Settings;
using PoproshaykaBot.WinForms.Forms.StreamHistory;
using PoproshaykaBot.WinForms.Forms.Users;
using PoproshaykaBot.WinForms.Infrastructure;
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.WinForms;

public partial class MainForm : Form
{
    private readonly IFormFactory _forms;
    private readonly IEventBus _eventBus;
    private readonly ChatHistoryManager _chatHistoryManager;
    private readonly SettingsManager _settingsManager;
    private readonly DashboardLayoutStore _dashboardLayoutStore;
    private readonly BotConnectionManager _connectionManager;
    private readonly OnboardingChecklist _onboardingChecklist;
    private readonly ITargetChannelProvider _targetChannelProvider;
    private readonly IUpdateCoordinator _updateCoordinator;
    private readonly ILogger<MainForm> _logger;
    private readonly List<IDisposable> _subs = [];

    private BotLifecyclePhase _currentPhase = BotLifecyclePhase.Idle;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;
    private bool _initialized;
    private bool _onboardingWizardOpen;
    private UserStatisticsForm? _userStatisticsForm;
    private StreamHistoryForm? _streamHistoryForm;
    private SettingsForm? _settingsForm;
    private StreamMonitoringStatus? _botMonitoringStatus;
    private StreamMonitoringStatus? _broadcasterMonitoringStatus;
    private string? _botMonitoringDetail;
    private string? _broadcasterMonitoringDetail;

    public MainForm(
        IServiceProvider services,
        IFormFactory forms,
        IEventBus eventBus,
        ChatHistoryManager chatHistoryManager,
        BotConnectionManager connectionManager,
        SettingsManager settingsManager,
        DashboardLayoutStore dashboardLayoutStore,
        OnboardingChecklist onboardingChecklist,
        ITargetChannelProvider targetChannelProvider,
        IUpdateCoordinator updateCoordinator,
        ILogger<MainForm> logger)
    {
        _forms = forms;
        _eventBus = eventBus;
        _chatHistoryManager = chatHistoryManager;
        _connectionManager = connectionManager;
        _settingsManager = settingsManager;
        _dashboardLayoutStore = dashboardLayoutStore;
        _onboardingChecklist = onboardingChecklist;
        _targetChannelProvider = targetChannelProvider;
        _updateCoordinator = updateCoordinator;
        _logger = logger;

        InitializeComponent();

        services.HydrateDescendants(this);

        RestoreWindowBounds();
    }

    protected override bool ShowWithoutActivation => Program.IsUiSmoke;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_initialized || DesignMode)
        {
            return;
        }

        _initialized = true;

        Text = $"Попрощайка Бот v{GetDisplayVersion()}";

        _subs.Add(_eventBus.SubscribeOnUi<BotConnectionStatusUpdated>(this, statusEvent => OnBotConnectionProgress(statusEvent.Message)));
        _subs.Add(_eventBus.SubscribeOnUi<BotLifecyclePhaseChanged>(this, OnBotLifecyclePhaseChanged));
        _subs.Add(_eventBus.SubscribeOnUi<StreamMonitoringStatusChanged>(this, OnStreamMonitoringStatusChanged));
        _subs.Add(_eventBus.SubscribeOnUi<UpdateAvailable>(this, _ => RefreshUpdateBanner()));
        _subs.DisposeOnClose(this);

        LoadSettings();
        OpenOnboardingWizardIfNeeded();
        RefreshOnboardingBanner();
        RefreshUpdateBanner();
        RefreshDebugBanner();
        _logger.LogInformation("Приложение запущено. Нажмите 'Подключить бота' для начала работы.");

        KeyPreview = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (e.Cancel)
        {
            return;
        }

        if (_shutdownStarted)
        {
            if (!_shutdownCompleted)
            {
                e.Cancel = true;
            }

            return;
        }

        SaveWindowBounds();

        _shutdownStarted = true;
        e.Cancel = true;
        _ = ShutdownAndCloseAsync();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Alt | Keys.U:
                OnOpenUserStatistics();
                return true;

            case Keys.Alt | Keys.H:
                OnOpenStreamHistory();
                return true;

            case Keys.Control | Keys.Shift | Keys.Delete:
                ClearChatHistory();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async void OnConnectButtonClicked(object? sender, EventArgs e)
    {
        switch (_currentPhase)
        {
            case BotLifecyclePhase.Idle:
            case BotLifecyclePhase.Cancelled:
            case BotLifecyclePhase.Failed:
            case BotLifecyclePhase.Disconnected:
                StartBotConnection();
                break;

            case BotLifecyclePhase.Connecting:
                _connectionManager.CancelConnection();
                _logger.LogInformation("Отмена подключения...");
                break;

            case BotLifecyclePhase.Connected:
                await DisconnectBotAsync();
                break;

            case BotLifecyclePhase.Disconnecting:
                break;
        }
    }

    private void OnSettingsButtonClicked(object? sender, EventArgs e)
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = _forms.Create<SettingsForm>();
        _settingsForm.SettingsApplied += OnSettingsApplied;
        _settingsForm.FormClosed += OnSettingsFormClosed;
        _settingsForm.Show(this);
    }

    private void OnSettingsApplied(object? sender, EventArgs e)
    {
        LoadSettings(reloadDashboard: true);
        RefreshOnboardingBanner();
        RefreshDebugBanner();
        _logger.LogInformation("Настройки обновлены.");
    }

    private void OnOnboardingBannerButtonClicked(object? sender, EventArgs e)
    {
        OpenOnboardingWizard();
    }

    private void OnSettingsFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is not SettingsForm form)
        {
            return;
        }

        var launchWizard = form.LaunchOnboardingAfterClose;

        form.SettingsApplied -= OnSettingsApplied;
        form.FormClosed -= OnSettingsFormClosed;
        form.Dispose();
        _settingsForm = null;

        if (launchWizard)
        {
            BeginInvoke(OpenOnboardingWizard);
        }
    }

    private void OnUserStatisticsButtonClicked(object? sender, EventArgs e)
    {
        OnOpenUserStatistics();
    }

    private void OnStreamHistoryButtonClicked(object? sender, EventArgs e)
    {
        OnOpenStreamHistory();
    }

    private async void OnUpdateBannerUpdateButtonClicked(object? sender, EventArgs e)
    {
        var candidate = _updateCoordinator.LatestCandidate;

        if (candidate is null)
        {
            return;
        }

        var answer = MessageBox.Show(this,
            $"Загрузить и установить версию {candidate.Version}?\n\nПриложение перезапустится автоматически.",
            "Установка обновления",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        _updateBannerUpdateButton.Enabled = false;
        _updateBannerSkipButton.Enabled = false;

        var progress = new Progress<int>(percent =>
            _updateBannerLabel.Text = $"Загрузка обновления {candidate.Version}… {percent}%");

        try
        {
            await _updateCoordinator.PrepareAsync(candidate, progress, CancellationToken.None);
            _logger.LogInformation("Обновление {Version} загружено, приложение перезапустится", candidate.Version);
            Application.Exit();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка загрузки обновления");

            MessageBox.Show(this,
                $"Не удалось установить обновление: {exception.Message}",
                "Ошибка обновления",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            _updateBannerUpdateButton.Enabled = true;
            _updateBannerSkipButton.Enabled = true;
            RefreshUpdateBanner();
        }
    }

    private void OnUpdateBannerSkipButtonClicked(object? sender, EventArgs e)
    {
        var candidate = _updateCoordinator.LatestCandidate;

        if (candidate is null)
        {
            return;
        }

        _updateCoordinator.SkipVersion(candidate.Version.ToString());
        _updateBannerPanel.Visible = false;
        _logger.LogInformation("Версия {Version} пропущена", candidate.Version);
    }

    private static string GetDisplayVersion()
    {
        var version = Application.ProductVersion;
        var plusIndex = version.IndexOf('+', StringComparison.Ordinal);

        if (plusIndex > 0)
        {
            version = version[..plusIndex];
        }

        return version;
    }

    private static bool IsBoundsVisibleOnAnyScreen(Rectangle bounds)
    {
        foreach (var screen in Screen.AllScreens)
        {
            if (screen.WorkingArea.IntersectsWith(bounds))
            {
                return true;
            }
        }

        return false;
    }

    private static string RoleLabel(TwitchOAuthRole role)
    {
        return role == TwitchOAuthRole.Bot ? "бот" : "broadcaster";
    }

    private void OpenOnboardingWizardIfNeeded()
    {
        if (!_onboardingChecklist.RequiresWizard)
        {
            return;
        }

        OpenOnboardingWizard();
    }

    private void OpenOnboardingWizard()
    {
        if (_onboardingWizardOpen)
        {
            return;
        }

        _onboardingWizardOpen = true;

        try
        {
            using var wizard = _forms.Create<OnboardingWizardForm>();
            wizard.ShowDialog(this);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка отображения мастера первичной настройки");
        }
        finally
        {
            _onboardingWizardOpen = false;
            RefreshOnboardingBanner();
        }
    }

    private void RefreshOnboardingBanner()
    {
        var missing = _onboardingChecklist.GetMissingItems();

        if (missing.Count == 0)
        {
            _onboardingBannerPanel.Visible = false;
            return;
        }

        _onboardingBannerLabel.Text = $"⚠ Бот не готов к работе. Не настроено: {string.Join(", ", missing)}.";
        _onboardingBannerPanel.Visible = true;
    }

    private void RefreshDebugBanner()
    {
        var target = _targetChannelProvider.Current;

        if (!target.IsDebugSession)
        {
            _debugBannerPanel.Visible = false;
            return;
        }

        if (target.IsSendingAllowed)
        {
            _debugBannerPanel.BackColor = Color.MistyRose;
            _debugBannerLabel.ForeColor = Color.Firebrick;
            _debugBannerLabel.Text = $"🐞 Отладка: бот подключается к каналу {target.Login} и пишет в его чат.";
        }
        else
        {
            _debugBannerPanel.BackColor = Color.LightCyan;
            _debugBannerLabel.ForeColor = Color.Teal;
            _debugBannerLabel.Text = $"🐞 Отладка: бот читает чат канала {target.Login}. Сообщения не отправляются – они видны в журнале.";
        }

        _debugBannerPanel.Visible = true;
    }

    private void RefreshUpdateBanner()
    {
        var candidate = _updateCoordinator.LatestCandidate;

        if (candidate is null || !_updateCoordinator.IsUpdatable)
        {
            _updateBannerPanel.Visible = false;
            return;
        }

        _updateBannerLabel.Text = $"🔄 Доступна новая версия {candidate.Version} (установлена {_updateCoordinator.CurrentVersion}).";
        _updateBannerPanel.Visible = true;
    }

    private async Task ShutdownAndCloseAsync()
    {
        try
        {
            if (_userStatisticsForm is { IsDisposed: false })
            {
                _userStatisticsForm.Close();
                _userStatisticsForm = null;
            }

            if (_streamHistoryForm is { IsDisposed: false })
            {
                _streamHistoryForm.Close();
                _streamHistoryForm = null;
            }

            if (_settingsForm is { IsDisposed: false })
            {
                _settingsForm.Close();
                _settingsForm = null;
            }

            await _connectionManager.ShutdownAsync(BotStopMode.Forced);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка завершения работы");
        }
        finally
        {
            _shutdownCompleted = true;
            Close();
        }
    }

    private void OnBotLifecyclePhaseChanged(BotLifecyclePhaseChanged phaseEvent)
    {
        _currentPhase = phaseEvent.Phase;
        ApplyPhaseVisuals(phaseEvent.Phase);
        RefreshOnboardingBanner();

        switch (phaseEvent.Phase)
        {
            case BotLifecyclePhase.Connecting:
                _logger.LogInformation("Подключение бота...");
                break;

            case BotLifecyclePhase.Connected:
                _logger.LogInformation("Бот успешно подключен!");
                break;

            case BotLifecyclePhase.Cancelled:
                _logger.LogInformation("Подключение отменено пользователем.");
                break;

            case BotLifecyclePhase.Failed:
                _logger.LogError(phaseEvent.Exception, "Ошибка подключения бота");

                MessageBox.Show($"Ошибка подключения бота: {phaseEvent.Exception?.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                break;

            case BotLifecyclePhase.Disconnecting:
                _logger.LogInformation("Отключение бота...");
                break;

            case BotLifecyclePhase.Disconnected:
                _logger.LogInformation("Бот отключен.");
                break;
        }
    }

    private void ApplyPhaseVisuals(BotLifecyclePhase phase)
    {
        switch (phase)
        {
            case BotLifecyclePhase.Connecting:
                _connectToolStripButton.Text = "⏹️ Отменить";
                _connectToolStripButton.BackColor = Color.Orange;
                ShowConnectionProgress(true);
                break;

            case BotLifecyclePhase.Connected:
                _connectToolStripButton.Text = "🔌 Отключить";
                _connectToolStripButton.BackColor = Color.LightGreen;
                ShowConnectionProgress(false);
                break;

            case BotLifecyclePhase.Disconnecting:
                _connectToolStripButton.Text = "⏳ Отключение...";
                _connectToolStripButton.BackColor = Color.Orange;
                ShowConnectionProgress(true);
                break;

            case BotLifecyclePhase.Idle:
            case BotLifecyclePhase.Disconnected:
            case BotLifecyclePhase.Cancelled:
            case BotLifecyclePhase.Failed:
                _connectToolStripButton.Text = "🔌 Подключить";
                _connectToolStripButton.BackColor = SystemColors.Control;
                ShowConnectionProgress(false);
                break;
        }
    }

    private void OnBotConnectionProgress(string message)
    {
        _connectionStatusLabel.Text = message;
    }

    private void OnStreamMonitoringStatusChanged(StreamMonitoringStatusChanged statusEvent)
    {
        switch (statusEvent.Role)
        {
            case TwitchOAuthRole.Bot:
                _botMonitoringStatus = statusEvent.Status;
                _botMonitoringDetail = statusEvent.Detail;
                break;

            case TwitchOAuthRole.Broadcaster:
                _broadcasterMonitoringStatus = statusEvent.Status;
                _broadcasterMonitoringDetail = statusEvent.Detail;
                break;
        }

        var aggregate = StreamMonitoringStatusAggregator.SelectWorst(_botMonitoringStatus, _broadcasterMonitoringStatus);

        if (aggregate is null)
        {
            return;
        }

        var (worstStatus, worstRole) = aggregate.Value;
        var (icon, label) = worstStatus switch
        {
            StreamMonitoringStatus.Connecting => ("🟡", "подключение"),
            StreamMonitoringStatus.Connected => ("🟢", "работает"),
            StreamMonitoringStatus.Reconnecting => ("🟡", "переподключение"),
            StreamMonitoringStatus.Failed => ("🔴", "ошибка"),
            _ => ("⚪", "остановлен"),
        };

        var bothReported = _botMonitoringStatus.HasValue && _broadcasterMonitoringStatus.HasValue;
        var asymmetric = bothReported && _botMonitoringStatus != _broadcasterMonitoringStatus;
        var roleSuffix = asymmetric ? $", {RoleLabel(worstRole)}" : string.Empty;
        var detailText = worstRole == TwitchOAuthRole.Bot ? _botMonitoringDetail : _broadcasterMonitoringDetail;
        var detail = string.IsNullOrWhiteSpace(detailText) ? string.Empty : $" ({detailText})";
        _streamMonitoringStatusLabel.Text = $"{icon} Стрим-мониторинг: {label}{roleSuffix}{detail}";
    }

    private void OnOpenUserStatistics()
    {
        if (_userStatisticsForm == null || _userStatisticsForm.IsDisposed)
        {
            _userStatisticsForm = _forms.Create<UserStatisticsForm>();
            _userStatisticsForm.Show(this);
        }
        else
        {
            _userStatisticsForm.Focus();
        }
    }

    private void OnOpenStreamHistory()
    {
        if (_streamHistoryForm == null || _streamHistoryForm.IsDisposed)
        {
            _streamHistoryForm = _forms.Create<StreamHistoryForm>();
            _streamHistoryForm.Show(this);
        }
        else
        {
            _streamHistoryForm.Focus();
        }
    }

    private void ClearChatHistory()
    {
        var result = MessageBox.Show("Вы уверены, что хотите очистить всю историю сообщений чата?\n\nЭто действие нельзя отменить.",
            "Очистка истории чата",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            return;
        }

        _chatHistoryManager.ClearHistory();
        _logger.LogInformation("История сообщений чата очищена.");
    }

    private void StartBotConnection()
    {
        try
        {
            _connectionManager.StartConnection();
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Ошибка запуска подключения");
        }
    }

    private void ShowConnectionProgress(bool show)
    {
        _connectionProgressBar.Visible = show;
        _connectionStatusLabel.Visible = show;

        if (show)
        {
            _connectionProgressBar.Style = ProgressBarStyle.Marquee;
            _connectionStatusLabel.Text = "Подключение...";
        }
        else
        {
            _connectionStatusLabel.Text = "";
        }
    }

    private async Task DisconnectBotAsync()
    {
        if (_currentPhase != BotLifecyclePhase.Connected)
        {
            return;
        }

        try
        {
            await _connectionManager.StopAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка при отключении бота");
        }
    }

    private void RestoreWindowBounds()
    {
        if (Program.IsUiSmoke)
        {
            OffScreenForm.PrepareIfHeadless(this);
            return;
        }

        var saved = _dashboardLayoutStore.LoadMainWindow();

        if (saved is null || saved.Width <= 0 || saved.Height <= 0)
        {
            return;
        }

        var bounds = new Rectangle(saved.X, saved.Y, saved.Width, saved.Height);

        if (!IsBoundsVisibleOnAnyScreen(bounds))
        {
            return;
        }

        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;

        if (saved.Maximized)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private void SaveWindowBounds()
    {
        if (Program.IsUiSmoke)
        {
            return;
        }

        var isMaximized = WindowState == FormWindowState.Maximized;
        var bounds = isMaximized || WindowState == FormWindowState.Minimized
            ? RestoreBounds
            : Bounds;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var window = new MainWindowSettings
        {
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = isMaximized,
        };

        try
        {
            _dashboardLayoutStore.SaveMainWindow(window);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось сохранить размер окна");
        }
    }

    private void LoadSettings(bool reloadDashboard = false)
    {
        try
        {
            _ = _settingsManager.Current;

            if (reloadDashboard)
            {
                _dashboardControl.ReloadDashboard();
            }

            _logger.LogInformation("Настройки Twitch загружены.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка загрузки настроек");
        }
    }
}
