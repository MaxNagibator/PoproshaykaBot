using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using System.Diagnostics;

namespace PoproshaykaBot.WinForms.Forms.Onboarding.Pages;

public sealed partial class AuthorizationPage : OnboardingPageBase
{
    private static readonly string[] BotDefaultScopes = [..TwitchScopes.BotRequired];
    private static readonly string[] BroadcasterDefaultScopes = [..TwitchScopes.BroadcasterRequired];

    private OnboardingContext? _context;
    private TwitchOAuthRole _role = TwitchOAuthRole.Bot;
    private CancellationTokenSource? _authCts;
    private string? _currentAuthUrl;
    private bool _statusSubscribed;
    private OnboardingContext? _credentialsSubscriptionContext;

    public AuthorizationPage()
    {
        InitializeComponent();
        Disposed += OnPageDisposed;
    }

    [Inject]
    public ITwitchOAuthService OAuthService { get; internal init; } = null!;

    [Inject]
    public SettingsManager SettingsManager { get; internal init; } = null!;

    [Inject]
    public IFormFactory FormFactory { get; internal init; } = null!;

    public TwitchOAuthRole Role
    {
        get => _role;
        set
        {
            _role = value;
            UpdateIntro();
        }
    }

    public override string PageTitle => _role == TwitchOAuthRole.Broadcaster
        ? "Авторизация стримера"
        : "Авторизация бота";

    public override void OnEnter(OnboardingContext context)
    {
        _context = context;

        if (!_statusSubscribed)
        {
            OAuthService.StatusChanged += OnOAuthStatusChanged;
            _statusSubscribed = true;
        }

        if (!ReferenceEquals(_credentialsSubscriptionContext, context))
        {
            if (_credentialsSubscriptionContext is not null)
            {
                _credentialsSubscriptionContext.CredentialsChanged -= OnCredentialsChanged;
            }

            context.CredentialsChanged += OnCredentialsChanged;
            _credentialsSubscriptionContext = context;
        }

        UpdateIntro();
        RefreshFromContext();
    }

    private void OnPageDisposed(object? sender, EventArgs e)
    {
        if (_statusSubscribed)
        {
            OAuthService.StatusChanged -= OnOAuthStatusChanged;
            _statusSubscribed = false;
        }

        if (_credentialsSubscriptionContext is not null)
        {
            _credentialsSubscriptionContext.CredentialsChanged -= OnCredentialsChanged;
            _credentialsSubscriptionContext = null;
        }

        _authCts?.Cancel();
        _authCts?.Dispose();
        _authCts = null;
    }

    private void OnCredentialsChanged(object? sender, EventArgs e)
    {
        if (_context is null || IsDisposed || Disposing)
        {
            return;
        }

        var account = GetAccount();

        if (string.IsNullOrWhiteSpace(account.AccessToken))
        {
            return;
        }

        account.AccessToken = string.Empty;
        account.RefreshToken = string.Empty;
        account.AccessTokenExpiresAt = null;
        account.Scopes = [];

        if (!IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(RefreshFromContext);
        }
        catch (ObjectDisposedException)
        {
            // page torn down
        }
        catch (InvalidOperationException) when (IsDisposed)
        {
            // handle destroyed during the callback
        }
    }

    private void OnEmbeddedLoginButtonClicked(object? sender, EventArgs e)
    {
        if (_context == null || _authCts != null)
        {
            return;
        }

        var settings = _context.Settings.Twitch;
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            _resultLabel.Text = "Не заполнены Client ID или Secret";
            _resultLabel.ForeColor = Color.DarkRed;
            return;
        }

        var scopes = _role == TwitchOAuthRole.Broadcaster ? BroadcasterDefaultScopes : BotDefaultScopes;
        var redirectUri = string.IsNullOrWhiteSpace(settings.RedirectUri) ? null : settings.RedirectUri;
        var checkBroadcasterChannel = _role != TwitchOAuthRole.Broadcaster || !_context.AutoDetectChannel;

        using var dialog = FormFactory.Create<EmbeddedTwitchAuthDialog>();
        dialog.Role = _role;
        dialog.ClientId = settings.ClientId;
        dialog.ClientSecret = settings.ClientSecret;
        dialog.Scopes = scopes;
        dialog.RedirectUri = redirectUri;
        dialog.CheckBroadcasterChannel = checkBroadcasterChannel;

        var result = dialog.ShowDialog(FindForm() ?? (IWin32Window)this);

        if (IsDisposed)
        {
            return;
        }

