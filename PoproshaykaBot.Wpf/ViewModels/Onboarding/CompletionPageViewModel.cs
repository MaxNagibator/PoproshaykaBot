using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Core.Twitch.Onboarding;
using PoproshaykaBot.Wpf.Bootstrap;

namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public sealed partial class CompletionPageViewModel : OnboardingPageViewModelBase, IDisposable
{
    private readonly SettingsManager _settingsManager;
    private readonly AccountsStore _accountsStore;
    private readonly IEventBus _eventBus;
    private readonly KestrelHttpServer _kestrelHttpServer;
    private readonly IOnboardingChannelValidator _channelValidator;
    private readonly BotConnectionManager _botConnectionManager;
    private readonly ILogger<CompletionPageViewModel> _logger;
    private readonly IDialogService _dialogService;

    private OnboardingContext? _context;
    private CancellationTokenSource? _validationCts;
    private bool _hasCriticalIssue;
    private ChannelValidationResult _lastChannelCheckResult = ChannelValidationResult.Skipped;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _validationText = string.Empty;

    [ObservableProperty]
    private bool _isValidationPending;

    [ObservableProperty]
    private bool _isValidationWarning;

    [ObservableProperty]
    private bool _isValidationFailure;

    [ObservableProperty]
    private bool _autoConnect = true;

    [ObservableProperty]
    private TwitchOAuthRole _chatDisplayAccount = TwitchOAuthRole.Bot;

    public CompletionPageViewModel(
        SettingsManager settingsManager,
        AccountsStore accountsStore,
        IEventBus eventBus,
        KestrelHttpServer kestrelHttpServer,
        IOnboardingChannelValidator channelValidator,
        BotConnectionManager botConnectionManager,
        ILogger<CompletionPageViewModel> logger,
        IDialogService dialogService)
    {
        _settingsManager = settingsManager;
        _accountsStore = accountsStore;
        _eventBus = eventBus;
        _kestrelHttpServer = kestrelHttpServer;
        _channelValidator = channelValidator;
        _botConnectionManager = botConnectionManager;
        _logger = logger;
        _dialogService = dialogService;
    }

    public override string PageTitle => "Готово";

    public override void OnEnter(OnboardingContext context)
    {
        _context = context;

        var botLogin = string.IsNullOrWhiteSpace(context.BotAccount.Login) ? "–" : context.BotAccount.Login;
        var broadcasterLogin = string.IsNullOrWhiteSpace(context.BroadcasterAccount.Login)
            ? "–"
            : context.BroadcasterAccount.Login;

        SummaryText =
            $"Канал: {context.Settings.Twitch.Channel}"
            + Environment.NewLine
            + $"Аккаунт бота: @{botLogin}"
            + Environment.NewLine
            + $"Аккаунт стримера: @{broadcasterLogin}";

        ChatDisplayAccount = context.Settings.Twitch.ChatDisplayAccount;

        CanAdvance = false;
        _hasCriticalIssue = false;
        _lastChannelCheckResult = ChannelValidationResult.Skipped;

        ValidationText = "Выполняется проверка...";
        IsValidationPending = true;
        IsValidationWarning = false;
        IsValidationFailure = false;

        _validationCts?.Cancel();
        _validationCts?.Dispose();
        _validationCts = new CancellationTokenSource();

        _ = RunValidationsAsync(_validationCts.Token);
    }

    public override async Task<bool> OnLeavingAsync(OnboardingContext context)
    {
        if (_hasCriticalIssue)
        {
            return false;
        }

        if (_lastChannelCheckResult == ChannelValidationResult.NotFound
            && !ConfirmIgnoreChannelNotFound(context))
        {
            return false;
        }

        var oldPort = _settingsManager.Current.Twitch.HttpServerPort;
        var newPort = context.Settings.Twitch.HttpServerPort;
        var portChanged = oldPort != newPort;

        bool settingsWritten;
        bool accountsWritten;

        try
        {
            settingsWritten = _settingsManager.SaveSettings(context.Settings);
            accountsWritten = _accountsStore.SaveAll(context.BotAccount, context.BroadcasterAccount);
        }
        catch (Exception exception)
        {
            _logger.OnboardingSettingsSaveFailed(exception);
            _dialogService.Error("Ошибка", $"Не удалось сохранить настройки: {exception.Message}");
            return false;
        }

        await _eventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Bot), CancellationToken.None);
        await _eventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Broadcaster), CancellationToken.None);

        if (!settingsWritten || !accountsWritten)
        {
            _logger.OnboardingCompletionNotWritten(settingsWritten, accountsWritten);
            _dialogService.Warning("Настройки не записаны", DescribeNotWritten(settingsWritten, accountsWritten));
        }

        if (portChanged)
        {
            await TryRestartHttpServerAsync(newPort, settingsWritten);
        }

        if (AutoConnect && _botConnectionManager.CurrentPhase != BotLifecyclePhase.Connected)
        {
            try
            {
                _botConnectionManager.StartConnection();
            }
            catch (InvalidOperationException exception)
            {
                _logger.OnboardingAutoConnectRejected(exception);
            }
        }

        return true;
    }

    public void Dispose()
    {
        _validationCts?.Cancel();
        _validationCts?.Dispose();
        _validationCts = null;
    }

    partial void OnChatDisplayAccountChanged(TwitchOAuthRole value)
    {
        if (_context is not null)
        {
            _context.Settings.Twitch.ChatDisplayAccount = value;
        }
    }

    private async Task RunValidationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunValidationsCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.OnboardingCompletionCheckFailed(exception);
            _hasCriticalIssue = false;
            CanAdvance = true;
        }
    }

    private async Task RunValidationsCoreAsync(CancellationToken cancellationToken)
    {
        if (_context is null)
        {
            return;
        }

        var lines = new List<ValidationLine>();
        var hasCritical = false;

        var botScopes = ValidateScopes("Доступы аккаунта бота",
            _context.BotAccount.Scopes,
            TwitchScopes.BotRequired);
        lines.Add(botScopes);
        hasCritical |= botScopes.Status == ValidationStatus.Failure;

        var broadcasterScopes = ValidateScopes("Доступы аккаунта стримера",
            _context.BroadcasterAccount.Scopes,
            TwitchScopes.BroadcasterRequired);
        lines.Add(broadcasterScopes);
        hasCritical |= broadcasterScopes.Status == ValidationStatus.Failure;

        var loginConsistency = ValidateBroadcasterLogin(_context);
        lines.Add(loginConsistency);

        var channelLine = new ValidationLine("Канал на Twitch", ValidationStatus.Pending, "проверка...");
        lines.Add(channelLine);

        UpdateValidation(lines);

        var (channelStatus, channelDetail, channelResult) =
            await CheckChannelExistsAsync(_context, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _lastChannelCheckResult = channelResult;
        lines[^1] = channelLine with { Status = channelStatus, Detail = channelDetail };
        hasCritical |= channelStatus == ValidationStatus.Failure;

        UpdateValidation(lines);

        _hasCriticalIssue = hasCritical;
        CanAdvance = !hasCritical;
    }

    private async Task<(ValidationStatus Status, string Detail, ChannelValidationResult Result)> CheckChannelExistsAsync(
        OnboardingContext context,
        CancellationToken cancellationToken)
    {
        var channel = context.Settings.Twitch.Channel?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(channel))
        {
            return (ValidationStatus.Skipped, "пропущено: канал не задан", ChannelValidationResult.Skipped);
        }

        var clientId = context.Settings.Twitch.ClientId;
        var accessToken = context.BotAccount.AccessToken;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accessToken))
        {
            return (ValidationStatus.Skipped, "пропущено: нет client id или токена бота", ChannelValidationResult.Skipped);
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

            var result = await _channelValidator.ValidateAsync(channel, clientId, accessToken, timeoutCts.Token);

            return result switch
            {
                ChannelValidationResult.Found =>
                    (ValidationStatus.Success, $"найден @{channel}", result),
                ChannelValidationResult.NotFound =>
                    (ValidationStatus.Failure,
                        $"канал «{channel}» не найден. Возможно, имя написано с ошибкой – вернитесь на шаг учётных данных.",
                        result),
                _ =>
                    (ValidationStatus.Skipped, "не удалось проверить (сеть)", result),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (ValidationStatus.Skipped, "превышено время ожидания", ChannelValidationResult.Skipped);
        }
        catch (Exception exception)
        {
            _logger.OnboardingChannelCheckFailed(exception, channel);
            return (ValidationStatus.Skipped, "ошибка при проверке", ChannelValidationResult.Skipped);
        }
    }

    private void UpdateValidation(List<ValidationLine> lines)
    {
        ValidationText = string.Join(Environment.NewLine, lines.Select(FormatLine));

        var anyFailure = lines.Any(l => l.Status == ValidationStatus.Failure);
        var anyWarning = lines.Any(l => l.Status == ValidationStatus.Warning);
        var anyPending = lines.Any(l => l.Status == ValidationStatus.Pending);

        IsValidationFailure = anyFailure;
        IsValidationWarning = !anyFailure && anyWarning;
        IsValidationPending = !anyFailure && !anyWarning && anyPending;
    }

    private bool ConfirmIgnoreChannelNotFound(OnboardingContext context)
    {
        return _dialogService.ConfirmWarning(
            "Канал не найден",
            $"Канал «{context.Settings.Twitch.Channel}» не найден на Twitch. Возможно, имя написано с ошибкой.\n\nВсё равно сохранить настройки?");
    }

    private static string DescribeNotWritten(bool settingsWritten, bool accountsWritten)
    {
        var what = (settingsWritten, accountsWritten) switch
        {
            (false, false) => "Настройки и вход в Twitch применены и работают до перезапуска. В файл они не записаны",
            (false, true) => "Настройки применены и работают до перезапуска. В файл они не записаны",
            _ => "Вход в Twitch применён и работает до перезапуска. В файл он не записан",
        };

        return $"{what}: туда только что перенесены данные предыдущей версии. Перезапустите приложение и пройдите настройку ещё раз.";
    }

    private async Task TryRestartHttpServerAsync(int newPort, bool settingsWritten)
    {
        try
        {
            if (_kestrelHttpServer.IsRunning)
            {
                await _kestrelHttpServer.StopAsync();
            }

            await _kestrelHttpServer.StartAsync();
        }
        catch (Exception exception)
        {
            _logger.OnboardingHttpServerRestartFailed(exception, newPort);
            _dialogService.Warning(
                "Внимание",
                settingsWritten
                    ? $"HTTP сервер не удалось перезапустить на порту {newPort}.\nНастройки сохранены – потребуется перезапуск приложения."
                    : $"HTTP сервер не удалось перезапустить на порту {newPort}.");
        }
    }

    private static ValidationLine ValidateScopes(string title, IReadOnlyCollection<string> actual, IReadOnlyList<string> required)
    {
        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);
        var missing = required.Where(scope => !actualSet.Contains(scope)).ToArray();

        if (missing.Length == 0)
        {
            return new(title, ValidationStatus.Success, "все необходимые scope получены");
        }

        return new(title,
            ValidationStatus.Failure,
            $"отсутствуют scope: {string.Join(", ", missing)}. Вернитесь на шаг авторизации и переавторизуйтесь.");
    }

    private static ValidationLine ValidateBroadcasterLogin(OnboardingContext context)
    {
        var channel = context.Settings.Twitch.Channel?.Trim() ?? string.Empty;
        var broadcasterLogin = context.BroadcasterAccount.Login?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(broadcasterLogin))
        {
            return new("Канал и стример", ValidationStatus.Skipped, "пропущено: не заданы поля");
        }

        if (string.Equals(channel, broadcasterLogin, StringComparison.OrdinalIgnoreCase))
        {
            return new("Канал и стример", ValidationStatus.Success, $"@{broadcasterLogin}");
        }

        return new("Канал и стример",
            ValidationStatus.Warning,
            $"канал «{channel}» и логин стримера «@{broadcasterLogin}» различаются – проверьте, что это намеренно.");
    }

    private static string FormatLine(ValidationLine line)
    {
        var prefix = line.Status switch
        {
            ValidationStatus.Success => "✓",
            ValidationStatus.Warning => "⚠",
            ValidationStatus.Failure => "✗",
            ValidationStatus.Pending => "⏳",
            _ => "–",
        };

        return $"{prefix} {line.Title}: {line.Detail}";
    }

    private enum ValidationStatus
    {
        None = 0,
        Pending = 1,
        Success = 2,
        Warning = 3,
        Failure = 4,
        Skipped = 5,
    }

    private readonly record struct ValidationLine(string Title, ValidationStatus Status, string Detail);
}
