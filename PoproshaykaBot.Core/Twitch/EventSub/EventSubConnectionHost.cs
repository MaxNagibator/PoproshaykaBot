using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Infrastructure.Reconnection;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Core.Twitch.EventSub;

public sealed class EventSubConnectionHost :
    IStreamHostedComponent,
    IEventHandler<TwitchAuthorizationRefreshed>,
    IAsyncDisposable
{
    private const int MaxReconnectAttempts = 5;

    private static readonly IProgress<string> NullProgress = new Progress<string>(_ => { });

    private readonly TwitchOAuthRole _role;
    private readonly ITwitchEventSubClient _eventSubClient;
    private readonly AccountsStore _accountsStore;
    private readonly IEventBus _eventBus;
    private readonly ILogger<EventSubConnectionHost> _logger;
    private readonly ExponentialBackoffPolicy _reconnectionPolicy = new(MaxReconnectAttempts);
    private readonly IDisposable _authSubscription;

    private readonly object _lockObj = new();
    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _reconnectCts;
    private bool _stopRequested;
    private bool _disposed;
    private StreamMonitoringStatus? _status;
    private long _lastMessageAtUtcTicks;

    public EventSubConnectionHost(
        TwitchOAuthRole role,
        ITwitchEventSubClient eventSubClient,
        AccountsStore accountsStore,
        IEventBus eventBus,
        ILogger<EventSubConnectionHost> logger)
    {
        _role = role;
        _eventSubClient = eventSubClient;
        _accountsStore = accountsStore;
        _eventBus = eventBus;
        _logger = logger;

        _eventSubClient.OnSessionWelcome += OnSessionWelcomeAsync;
        _eventSubClient.OnNotification += OnNotificationAsync;
        _eventSubClient.OnDisconnected += OnDisconnectedAsync;
        _authSubscription = _eventBus.Subscribe(this);
    }

    public TwitchOAuthRole Role => _role;

    public StreamMonitoringStatus? CurrentStatus
    {
        get
        {
            lock (_lockObj)
            {
                return _status;
            }
        }
    }

    public DateTimeOffset? LastMessageAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastMessageAtUtcTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public int ReconnectAttempt => _reconnectionPolicy.CurrentAttempt;

    public int ReconnectLimit => _reconnectionPolicy.MaxAttempts;

    public string Name => _role == TwitchOAuthRole.Bot
        ? "EventSub WebSocket (бот)"
        : "EventSub WebSocket (broadcaster)";

    public int StartOrder => _role == TwitchOAuthRole.Bot ? 275 : 276;

    public async Task StartAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (!HasAccessToken())
        {
            _logger.LogInformation("EventSub WebSocket ({Role}): запуск отложен – нет access-токена для роли. Ждём TwitchAuthorizationRefreshed.", _role);
            return;
        }

        await StartInternalAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        CancellationTokenSource? cts;

        lock (_lockObj)
        {
            _stopRequested = true;
            CancelAndResetReconnectToken();
            cts = _runCts;
            _runCts = null;
        }

        if (cts != null)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }

        _logger.LogInformation("Отключение от EventSub WebSocket ({Role})...", _role);
        await PublishStatusAsync(StreamMonitoringStatus.Disconnected).ConfigureAwait(false);

        try
        {
            await _eventSubClient.StopAsync(cancellationToken);
            _logger.LogInformation("EventSub WebSocket ({Role}) остановлен", _role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка остановки EventSub WebSocket ({Role})", _role);
        }
    }

    public async Task HandleAsync(TwitchAuthorizationRefreshed @event, CancellationToken cancellationToken)
    {
        if (@event.Role != _role)
        {
            _logger.LogDebug("TwitchAuthorizationRefreshed для роли {Role} игнорируется EventSubConnectionHost ({HostRole})", @event.Role, _role);
            return;
        }

        if (_disposed)
        {
            _logger.LogDebug("TwitchAuthorizationRefreshed получен после dispose – игнорируется ({Role})", _role);
            return;
        }

        bool isRunning;
        lock (_lockObj)
        {
            isRunning = _runCts is { IsCancellationRequested: false };
        }

        if (isRunning)
        {
            _logger.LogInformation("Перезапуск EventSub WebSocket ({Role}) после обновления авторизации", _role);
            await StopAsync(NullProgress, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation("Запуск EventSub WebSocket ({Role}) после получения авторизации", _role);
        }

        await StartInternalAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _authSubscription.Dispose();
        _eventSubClient.OnSessionWelcome -= OnSessionWelcomeAsync;
        _eventSubClient.OnNotification -= OnNotificationAsync;
        _eventSubClient.OnDisconnected -= OnDisconnectedAsync;

        CancellationTokenSource? runCts;
        lock (_lockObj)
        {
            _stopRequested = true;
            CancelAndResetReconnectToken();
            runCts = _runCts;
            _runCts = null;
        }

        if (runCts != null)
        {
            await runCts.CancelAsync();
            runCts.Dispose();
        }
    }

    private Task OnNotificationAsync(EventSubNotificationArgs args, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _lastMessageAtUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
        return Task.CompletedTask;
    }

    private Task OnSessionWelcomeAsync(EventSubSessionWelcomeArgs args, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _lastMessageAtUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
        _reconnectionPolicy.Reset();
        _logger.LogInformation("EventSub session welcome ({Role}, SessionId: {SessionId}) – статус мониторинга: Connected", _role, args.SessionId);
        return PublishStatusAsync(StreamMonitoringStatus.Connected);
    }

    private async Task OnDisconnectedAsync(EventSubDisconnectedArgs args, CancellationToken cancellationToken)
    {
        if (_disposed || _stopRequested)
        {
            _logger.LogDebug("EventSub WebSocket ({Role}) отключен ({Reason}); переподключение пропущено: disposed={IsDisposed}, stopRequested={IsStopRequested}",
                _role, args.Reason, _disposed, _stopRequested);

            return;
        }

        if (!_reconnectionPolicy.TryNextAttempt(out var delay))
        {
            _logger.LogError("EventSub WebSocket ({Role}) отключен ({Reason}); превышено максимальное количество попыток переподключения ({MaxAttempts}). Остановлен.",
                _role, args.Reason, _reconnectionPolicy.MaxAttempts);

            await PublishStatusAsync(StreamMonitoringStatus.Failed).ConfigureAwait(false);

            lock (_lockObj)
            {
                _stopRequested = true;
            }

            return;
        }

        var attempt = _reconnectionPolicy.CurrentAttempt;
        var maxAttempts = _reconnectionPolicy.MaxAttempts;

        _logger.LogWarning("EventSub WebSocket ({Role}) отключен ({Reason}); попытка переподключения {Attempt}/{MaxAttempts} через {DelaySeconds} сек...",
            _role, args.Reason, attempt, maxAttempts, (int)delay.TotalSeconds);

        await PublishStatusAsync(StreamMonitoringStatus.Reconnecting, $"Попытка {attempt}/{maxAttempts}").ConfigureAwait(false);

        CancellationToken token;
        lock (_lockObj)
        {
            if (_runCts == null)
            {
                _logger.LogDebug("Переподключение EventSub ({Role}) отменено: _runCts уже null (видимо, StopAsync прошёл одновременно)", _role);
                return;
            }

            CancelAndResetReconnectToken();
            _reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(_runCts.Token);
            token = _reconnectCts.Token;
        }

        try
        {
            await Task.Delay(delay, token);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Задержка перед переподключением EventSub ({Role}) отменена (попытка {Attempt}/{MaxAttempts})", _role, attempt, maxAttempts);
            return;
        }

        try
        {
            await _eventSubClient.StartAsync(token);
            _logger.LogInformation("Переподключение EventSub WebSocket ({Role}) инициировано (попытка {Attempt})", _role, attempt);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Переподключение EventSub ({Role}) отменено (попытка {Attempt}/{MaxAttempts})", _role, attempt, maxAttempts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка переподключения EventSub ({Role}) (попытка {Attempt}/{MaxAttempts})", _role, attempt, maxAttempts);
        }
    }

    private bool HasAccessToken()
    {
        try
        {
            return !string.IsNullOrEmpty(_accountsStore.Load(_role).AccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EventSubConnectionHost ({Role}): не удалось прочитать AccountsStore – считаем, что токена нет", _role);
            return false;
        }
    }

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource cts;

        lock (_lockObj)
        {
            if (_runCts is { IsCancellationRequested: false })
            {
                _logger.LogWarning("EventSubConnectionHost ({Role}) уже запущен", _role);
                return;
            }

            _runCts?.Dispose();
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runCts = cts;
            _stopRequested = false;
        }

        _reconnectionPolicy.Reset();

        await PublishStatusAsync(StreamMonitoringStatus.Connecting).ConfigureAwait(false);
        _logger.LogInformation("Подключение к EventSub WebSocket ({Role})...", _role);

        try
        {
            await _eventSubClient.StartAsync(cts.Token);
            _logger.LogInformation("Соединение EventSub ({Role}) инициировано. Ожидание подтверждения подключения...", _role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка запуска EventSub WebSocket ({Role})", _role);
            throw new InvalidOperationException($"Не удалось запустить EventSub WebSocket ({_role})", ex);
        }
    }

    private void CancelAndResetReconnectToken()
    {
        if (_reconnectCts == null)
        {
            return;
        }

        _reconnectCts.Cancel();
        _reconnectCts.Dispose();
        _reconnectCts = null;
    }

    private Task PublishStatusAsync(StreamMonitoringStatus status, string? detail = null)
    {
        lock (_lockObj)
        {
            _status = status;
        }

        _logger.LogDebug("Публикация StreamMonitoringStatusChanged: {Role} {Status} ({Detail})", _role, status, detail ?? "–");
        return _eventBus.PublishAsync(new StreamMonitoringStatusChanged(_role, status, detail), CancellationToken.None);
    }
}
