namespace PoproshaykaBot.WinForms.Settings;

public partial class MessagesSettingsControl : UserControl
{
    private static readonly MessageSettings DefaultMessageSettings = new();

    public MessagesSettingsControl()
    {
        InitializeComponent();
        SetPlaceholders();
    }

    public event EventHandler? SettingChanged;

    public void LoadSettings(MessageSettings settings)
    {
        _welcomeMessageEnabledCheckBox.Checked = settings.WelcomeEnabled;
        _welcomeMessageTextBox.Text = settings.Welcome;
        _farewellMessageEnabledCheckBox.Checked = settings.FarewellEnabled;
        _farewellMessageTextBox.Text = settings.Farewell;
        _connectionMessageEnabledCheckBox.Checked = settings.ConnectionEnabled;
        _connectionMessageTextBox.Text = settings.Connection;
        _disconnectionMessageEnabledCheckBox.Checked = settings.DisconnectionEnabled;
        _disconnectionMessageTextBox.Text = settings.Disconnection;
    }

    public void SaveSettings(MessageSettings settings)
    {
        settings.WelcomeEnabled = _welcomeMessageEnabledCheckBox.Checked;
        settings.Welcome = _welcomeMessageTextBox.Text.Trim();
        settings.FarewellEnabled = _farewellMessageEnabledCheckBox.Checked;
        settings.Farewell = _farewellMessageTextBox.Text.Trim();
        settings.ConnectionEnabled = _connectionMessageEnabledCheckBox.Checked;
        settings.Connection = _connectionMessageTextBox.Text.Trim();
        settings.DisconnectionEnabled = _disconnectionMessageEnabledCheckBox.Checked;
        settings.Disconnection = _disconnectionMessageTextBox.Text.Trim();
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWelcomeMessageResetButtonClicked(object sender, EventArgs e)
    {
        ResetWelcomeMessage();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFarewellMessageResetButtonClicked(object sender, EventArgs e)
    {
        ResetFarewellMessage();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnConnectionMessageResetButtonClicked(object sender, EventArgs e)
    {
        ResetConnectionMessage();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDisconnectionMessageResetButtonClicked(object sender, EventArgs e)
    {
        ResetDisconnectionMessage();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetWelcomeMessage()
    {
        _welcomeMessageEnabledCheckBox.Checked = DefaultMessageSettings.WelcomeEnabled;
        _welcomeMessageTextBox.Text = DefaultMessageSettings.Welcome;
    }

    private void ResetFarewellMessage()
    {
        _farewellMessageEnabledCheckBox.Checked = DefaultMessageSettings.FarewellEnabled;
        _farewellMessageTextBox.Text = DefaultMessageSettings.Farewell;
    }

    private void ResetConnectionMessage()
    {
        _connectionMessageEnabledCheckBox.Checked = DefaultMessageSettings.ConnectionEnabled;
        _connectionMessageTextBox.Text = DefaultMessageSettings.Connection;
    }

    private void ResetDisconnectionMessage()
    {
        _disconnectionMessageEnabledCheckBox.Checked = DefaultMessageSettings.DisconnectionEnabled;
        _disconnectionMessageTextBox.Text = DefaultMessageSettings.Disconnection;
    }

    private void SetPlaceholders()
    {
        _welcomeMessageTextBox.PlaceholderText = DefaultMessageSettings.Welcome;
        _farewellMessageTextBox.PlaceholderText = DefaultMessageSettings.Farewell;
        _connectionMessageTextBox.PlaceholderText = DefaultMessageSettings.Connection;
        _disconnectionMessageTextBox.PlaceholderText = DefaultMessageSettings.Disconnection;
    }
}
