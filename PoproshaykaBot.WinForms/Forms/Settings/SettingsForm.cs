using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Settings.Debugging;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Core.Settings.Update;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using PoproshaykaBot.WinForms.Tiles;
using System.Text.Json;

namespace PoproshaykaBot.WinForms.Forms.Settings;

public partial class SettingsForm : Form
{
    private readonly SettingsManager _settingsManager;
    private readonly AccountsStore _accountsStore;
    private readonly ObsChatStore _obsChatStore;
    private readonly ObsIntegrationStore _obsIntegrationStore;
    private readonly DashboardLayoutCoordinator _dashboardLayoutCoordinator;
    private readonly PollsStore _pollsStore;
    private readonly UpdateStore _updateStore;
    private readonly DebugChannelStore _debugChannelStore;
    private readonly IEventBus _eventBus;
    private readonly KestrelHttpServer _kestrelHttpServer;
    private readonly ILogger<SettingsForm> _logger;
    private readonly DebugChannelOverride _debugChannelOverride;
    private readonly List<SettingsDraftSection> _sections;
    private AppSettings _settings;
    private TwitchAccountSettings _botDraft;
    private TwitchAccountSettings _broadcasterDraft;
    private ObsChatSettings _obsChatDraft;
    private string _obsChatBaselineJson;
    private ObsIntegrationSettings _obsIntegrationDraft;
    private PollsSettings _pollsDraft;
    private UpdateSettings _updateDraft;
    private DebugChannelSettings _debugChannelDraft;
    private DashboardLayoutSettings? _dashboardDraft;
    private int _dashboardRevision;
    private bool _initialized;
    private bool _hasChanges;

    public SettingsForm(
        SettingsManager settingsManager,
        AccountsStore accountsStore,
        ObsChatStore obsChatStore,
        ObsIntegrationStore obsIntegrationStore,
        DashboardLayoutCoordinator dashboardLayoutCoordinator,
        PollsStore pollsStore,
        UpdateStore updateStore,
        DebugChannelStore debugChannelStore,
        DebugChannelOverride debugChannelOverride,
        IEventBus eventBus,
        KestrelHttpServer kestrelHttpServer,
        ILogger<SettingsForm> logger)
    {
        _settingsManager = settingsManager;
        _accountsStore = accountsStore;
        _obsChatStore = obsChatStore;
        _obsIntegrationStore = obsIntegrationStore;
        _dashboardLayoutCoordinator = dashboardLayoutCoordinator;
        _pollsStore = pollsStore;
        _updateStore = updateStore;
        _debugChannelStore = debugChannelStore;
        _debugChannelOverride = debugChannelOverride;
        _eventBus = eventBus;
        _kestrelHttpServer = kestrelHttpServer;
        _logger = logger;

        _settings = JsonStoreClone.DeepClone(settingsManager.Current);
        _botDraft = JsonStoreClone.DeepClone(accountsStore.LoadBot());
        _broadcasterDraft = JsonStoreClone.DeepClone(accountsStore.LoadBroadcaster());
        _obsChatDraft = JsonStoreClone.DeepClone(obsChatStore.Load());
        _obsChatBaselineJson = SerializeObsChat(_obsChatDraft);
        _obsIntegrationDraft = JsonStoreClone.DeepClone(obsIntegrationStore.Load());
        _pollsDraft = JsonStoreClone.DeepClone(pollsStore.Load());
        _updateDraft = JsonStoreClone.DeepClone(updateStore.Load());
        _debugChannelDraft = JsonStoreClone.DeepClone(debugChannelStore.Load());
        var dashboardSnapshot = dashboardLayoutCoordinator.Read();

        _dashboardRevision = dashboardSnapshot.Revision;
        _dashboardDraft = dashboardSnapshot.Layout;

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
            new(() => _obsChatSettingsControl.LoadSettings(_obsChatDraft, _settings.Twitch.HttpServerPort),
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
            new(() => _updateSettingsControl.LoadSettings(_updateDraft),
                () => _updateSettingsControl.SaveSettings(_updateDraft)),
            new(() => _debugChannelSettingsControl.LoadSettings(_debugChannelDraft, _debugChannelOverride),
                () => _debugChannelSettingsControl.SaveSettings(_debugChannelDraft)),
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
        else if (_tabControl.SelectedTab == _obsIntegrationTabPage)
        {
            _obsIntegrationSettingsControl.RunAutoConnectionCheck();
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
        _updateDraft = new();
        _debugChannelDraft = new();
        var dashboardSnapshot = _dashboardLayoutCoordinator.Read();

        _dashboardRevision = dashboardSnapshot.Revision;
        _dashboardDraft = DashboardLayoutReconciler.ResetToDefaults(DashboardLayoutDefaults.Create(), dashboardSnapshot.Layout);

        LoadSettingsToControls();
        _hasChanges = true;
        UpdateButtonStates();
    }

    private static string SerializeObsChat(ObsChatSettings settings)
    {
        return JsonSerializer.Serialize(settings, JsonStoreOptions.Default);
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

    private void SaveObsChatDraftWithConflictCheck()
    {
        var current = _obsChatStore.Load();
        var currentJson = SerializeObsChat(current);

        if (!string.Equals(currentJson, _obsChatBaselineJson, StringComparison.Ordinal))
        {
            var answer = MessageBox.Show(this,
                """
                Настройки чат-оверлея были изменены извне (например, через демо-страницу) уже после открытия этого окна.

                Перезаписать их значениями из этого окна?

                «Да» – применить значения из этого окна.
                «Нет» – оставить внешние изменения, не трогая вкладку «OBS Чат».
                """,
                "Конфликт настроек чат-оверлея",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
            {
                _obsChatDraft = JsonStoreClone.DeepClone(current);
                _obsChatSettingsControl.LoadSettings(_obsChatDraft, _settings.Twitch.HttpServerPort);
                _obsChatBaselineJson = currentJson;
                return;
            }
        }

        _obsChatStore.Save(_obsChatDraft);
        _obsChatBaselineJson = SerializeObsChat(_obsChatDraft);
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
            SaveObsChatDraftWithConflictCheck();
            _obsIntegrationStore.Save(_obsIntegrationDraft);
            _pollsStore.Save(_pollsDraft);
            _updateStore.Save(_updateDraft);
            _debugChannelStore.Save(_debugChannelDraft);

            if (_dashboardDraft != null)
            {
                _dashboardRevision = _dashboardLayoutCoordinator.Commit(_dashboardDraft, _dashboardRevision).Snapshot.Revision;
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
            _logger.LogWarning("Некорректный RedirectUri '{RedirectUri}' – порт HTTP сервера не обновлён",
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
