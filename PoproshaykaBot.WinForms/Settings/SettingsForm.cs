using System.Text.Json;

namespace PoproshaykaBot.WinForms.Settings;

public partial class SettingsForm : Form
{
    private readonly SettingsManager _settingsManager;
    private AppSettings _settings;
    private bool _hasChanges;

    public SettingsForm(SettingsManager settingsManager, TwitchOAuthService oauthService, KestrelHttpServer httpServer)
    {
        _settingsManager = settingsManager;
        _settings = new();

        _settings = CopySettings(settingsManager.Current);

        _oauthSettingsControl = new(settingsManager, oauthService);
        _miscSettingsControl = new(settingsManager);
        _httpServerSettingsControl = new(httpServer);

        InitializeComponent();
        LoadSettingsToControls();
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        _hasChanges = true;
        UpdateButtonStates();
    }

    private void OnOkButtonClicked(object sender, EventArgs e)
    {
        if (_hasChanges)
        {
            ApplySettings();
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnCancelButtonClicked(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void OnApplyButtonClicked(object sender, EventArgs e)
    {
        ApplySettings();
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
        LoadSettingsToControls();
        _hasChanges = true;
        UpdateButtonStates();
    }

    private static AppSettings CopySettings(AppSettings source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<AppSettings>(json)!;
    }

    private void LoadSettingsToControls()
    {
        _basicSettingsControl.LoadSettings(_settings.Twitch);
        _rateLimitingSettingsControl.LoadSettings(_settings.Twitch);
        _messagesSettingsControl.LoadSettings(_settings.Twitch.Messages);
        _httpServerSettingsControl.LoadSettings(_settings.Twitch);
        _oauthSettingsControl.LoadSettings(_settings);
        _obsChatSettingsControl.LoadSettings(_settings.Twitch.ObsChat);
        _autoBroadcastSettingsControl.LoadSettings(_settings.Twitch.AutoBroadcast);
        _miscSettingsControl.LoadSettings(_settings);

        _hasChanges = false;
        UpdateButtonStates();
    }

    private void SaveSettingsFromControls()
    {
        _basicSettingsControl.SaveSettings(_settings.Twitch);
        _rateLimitingSettingsControl.SaveSettings(_settings.Twitch);
        _messagesSettingsControl.SaveSettings(_settings.Twitch.Messages);
        _httpServerSettingsControl.SaveSettings(_settings.Twitch);
        _oauthSettingsControl.SaveSettings(_settings);
        _obsChatSettingsControl.SaveSettings(_settings.Twitch.ObsChat);
        _autoBroadcastSettingsControl.SaveSettings(_settings.Twitch.AutoBroadcast);
        _miscSettingsControl.SaveSettings(_settings);
    }

    private void UpdateButtonStates()
    {
        _applyButton.Enabled = _hasChanges;
    }

    private void ApplySettings()
    {
        try
        {
            SaveSettingsFromControls();
            _settingsManager.SaveSettings(_settings);
            _hasChanges = false;
            UpdateButtonStates();

            MessageBox.Show("Настройки успешно сохранены.", "Настройки",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Ошибка сохранения настроек: {exception.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
