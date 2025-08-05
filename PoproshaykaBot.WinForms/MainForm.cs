using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;

namespace PoproshaykaBot.WinForms;

public partial class MainForm : Form
{
    private readonly ChatHistoryManager _chatHistoryManager;
    private Bot? _bot;
    private bool _isConnected;
    private BotConnectionManager? _connectionManager;
    private ChatWindow? _chatWindow;
    private UnifiedHttpServer? _httpServer;

    public MainForm()
    {
        _chatHistoryManager = new();

        InitializeComponent();
        InitializeConnectionManager();
        LoadSettings();
        UpdateBroadcastButtonState();
        InitializePanelVisibility();
        InitializeHttpServer();

        _chatHistoryManager.RegisterChatDisplay(_chatDisplay);

        AddLogMessage("Приложение запущено. Нажмите 'Подключить бота' для начала работы.");

        KeyPreview = true;
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        _connectionManager?.CancelConnection();
        _connectionManager?.Dispose();

        if (_chatWindow is { IsDisposed: false })
        {
            _chatWindow.FormClosed -= OnChatWindowClosed;
            _chatWindow.Close();
            _chatWindow = null;
        }

        await DisconnectBotAsync();
        base.OnFormClosed(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Alt | Keys.L:
                OnToggleLogsButtonClicked(_toggleLogsButton, EventArgs.Empty);
                return true;

            case Keys.Alt | Keys.C:
                OnToggleChatButtonClicked(_toggleChatButton, EventArgs.Empty);
                return true;

            case Keys.Alt | Keys.W:
                OnOpenChatWindowButtonClicked(_openChatWindowButton, EventArgs.Empty);
                return true;

            case Keys.Control | Keys.Shift | Keys.Delete:
                ClearChatHistory();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if (_httpServer != null)
        {
            try
            {
                SettingsManager.ChatSettingsChanged -= _httpServer.NotifyChatSettingsChanged;

                await _httpServer.StopAsync();
                _httpServer.Dispose();
            }
            catch (Exception ex)
            {
                AddLogMessage($"Ошибка остановки HTTP сервера: {ex.Message}");
            }
        }

        base.OnFormClosing(e);
    }

    private async void OnConnectButtonClicked(object sender, EventArgs e)
    {
        if (_isConnected == false)
        {
            if (_connectionManager?.IsBusy == true)
            {
                return;
            }

            StartBotConnection();
        }
        else
        {
            if (_connectionManager?.IsBusy == true)
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

    private void OnBroadcastButtonClicked(object sender, EventArgs e)
    {
        if (_bot == null)
        {
            return;
        }

        if (_bot.IsBroadcastActive)
        {
            _bot.StopBroadcast();
            UpdateBroadcastButtonState();
            AddLogMessage("Рассылка остановлена.");
        }
        else
        {
            _bot.StartBroadcast();
            UpdateBroadcastButtonState();
            AddLogMessage("Рассылка запущена.");
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
            _connectButton.Text = "Подключить бота";
            _connectButton.BackColor = SystemColors.Control;
        }
        else if (result.IsFailed)
        {
            AddLogMessage($"Ошибка подключения бота: {result.Exception?.Message}");

            MessageBox.Show($"Ошибка подключения бота: {result.Exception?.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

            _connectButton.Text = "Подключить бота";
            _connectButton.BackColor = SystemColors.Control;
        }
        else if (result is { IsSuccess: true, Bot: not null })
        {
            _bot = result.Bot;
            _bot.Connected += OnBotConnected;
            _bot.LogMessage += OnBotLogMessage;
            _bot.ConnectionProgress += OnBotConnectionProgress;
            _bot.ChatMessageReceived += OnChatMessageReceived;

            _isConnected = true;
            _connectButton.Text = "Отключить бота";
            _connectButton.BackColor = Color.LightGreen;
            UpdateBroadcastButtonState();
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
        AddLogMessage($"[Прогресс] {message}");
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

    private void OnBotConnected(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(OnBotConnected), message);
            return;
        }

        AddLogMessage($"Успешное подключение: {message}");

        UpdateBroadcastButtonState();
    }

    private void OnBotLogMessage(string message)
    {
        AddLogMessage($"[Бот] {message}");
    }

    private void OnChatMessageReceived(ChatMessageData chatMessage)
    {
        _chatHistoryManager.AddMessage(chatMessage);
    }

    private void OnToggleLogsButtonClicked(object sender, EventArgs e)
    {
        var settings = SettingsManager.Current;
        settings.Ui.ShowLogsPanel = !settings.Ui.ShowLogsPanel;
        SettingsManager.SaveSettings(settings);
        UpdatePanelVisibility();
    }

    private void OnToggleChatButtonClicked(object sender, EventArgs e)
    {
        var settings = SettingsManager.Current;
        settings.Ui.ShowChatPanel = !settings.Ui.ShowChatPanel;
        SettingsManager.SaveSettings(settings);
        UpdatePanelVisibility();
    }

    private void OnOpenChatWindowButtonClicked(object sender, EventArgs e)
    {
        if (_chatWindow == null || _chatWindow.IsDisposed)
        {
            _chatWindow = new(_chatHistoryManager);
            _chatWindow.FormClosed += OnChatWindowClosed;
            _chatWindow.Show(this);
            _openChatWindowButton.Text = "Закрыть окно (Alt+W)";
            _openChatWindowButton.BackColor = Color.LightGreen;
        }
        else
        {
            _chatWindow.Close();
        }
    }

    private void OnChatWindowClosed(object? sender, FormClosedEventArgs e)
    {
        _openChatWindowButton.Text = "Чат в окне (Alt+W)";
        _openChatWindowButton.BackColor = SystemColors.Control;

        if (_chatWindow == null)
        {
            return;
        }

        _chatWindow.FormClosed -= OnChatWindowClosed;
        _chatWindow = null;
    }

    private void OnSettingsButtonClicked(object sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm();

        if (settingsForm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        LoadSettings();
        AddLogMessage("Настройки обновлены.");
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

    private static Bot CreateBotWithSettings(string accessToken)
    {
        var settings = SettingsManager.Current.Twitch;
        var statisticsCollector = new StatisticsCollector();

        return new(accessToken, settings, statisticsCollector);
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

        var settings = SettingsManager.Current.Ui;
        var showLogs = settings.ShowLogsPanel;
        var showChat = settings.ShowChatPanel;

        _toggleLogsButton.Text = showLogs ? "Скрыть логи" : "Показать логи";
        _toggleLogsButton.BackColor = showLogs ? Color.LightGreen : SystemColors.Control;

        _toggleChatButton.Text = showChat ? "Скрыть чат" : "Показать чат";
        _toggleChatButton.BackColor = showChat ? Color.LightGreen : SystemColors.Control;

        _contentTableLayoutPanel.ColumnStyles.Clear();

        if (showLogs && showChat)
        {
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Percent, 50F));
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Percent, 50F));
        }
        else if (showLogs && showChat == false)
        {
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Percent, 100F));
            _contentTableLayoutPanel.ColumnStyles.Add(new(SizeType.Absolute, 0F));
        }
        else if (showLogs == false && showChat)
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
        _chatDisplay.Visible = showChat;

        _contentTableLayoutPanel.PerformLayout();
    }