        if (result == DialogResult.OK && dialog.AuthResult is { } authResult)
        {
            ApplyAuthResult(authResult);
        }

        RefreshFromContext();
    }

    private void OnOpenBrowserButtonClicked(object? sender, EventArgs e)
    {
        if (_authCts != null)
        {
            if (_currentAuthUrl != null)
            {
                OpenInBrowser(_currentAuthUrl);
            }

            return;
        }

        BeginAuthorization(authUrl =>
        {
            _currentAuthUrl = authUrl;
            OpenInBrowser(authUrl);
        });
    }

    private void OnCopyLinkButtonClicked(object? sender, EventArgs e)
    {
        if (_authCts != null)
        {
            if (_currentAuthUrl != null)
            {
                CopyToClipboard(_currentAuthUrl);
            }

            return;
        }

        BeginAuthorization(authUrl =>
        {
            _currentAuthUrl = authUrl;
            CopyToClipboard(authUrl);
        });
    }

    private async void OnCancelButtonClicked(object? sender, EventArgs e)
    {
        if (_authCts is { } pending)
        {
            try
            {
                await pending.CancelAsync();
            }
            catch
            {
                // cancel is best-effort
            }
        }
    }

    private void OnOAuthStatusChanged(TwitchOAuthRole role, string message)
    {
        if (role != _role || IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(() => _statusLabel.Text = message);
        }
        catch (ObjectDisposedException)
        {
            // page torn down
        }
        catch (InvalidOperationException) when (IsDisposed)
        {
            // handle destroyed
        }
    }

    private static string SafeAuthMessage(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "Авторизация отменена",
            TimeoutException => "Время ожидания авторизации истекло",
            HttpRequestException => "Ошибка сети при запросе токена",
            ArgumentException => "Не заполнены Client ID или Client Secret",
            InvalidOperationException => "Ошибка авторизации",
            _ => "Ошибка авторизации",
        };
    }

    private static string AuthErrorDetails(Exception exception)
    {
        return exception switch
        {
            InvalidOperationException invalid => invalid.Message,
            HttpRequestException => "Проверьте подключение к интернету.",
            TimeoutException => "Попробуйте ещё раз.",
            ArgumentException => "Заполните Client ID и Client Secret на предыдущем шаге.",
            _ => "Проверьте Client ID, Client Secret и Redirect URI.",
        };
    }

    private void BeginAuthorization(Action<string> onAuthUrlReady)
    {
        _ = StartAuthorizationAsync(onAuthUrlReady);
    }

    private async Task StartAuthorizationAsync(Action<string> onAuthUrlReady)
    {
        if (_context == null)
        {
            return;
        }

        if (_authCts != null)
        {
            return;
        }

        var settings = _context.Settings.Twitch;
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            _resultLabel.Text = "Не заполнены Client ID или Secret";
            _resultLabel.ForeColor = Color.DarkRed;
            return;
        }

        var cts = new CancellationTokenSource();
        _authCts = cts;
        _currentAuthUrl = null;
        SetButtonsForActiveFlow(true);
        _statusLabel.Text = "Подготовка ссылки...";
        _resultLabel.Text = "";

        var hadError = false;

        try
        {
            var scopes = _role == TwitchOAuthRole.Broadcaster ? BroadcasterDefaultScopes : BotDefaultScopes;
            var redirectUri = string.IsNullOrWhiteSpace(settings.RedirectUri) ? null : settings.RedirectUri;
            var checkBroadcasterChannel = _role != TwitchOAuthRole.Broadcaster || !_context.AutoDetectChannel;

            var result = await OAuthService.StartOAuthFlowToDraftAsync(_role,
                settings.ClientId,
                settings.ClientSecret,
                scopes,
                redirectUri,
                onAuthUrlReady,
                checkBroadcasterChannel,
                cts.Token);

            if (!ReferenceEquals(_authCts, cts))
            {
                return;
            }

            ApplyAuthResult(result);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            hadError = true;
            _statusLabel.Text = "Авторизация отменена";
            _statusLabel.ForeColor = Color.Gray;
            _resultLabel.Text = "";
        }
        catch (Exception exception) when (ReferenceEquals(_authCts, cts))
        {
            hadError = true;
            _resultLabel.Text = SafeAuthMessage(exception);
            _resultLabel.ForeColor = Color.DarkRed;
            _statusLabel.Text = AuthErrorDetails(exception);
            _statusLabel.ForeColor = Color.DarkRed;
        }
        finally
        {
            if (ReferenceEquals(_authCts, cts))
            {
                _authCts = null;
                _currentAuthUrl = null;
                SetButtonsForActiveFlow(false);

                if (hadError)
                {
                    UpdateActionButtonsLabels();
                }
                else
                {
                    RefreshFromContext();
                }
            }

            cts.Dispose();
        }
    }

    private void OpenInBrowser(string authUrl)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true,
            });

            _statusLabel.Text = "Браузер открыт. Подтвердите доступ.";
            _statusLabel.ForeColor = Color.Blue;
        }
        catch (Exception)
        {
            _statusLabel.Text = "Не удалось открыть браузер. Скопируйте ссылку и откройте вручную.";
            _statusLabel.ForeColor = Color.DarkRed;
        }
    }

    private void CopyToClipboard(string authUrl)
    {
        try
        {
            Clipboard.SetText(authUrl);
            _statusLabel.Text = "Ссылка скопирована. Откройте её в браузере и подтвердите доступ.";
            _statusLabel.ForeColor = Color.Blue;
        }
        catch (Exception)
        {
            _statusLabel.Text = "Не удалось скопировать в буфер обмена.";
            _statusLabel.ForeColor = Color.DarkRed;
        }
    }

    private void UpdateActionButtonsLabels()
    {
        var hasToken = !string.IsNullOrWhiteSpace(GetAccount().AccessToken);

        if (hasToken)
        {
            _openBrowserButton.Text = "🔄 Сменить через браузер";
            _copyLinkButton.Text = "⎘ Ссылка для смены";
            _embeddedLoginButton.Text = "🍪 Сменить + обновить куки";
        }
        else
        {
            _openBrowserButton.Text = "🌐 Открыть в браузере";
            _copyLinkButton.Text = "⎘ Скопировать ссылку";
            _embeddedLoginButton.Text = "🍪 Войти + куки чат-плитки";
        }
    }

    private void ApplyAuthResult(OAuthFlowResult result)
    {
        if (_context == null)
        {
            return;
        }

        var account = GetAccount();
        account.AccessToken = result.AccessToken;
        account.RefreshToken = result.RefreshToken;
        account.Login = result.Login;
        account.UserId = result.UserId;
        account.Scopes = result.Scopes;
        account.AccessTokenExpiresAt = result.ExpiresInSeconds > 0
            ? DateTimeOffset.UtcNow.AddSeconds(result.ExpiresInSeconds)
            : null;

        if (_role != TwitchOAuthRole.Broadcaster
            || !_context.AutoDetectChannel
            || string.IsNullOrWhiteSpace(result.Login))
        {
            return;
        }

        _context.Settings.Twitch.Channel = result.Login;
        SettingsManager.UpdateCurrent(settings => settings.Twitch.Channel = result.Login);
    }

    private void RefreshFromContext()
    {
        var account = GetAccount();
        var hasToken = !string.IsNullOrWhiteSpace(account.AccessToken);

        if (hasToken)
        {
            var login = string.IsNullOrWhiteSpace(account.Login) ? "–" : account.Login;
            _resultLabel.Text = $"Авторизован: @{login}";
            _resultLabel.ForeColor = Color.Green;

            if (_authCts == null)
            {
                _statusLabel.Text = "Можно идти дальше или сменить аккаунт.";
                UpdateActionButtonsLabels();
            }

            SetCanAdvance(true);
        }
        else
        {
            if (_authCts == null)
            {
                _resultLabel.Text = "Не авторизован";
                _resultLabel.ForeColor = Color.DarkRed;
                _statusLabel.Text = "Откройте ссылку в браузере и подтвердите доступ.";
                _statusLabel.ForeColor = Color.Gray;
                UpdateActionButtonsLabels();
            }

            SetCanAdvance(false);
        }
    }

    private void SetButtonsForActiveFlow(bool active)
    {
        _cancelButton.Visible = active;
    }

    private TwitchAccountSettings GetAccount()
    {
        if (_context == null)
        {
            throw new InvalidOperationException("OnEnter must be called before accessing context");
        }

        return _role == TwitchOAuthRole.Broadcaster
            ? _context.BroadcasterAccount
            : _context.BotAccount;
    }

    private void UpdateIntro()
    {
        _intro.Text = _role == TwitchOAuthRole.Broadcaster
            ? "Авторизуйте аккаунт стримера. Этот токен нужен для управления опросами и информацией трансляции."
            : "Авторизуйте аккаунт бота. От его имени бот будет писать в чат и слушать сообщения.";
    }
}
