using CommunityToolkit.Mvvm.ComponentModel;
using KeepShell.ViewModels;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Onboarding;
using PoproshaykaBot.Wpf.Bootstrap;

namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public sealed partial class CredentialsPageViewModel : OnboardingPageViewModelBase, IDisposable
{
    private readonly IClientCredentialsValidator _clientCredentialsValidator;
    private readonly IOnboardingChannelValidator _channelValidator;
    private readonly ILogger<CredentialsPageViewModel> _logger;

    private readonly IUiTimer _clientDebounce;
    private readonly IUiTimer _channelDebounce;

    private CancellationTokenSource? _clientValidationCts;
    private CancellationTokenSource? _channelValidationCts;

    private bool _suspendValidation;
    private string _initialChannel = string.Empty;
    private string _initialClientId = string.Empty;
    private string _initialClientSecret = string.Empty;
    private string _initialRedirectUri = string.Empty;
    private string _lastValidatedClientId = string.Empty;
    private string _lastValidatedClientSecret = string.Empty;
    private string _lastValidatedChannel = string.Empty;
    private string _lastValidatedChannelClientId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChannelReadOnly))]
    private bool _autoDetectChannel = true;

    [ObservableProperty]
    private string _channel = string.Empty;

    [ObservableProperty]
    private string _clientId = string.Empty;

    [ObservableProperty]
    private string _clientSecret = string.Empty;

    [ObservableProperty]
    private string _redirectUri = string.Empty;

    [ObservableProperty]
    private string _channelStatusText = string.Empty;

    [ObservableProperty]
    private StatusSeverity _channelStatusSeverity = StatusSeverity.None;

    [ObservableProperty]
    private string _clientStatusText = string.Empty;

    [ObservableProperty]
    private StatusSeverity _clientStatusSeverity = StatusSeverity.None;

    [ObservableProperty]
    private string _portHintText = string.Empty;

    [ObservableProperty]
    private string _validationErrorText = string.Empty;

    private bool _disposed;

    public CredentialsPageViewModel(
        IClientCredentialsValidator clientCredentialsValidator,
        IOnboardingChannelValidator channelValidator,
        ILogger<CredentialsPageViewModel> logger,
        IUiDispatcher uiDispatcher)
    {
        _clientCredentialsValidator = clientCredentialsValidator;
        _channelValidator = channelValidator;
        _logger = logger;

        _clientDebounce = uiDispatcher.CreateTimer(TimeSpan.FromMilliseconds(800), OnClientDebounceTick);
        _channelDebounce = uiDispatcher.CreateTimer(TimeSpan.FromMilliseconds(800), OnChannelDebounceTick);
    }

    public override string PageTitle => "Учётные данные приложения";

    public bool IsChannelReadOnly => AutoDetectChannel;

    public override void OnEnter(OnboardingContext context)
    {
        _suspendValidation = true;
        try
        {
            AutoDetectChannel = context.AutoDetectChannel;

            var contextChannel = context.Settings.Twitch.Channel;
            var broadcasterLogin = context.BroadcasterAccount.Login;

            if (context.AutoDetectChannel && !string.IsNullOrWhiteSpace(broadcasterLogin))
            {
                Channel = broadcasterLogin;
            }
            else
            {
                Channel = contextChannel;
            }

            ApplyAutoDetectChannelStatus(context);

            ClientId = context.Settings.Twitch.ClientId;
            ClientSecret = context.Settings.Twitch.ClientSecret;
            RedirectUri = string.IsNullOrWhiteSpace(context.Settings.Twitch.RedirectUri)
                ? "http://localhost:3000"
                : context.Settings.Twitch.RedirectUri;
        }
        finally
        {
            _suspendValidation = false;
        }

        SnapshotInitialValues();
        _lastValidatedClientId = string.Empty;
        _lastValidatedClientSecret = string.Empty;
        _lastValidatedChannel = string.Empty;
        _lastValidatedChannelClientId = string.Empty;

        ValidateInputs();
        ScheduleClientCredentialsCheck();
        ScheduleChannelCheck();
    }

    public override Task<bool> OnLeavingAsync(OnboardingContext context)
    {
        WriteToContext(context);

        if (HasCredentialsChanged())
        {
            SnapshotInitialValues();
            context.RaiseCredentialsChanged();
        }

        return Task.FromResult(CanAdvance);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _clientDebounce.Stop();
        _channelDebounce.Stop();

        _clientValidationCts?.Cancel();
        _clientValidationCts?.Dispose();
        _clientValidationCts = null;

        _channelValidationCts?.Cancel();
        _channelValidationCts?.Dispose();
        _channelValidationCts = null;
    }

    partial void OnChannelChanged(string value)
    {
        if (_suspendValidation)
        {
            return;
        }

        ValidateInputs();
        ScheduleChannelCheck();
    }

    partial void OnClientIdChanged(string value)
    {
        if (_suspendValidation)
        {
            return;
        }

        ValidateInputs();
        ScheduleClientCredentialsCheck();
        ScheduleChannelCheck();
    }

    partial void OnClientSecretChanged(string value)
    {
        if (_suspendValidation)
        {
            return;
        }

        ValidateInputs();
        ScheduleClientCredentialsCheck();
        ScheduleChannelCheck();
    }

    partial void OnRedirectUriChanged(string value)
    {
        if (_suspendValidation)
        {
            return;
        }

        ValidateInputs();
    }

    partial void OnAutoDetectChannelChanged(bool value)
    {
        if (_suspendValidation)
        {
            return;
        }

        ValidateInputs();
        ScheduleChannelCheck();
    }

    private void ApplyAutoDetectChannelStatus(OnboardingContext context)
    {
        if (!AutoDetectChannel)
        {
            ChannelStatusText = string.Empty;
            ChannelStatusSeverity = StatusSeverity.None;
            return;
        }

        var login = context.BroadcasterAccount.Login;
        if (string.IsNullOrWhiteSpace(login))
        {
            ChannelStatusText = "Канал заполнится автоматически после авторизации стримера.";
            ChannelStatusSeverity = StatusSeverity.Info;
        }
        else
        {
            ChannelStatusText = $"Использован логин стримера: @{login}";
            ChannelStatusSeverity = StatusSeverity.Info;
        }
    }

    private void ScheduleClientCredentialsCheck()
    {
        _clientValidationCts?.Cancel();
        _clientDebounce.Stop();

        var clientId = ClientId.Trim();
        var clientSecret = ClientSecret.Trim();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            ClientStatusText = string.Empty;
            ClientStatusSeverity = StatusSeverity.None;
            return;
        }

        ClientStatusText = "Ожидание ввода...";
        ClientStatusSeverity = StatusSeverity.None;
        _clientDebounce.Start();
    }

    private void ScheduleChannelCheck()
    {
        _channelValidationCts?.Cancel();
        _channelDebounce.Stop();

        if (AutoDetectChannel)
        {
            return;
        }

        var channel = Channel.Trim();
        var clientId = ClientId.Trim();
        var clientSecret = ClientSecret.Trim();

        if (string.IsNullOrWhiteSpace(channel)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret))
        {
            return;
        }

        _channelDebounce.Start();
    }

    private async void OnClientDebounceTick()
    {
        _clientDebounce.Stop();
        await RunCredentialsCheckAsync();
    }

    private async void OnChannelDebounceTick()
    {
        _channelDebounce.Stop();
        await RunChannelCheckAsync();
    }

    private async Task RunCredentialsCheckAsync()
    {
        var clientId = ClientId.Trim();
        var clientSecret = ClientSecret.Trim();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            ClientStatusText = string.Empty;
            ClientStatusSeverity = StatusSeverity.None;
            return;
        }

        if (string.Equals(clientId, _lastValidatedClientId, StringComparison.Ordinal)
            && string.Equals(clientSecret, _lastValidatedClientSecret, StringComparison.Ordinal))
        {
            return;
        }

        var previousCts = _clientValidationCts;
        _clientValidationCts = new CancellationTokenSource();
        var token = _clientValidationCts.Token;

        if (previousCts is not null)
        {
            await previousCts.CancelAsync();
            previousCts.Dispose();
        }

        ClientStatusText = "Проверка Client ID и Secret...";
        ClientStatusSeverity = StatusSeverity.Info;

        ClientCredentialsValidationResult result;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(6));
            result = await _clientCredentialsValidator.ValidateAsync(clientId, clientSecret, timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.OnboardingCredentialsCheckFailed(exception);
            ClientStatusText = "Ошибка проверки";
            ClientStatusSeverity = StatusSeverity.Warning;
            return;
        }

        if (token.IsCancellationRequested || _disposed)
        {
            return;
        }

        if (!string.Equals(clientId, ClientId.Trim(), StringComparison.Ordinal)
            || !string.Equals(clientSecret, ClientSecret.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        _lastValidatedClientId = clientId;
        _lastValidatedClientSecret = clientSecret;

        switch (result)
        {
            case ClientCredentialsValidationResult.Valid:
                ClientStatusText = "✓ Client ID и Secret валидны";
                ClientStatusSeverity = StatusSeverity.Success;
                break;

            case ClientCredentialsValidationResult.Invalid:
                ClientStatusText = "✗ Client ID или Secret недействительны";
                ClientStatusSeverity = StatusSeverity.Error;
                break;

            case ClientCredentialsValidationResult.NetworkError:
                ClientStatusText = "Не удалось проверить (сеть)";
                ClientStatusSeverity = StatusSeverity.Warning;
                break;

            default:
                ClientStatusText = string.Empty;
                ClientStatusSeverity = StatusSeverity.None;
                break;
        }
    }

    private async Task RunChannelCheckAsync()
    {
        var channel = Channel.Trim();
        var clientId = ClientId.Trim();
        var clientSecret = ClientSecret.Trim();

        if (string.IsNullOrWhiteSpace(channel)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || AutoDetectChannel)
        {
            return;
        }

        if (string.Equals(channel, _lastValidatedChannel, StringComparison.OrdinalIgnoreCase)
            && string.Equals(clientId, _lastValidatedChannelClientId, StringComparison.Ordinal))
        {
            return;
        }

        var previousCts = _channelValidationCts;
        _channelValidationCts = new CancellationTokenSource();
        var token = _channelValidationCts.Token;

        if (previousCts is not null)
        {
            await previousCts.CancelAsync();
            previousCts.Dispose();
        }

        ChannelStatusText = "Проверка канала...";
        ChannelStatusSeverity = StatusSeverity.Info;

        ChannelValidationResult result;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));
            result = await _channelValidator.ValidateWithAppTokenAsync(channel, clientId, clientSecret, timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.OnboardingChannelCredentialsCheckFailed(exception, channel);
            ChannelStatusText = "Не удалось проверить канал";
            ChannelStatusSeverity = StatusSeverity.Warning;
            return;
        }

        if (token.IsCancellationRequested || _disposed)
        {
            return;
        }

        if (!string.Equals(channel, Channel.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(clientId, ClientId.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        _lastValidatedChannel = channel;
        _lastValidatedChannelClientId = clientId;

        switch (result)
        {
            case ChannelValidationResult.Found:
                ChannelStatusText = $"✓ Канал @{channel} найден на Twitch";
                ChannelStatusSeverity = StatusSeverity.Success;
                break;

            case ChannelValidationResult.NotFound:
                ChannelStatusText = $"✗ Канал @{channel} не найден на Twitch";
                ChannelStatusSeverity = StatusSeverity.Error;
                break;

            default:
                ChannelStatusText = "Не удалось проверить канал";
                ChannelStatusSeverity = StatusSeverity.Warning;
                break;
        }
    }

    private void ValidateInputs()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Channel) && !AutoDetectChannel)
        {
            errors.Add("канал");
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            errors.Add("Client ID");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            errors.Add("Client Secret");
        }

        var redirectValid = RedirectUriPortResolver.TryResolve(RedirectUri, out var port);
        if (!redirectValid)
        {
            errors.Add("Redirect URI (некорректный формат)");
            PortHintText = string.Empty;
        }
        else
        {
            PortHintText = $"HTTP сервер будет слушать порт {port}";
        }

        ValidationErrorText = errors.Count > 0
            ? $"Заполните: {string.Join(", ", errors)}"
            : string.Empty;

        CanAdvance = errors.Count == 0;
    }

    private void SnapshotInitialValues()
    {
        _initialChannel = Channel.Trim();
        _initialClientId = ClientId.Trim();
        _initialClientSecret = ClientSecret.Trim();
        _initialRedirectUri = RedirectUri.Trim();
    }

    private bool HasCredentialsChanged()
    {
        return !string.Equals(_initialChannel, Channel.Trim(), StringComparison.OrdinalIgnoreCase)
               || !string.Equals(_initialClientId, ClientId.Trim(), StringComparison.Ordinal)
               || !string.Equals(_initialClientSecret, ClientSecret.Trim(), StringComparison.Ordinal)
               || !string.Equals(_initialRedirectUri, RedirectUri.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void WriteToContext(OnboardingContext context)
    {
        context.AutoDetectChannel = AutoDetectChannel;
        context.Settings.Twitch.Channel = Channel.Trim();
        context.Settings.Twitch.ClientId = ClientId.Trim();
        context.Settings.Twitch.ClientSecret = ClientSecret.Trim();
        context.Settings.Twitch.RedirectUri = RedirectUri.Trim();
    }
}
