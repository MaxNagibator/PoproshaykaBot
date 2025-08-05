namespace PoproshaykaBot.WinForms.Settings;

public partial class RateLimitingSettingsControl : UserControl
{
    private static readonly TwitchSettings DefaultSettings = new();

    public RateLimitingSettingsControl()
    {
        InitializeComponent();
    }

    public event EventHandler? SettingChanged;

    public void LoadSettings(TwitchSettings settings)
    {
        _messagesAllowedNumeric.Value = settings.MessagesAllowedInPeriod;
        _throttlingPeriodNumeric.Value = settings.ThrottlingPeriodSeconds;
    }

    public void SaveSettings(TwitchSettings settings)
    {
        settings.MessagesAllowedInPeriod = (int)_messagesAllowedNumeric.Value;
        settings.ThrottlingPeriodSeconds = (int)_throttlingPeriodNumeric.Value;
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMessagesAllowedResetButtonClicked(object sender, EventArgs e)
    {
        ResetMessagesAllowed();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnThrottlingPeriodResetButtonClicked(object sender, EventArgs e)
    {
        ResetThrottlingPeriod();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetMessagesAllowed()
    {
        _messagesAllowedNumeric.Value = DefaultSettings.MessagesAllowedInPeriod;
    }

    private void ResetThrottlingPeriod()
    {
        _throttlingPeriodNumeric.Value = DefaultSettings.ThrottlingPeriodSeconds;
    }
}
