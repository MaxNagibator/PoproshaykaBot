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
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.WinForms.Forms.Onboarding.Pages;

public sealed partial class CompletionPage : OnboardingPageBase
{
    private OnboardingContext? _context;
    private CancellationTokenSource? _validationCts;
    private bool _hasCriticalIssue;
    private ChannelValidationResult _lastChannelCheckResult = ChannelValidationResult.Skipped;

    public CompletionPage()
    {
        InitializeComponent();
        Disposed += OnPageDisposed;
    }

    private enum ValidationStatus
    {
        Pending = 0,
        Success = 1,
        Warning = 2,
        Failure = 3,
        Skipped = 4,
    }

    [Inject]
    public SettingsManager SettingsManager { get; internal init; } = null!;

    [Inject]
    public AccountsStore AccountsStore { get; internal init; } = null!;

    [Inject]
    public IEventBus EventBus { get; internal init; } = null!;

    [Inject]
    public KestrelHttpServer KestrelHttpServer { get; internal init; } = null!;

    [Inject]
    public IOnboardingChannelValidator ChannelValidator { get; internal init; } = null!;

    [Inject]
    public BotConnectionManager BotConnectionManager { get; internal init; } = null!;

    [Inject]
    public ILogger<CompletionPage> Logger { get; internal init; } = null!;

    public override string PageTitle => "Готово";

