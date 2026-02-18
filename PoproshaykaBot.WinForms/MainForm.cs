using PoproshaykaBot.WinForms.Broadcast;
using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;

namespace PoproshaykaBot.WinForms;

public partial class MainForm : Form
{
    private readonly ChatHistoryManager _chatHistoryManager;
    private readonly SettingsManager _settingsManager;
    private readonly BotConnectionManager _connectionManager;
    private readonly UnifiedHttpServer _httpServer;
    private readonly TwitchOAuthService _oauthService;
    private readonly StatisticsCollector _statisticsCollector;
    private readonly UserRankService _userRankService;
    private readonly StreamStatusManager _streamStatusManager;
    private readonly BroadcastScheduler _broadcastScheduler;
    private readonly UserMessagesManagementService _userMessagesManagementService;

    private Bot? _bot;
    private bool _isConnected;
    private UserStatisticsForm? _юзерФорма;

    public MainForm(
        ChatHistoryManager chatHistoryManager,
        UnifiedHttpServer httpServer,
        BotConnectionManager connectionManager,
        SettingsManager settingsManager,
        TwitchOAuthService oauthService,
        StatisticsCollector statisticsCollector,
        UserRankService userRankService,
        StreamStatusManager streamStatusManager,
        BroadcastScheduler broadcastScheduler,
        UserMessagesManagementService userMessagesManagementService)
    {
        _chatHistoryManager = chatHistoryManager;
        _httpServer = httpServer;
        _connectionManager = connectionManager;
        _settingsManager = settingsManager;
        _oauthService = oauthService;
        _statisticsCollector = statisticsCollector;
        _userRankService = userRankService;
        _streamStatusManager = streamStatusManager;
        _broadcastScheduler = broadcastScheduler;
        _userMessagesManagementService = userMessagesManagementService;

        InitializeComponent();

        _connectionManager.ProgressChanged += OnConnectionProgress;
        _connectionManager.ConnectionCompleted += OnConnectionCompleted;

        _streamStatusManager.StreamStatusChanged += _ => OnStreamStatusChanged();
        _broadcastScheduler.StateChanged += OnBroadcastStateChanged;

        LoadSettings();
        _broadcastInfoWidget.Setup(_settingsManager, _streamStatusManager, _broadcastScheduler);
        UpdateBroadcastButtonState();
        UpdateStreamStatus();
        InitializePanelVisibility();

        _chatHistoryManager.RegisterChatDisplay(_chatDisplay);

        _httpServer.LogMessage += OnHttpServerLogMessage;
        _settingsManager.ChatSettingsChanged += _httpServer.NotifyChatSettingsChanged;

        AddLogMessage("Приложение запущено. Нажмите 'Подключить бота' для начала работы.");

        KeyPreview = true;

        InitializeWebViewAsync();
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        _connectionManager.CancelConnection();

        if (_юзерФорма is { IsDisposed: false })
        {
            _юзерФорма.Close();
            _юзерФорма = null;
        }

        await DisconnectBotAsync();
        base.OnFormClosed(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Alt | Keys.L:
                OnToggleLogsButtonClicked(_logsToolStripButton, EventArgs.Empty);
                return true;

            case Keys.Alt | Keys.C:
                OnToggleChatButtonClicked(_chatToolStripButton, EventArgs.Empty);
                return true;

            case Keys.Alt | Keys.U:
                OnOpenUserStatistics();
                return true;

            case Keys.Control | Keys.Shift | Keys.Delete:
                ClearChatHistory();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        _connectionManager.ProgressChanged -= OnConnectionProgress;
        _connectionManager.ConnectionCompleted -= OnConnectionCompleted;

        if (_httpServer != null)
        {
            try
            {
                _httpServer.LogMessage -= OnHttpServerLogMessage;
                _settingsManager.ChatSettingsChanged -= _httpServer.NotifyChatSettingsChanged;
            }
            catch (Exception ex)
            {
                AddLogMessage($"Ошибка остановки HTTP сервера: {ex.Message}");
            }
        }

        base.OnFormClosing(e);
    }

    private async void OnConnectButtonClicked(object? sender, EventArgs e)
    {
        if (!_isConnected)
        {
            if (_connectionManager.IsBusy)
            {
                return;
            }

            StartBotConnection();
        }
        else
        {
            if (_connectionManager.IsBusy)
            {
                _connectionManager.CancelConnection();
                AddLogMessage("Отмена подключения...");
            }
            else
            {
                await DisconnectBotAsync();
            }
        }
    }

    private void OnConnectionProgress(object? sender, string message)
    {
        OnBotConnectionProgress(message);
    }

    private void OnConnectionCompleted(object? sender, BotConnectionResult result)
    {
        ShowConnectionProgress(false);

        if (result.IsCancelled)
        {
            AddLogMessage("Подключение отменено пользователем.");
            _connectToolStripButton.Text = "🔌 Подключить";
            _connectToolStripButton.BackColor = SystemColors.Control;
        }
        else if (result.IsFailed)
        {
            AddLogMessage($"Ошибка подключения бота: {result.Exception?.Message}");

            MessageBox.Show($"Ошибка подключения бота: {result.Exception?.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

            _connectToolStripButton.Text = "🔌 Подключить";
            _connectToolStripButton.BackColor = SystemColors.Control;
        }
        else if (result is { IsSuccess: true, Bot: not null })
        {
            _bot = result.Bot;
            _bot.Connected += OnBotConnected;
            _bot.LogMessage += OnBotLogMessage;
            _bot.ConnectionProgress += OnBotConnectionProgress;

            _isConnected = true;
            _connectToolStripButton.Text = "🔌 Отключить";
            _connectToolStripButton.BackColor = Color.LightGreen;
            _broadcastInfoWidget.Setup(_settingsManager, _streamStatusManager, _broadcastScheduler, _bot);
            UpdateBroadcastButtonState();
            UpdateStreamStatus();
            AddLogMessage("Бот успешно подключен!");
        }
    }

    private void OnBotConnectionProgress(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(OnBotConnectionProgress), message);
            return;
        }

        _connectionStatusLabel.Text = message;
    }

