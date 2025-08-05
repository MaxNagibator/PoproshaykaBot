namespace PoproshaykaBot.WinForms.Settings;

public partial class BasicSettingsControl : UserControl
{
    private static readonly TwitchSettings DefaultSettings = new();

    public BasicSettingsControl()
    {
        InitializeComponent();
        SetPlaceholders();
    }

    public event EventHandler? SettingChanged;

    public void LoadSettings(TwitchSettings settings)
    {
        _botUsernameTextBox.Text = settings.BotUsername;
        _channelTextBox.Text = settings.Channel;
    }

    public void SaveSettings(TwitchSettings settings)
    {
        settings.BotUsername = _botUsernameTextBox.Text.Trim();
        settings.Channel = _channelTextBox.Text.Trim();
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBotUsernameResetButtonClicked(object sender, EventArgs e)
    {
        ResetBotUsername();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnChannelResetButtonClicked(object sender, EventArgs e)
    {
        ResetChannel();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetBotUsername()
    {
        _botUsernameTextBox.Text = DefaultSettings.BotUsername;
    }

    private void ResetChannel()
    {
        _channelTextBox.Text = DefaultSettings.Channel;
    }

    private void SetPlaceholders()
    {
        _botUsernameTextBox.PlaceholderText = DefaultSettings.BotUsername;
        _channelTextBox.PlaceholderText = DefaultSettings.Channel;
    }
}
