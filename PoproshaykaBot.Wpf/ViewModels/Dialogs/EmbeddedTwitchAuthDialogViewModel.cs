using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Net.Http;

namespace PoproshaykaBot.Wpf.ViewModels.Dialogs;

public sealed partial class EmbeddedTwitchAuthDialogViewModel : ObservableObject, IDisposable
{
    private const string WebView2RuntimeDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    private readonly ITwitchOAuthService _oauthService;
    private readonly IShellLauncher _shellLauncher;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ILogger<EmbeddedTwitchAuthDialogViewModel> _logger;

    private EmbeddedTwitchAuthRequest? _request;
    private CancellationTokenSource? _flowCts;
    private bool _statusSubscribed;
    private bool _finished;
    private bool _disposed;

    [ObservableProperty]
    private string _title = "Авторизация в Twitch";

    [ObservableProperty]
    private string _statusMessage = "Готовим встроенный браузер...";

    [ObservableProperty]
    private StatusSeverity _statusSeverity = StatusSeverity.Info;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorHeading = string.Empty;

    [ObservableProperty]
    private string _errorDescription = string.Empty;

    [ObservableProperty]
    private bool _canDownloadRuntime;

    [ObservableProperty]
    private string _closeButtonText = "Отмена";

    public EmbeddedTwitchAuthDialogViewModel(
        ITwitchOAuthService oauthService,
        IShellLauncher shellLauncher,
        IUiDispatcher uiDispatcher,
        ILogger<EmbeddedTwitchAuthDialogViewModel> logger)
    {
        _oauthService = oauthService;
        _shellLauncher = shellLauncher;
        _uiDispatcher = uiDispatcher;
        _logger = logger;
    }

    public event Action<string>? NavigateRequested;

    public event EventHandler? CloseRequested;

    public EmbeddedTwitchAuthResult Result { get; private set; } =
        EmbeddedTwitchAuthResult.Canceled("Авторизация отменена");

    public ILogger Logger => _logger;

    public string UserDataFolder => WebView2UserDataFolders.Resolve(_request?.Role ?? TwitchOAuthRole.Bot);

    public void Configure(EmbeddedTwitchAuthRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _request = request;

        Title = request.Role == TwitchOAuthRole.Broadcaster
            ? "Авторизация стримера в Twitch"
            : "Авторизация бота в Twitch";
    }

    public async Task RunFlowAsync()
    {
        if (_request is not { } request || _finished || _disposed)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _flowCts = cts;

        SubscribeStatus();
        SetStatus("Открываем страницу авторизации Twitch...", StatusSeverity.Info);

        try
        {
            var flow = await _oauthService.StartOAuthFlowToDraftAsync(
                request.Role,
                request.ClientId,
                request.ClientSecret,
                request.Scopes,
                request.RedirectUri,
                RaiseNavigate,
                request.CheckBroadcasterChannel,
                cts.Token);

            Finish(EmbeddedTwitchAuthResult.Completed(flow));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Finish(EmbeddedTwitchAuthResult.Canceled("Авторизация отменена"));
        }
        catch (Exception exception)
        {
            _logger.EmbeddedAuthFlowFailed(exception, request.Role);

            var message = SafeMessage(exception);
            Result = EmbeddedTwitchAuthResult.Failed(message);
            ShowError("Не удалось авторизоваться", message, false);
        }
        finally
        {
            UnsubscribeStatus();

            if (ReferenceEquals(_flowCts, cts))
            {
                _flowCts = null;
            }

            cts.Dispose();
        }
    }

    public void ShowRuntimeMissing()
    {
        Result = EmbeddedTwitchAuthResult.Unavailable(
            "Требуется Microsoft Edge WebView2 Runtime – используйте «Открыть в браузере».");

        ShowError("Требуется Microsoft Edge WebView2 Runtime",
            "Без него встроенный вход недоступен. Установите его или авторизуйтесь через системный браузер.",
            true);
    }

    public void ShowInitializationFailure()
    {
        Result = EmbeddedTwitchAuthResult.Unavailable(
            "Встроенный браузер не запустился – используйте «Открыть в браузере».");

        ShowError("Не удалось открыть встроенный браузер",
            "Авторизуйтесь через системный браузер. Подробности – в логах.",
            false);
    }

    public void Cancel()
    {
        if (_flowCts is not { IsCancellationRequested: false } cts)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        UnsubscribeStatus();

        if (_flowCts is { } cts)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    [RelayCommand]
    private void CloseDialog()
    {
        Cancel();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void DownloadRuntime()
    {
        if (_shellLauncher.Open(WebView2RuntimeDownloadUrl))
        {
            return;
        }

        SetStatus("Не удалось открыть ссылку на WebView2 Runtime.", StatusSeverity.Error);
    }

    private void RaiseNavigate(string authUrl)
    {
        _uiDispatcher.Invoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            NavigateRequested?.Invoke(authUrl);
        });
    }

    private void Finish(EmbeddedTwitchAuthResult result)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Result = result;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowError(string heading, string description, bool offerRuntimeDownload)
    {
        HasError = true;
        ErrorHeading = heading;
        ErrorDescription = description;
        CanDownloadRuntime = offerRuntimeDownload;
        CloseButtonText = "Закрыть";

        SetStatus(description, StatusSeverity.Error);
    }

    private void SetStatus(string message, StatusSeverity severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
    }

    private void OnOAuthStatusChanged(TwitchOAuthRole role, string message)
    {
        if (_request is null || role != _request.Role)
        {
            return;
        }

        _uiDispatcher.Invoke(() =>
        {
            if (_disposed || HasError)
            {
                return;
            }

            SetStatus(message, StatusSeverity.Info);
        });
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

    private static string SafeMessage(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "Авторизация отменена",
            TimeoutException => "Время ожидания авторизации истекло",
            HttpRequestException => "Ошибка сети при запросе токена",
            ArgumentException => "Не заполнены Client ID или Client Secret",
            _ => "Проверьте Client ID, Client Secret и Redirect URI.",
        };
    }
}
