namespace PoproshaykaBot.WinForms.Settings;

public partial class SettingsForm : Form
{
    private AppSettings _settings;
    private bool _hasChanges;

    public SettingsForm()
    {
        _settings = new();
        CopySettings(SettingsManager.Current, _settings);
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

    private static void CopySettings(AppSettings source, AppSettings destination)
    {
        destination.Twitch.BotUsername = source.Twitch.BotUsername;
        destination.Twitch.Channel = source.Twitch.Channel;
        destination.Twitch.MessagesAllowedInPeriod = source.Twitch.MessagesAllowedInPeriod;
        destination.Twitch.ThrottlingPeriodSeconds = source.Twitch.ThrottlingPeriodSeconds;
        destination.Twitch.ClientId = source.Twitch.ClientId;
        destination.Twitch.ClientSecret = source.Twitch.ClientSecret;
        destination.Twitch.AccessToken = source.Twitch.AccessToken;
        destination.Twitch.RefreshToken = source.Twitch.RefreshToken;
        destination.Twitch.RedirectUri = source.Twitch.RedirectUri;
        destination.Twitch.Scopes = source.Twitch.Scopes;
        destination.Twitch.HttpServerPort = source.Twitch.HttpServerPort;
        destination.Twitch.HttpServerEnabled = source.Twitch.HttpServerEnabled;
        destination.Twitch.ObsOverlayEnabled = source.Twitch.ObsOverlayEnabled;
        destination.Twitch.Messages.WelcomeEnabled = source.Twitch.Messages.WelcomeEnabled;
        destination.Twitch.Messages.Welcome = source.Twitch.Messages.Welcome;
        destination.Twitch.Messages.FarewellEnabled = source.Twitch.Messages.FarewellEnabled;
        destination.Twitch.Messages.Farewell = source.Twitch.Messages.Farewell;
        destination.Twitch.Messages.ConnectionEnabled = source.Twitch.Messages.ConnectionEnabled;
        destination.Twitch.Messages.Connection = source.Twitch.Messages.Connection;
        destination.Twitch.Messages.DisconnectionEnabled = source.Twitch.Messages.DisconnectionEnabled;
        destination.Twitch.Messages.Disconnection = source.Twitch.Messages.Disconnection;
        destination.Twitch.ObsChat.BackgroundColor = source.Twitch.ObsChat.BackgroundColor;
        destination.Twitch.ObsChat.TextColor = source.Twitch.ObsChat.TextColor;
        destination.Twitch.ObsChat.UsernameColor = source.Twitch.ObsChat.UsernameColor;
        destination.Twitch.ObsChat.SystemMessageColor = source.Twitch.ObsChat.SystemMessageColor;
        destination.Twitch.ObsChat.TimestampColor = source.Twitch.ObsChat.TimestampColor;
        destination.Twitch.ObsChat.FontFamily = source.Twitch.ObsChat.FontFamily;
        destination.Twitch.ObsChat.FontSize = source.Twitch.ObsChat.FontSize;
        destination.Twitch.ObsChat.FontBold = source.Twitch.ObsChat.FontBold;
        destination.Twitch.ObsChat.Padding = source.Twitch.ObsChat.Padding;
        destination.Twitch.ObsChat.Margin = source.Twitch.ObsChat.Margin;
        destination.Twitch.ObsChat.BorderRadius = source.Twitch.ObsChat.BorderRadius;
        destination.Twitch.ObsChat.AnimationDuration = source.Twitch.ObsChat.AnimationDuration;
        destination.Twitch.ObsChat.EnableAnimations = source.Twitch.ObsChat.EnableAnimations;
        destination.Twitch.ObsChat.MaxMessages = source.Twitch.ObsChat.MaxMessages;
        destination.Twitch.ObsChat.ShowTimestamp = source.Twitch.ObsChat.ShowTimestamp;
        destination.Ui.ShowLogsPanel = source.Ui.ShowLogsPanel;
        destination.Ui.ShowChatPanel = source.Ui.ShowChatPanel;
    }

    private void LoadSettingsToControls()
    {
        _basicSettingsControl.LoadSettings(_settings.Twitch);
        _rateLimitingSettingsControl.LoadSettings(_settings.Twitch);
        _messagesSettingsControl.LoadSettings(_settings.Twitch.Messages);
        _httpServerSettingsControl.LoadSettings(_settings.Twitch);
        _oauthSettingsControl.LoadSettings(_settings);
        _obsChatSettingsControl.LoadSettings(_settings.Twitch.ObsChat);

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
            SettingsManager.SaveSettings(_settings);
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
