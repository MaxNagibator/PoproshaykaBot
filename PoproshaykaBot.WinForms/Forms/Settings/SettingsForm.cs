using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.WinForms.Forms.Settings;

public partial class SettingsForm : Form
{
    private readonly SettingsManager _settingsManager;
    private readonly AccountsStore _accountsStore;
    private readonly ObsChatStore _obsChatStore;
    private readonly ObsIntegrationStore _obsIntegrationStore;
    private readonly DashboardLayoutStore _dashboardLayoutStore;
    private readonly PollsStore _pollsStore;
    private readonly IEventBus _eventBus;
    private readonly KestrelHttpServer _kestrelHttpServer;
    private readonly ILogger<SettingsForm> _logger;
    private readonly List<SettingsDraftSection> _sections;
    private AppSettings _settings;
    private TwitchAccountSettings _botDraft;
    private TwitchAccountSettings _broadcasterDraft;
    private ObsChatSettings _obsChatDraft;
    private ObsIntegrationSettings _obsIntegrationDraft;
    private PollsSettings _pollsDraft;
    private DashboardLayoutSettings? _dashboardDraft;
    private bool _initialized;
    private bool _hasChanges;

    public SettingsForm(
        SettingsManager settingsManager,
        AccountsStore accountsStore,
        ObsChatStore obsChatStore,
        ObsIntegrationStore obsIntegrationStore,
        DashboardLayoutStore dashboardLayoutStore,
        PollsStore pollsStore,
        IEventBus eventBus,
        KestrelHttpServer kestrelHttpServer,
        ILogger<SettingsForm> logger)
    {
        _settingsManager = settingsManager;
        _accountsStore = accountsStore;
        _obsChatStore = obsChatStore;
        _obsIntegrationStore = obsIntegrationStore;
        _dashboardLayoutStore = dashboardLayoutStore;
        _pollsStore = pollsStore;
        _eventBus = eventBus;
        _kestrelHttpServer = kestrelHttpServer;
        _logger = logger;

        _settings = JsonStoreClone.DeepClone(settingsManager.Current);
        _botDraft = JsonStoreClone.DeepClone(accountsStore.LoadBot());
        _broadcasterDraft = JsonStoreClone.DeepClone(accountsStore.LoadBroadcaster());
        _obsChatDraft = JsonStoreClone.DeepClone(obsChatStore.Load());
        _obsIntegrationDraft = JsonStoreClone.DeepClone(obsIntegrationStore.Load());
        _pollsDraft = JsonStoreClone.DeepClone(pollsStore.Load());
        _dashboardDraft = JsonStoreClone.DeepCloneNullable(dashboardLayoutStore.LoadDashboard());

        InitializeComponent();

        _sections =
        [
            new(() => _basicSettingsControl.LoadSettings(_settings.Twitch),
                () => _basicSettingsControl.SaveSettings(_settings.Twitch)),
            new(() => _rateLimitingSettingsControl.LoadSettings(_settings.Twitch),
                () => _rateLimitingSettingsControl.SaveSettings(_settings.Twitch)),
            new(() => _messagesSettingsControl.LoadSettings(_settings.Twitch.Messages),
                () => _messagesSettingsControl.SaveSettings(_settings.Twitch.Messages)),
            new(() => _httpServerSettingsControl.LoadSettings(_settings.Twitch),
                () =>
                {
                    // read-only
                }),
            new(() => _oauthSettingsControl.LoadSettings(_settings, _botDraft, _broadcasterDraft),
                () => _oauthSettingsControl.SaveSettings(_settings)),
            new(() => _obsChatSettingsControl.LoadSettings(_obsChatDraft),
                () => _obsChatSettingsControl.SaveSettings(_obsChatDraft)),
            new(() => _obsIntegrationSettingsControl.LoadSettings(_obsIntegrationDraft, _settings.Twitch.HttpServerPort),
                () => _obsIntegrationSettingsControl.SaveSettings(_obsIntegrationDraft)),
            new(() => _autoBroadcastSettingsControl.LoadSettings(_settings.Twitch.AutoBroadcast),
                () => _autoBroadcastSettingsControl.SaveSettings(_settings.Twitch.AutoBroadcast)),
            new(() => _botLifecycleAutomationSettingsControl.LoadSettings(_settings.Twitch.BotLifecycleAutomation),
                () => _botLifecycleAutomationSettingsControl.SaveSettings(_settings.Twitch.BotLifecycleAutomation)),
            new(() => _miscSettingsControl.LoadSettings(_settings),
                () => _miscSettingsControl.SaveSettings(_settings)),
            new(() => _pollsSettingsControl.LoadSettings(_pollsDraft),
                () => _pollsSettingsControl.SaveSettings(_pollsDraft)),
            new(() => _dashboardSettingsControl.LoadSettings(_dashboardDraft),
                () => _dashboardDraft = _dashboardSettingsControl.SaveSettings()),
        ];

        _oauthSettingsControl.FlushDraft = FlushDraftFromControls;
    }

    public event EventHandler? SettingsApplied;

