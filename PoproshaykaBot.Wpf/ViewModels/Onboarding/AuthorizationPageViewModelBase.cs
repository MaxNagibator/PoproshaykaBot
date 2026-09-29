using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;
using System.Diagnostics;
using System.Net.Http;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public abstract partial class AuthorizationPageViewModelBase : OnboardingPageViewModelBase, IDisposable
{
    private static readonly string[] BotDefaultScopes = [..TwitchScopes.BotRequired, ..TwitchScopes.BotOptional];
    private static readonly string[] BroadcasterDefaultScopes = [..TwitchScopes.BroadcasterRequired];

    private readonly TwitchOAuthRole _role;
    private readonly ITwitchOAuthService _oauthService;
    private readonly SettingsManager _settingsManager;
    private readonly ILogger _logger;
    private readonly IShellLauncher _shellLauncher;
    private readonly IClipboardService _clipboard;
    private readonly IEmbeddedTwitchAuthDialog _embeddedAuth;
    private readonly IUiDispatcher _uiDispatcher;

    private OnboardingContext? _context;
    private OnboardingContext? _credentialsSubscriptionContext;
    private CancellationTokenSource? _authCts;
    private string? _currentAuthUrl;
    private bool _statusSubscribed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenBrowserButtonText))]
    [NotifyPropertyChangedFor(nameof(CopyLinkButtonText))]
    [NotifyPropertyChangedFor(nameof(EmbeddedAuthButtonText))]
    private bool _hasToken;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private StatusSeverity _resultSeverity;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private StatusSeverity _statusMessageSeverity;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AuthorizeInAppCommand))]
    private bool _isAuthInProgress;

    protected AuthorizationPageViewModelBase(
        TwitchOAuthRole role,
        ITwitchOAuthService oauthService,
        SettingsManager settingsManager,
        ILogger logger,
        IShellLauncher shellLauncher,
        IClipboardService clipboard,
        IEmbeddedTwitchAuthDialog embeddedAuth,
        IUiDispatcher uiDispatcher)
    {
        _role = role;
        _oauthService = oauthService;
        _settingsManager = settingsManager;
        _logger = logger;
        _shellLauncher = shellLauncher;
        _clipboard = clipboard;
        _embeddedAuth = embeddedAuth;
        _uiDispatcher = uiDispatcher;
    }

    public override string PageTitle => _role == TwitchOAuthRole.Broadcaster
        ? "Авторизация стримера"
        : "Авторизация бота";

    public string IntroText => _role == TwitchOAuthRole.Broadcaster
        ? "Авторизуйте аккаунт стримера. Этот токен нужен для управления опросами и информацией трансляции."
        : "Авторизуйте аккаунт бота. От его имени бот будет писать в чат и слушать сообщения.";

    public string OpenBrowserButtonText => HasToken
        ? "Сменить через браузер"
        : "Открыть в браузере";

    public string CopyLinkButtonText => HasToken
        ? "Ссылка для смены"
        : "Скопировать ссылку";

    public string EmbeddedAuthButtonText => HasToken
        ? "Сменить в приложении"
        : "Войти в приложении";

    public override void OnEnter(OnboardingContext context)
    {
        _context = context;

        if (!ReferenceEquals(_credentialsSubscriptionContext, context))
        {
            if (_credentialsSubscriptionContext is not null)
            {
                _credentialsSubscriptionContext.CredentialsChanged -= OnCredentialsChanged;
            }

            context.CredentialsChanged += OnCredentialsChanged;
            _credentialsSubscriptionContext = context;
        }

        RefreshFromContext();
    }

    public void Dispose()
    {
        UnsubscribeStatus();

        if (_credentialsSubscriptionContext is not null)
        {
            _credentialsSubscriptionContext.CredentialsChanged -= OnCredentialsChanged;
            _credentialsSubscriptionContext = null;
        }

        _authCts?.Cancel();
        _authCts?.Dispose();
        _authCts = null;

        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    private async Task OpenBrowserAsync()
    {
        if (IsAuthInProgress)
        {
            if (_currentAuthUrl is { } pendingUrl)
            {
                OpenInBrowser(pendingUrl);
            }

            return;
        }

        await StartAuthorizationAsync(authUrl =>
        {
            _currentAuthUrl = authUrl;
            OpenInBrowser(authUrl);
        });
    }

    [RelayCommand]
    private async Task CopyLinkAsync()
    {
        if (IsAuthInProgress)
        {
            if (_currentAuthUrl is { } pendingUrl)
            {
                CopyToClipboard(pendingUrl);
            }

            return;
        }

        await StartAuthorizationAsync(authUrl =>
        {
            _currentAuthUrl = authUrl;
            CopyToClipboard(authUrl);
        });
    }

    [RelayCommand(CanExecute = nameof(CanStartEmbeddedAuth))]
    private async Task AuthorizeInAppAsync()
    {
        if (_context is null || _authCts is not null || IsAuthInProgress)
        {
            return;
        }

        var settings = _context.Settings.Twitch;

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            ResultText = "Не заполнены Client ID или Secret";
            ResultSeverity = StatusSeverity.Error;
            return;
        }

        var request = new EmbeddedTwitchAuthRequest(
            _role,
            settings.ClientId,
            settings.ClientSecret,
            GetDefaultScopes(),
            string.IsNullOrWhiteSpace(settings.RedirectUri) ? null : settings.RedirectUri,
            _role != TwitchOAuthRole.Broadcaster || !_context.AutoDetectChannel);

        IsAuthInProgress = true;
        ResultText = string.Empty;
        StatusMessage = "Открываем встроенное окно авторизации...";
        StatusMessageSeverity = StatusSeverity.Info;
        _logger.OAuthEmbeddedFlowStarted(_role, OAuthAuthorizationSurface.OnboardingWizard);

        var completed = false;

        try
        {
            var result = await _embeddedAuth.AuthorizeAsync(request);

            if (result is { Outcome: EmbeddedTwitchAuthOutcome.Completed, Flow: { } flow })
            {
                ApplyAuthResult(flow);
                completed = true;
            }
            else if (result.Outcome is EmbeddedTwitchAuthOutcome.Canceled or EmbeddedTwitchAuthOutcome.Unavailable)
            {
                StatusMessage = result.Message;
                StatusMessageSeverity = StatusSeverity.Warning;
            }
            else
            {
                ResultText = "Ошибка авторизации";
                ResultSeverity = StatusSeverity.Error;
                StatusMessage = result.Message;
                StatusMessageSeverity = StatusSeverity.Error;
            }
        }
        finally
        {
            IsAuthInProgress = false;
        }

        if (completed)
        {
            RefreshFromContext();
        }
    }

    private bool CanStartEmbeddedAuth()
    {
        return !IsAuthInProgress;
    }

    [RelayCommand]
    private async Task CancelAuthAsync()
    {
        if (_authCts is not { } pending)
        {
            return;
        }

        try
        {
            await pending.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task StartAuthorizationAsync(Action<string> onAuthUrlReady)
    {
        if (_context is null || _authCts is not null)
        {
            return;
        }

        var settings = _context.Settings.Twitch;

        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            ResultText = "Не заполнены Client ID или Secret";
            ResultSeverity = StatusSeverity.Error;
            return;
        }

        var cts = new CancellationTokenSource();
        _authCts = cts;
        _currentAuthUrl = null;
        IsAuthInProgress = true;
        _logger.OAuthBrowserFlowStarted(_role, OAuthAuthorizationSurface.OnboardingWizard);
        StatusMessage = "Подготовка ссылки...";
        StatusMessageSeverity = StatusSeverity.Info;
        ResultText = string.Empty;
        SubscribeStatus();

        var hadError = false;

        try
        {
            var scopes = GetDefaultScopes();
            var redirectUri = string.IsNullOrWhiteSpace(settings.RedirectUri) ? null : settings.RedirectUri;
            var checkBroadcasterChannel = _role != TwitchOAuthRole.Broadcaster || !_context.AutoDetectChannel;

            var result = await _oauthService.StartOAuthFlowToDraftAsync(
                _role,
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
            StatusMessage = "Авторизация отменена";
            StatusMessageSeverity = StatusSeverity.Warning;
            ResultText = string.Empty;
        }
        catch (Exception exception) when (ReferenceEquals(_authCts, cts))
        {
            hadError = true;
            ResultText = SafeAuthMessage(exception);
            ResultSeverity = StatusSeverity.Error;
            StatusMessage = AuthErrorDetails(exception);
            StatusMessageSeverity = StatusSeverity.Error;
            _logger.OAuthFlowFailed(exception, _role);
        }
        finally
        {
            UnsubscribeStatus();

            if (ReferenceEquals(_authCts, cts))
            {
                _authCts = null;
                _currentAuthUrl = null;
                IsAuthInProgress = false;

                if (!hadError)
                {
                    RefreshFromContext();
                }
            }

            cts.Dispose();
        }
    }

    private void ApplyAuthResult(OAuthFlowResult result)
    {
        if (_context is null)
        {
            return;
        }

        var account = GetAccount();
        result.ApplyTo(account, DateTimeOffset.UtcNow);

        if (_role != TwitchOAuthRole.Broadcaster
            || !_context.AutoDetectChannel
            || string.IsNullOrWhiteSpace(result.Login))
        {
            return;
        }

        _context.Settings.Twitch.Channel = result.Login;
        _settingsManager.UpdateCurrent(settings => settings.Twitch.Channel = result.Login);
    }

    private void RefreshFromContext()
    {
        var account = GetAccount();
        HasToken = !string.IsNullOrWhiteSpace(account.AccessToken);

        if (HasToken)
        {
            var login = string.IsNullOrWhiteSpace(account.Login) ? "–" : account.Login;
            ResultText = $"Авторизован: @{login}";
            ResultSeverity = StatusSeverity.Success;

            if (_authCts is null)
            {
                StatusMessage = "Можно идти дальше или сменить аккаунт.";
                StatusMessageSeverity = StatusSeverity.None;
            }

            CanAdvance = true;
        }
        else
        {
            if (_authCts is null)
            {
                ResultText = "Не авторизован";
                ResultSeverity = StatusSeverity.Error;
                StatusMessage = "Откройте ссылку в браузере и подтвердите доступ.";
                StatusMessageSeverity = StatusSeverity.None;
            }

            CanAdvance = false;
        }
    }

    private void OnCredentialsChanged(object? sender, EventArgs e)
    {
        if (_context is null)
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
        account.StoredScopes = [];

        RefreshFromContext();
    }

    private void OnOAuthStatusChanged(TwitchOAuthRole role, string message)
    {
        if (role != _role)
        {
            return;
        }

        _uiDispatcher.Invoke(() => ApplyStatusMessage(message));
    }

    private void ApplyStatusMessage(string message)
    {
        StatusMessage = message;
        StatusMessageSeverity = StatusSeverity.Info;
    }

    private void SubscribeStatus()
    {
        if (_statusSubscribed)
        {
            return;
        }

        _oauthService.StatusChanged += OnOAuthStatusChanged;
        _statusSubscribed = true;
    }

    private void UnsubscribeStatus()
    {
        if (!_statusSubscribed)
        {
            return;
        }

        _oauthService.StatusChanged -= OnOAuthStatusChanged;
        _statusSubscribed = false;
    }

    private TwitchAccountSettings GetAccount()
    {
        if (_context is null)
        {
            throw new InvalidOperationException("OnEnter must be called before accessing context");
        }

        return _role == TwitchOAuthRole.Broadcaster
            ? _context.BroadcasterAccount
            : _context.BotAccount;
    }

    private string[] GetDefaultScopes()
    {
        return _role == TwitchOAuthRole.Broadcaster ? BroadcasterDefaultScopes : BotDefaultScopes;
    }

    private void OpenInBrowser(string authUrl)
    {
        if (_shellLauncher.Open(authUrl))
        {
            StatusMessage = "Браузер открыт. Подтвердите доступ.";
            StatusMessageSeverity = StatusSeverity.Info;
            return;
        }

        StatusMessage = "Не удалось открыть браузер. Скопируйте ссылку и откройте вручную.";
        StatusMessageSeverity = StatusSeverity.Error;
    }

    private void CopyToClipboard(string authUrl)
    {
        if (_clipboard.TrySetText(authUrl))
        {
            StatusMessage = "Ссылка скопирована. Откройте её в браузере и подтвердите доступ.";
            StatusMessageSeverity = StatusSeverity.Info;
            return;
        }

        StatusMessage = "Не удалось скопировать в буфер обмена.";
        StatusMessageSeverity = StatusSeverity.Error;
    }

    private static string SafeAuthMessage(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "Авторизация отменена",
            TimeoutException => "Время ожидания авторизации истекло",
            HttpRequestException => "Ошибка сети при запросе токена",
            ArgumentException => "Не заполнены Client ID или Client Secret",
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
}
