using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.WinForms.Forms.Settings;

public partial class OAuthSettingsControl : UserControl
{
    private static readonly TwitchSettings DefaultSettings = new();

    private bool _initialized;
    private bool _redirectUriEditable;

    public OAuthSettingsControl()
    {
        InitializeComponent();
        _botAccountSection.Role = TwitchOAuthRole.Bot;
        _broadcasterAccountSection.Role = TwitchOAuthRole.Broadcaster;
        _botAccountSection.FlushParentDraft = FlushDraftFromUi;
        _broadcasterAccountSection.FlushParentDraft = FlushDraftFromUi;
    }

    public event EventHandler? LaunchOnboardingRequested;

    public event EventHandler? SettingChanged;

    [Inject]
    public ITwitchOAuthService OAuthService { get; internal init; } = null!;

    public Action<AppSettings>? FlushDraft { get; set; }

    public void LoadSettings(AppSettings settings, TwitchAccountSettings botDraft, TwitchAccountSettings broadcasterDraft)
    {
        _clientIdTextBox.Text = settings.Twitch.ClientId;
        _clientSecretTextBox.Text = settings.Twitch.ClientSecret;
        _redirectUriTextBox.Text = settings.Twitch.RedirectUri;

        _clientIdTextBox.UseSystemPasswordChar = true;
        _clientSecretTextBox.UseSystemPasswordChar = true;
        _clientIdViewButton.Text = "👁";
        _clientSecretViewButton.Text = "👁";

        _botAccountSection.LoadSettings(settings, botDraft);
        _broadcasterAccountSection.LoadSettings(settings, broadcasterDraft);

        RefreshChannelHint(settings.Twitch.Channel);
        UpdateRedirectUriEditState();
    }

    public void RefreshChannelHint(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            _channelHintLabel.Text = "⚠ Канал не задан – заполните его на вкладке «Основные», иначе авторизация стримера не пройдёт.";
            _channelHintLabel.ForeColor = Color.Red;
        }
        else
        {
            _channelHintLabel.Text = $"Канал: {channel}";
            _channelHintLabel.ForeColor = Color.Gray;
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        settings.Twitch.ClientId = _clientIdTextBox.Text.Trim();
        settings.Twitch.ClientSecret = _clientSecretTextBox.Text.Trim();
        settings.Twitch.RedirectUri = _redirectUriTextBox.Text.Trim();

        _botAccountSection.SaveSettings();
        _broadcasterAccountSection.SaveSettings();
    }

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

        SetPlaceholders();
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnAccountSettingChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnClientIdResetButtonClicked(object sender, EventArgs e)
    {
        _clientIdTextBox.Text = DefaultSettings.ClientId;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnClientSecretResetButtonClicked(object sender, EventArgs e)
    {
        _clientSecretTextBox.Text = DefaultSettings.ClientSecret;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnClientIdViewButtonClicked(object sender, EventArgs e)
    {
        ToggleSecretVisibility(_clientIdTextBox, _clientIdViewButton);
    }

    private void OnClientSecretViewButtonClicked(object sender, EventArgs e)
    {
        ToggleSecretVisibility(_clientSecretTextBox, _clientSecretViewButton);
    }

    private void OnRedirectUriResetButtonClicked(object sender, EventArgs e)
    {
        _redirectUriTextBox.Text = DefaultSettings.RedirectUri;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRedirectUriEditButtonClicked(object sender, EventArgs e)
    {
        _redirectUriEditable = !_redirectUriEditable;
        UpdateRedirectUriEditState();

        if (!_redirectUriEditable)
        {
            SettingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnLaunchOnboardingButtonClicked(object sender, EventArgs e)
    {
        LaunchOnboardingRequested?.Invoke(this, EventArgs.Empty);
    }

    private static void ToggleSecretVisibility(TextBox textBox, Button viewButton)
    {
        textBox.UseSystemPasswordChar = !textBox.UseSystemPasswordChar;
        viewButton.Text = textBox.UseSystemPasswordChar ? "👁" : "🙈";
    }

    private void FlushDraftFromUi(AppSettings settings)
    {
        if (FlushDraft is { } external)
        {
            external(settings);
            return;
        }

        SaveSettings(settings);
    }

    private void SetPlaceholders()
    {
        _clientIdTextBox.PlaceholderText = string.IsNullOrWhiteSpace(DefaultSettings.ClientId)
            ? "Скопируйте из консоли Twitch Dev"
            : DefaultSettings.ClientId;

        _clientSecretTextBox.PlaceholderText = string.IsNullOrWhiteSpace(DefaultSettings.ClientSecret)
            ? "Сгенерируйте в консоли Twitch Dev"
            : DefaultSettings.ClientSecret;

        _redirectUriTextBox.PlaceholderText = DefaultSettings.RedirectUri;
    }

    private void UpdateRedirectUriEditState()
    {
        _redirectUriTextBox.ReadOnly = !_redirectUriEditable;
        _redirectUriEditButton.Text = _redirectUriEditable ? "💾" : "✏";
        _redirectUriResetButton.Enabled = _redirectUriEditable;

        if (_redirectUriEditable)
        {
            _redirectUriTextBox.Focus();
            _redirectUriTextBox.SelectAll();
        }
    }
}