    private void InitializeConnectionManager()
    {
        _connectionManager = new(CreateBotWithSettings, GetAccessTokenAsync);
        _connectionManager.ProgressChanged += OnConnectionProgress;
        _connectionManager.ConnectionCompleted += OnConnectionCompleted;
    }

    private void StartBotConnection()
    {
        if (_connectionManager?.IsBusy == true)
        {
            return;
        }

        _connectButton.Text = "Отменить";
        _connectButton.BackColor = Color.Orange;
        ShowConnectionProgress(true);

        try
        {
            _connectionManager?.StartConnection();
        }
        catch (InvalidOperationException exception)
        {
            AddLogMessage($"Ошибка запуска подключения: {exception.Message}");
            ShowConnectionProgress(false);
            _connectButton.Text = "Подключить бота";
            _connectButton.BackColor = SystemColors.Control;
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

        var isBroadcastActive = _bot is { IsBroadcastActive: true };
        var isConnected = _isConnected;

        if (isConnected == false)
        {
            _broadcastButton.Enabled = false;
            _broadcastButton.Text = "Рассылка недоступна";
            _broadcastButton.BackColor = SystemColors.Control;
        }
        else if (isBroadcastActive)
        {
            _broadcastButton.Enabled = true;
            _broadcastButton.Text = "Остановить рассылку";
            _broadcastButton.BackColor = Color.LightGreen;
        }
        else
        {
            _broadcastButton.Enabled = true;
            _broadcastButton.Text = "Запустить рассылку";
            _broadcastButton.BackColor = SystemColors.Control;
        }
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
            _bot.ChatMessageReceived -= OnChatMessageReceived;

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
        _connectButton.Text = "Подключить бота";
        _connectButton.BackColor = SystemColors.Control;
        UpdateBroadcastButtonState();

        AddLogMessage("Бот отключен.");
    }

    private void LoadSettings()
    {
        try
        {
            var settings = SettingsManager.Current;
            AddLogMessage("Настройки Twitch загружены.");
        }
        catch (Exception exception)
        {
            AddLogMessage($"Ошибка загрузки настроек: {exception.Message}");
        }
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        var settings = SettingsManager.Current.Twitch;

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            MessageBox.Show("OAuth настройки не настроены.\n\n" + "Пожалуйста, настройте ClientId и ClientSecret в настройках приложения.",
                "Настройки OAuth",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return null;
        }

        if (string.IsNullOrWhiteSpace(settings.AccessToken) == false)
        {
            if (await TwitchOAuthService.IsTokenValidAsync(settings.AccessToken))
            {
                AddLogMessage("Используется сохраненный токен доступа.");
                return settings.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(settings.RefreshToken) == false)
            {
                try
                {
                    AddLogMessage("Обновление токена доступа...");

                    var validToken = await TwitchOAuthService.GetValidTokenAsync(settings.ClientId,
                        settings.ClientSecret,
                        settings.AccessToken,
                        settings.RefreshToken);

                    AddLogMessage("Токен доступа обновлен.");
                    return validToken;
                }
                catch (Exception exception)
                {
                    AddLogMessage($"Не удалось обновить токен: {exception.Message}");
                }
            }
        }

        TwitchOAuthService.StatusChanged += OnOAuthStatusChanged;

        try
        {
            var oauthTask = TwitchOAuthService.StartOAuthFlowAsync(settings.ClientId,
                settings.ClientSecret,
                _httpServer,
                settings.Scopes,
                settings.RedirectUri);

            var accessToken = await oauthTask;
            AddLogMessage("OAuth авторизация завершена успешно.");
            return accessToken;
        }
        finally
        {
            TwitchOAuthService.StatusChanged -= OnOAuthStatusChanged;
        }
    }

    private async void InitializeHttpServer()
    {
        var settings = SettingsManager.Current.Twitch;

        if (settings.HttpServerEnabled == false)
        {
            AddLogMessage("HTTP сервер отключен в настройках.");
            return;
        }

        try
        {
            if (PortValidator.ValidateAndResolvePortConflictAsync(SettingsManager.Current) == false)
            {
                AddLogMessage("Не удалось разрешить конфликт портов. HTTP сервер не запущен.");
                return;
            }

            _httpServer = new(_chatHistoryManager, settings.HttpServerPort);
            _httpServer.LogMessage += OnHttpServerLogMessage;

            SettingsManager.ChatSettingsChanged += _httpServer.NotifyChatSettingsChanged;

            await _httpServer.StartAsync();

            AddLogMessage($"HTTP сервер запущен на порту {settings.HttpServerPort}");

            if (settings.ObsOverlayEnabled)
            {
                AddLogMessage($"OBS overlay доступен по адресу: http://localhost:{settings.HttpServerPort}/chat");
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Ошибка запуска HTTP сервера: {ex.Message}");
        }
    }
}
