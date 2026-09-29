using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using System.ComponentModel;
using System.Diagnostics;

namespace PoproshaykaBot.WinForms.Forms.Settings;

public sealed partial class OAuthAccountSection : UserControl
{
    private static readonly string[] BotDefaultScopes = [..TwitchScopes.BotRequired];
    private static readonly string[] BroadcasterDefaultScopes = [..TwitchScopes.BroadcasterRequired];

    private AppSettings _settings = new();
    private TwitchAccountSettings _draft = new();
    private TwitchOAuthRole _role = TwitchOAuthRole.Bot;
    private bool _initialized;
    private bool _tokensVisible;
    private CancellationTokenSource? _authCts;

    public OAuthAccountSection()
    {
        InitializeComponent();
    }

    public event EventHandler? SettingChanged;

    [Inject]
    public AccountsStore AccountsStore { get; internal init; } = null!;

    [Inject]
    public ITwitchOAuthService OAuthService { get; internal init; } = null!;

    public Action<AppSettings>? FlushParentDraft { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwitchOAuthRole Role
    {
        get => _role;
        set
        {
            if (_role == value)
            {
                return;
            }

            _role = value;
            ApplyRoleLabels();
        }
    }

    public void LoadSettings(AppSettings settings, TwitchAccountSettings draft)
    {
        _settings = settings;
        _draft = draft;

        _scopesTextBox.Text = string.Join(" ", _draft.Scopes);

        LoadTokenInformation();
    }

    public void SaveSettings()
    {
        _draft.Scopes = _scopesTextBox.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
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

        ApplyRoleLabels();
        SetPlaceholders();

        OAuthService.StatusChanged += OnOAuthStatusChanged;
        Disposed += (_, _) => OAuthService.StatusChanged -= OnOAuthStatusChanged;
    }

    private void OnScopesTextChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnScopesResetButtonClicked(object? sender, EventArgs e)
    {
        _scopesTextBox.Text = string.Join(" ", GetDefaultScopes());
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnTestAuthButtonClicked(object? sender, EventArgs e)
    {
        if (await TryCancelPendingAuthAsync())
        {
            return;
        }

        FlushParentDraft?.Invoke(_settings);

        if (!ValidateAuthInputs(out var clientId, out var clientSecret))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _authCts = cts;
        SetAuthButtonToCancelMode();
        _authStatusLabel.Text = "Авторизация...";
        _authStatusLabel.ForeColor = Color.Blue;

        try
        {
            await RunAuthFlowAsync(clientId, clientSecret, cts);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // user cancelled the flow
        }
        catch (Exception exception) when (ReferenceEquals(_authCts, cts))
        {
            _authStatusLabel.Text = $"Ошибка: {exception.Message}";
            _authStatusLabel.ForeColor = Color.Red;
        }
        catch
        {
            // newer flow superseded this one; status will be set elsewhere
        }
        finally
        {
            if (ReferenceEquals(_authCts, cts))
            {
                _authCts = null;
                RestoreAuthButtonMode();
            }

            cts.Dispose();
        }
    }

    private async void OnValidateTokenButtonClicked(object? sender, EventArgs e)
    {
        var accessToken = GetLiveAccount().AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _tokenStatusValueLabel.Text = "Токен отсутствует";
            _tokenStatusValueLabel.ForeColor = Color.Red;
            return;
        }

        _validateTokenButton.Enabled = false;
        _tokenStatusValueLabel.Text = "Проверка...";
        _tokenStatusValueLabel.ForeColor = Color.Blue;

        try
        {
            var isValid = await OAuthService.IsTokenValidAsync(accessToken);

            if (isValid)
            {
                _tokenStatusValueLabel.Text = "Действителен";
                _tokenStatusValueLabel.ForeColor = Color.Green;
            }
            else
            {
                _tokenStatusValueLabel.Text = "Недействителен";
                _tokenStatusValueLabel.ForeColor = Color.Red;
            }
        }
        catch (Exception exception)
        {
            _tokenStatusValueLabel.Text = $"Ошибка: {exception.Message}";
            _tokenStatusValueLabel.ForeColor = Color.Red;
        }
        finally
        {
            _validateTokenButton.Enabled = true;
        }
    }

    private async void OnRefreshTokenButtonClicked(object? sender, EventArgs e)
    {
        var clientId = _settings.Twitch.ClientId;
        var clientSecret = _settings.Twitch.ClientSecret;
        var refreshToken = GetLiveAccount().RefreshToken;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            MessageBox.Show("Client ID и Client Secret должны быть настроены для обновления токена.",
                "Настройки отсутствуют", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            MessageBox.Show("Refresh Token отсутствует. Требуется повторная авторизация.",
                "Refresh Token отсутствует", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return;
        }

        _refreshTokenButton.Enabled = false;
        _tokenStatusValueLabel.Text = "Обновление...";
        _tokenStatusValueLabel.ForeColor = Color.Blue;

        try
        {
            await OAuthService.RefreshTokenAsync(_role, clientId, clientSecret, refreshToken);

            var refreshedLive = AccountsStore.Load(_role);
            _draft.AccessToken = refreshedLive.AccessToken;
            _draft.RefreshToken = refreshedLive.RefreshToken;
            _draft.Login = refreshedLive.Login;
            _draft.UserId = refreshedLive.UserId;
            _draft.Scopes = refreshedLive.Scopes;
            _draft.AccessTokenExpiresAt = refreshedLive.AccessTokenExpiresAt;

            LoadTokenInformation();
            _tokenStatusValueLabel.Text = "Обновлен успешно";
            _tokenStatusValueLabel.ForeColor = Color.Green;
            _lastRefreshValueLabel.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            _lastRefreshValueLabel.ForeColor = Color.Black;

            SettingChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            _tokenStatusValueLabel.Text = $"Ошибка: {exception.Message}";
            _tokenStatusValueLabel.ForeColor = Color.Red;

            MessageBox.Show($"Не удалось обновить токен: {exception.Message}",
                "Ошибка обновления токена", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _refreshTokenButton.Enabled = true;
        }
    }

    private void OnClearTokensButtonClicked(object? sender, EventArgs e)
    {
        var roleName = _role == TwitchOAuthRole.Broadcaster ? "стримера" : "бота";
        var result = MessageBox.Show($"Вы уверены, что хотите очистить токены {roleName}?\n\nИзменения вступят в силу после нажатия «Применить».",
            "Подтверждение очистки токенов",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        _draft.AccessToken = string.Empty;
        _draft.RefreshToken = string.Empty;
        _draft.Login = string.Empty;
        _draft.UserId = string.Empty;
        _draft.AccessTokenExpiresAt = null;

        LoadTokenInformation();
        _tokenStatusValueLabel.Text = "Токены очищены (применятся после «Применить»)";
        _tokenStatusValueLabel.ForeColor = Color.Orange;
        _lastRefreshValueLabel.Text = "Неизвестно";
        _lastRefreshValueLabel.ForeColor = Color.Gray;

        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnShowTokenButtonClicked(object? sender, EventArgs e)
    {
        _tokensVisible = !_tokensVisible;
        UpdateTokenDisplay();
        UpdateShowTokenButton();
    }

    private void OnOAuthStatusChanged(TwitchOAuthRole role, string message)
    {
        if (role != _role)
        {
            return;
        }

        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(() =>
            {
                _authStatusLabel.Text = message;
                _authStatusLabel.ForeColor = Color.Blue;
            });
        }
        catch (ObjectDisposedException)
        {
            // section was disposed before status reached UI thread
        }
        catch (InvalidOperationException) when (IsDisposed)
        {
            // handle destroyed
        }
    }

    private static void OpenInDefaultBrowser(string authUrl)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = authUrl,
            UseShellExecute = true,
        });
    }

    private async Task<bool> TryCancelPendingAuthAsync()
    {
        if (_authCts is not { } pending)
        {
            return false;
        }

        _authCts = null;
        try
        {
            await pending.CancelAsync();
        }
        catch
        {
            // cancel is best-effort
        }

        _authStatusLabel.Text = "Авторизация отменена";
        _authStatusLabel.ForeColor = Color.Orange;
        RestoreAuthButtonMode();
        return true;
    }

    private bool ValidateAuthInputs(out string clientId, out string clientSecret)
    {
        clientId = _settings.Twitch.ClientId;
        clientSecret = _settings.Twitch.ClientSecret;

        if (string.IsNullOrWhiteSpace(clientId))
        {
            _authStatusLabel.Text = "Введите Client ID";
            _authStatusLabel.ForeColor = Color.Red;
            return false;
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            _authStatusLabel.Text = "Введите Client Secret";
            _authStatusLabel.ForeColor = Color.Red;
            return false;
        }

        if (_role == TwitchOAuthRole.Broadcaster && string.IsNullOrWhiteSpace(_settings.Twitch.Channel))
        {
            _authStatusLabel.Text = "Не задан Channel в настройках Twitch";
            _authStatusLabel.ForeColor = Color.Red;
            return false;
        }

        return true;
    }

    private async Task RunAuthFlowAsync(string clientId, string clientSecret, CancellationTokenSource cts)
    {
        var scopes = _scopesTextBox.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var redirectUri = _settings.Twitch.RedirectUri;

        if (scopes.Length == 0)
        {
            scopes = GetDefaultScopes();
        }

        var result = await OAuthService.StartOAuthFlowToDraftAsync(_role,
            clientId,
            clientSecret,
            scopes,
            string.IsNullOrWhiteSpace(redirectUri) ? null : redirectUri,
            OpenInDefaultBrowser,
            ct: cts.Token);

        if (!ReferenceEquals(_authCts, cts))
        {
            return;
        }

        if (string.IsNullOrEmpty(result.AccessToken))
        {
            return;
        }

        _draft.AccessToken = result.AccessToken;
        _draft.RefreshToken = result.RefreshToken;
        _draft.Login = result.Login;
        _draft.UserId = result.UserId;
        _draft.Scopes = result.Scopes;
        _draft.AccessTokenExpiresAt = result.ExpiresInSeconds > 0
            ? DateTimeOffset.UtcNow.AddSeconds(result.ExpiresInSeconds)
            : null;

        _scopesTextBox.Text = string.Join(" ", _draft.Scopes);

        _authStatusLabel.Text = "Авторизация успешна! Не забудьте «Применить», чтобы сохранить токены.";
        _authStatusLabel.ForeColor = Color.Green;

        LoadTokenInformation();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private TwitchAccountSettings GetLiveAccount()
    {
        return _draft;
    }

    private string[] GetDefaultScopes()
    {
        return _role == TwitchOAuthRole.Broadcaster ? BroadcasterDefaultScopes : BotDefaultScopes;
    }

    private void SetAuthButtonToCancelMode()
    {
        _testAuthButton.Enabled = true;
        _testAuthButton.Text = "Отменить авторизацию";
    }

    private void RestoreAuthButtonMode()
    {
        _testAuthButton.Enabled = true;
        ApplyRoleLabels();
    }

    private void ApplyRoleLabels()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        _sectionHeaderLabel.Text = _role switch
        {
            TwitchOAuthRole.Bot => "Авторизация бота",
            TwitchOAuthRole.Broadcaster => "Авторизация стримера",
            _ => "Авторизация",
        };

        _testAuthButton.Text = _role switch
        {
            TwitchOAuthRole.Bot => "Авторизовать бота",
            TwitchOAuthRole.Broadcaster => "Авторизовать стримера",
            _ => "Авторизовать",
        };
    }

    private void UpdateShowTokenButton()
    {
        _showTokenButton.Text = _tokensVisible ? "🙈" : "👁";
    }

    private void UpdateTokenDisplay()
    {
        var account = GetLiveAccount();
        var accessToken = account.AccessToken;
        var refreshToken = account.RefreshToken;

        _accessTokenTextBox.Text = !string.IsNullOrWhiteSpace(accessToken) ? accessToken : "Не установлен";
        _refreshTokenTextBox.Text = !string.IsNullOrWhiteSpace(refreshToken) ? refreshToken : "Не установлен";

        _accessTokenTextBox.UseSystemPasswordChar = !_tokensVisible && !string.IsNullOrWhiteSpace(accessToken);
        _refreshTokenTextBox.UseSystemPasswordChar = !_tokensVisible && !string.IsNullOrWhiteSpace(refreshToken);
    }

    private void SetPlaceholders()
    {
        _scopesTextBox.PlaceholderText = string.Join(" ", GetDefaultScopes());
    }

    private void LoadTokenInformation()
    {
        var account = GetLiveAccount();
        var accessToken = account.AccessToken;
        var refreshToken = account.RefreshToken;
        var login = account.Login;

        UpdateTokenDisplay();
        UpdateShowTokenButton();

        if (!string.IsNullOrWhiteSpace(login))
        {
            _accountLoginValueLabel.Text = login;
            _accountLoginValueLabel.ForeColor = Color.Black;
        }
        else
        {
            _accountLoginValueLabel.Text = "–";
            _accountLoginValueLabel.ForeColor = Color.Gray;
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _tokenStatusValueLabel.Text = "Токен отсутствует";
            _tokenStatusValueLabel.ForeColor = Color.Red;
        }
        else
        {
            _tokenStatusValueLabel.Text = "Не проверен";
            _tokenStatusValueLabel.ForeColor = Color.Gray;
        }

        var hasTokens = !string.IsNullOrWhiteSpace(accessToken);
        var hasRefreshToken = !string.IsNullOrWhiteSpace(refreshToken);
        var hasCredentials = !string.IsNullOrWhiteSpace(_settings.Twitch.ClientId)
                             && !string.IsNullOrWhiteSpace(_settings.Twitch.ClientSecret);

        _validateTokenButton.Enabled = hasTokens;
        _refreshTokenButton.Enabled = hasRefreshToken && hasCredentials;
        _clearTokensButton.Enabled = hasTokens || hasRefreshToken;
        _showTokenButton.Enabled = hasTokens || hasRefreshToken;
    }
}
