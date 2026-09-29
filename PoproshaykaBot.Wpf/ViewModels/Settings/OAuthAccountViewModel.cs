using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Diagnostics;
using System.Globalization;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class OAuthAccountViewModel : ObservableObject, IDisposable
{
    private static readonly string[] BotDefaultScopes = [..TwitchScopes.BotRequired, ..TwitchScopes.BotOptional];
    private static readonly string[] BroadcasterDefaultScopes = [..TwitchScopes.BroadcasterRequired];

    private readonly TwitchOAuthRole _role;
    private readonly ITwitchOAuthService _oauthService;
    private readonly AccountsStore _accountsStore;
    private readonly Func<OAuthCredentialsSnapshot> _credentialsProvider;
    private readonly IDialogService _dialogService;
    private readonly IShellLauncher _shellLauncher;
    private readonly IEmbeddedTwitchAuthDialog _embeddedAuth;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ILogger _logger;

    private TwitchAccountSettings _draft = new();
    private CancellationTokenSource? _authCts;

    [ObservableProperty]
    private string _scopesText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccessToken))]
    [NotifyCanExecuteChangedFor(nameof(ValidateTokenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearTokensCommand))]
    private string _accessTokenValue = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRefreshToken))]
    [NotifyCanExecuteChangedFor(nameof(RefreshTokenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearTokensCommand))]
    private string _refreshTokenValue = string.Empty;

    [ObservableProperty]
    private string _loginText = "–";

    [ObservableProperty]
    private string _tokenStatusText = string.Empty;

    [ObservableProperty]
    private StatusSeverity _tokenStatusSeverity;

    [ObservableProperty]
    private string _lastRefreshText = "Неизвестно";

    [ObservableProperty]
    private string _authStatusText = string.Empty;

    [ObservableProperty]
    private StatusSeverity _authStatusSeverity;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AuthorizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(AuthorizeInAppCommand))]
    private bool _isAuthInProgress;

    public OAuthAccountViewModel(
        TwitchOAuthRole role,
        ITwitchOAuthService oauthService,
        AccountsStore accountsStore,
        Func<OAuthCredentialsSnapshot> credentialsProvider,
        IDialogService dialogService,
        IShellLauncher shellLauncher,
        IEmbeddedTwitchAuthDialog embeddedAuth,
        IUiDispatcher uiDispatcher,
        ILogger logger)
    {
        _role = role;
        _oauthService = oauthService;
        _accountsStore = accountsStore;
        _credentialsProvider = credentialsProvider;
        _dialogService = dialogService;
        _shellLauncher = shellLauncher;
        _embeddedAuth = embeddedAuth;
        _uiDispatcher = uiDispatcher;
        _logger = logger;

        _oauthService.StatusChanged += OnOAuthStatusChanged;
    }

    public event EventHandler? SettingChanged;

    public string AuthorizeButtonText => _role switch
    {
        TwitchOAuthRole.Broadcaster => "Авторизовать стримера в браузере",
        _ => "Авторизовать бота в браузере",
    };

    public string EmbeddedAuthorizeButtonText => _role switch
    {
        TwitchOAuthRole.Broadcaster => "Авторизовать стримера в приложении",
        _ => "Авторизовать бота в приложении",
    };

    public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessTokenValue);

    public bool HasRefreshToken => !string.IsNullOrWhiteSpace(RefreshTokenValue);

    public void Load(TwitchAccountSettings draft)
    {
        _draft = draft;
        ScopesText = string.Join(" ", draft.Scopes);
        LoadTokenInformation();
    }

    public void Save()
    {
        _draft.Scopes = ParseScopes();
    }

    public void Dispose()
    {
        _oauthService.StatusChanged -= OnOAuthStatusChanged;
        _authCts?.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanAuthorize))]
    private async Task AuthorizeAsync()
    {
        var credentials = _credentialsProvider();

        if (!ValidateAuthInputs(credentials))
        {
            return;
        }

        var scopes = GetRequestedScopes();

        var cts = new CancellationTokenSource();
        _authCts = cts;
        IsAuthInProgress = true;
        SetAuthStatus("Авторизация...", StatusSeverity.Info);
        _logger.OAuthBrowserFlowStarted(_role, OAuthAuthorizationSurface.Settings);

        try
        {
            var result = await _oauthService.StartOAuthFlowToDraftAsync(
                _role,
                credentials.ClientId,
                credentials.ClientSecret,
                scopes,
                string.IsNullOrWhiteSpace(credentials.RedirectUri) ? null : credentials.RedirectUri,
                OpenInDefaultBrowser,
                ct: cts.Token);

            if (!ReferenceEquals(_authCts, cts) || string.IsNullOrEmpty(result.AccessToken))
            {
                return;
            }

            ApplyAuthResult(result);
            SetAuthStatus("Авторизация успешна! Не забудьте «Сохранить», чтобы сохранить токены.", StatusSeverity.Success);
            RaiseSettingChanged();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ReferenceEquals(_authCts, cts))
        {
            SetAuthStatus($"Ошибка: {SafeMessage(exception)}", StatusSeverity.Error);
        }
        catch
        {
        }
        finally
        {
            if (ReferenceEquals(_authCts, cts))
            {
                _authCts = null;
                IsAuthInProgress = false;
            }

            cts.Dispose();
        }
    }

    private bool CanAuthorize()
    {
        return !IsAuthInProgress;
    }

    [RelayCommand(CanExecute = nameof(CanAuthorize))]
    private async Task AuthorizeInAppAsync()
    {
        var credentials = _credentialsProvider();

        if (!ValidateAuthInputs(credentials))
        {
            return;
        }

        var scopes = GetRequestedScopes();

        var request = new EmbeddedTwitchAuthRequest(
            _role,
            credentials.ClientId,
            credentials.ClientSecret,
            scopes,
            string.IsNullOrWhiteSpace(credentials.RedirectUri) ? null : credentials.RedirectUri,
            true);

        IsAuthInProgress = true;
        SetAuthStatus("Авторизация во встроенном окне...", StatusSeverity.Info);
        _logger.OAuthEmbeddedFlowStarted(_role, OAuthAuthorizationSurface.Settings);

        try
        {
            var result = await _embeddedAuth.AuthorizeAsync(request);

            if (result is { Outcome: EmbeddedTwitchAuthOutcome.Completed, Flow: { } flow }
                && !string.IsNullOrEmpty(flow.AccessToken))
            {
                ApplyAuthResult(flow);
                SetAuthStatus("Авторизация успешна! Не забудьте «Сохранить», чтобы сохранить токены.", StatusSeverity.Success);
                RaiseSettingChanged();
                return;
            }

            SetAuthStatus(result.Message,
                result.Outcome == EmbeddedTwitchAuthOutcome.Failed ? StatusSeverity.Error : StatusSeverity.Warning);
        }
        finally
        {
            IsAuthInProgress = false;
        }
    }

    [RelayCommand]
    private async Task CancelAuthAsync()
    {
        if (_authCts is not { } pending)
        {
            return;
        }

        _authCts = null;

        try
        {
            await pending.CancelAsync();
        }
        catch
        {
        }

        SetAuthStatus("Авторизация отменена", StatusSeverity.Warning);
        IsAuthInProgress = false;
    }

    [RelayCommand(CanExecute = nameof(HasAccessToken))]
    private async Task ValidateTokenAsync()
    {
        var accessToken = _draft.AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            SetTokenStatus("Токен отсутствует", StatusSeverity.Error);
            return;
        }

        SetTokenStatus("Проверка...", StatusSeverity.Info);

        try
        {
            var isValid = await _oauthService.IsTokenValidAsync(accessToken, CancellationToken.None);

            SetTokenStatus(
                isValid ? "Действителен" : "Недействителен",
                isValid ? StatusSeverity.Success : StatusSeverity.Error);
        }
        catch (Exception exception)
        {
            SetTokenStatus($"Ошибка: {SafeMessage(exception)}", StatusSeverity.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(HasRefreshToken))]
    private async Task RefreshTokenAsync()
    {
        var credentials = _credentialsProvider();
        var refreshToken = _draft.RefreshToken;

        if (string.IsNullOrWhiteSpace(credentials.ClientId) || string.IsNullOrWhiteSpace(credentials.ClientSecret))
        {
            _dialogService.Warning("Настройки отсутствуют", "Client ID и Client Secret должны быть настроены для обновления токена.");
            return;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _dialogService.Warning("Refresh Token отсутствует", "Refresh Token отсутствует. Требуется повторная авторизация.");
            return;
        }

        SetTokenStatus("Обновление...", StatusSeverity.Info);

        try
        {
            await _oauthService.RefreshTokenAsync(_role, credentials.ClientId, credentials.ClientSecret, refreshToken, CancellationToken.None);

            var refreshedLive = _accountsStore.Load(_role);
            _draft.AccessToken = refreshedLive.AccessToken;
            _draft.RefreshToken = refreshedLive.RefreshToken;
            _draft.Login = refreshedLive.Login;
            _draft.UserId = refreshedLive.UserId;
            _draft.Scopes = refreshedLive.Scopes;
            _draft.StoredScopes = refreshedLive.StoredScopes;
            _draft.AccessTokenExpiresAt = refreshedLive.AccessTokenExpiresAt;

            LoadTokenInformation();
            SetTokenStatus("Обновлён успешно", StatusSeverity.Success);
            LastRefreshText = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture);

            RaiseSettingChanged();
        }
        catch (Exception exception)
        {
            SetTokenStatus($"Ошибка: {SafeMessage(exception)}", StatusSeverity.Error);

            _dialogService.Error("Ошибка обновления токена", $"Не удалось обновить токен: {SafeMessage(exception)}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearTokens))]
    private void ClearTokens()
    {
        var roleName = _role == TwitchOAuthRole.Broadcaster ? "стримера" : "бота";

        if (!_dialogService.Confirm("Подтверждение очистки токенов", $"Вы уверены, что хотите очистить токены {roleName}?\n\nИзменения вступят в силу после нажатия «Сохранить»."))
        {
            return;
        }

        _draft.AccessToken = string.Empty;
        _draft.RefreshToken = string.Empty;
        _draft.Login = string.Empty;
        _draft.UserId = string.Empty;
        _draft.AccessTokenExpiresAt = null;
        _draft.StoredScopes = [];

        LoadTokenInformation();
        SetTokenStatus("Токены очищены (применятся после «Сохранить»)", StatusSeverity.Warning);
        LastRefreshText = "Неизвестно";

        RaiseSettingChanged();
    }

    private bool CanClearTokens()
    {
        return HasAccessToken || HasRefreshToken;
    }

    [RelayCommand]
    private void ResetScopes()
    {
        ScopesText = string.Join(" ", GetDefaultScopes());
        RaiseSettingChanged();
    }

    private void ApplyAuthResult(OAuthFlowResult result)
    {
        result.ApplyTo(_draft, DateTimeOffset.UtcNow);

        ScopesText = string.Join(" ", _draft.Scopes);
        LoadTokenInformation();
    }

    private void LoadTokenInformation()
    {
        AccessTokenValue = _draft.AccessToken;
        RefreshTokenValue = _draft.RefreshToken;

        LoginText = string.IsNullOrWhiteSpace(_draft.Login) ? "–" : _draft.Login;

        if (string.IsNullOrWhiteSpace(_draft.AccessToken))
        {
            SetTokenStatus("Токен отсутствует", StatusSeverity.Error);
            return;
        }

        if (_draft.AccessTokenExpiresAt is not { } expiresAt)
        {
            SetTokenStatus("Не проверен", StatusSeverity.None);
            return;
        }

        var expiresAtLocal = expiresAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);

        if (expiresAt > DateTimeOffset.Now)
        {
            SetTokenStatus($"действует до {expiresAtLocal}", StatusSeverity.None);
        }
        else
        {
            SetTokenStatus($"истёк {expiresAtLocal}", StatusSeverity.Warning);
        }
    }

    private bool ValidateAuthInputs(OAuthCredentialsSnapshot credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.ClientId))
        {
            SetAuthStatus("Введите Client ID", StatusSeverity.Error);
            return false;
        }

        if (string.IsNullOrWhiteSpace(credentials.ClientSecret))
        {
            SetAuthStatus("Введите Client Secret", StatusSeverity.Error);
            return false;
        }

        if (_role == TwitchOAuthRole.Broadcaster && string.IsNullOrWhiteSpace(credentials.Channel))
        {
            SetAuthStatus("Не задан Channel в настройках Twitch", StatusSeverity.Error);
            return false;
        }

        return true;
    }

    private string[] ParseScopes()
    {
        return ScopesText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private string[] GetRequestedScopes()
    {
        var required = _role == TwitchOAuthRole.Broadcaster ? TwitchScopes.BroadcasterRequired : TwitchScopes.BotRequired;

        return ParseScopes().Union(required, StringComparer.Ordinal).ToArray();
    }

    private string[] GetDefaultScopes()
    {
        return _role == TwitchOAuthRole.Broadcaster ? BroadcasterDefaultScopes : BotDefaultScopes;
    }

    private void OnOAuthStatusChanged(TwitchOAuthRole role, string message)
    {
        if (role != _role)
        {
            return;
        }

        _uiDispatcher.Invoke(() => SetAuthStatus(message, StatusSeverity.Info));
    }

    private void SetAuthStatus(string text, StatusSeverity severity)
    {
        AuthStatusText = text;
        AuthStatusSeverity = severity;
    }

    private void SetTokenStatus(string text, StatusSeverity severity)
    {
        TokenStatusText = text;
        TokenStatusSeverity = severity;
    }

    private void RaiseSettingChanged()
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OpenInDefaultBrowser(string authUrl)
    {
        _shellLauncher.Open(authUrl);
    }

    private static string SafeMessage(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "операция отменена",
            TimeoutException => "превышено время ожидания",
            _ => "не удалось выполнить операцию, проверьте настройки и подключение",
        };
    }
}