    private void OnBotConnected(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(OnBotConnected), message);
            return;
        }

        UpdateBroadcastButtonState();
        UpdateStreamStatus();
    }

    private void OnBotLogMessage(string message)
    {
        AddLogMessage($"[Бот] {message}");
    }

    private void OnToggleLogsButtonClicked(object? sender, EventArgs e)
    {
        var settings = _settingsManager.Current;
        settings.Ui.ShowLogsPanel = !settings.Ui.ShowLogsPanel;
        _settingsManager.SaveSettings(settings);
        UpdatePanelVisibility();
    }

    private void OnToggleChatButtonClicked(object? sender, EventArgs e)
    {
        var settings = _settingsManager.Current;
        settings.Ui.ShowChatPanel = !settings.Ui.ShowChatPanel;
        _settingsManager.SaveSettings(settings);
        UpdatePanelVisibility();
    }

    private void OnSwitchChatViewButtonClicked(object? sender, EventArgs e)
    {
        var settings = _settingsManager.Current;
        settings.Ui.CurrentChatViewMode = settings.Ui.CurrentChatViewMode == ChatViewMode.Legacy
            ? ChatViewMode.Overlay
            : ChatViewMode.Legacy;

        _settingsManager.SaveSettings(settings);
        UpdateChatViewMode();
    }

    private void OnSettingsButtonClicked(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm(_settingsManager, _oauthService, _httpServer);

        if (settingsForm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        LoadSettings();
        AddLogMessage("Настройки обновлены.");
    }

    private void OnUserStatisticsButtonClicked(object? sender, EventArgs e)
    {
        OnOpenUserStatistics();
    }

    private void OnHttpServerLogMessage(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(OnHttpServerLogMessage), message);
            return;
        }

        AddLogMessage($"HTTP: {message}");
    }

    private void OnBroadcastStateChanged()
    {
        _broadcastInfoWidget.UpdateState();
        UpdateBroadcastButtonState();
        _settingsManager.SaveSettings(_settingsManager.Current);
    }

    private async void OnStreamInfoTimerTick(object? sender, EventArgs e)
    {
        if (_streamStatusManager.CurrentStatus == StreamStatus.Online)
        {
            await _streamStatusManager.RefreshCurrentStatusAsync();
            UpdateStreamInfo();
        }
    }

    private void OnStreamStatusChanged()
    {
        UpdateStreamStatus();
    }

    private void OnOpenUserStatistics()
    {
        if (_юзерФорма == null || _юзерФорма.IsDisposed)
        {
            _юзерФорма = new(_statisticsCollector, _userRankService, _userMessagesManagementService, _bot);
            _юзерФорма.Show(this);
        }
        else
        {
            _юзерФорма.UpdateBotReference(_bot);
            _юзерФорма.Focus();
        }
    }

    private void OnOAuthStatusChanged(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(OnOAuthStatusChanged), message);
            return;
        }

        _connectionStatusLabel.Text = message;
        AddLogMessage($"[OAuth] {message}");
    }

    private void UpdateStreamStatus()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateStreamStatus);
            return;
        }

        _streamInfoWidget.UpdateStatus(_streamStatusManager.CurrentStatus, _streamStatusManager.CurrentStream);

        if (_streamStatusManager.CurrentStatus == StreamStatus.Online)
        {
            if (!_streamInfoTimer.Enabled)
            {
                _streamInfoTimer.Start();
            }
        }
        else
        {
            if (_streamInfoTimer.Enabled)
            {
                _streamInfoTimer.Stop();
            }
        }
    }

    private void UpdateStreamInfo()
    {
        UpdateStreamStatus();
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
        AddLogMessage("История сообщений чата очищена.");
    }

    private void InitializePanelVisibility()
    {
        UpdatePanelVisibility();
    }

    private void UpdatePanelVisibility()
    {
        if (InvokeRequired)
        {
            Invoke(UpdatePanelVisibility);
            return;
        }

        var settings = _settingsManager.Current.Ui;
        var showLogs = settings.ShowLogsPanel;
        var showChat = settings.ShowChatPanel;

        _logsToolStripButton.Checked = showLogs;
        _logsToolStripButton.BackColor = showLogs ? Color.LightGreen : SystemColors.Control;
        _logsToolStripButton.Text = showLogs ? "📜 Логи" : "📜 Логи"; // Keep icon consistent

        _chatToolStripButton.Checked = showChat;
        _chatToolStripButton.BackColor = showChat ? Color.LightGreen : SystemColors.Control;
        _chatToolStripButton.Text = showChat ? "💬 Чат" : "💬 Чат";

        _contentTableLayoutPanel.ColumnStyles.Clear();

        if (showLogs && showChat)
        {
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Percent, 50F));
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Percent, 50F));
        }
        else if (showLogs && !showChat)
        {
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Percent, 100F));
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Absolute, 0F));
        }
        else if (!showLogs && showChat)
        {
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Absolute, 0F));
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Percent, 100F));
        }
        else
        {
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Percent, 50F));
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Percent, 50F));
        }

        _logLabel.Visible = showLogs;
        _logTextBox.Visible = showLogs;

        UpdateChatViewMode();

        _contentTableLayoutPanel.PerformLayout();
    }

    private void UpdateChatViewMode()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateChatViewMode);
            return;
        }

        var settings = _settingsManager.Current.Ui;
        var showChat = settings.ShowChatPanel;
        var mode = settings.CurrentChatViewMode;

        if (!showChat)
        {
            _chatDisplay.Visible = false;
            _overlayWebView.Visible = false;
            _chatViewToolStripButton.Enabled = false;
        }
        else
        {
            _chatViewToolStripButton.Enabled = true;
            if (mode == ChatViewMode.Legacy)
            {
                _chatDisplay.Visible = true;
                _overlayWebView.Visible = false;
                _chatViewToolStripButton.Text = "👁️ Чат";
                _chatViewToolStripButton.Checked = false;
                _chatViewToolStripButton.BackColor = SystemColors.Control;
            }
            else
            {
                _chatDisplay.Visible = false;
                _overlayWebView.Visible = true;
                _chatViewToolStripButton.Text = "👁️ Overlay";
                _chatViewToolStripButton.Checked = true;
                _chatViewToolStripButton.BackColor = Color.LightBlue;

                if (_overlayWebView.CoreWebView2 == null)
                {
                    InitializeWebViewAsync();
                }
                else
                {
                    UpdateOverlayUrl();
                }
            }
        }
    }

    private async void InitializeWebViewAsync()
    {
        try
        {
            await _overlayWebView.EnsureCoreWebView2Async(null);
            UpdateOverlayUrl();
        }
        catch (Exception ex)
        {
            AddLogMessage($"Ошибка инициализации WebView2: {ex.Message}");
        }
    }

    private void UpdateOverlayUrl()
    {
        if (_overlayWebView.CoreWebView2 == null)
        {
            return;
        }

        var port = _settingsManager.Current.Twitch.HttpServerPort;
        var url = $"http://localhost:{port}/chat?preview=true";

        if (_overlayWebView.Source?.ToString() != url)
        {
            _overlayWebView.CoreWebView2.Navigate(url);
        }
    }

    private void StartBotConnection()
    {
        if (_connectionManager.IsBusy)
        {
            return;
        }

        _connectToolStripButton.Text = "⏹️ Отменить";
        _connectToolStripButton.BackColor = Color.Orange;
        ShowConnectionProgress(true);

        try
        {
            _connectionManager.StartConnection();
        }
        catch (InvalidOperationException exception)
        {
            AddLogMessage($"Ошибка запуска подключения: {exception.Message}");
            ShowConnectionProgress(false);
            _connectToolStripButton.Text = "🔌 Подключить";
            _connectToolStripButton.BackColor = SystemColors.Control;
        }
    }

    private void AddLogMessage(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(AddLogMessage), message);
            return;
        }

        _logTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}");
        _logTextBox.SelectionStart = _logTextBox.Text.Length;
        _logTextBox.ScrollToCaret();
    }

    private void UpdateBroadcastButtonState()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateBroadcastButtonState);
            return;
        }

        _broadcastInfoWidget.UpdateState();
    }

    private void ShowConnectionProgress(bool show)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<bool>(ShowConnectionProgress), show);
            return;
        }

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
        AddLogMessage("Отключение бота...");

        if (_bot != null)
        {
            _bot.Connected -= OnBotConnected;
            _bot.LogMessage -= OnBotLogMessage;
            _bot.ConnectionProgress -= OnBotConnectionProgress;

            try
            {
                await _bot.DisconnectAsync();
            }
            catch (Exception exception)
            {
                AddLogMessage($"Ошибка при отключении бота: {exception.Message}");
            }

            await _bot.DisposeAsync();
            _bot = null;
        }

        _isConnected = false;
        _connectToolStripButton.Text = "🔌 Подключить";
        _connectToolStripButton.BackColor = SystemColors.Control;
        UpdateBroadcastButtonState();
        UpdateStreamStatus();

        AddLogMessage("Бот отключен.");
    }

    private void LoadSettings()
    {
        try
        {
            var settings = _settingsManager.Current;
            AddLogMessage("Настройки Twitch загружены.");
            UpdatePanelVisibility();
        }
        catch (Exception exception)
        {
            AddLogMessage($"Ошибка загрузки настроек: {exception.Message}");
        }
    }
}