    public override void OnEnter(OnboardingContext context)
    {
        _context = context;

        var botLogin = string.IsNullOrWhiteSpace(context.BotAccount.Login) ? "–" : context.BotAccount.Login;
        var broadcasterLogin = string.IsNullOrWhiteSpace(context.BroadcasterAccount.Login)
            ? "–"
            : context.BroadcasterAccount.Login;

        _summaryLabel.Text =
            $"Канал: {context.Settings.Twitch.Channel}"
            + Environment.NewLine
            + $"Аккаунт бота: @{botLogin}"
            + Environment.NewLine
            + $"Аккаунт стримера: @{broadcasterLogin}";

        if (context.Settings.Twitch.ChatDisplayAccount == TwitchOAuthRole.Broadcaster)
        {
            _chatAccountBroadcasterRadio.Checked = true;
        }
        else
        {
            _chatAccountBotRadio.Checked = true;
        }

        SetCanAdvance(false);
        _hasCriticalIssue = false;
        _lastChannelCheckResult = ChannelValidationResult.Skipped;

        _validationCts?.Cancel();
        _validationCts?.Dispose();
        _validationCts = new();

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

        var oldPort = SettingsManager.Current.Twitch.HttpServerPort;
        var newPort = context.Settings.Twitch.HttpServerPort;
        var portChanged = oldPort != newPort;

        try
        {
            SettingsManager.SaveSettings(context.Settings);
            AccountsStore.SaveAll(context.BotAccount, context.BroadcasterAccount);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Не удалось сохранить настройки в onboarding-мастере");
            MessageBox.Show(this,
                $"Не удалось сохранить настройки: {exception.Message}",
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return false;
        }

        await EventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Bot));
        await EventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Broadcaster));

        if (portChanged)
        {
            await TryRestartHttpServerAsync(newPort);
        }

        if (_autoConnectCheckBox.Checked && BotConnectionManager.CurrentPhase != BotLifecyclePhase.Connected)
        {
            try
            {
                BotConnectionManager.StartConnection();
            }
            catch (InvalidOperationException exception)
            {
                Logger.LogWarning(exception, "Авто-подключение бота отклонено");
            }
        }

        return true;
    }

    private void OnPageDisposed(object? sender, EventArgs e)
    {
        _validationCts?.Cancel();
        _validationCts?.Dispose();
        _validationCts = null;
    }

    private void OnChatAccountRadioChanged(object? sender, EventArgs e)
    {
        if (_context == null || sender is not RadioButton { Checked: true } radio)
        {
            return;
        }

        _context.Settings.Twitch.ChatDisplayAccount = radio == _chatAccountBroadcasterRadio
            ? TwitchOAuthRole.Broadcaster
            : TwitchOAuthRole.Bot;
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
            _ => "−",
        };

        return $"{prefix} {line.Title}: {line.Detail}";
    }

    private static Color SelectValidationColor(bool anyFailure, bool anyWarning, bool anyPending)
    {
        if (anyFailure)
        {
            return Color.DarkRed;
        }

        if (anyWarning)
        {
            return Color.DarkOrange;
        }

        if (anyPending)
        {
            return Color.DarkGray;
        }

        return Color.DarkGreen;
    }

    private async Task RunValidationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunValidationsCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // page navigated away mid-validation
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Сбой проверок на CompletionPage");
            if (!IsDisposed)
            {
                _hasCriticalIssue = false;
                SetCanAdvance(true);
            }
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

        UpdateValidationLabel(lines);

        var (channelStatus, channelDetail, channelResult) = await CheckChannelExistsAsync(_context, cancellationToken);
        if (cancellationToken.IsCancellationRequested || IsDisposed)
        {
            return;
        }

        _lastChannelCheckResult = channelResult;
        lines[^1] = channelLine with { Status = channelStatus, Detail = channelDetail };
        hasCritical |= channelStatus == ValidationStatus.Failure;

        UpdateValidationLabel(lines);

        _hasCriticalIssue = hasCritical;
        SetCanAdvance(!hasCritical);
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

            var result = await ChannelValidator.ValidateAsync(channel, clientId, accessToken, timeoutCts.Token);

            return result switch
            {
                ChannelValidationResult.Found => (ValidationStatus.Success, $"найден @{channel}", result),
                ChannelValidationResult.NotFound => (ValidationStatus.Failure,
                    $"канал «{channel}» не найден. Возможно, имя написано с ошибкой – вернитесь на шаг учётных данных.",
                    result),
                _ => (ValidationStatus.Skipped, "не удалось проверить (сеть)", result),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (ValidationStatus.Skipped, "превышено время ожидания", ChannelValidationResult.Skipped);
        }
        catch (Exception exception)
        {
            Logger.LogDebug(exception, "Сбой проверки канала {Channel}", channel);
            return (ValidationStatus.Skipped, "ошибка при проверке", ChannelValidationResult.Skipped);
        }
    }

    private void UpdateValidationLabel(List<ValidationLine> lines)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        var anyFailure = lines.Any(l => l.Status == ValidationStatus.Failure);
        var anyWarning = lines.Any(l => l.Status == ValidationStatus.Warning);
        var anyPending = lines.Any(l => l.Status == ValidationStatus.Pending);

        _validationsListLabel.Text = string.Join(Environment.NewLine, lines.Select(FormatLine));
        _validationsListLabel.ForeColor = SelectValidationColor(anyFailure, anyWarning, anyPending);
    }

    private bool ConfirmIgnoreChannelNotFound(OnboardingContext context)
    {
        var answer = MessageBox.Show(this,
            $"""
             Канал «{context.Settings.Twitch.Channel}» не найден на Twitch. Возможно, имя написано с ошибкой.

             Всё равно сохранить настройки?
             """,
            "Канал не найден",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        return answer == DialogResult.Yes;
    }

    private async Task TryRestartHttpServerAsync(int newPort)
    {
        try
        {
            if (KestrelHttpServer.IsRunning)
            {
                await KestrelHttpServer.StopAsync();
            }

            await KestrelHttpServer.StartAsync();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Не удалось перезапустить HTTP сервер на порту {Port}", newPort);
            MessageBox.Show(this,
                $"""
                 HTTP сервер не удалось перезапустить на порту {newPort}.
                 Настройки сохранены – потребуется перезапуск приложения.
                 """,
                "Внимание",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private readonly record struct ValidationLine(string Title, ValidationStatus Status, string Detail);
}