    public bool LaunchOnboardingAfterClose { get; private set; }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_initialized)
        {
            return;
        }

        if (this.IsInDesignMode())
        {
            return;
        }

        _initialized = true;

        LoadSettingsToControls();
    }

    private void OnOAuthLaunchOnboardingRequested(object? sender, EventArgs e)
    {
        if (_hasChanges)
        {
            var answer = MessageBox.Show(this,
                "Несохранённые изменения будут потеряны. Запустить мастер настройки?",
                "Запуск мастера",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
            {
                return;
            }
        }

        LaunchOnboardingAfterClose = true;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        _hasChanges = true;
        UpdateButtonStates();
    }

    private void OnTabControlSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_tabControl.SelectedTab == _oauthTabPage)
        {
            _oauthSettingsControl.RefreshChannelHint(_basicSettingsControl.GetChannel());
        }
    }

    private async void OnOkButtonClicked(object sender, EventArgs e)
    {
        if (_hasChanges)
        {
            await ApplySettingsAsync();
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnCancelButtonClicked(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private async void OnApplyButtonClicked(object sender, EventArgs e)
    {
        await ApplySettingsAsync();
    }

    private void OnResetButtonClicked(object sender, EventArgs e)
    {
        var result = MessageBox.Show("Вы уверены, что хотите сбросить все настройки к значениям по умолчанию?",
            "Подтверждение сброса",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        _settings = new();
        _botDraft = new();
        _broadcasterDraft = new();
        _obsChatDraft = new();
        _obsIntegrationDraft = new();
        _pollsDraft = new();
        _dashboardDraft = null;

        LoadSettingsToControls();
        _hasChanges = true;
        UpdateButtonStates();
    }

    private void FlushDraftFromControls(AppSettings settings)
    {
        _basicSettingsControl.SaveSettings(settings.Twitch);
        _oauthSettingsControl.SaveSettings(settings);
    }

    private void LoadSettingsToControls()
    {
        foreach (var section in _sections)
        {
            section.Load();
        }

        _hasChanges = false;
        UpdateButtonStates();
    }

    private void SaveSettingsFromControls()
    {
        foreach (var section in _sections)
        {
            section.Save();
        }
    }

    private void UpdateButtonStates()
    {
        _applyButton.Enabled = _hasChanges;
    }

    private async Task ApplySettingsAsync()
    {
        _applyButton.Enabled = false;
        _okButton.Enabled = false;

        try
        {
            SaveSettingsFromControls();

            var prevBotToken = _accountsStore.LoadBot().AccessToken;
            var prevBroadcasterToken = _accountsStore.LoadBroadcaster().AccessToken;
            var prevPort = _settingsManager.Current.Twitch.HttpServerPort;

            ReconcileHttpServerPort();
            var newPort = _settings.Twitch.HttpServerPort;

            _settingsManager.SaveSettings(_settings);
            _accountsStore.SaveAll(_botDraft, _broadcasterDraft);
            _obsChatStore.Save(_obsChatDraft);
            _obsIntegrationStore.Save(_obsIntegrationDraft);
            _pollsStore.Save(_pollsDraft);

            if (_dashboardDraft != null)
            {
                _dashboardLayoutStore.SaveDashboard(_dashboardDraft);
            }

            if (!string.Equals(prevBotToken, _botDraft.AccessToken, StringComparison.Ordinal))
            {
                _ = _eventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Bot));
            }

            if (!string.Equals(prevBroadcasterToken, _broadcasterDraft.AccessToken, StringComparison.Ordinal))
            {
                _ = _eventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Broadcaster));
            }

            var portChanged = newPort != prevPort;
            var restartFailed = false;

            if (portChanged && _kestrelHttpServer.IsRunning)
            {
                restartFailed = !await TryRestartHttpServerAsync(prevPort, newPort);
            }

            _hasChanges = false;
            SettingsApplied?.Invoke(this, EventArgs.Empty);

            if (restartFailed)
            {
                return;
            }

            var info = portChanged
                ? $"Настройки сохранены. HTTP сервер перезапущен на порту {newPort}."
                : "Настройки успешно сохранены.";

            MessageBox.Show(info, "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка сохранения настроек");
            MessageBox.Show($"Ошибка сохранения настроек: {exception.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _okButton.Enabled = true;
            UpdateButtonStates();
        }
    }

    private void ReconcileHttpServerPort()
    {
        if (!RedirectUriPortResolver.TryResolve(_settings.Twitch.RedirectUri, out var derivedPort))
        {
            _logger.LogWarning("Некорректный RedirectUri '{RedirectUri}' — порт HTTP сервера не обновлён",
                _settings.Twitch.RedirectUri);

            return;
        }

        if (_settings.Twitch.HttpServerPort == derivedPort)
        {
            return;
        }

        _logger.LogInformation("Порт HTTP сервера обновлён с {OldPort} на {NewPort} в соответствии с RedirectUri",
            _settings.Twitch.HttpServerPort, derivedPort);

        _settings.Twitch.HttpServerPort = derivedPort;
    }

    private async Task<bool> TryRestartHttpServerAsync(int prevPort, int newPort)
    {
        try
        {
            _logger.LogInformation("Перезапуск HTTP сервера: порт {OldPort} → {NewPort}", prevPort, newPort);
            await _kestrelHttpServer.StopAsync();
            await _kestrelHttpServer.StartAsync();
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка перезапуска HTTP сервера на порту {NewPort}", newPort);
            MessageBox.Show($"""
                             Не удалось перезапустить HTTP сервер на порту {newPort}: {exception.Message}

                             Настройки сохранены. Перезапустите приложение, чтобы изменения вступили в силу.
                             """,
                "Ошибка перезапуска HTTP сервера",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return false;
        }
    }

    private readonly record struct SettingsDraftSection(Action Load, Action Save);
}
