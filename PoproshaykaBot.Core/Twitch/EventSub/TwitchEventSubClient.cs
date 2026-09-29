using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace PoproshaykaBot.Core.Twitch.EventSub;

public sealed class TwitchEventSubClient(ILogger<TwitchEventSubClient> logger) : ITwitchEventSubClient, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan KeepaliveGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultKeepaliveTimeout = TimeSpan.FromSeconds(10);

    private readonly object _stateLock = new();

    private CancellationTokenSource? _internalCts;
    private Task? _runnerTask;
    private EventSubSession? _pendingMigration;

    public event EventSubAsyncHandler<EventSubDisconnectedArgs>? OnDisconnected;

    public event EventSubAsyncHandler<EventSubNotificationArgs>? OnNotification;

    public event EventSubAsyncHandler<EventSubRevocationArgs>? OnRevocation;

    public event EventSubAsyncHandler<EventSubReconnectArgs>? OnSessionReconnect;

    public event EventSubAsyncHandler<EventSubSessionWelcomeArgs>? OnSessionWelcome;

    internal string BaseUrl { get; init; } = TwitchEndpoints.EventSubWebSocket;

    public string? SessionId
    {
        get
        {
            lock (_stateLock)
            {
                return field;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                field = value;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource cts;

        lock (_stateLock)
        {
            if (_runnerTask is { IsCompleted: false })
            {
                return Task.CompletedTask;
            }

            _internalCts?.Dispose();
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _internalCts = cts;
            _runnerTask = Task.Run(() => RunAsync(cts.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cts;
        Task? runner;

        lock (_stateLock)
        {
            cts = _internalCts;
            runner = _runnerTask;
        }

        if (cts is null)
        {
            return;
        }

        await cts.CancelAsync();

        if (runner is not null)
        {
            try
            {
                await runner.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
        }

        lock (_stateLock)
        {
            if (ReferenceEquals(_runnerTask, runner))
            {
                _runnerTask = null;
            }

            if (ReferenceEquals(_internalCts, cts))
            {
                _internalCts.Dispose();
                _internalCts = null;
            }
        }

        SessionId = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
    }

    private static string DescribeSession(EventSubSession session)
    {
        return string.IsNullOrEmpty(session.SessionId) ? "без id" : session.SessionId;
    }

    private static string MapDisconnectReason(Exception ex)
    {
        return ex switch
        {
            OperationCanceledException => "Остановка EventSub-клиента",
            WebSocketException or SocketException => "Соединение EventSub прервано",
            _ when ex.GetType().Namespace?.StartsWith("System.Net", StringComparison.Ordinal) == true
                   || ex.InnerException is WebSocketException or SocketException
                => "Соединение EventSub прервано",
            _ => "Неизвестная ошибка соединения EventSub",
        };
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        string? disconnectReason = null;
        var current = new EventSubSession(new(BaseUrl), null, string.Empty);

        try
        {
            current.Runner = RunSessionAsync(current, cancellationToken);

            while (true)
            {
                Exception? failure = null;

                try
                {
                    await current.Runner;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                if (ClaimMigratedSession() is { } successor)
                {
                    current = successor;
                    continue;
                }

                if (current.KeepaliveExpired)
                {
                    disconnectReason = "Соединение EventSub прервано: нет keepalive";
                    logger.LogWarning("EventSub соединение разорвано: сессия {SessionId}, нет keepalive", DescribeSession(current));
                }
                else if (failure is not null)
                {
                    logger.LogWarning(failure, "EventSub соединение разорвано: сессия {SessionId}, код закрытия {CloseStatus} {CloseDescription}",
                        DescribeSession(current),
                        current.CloseStatus,
                        current.CloseDescription);

                    disconnectReason = MapDisconnectReason(failure);
                }

                break;
            }
        }
        finally
        {
            await AbortPendingMigrationAsync();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _ = SafeRaiseAsync(OnDisconnected, new(disconnectReason ?? "Сессия EventSub завершена"), CancellationToken.None);
    }

    private async Task RunSessionAsync(EventSubSession session, CancellationToken cancellationToken)
    {
        try
        {
            await PumpSessionAsync(session, cancellationToken);
        }
        finally
        {
            session.Dispose();
        }
    }

    private async Task RunMigrationAsync(EventSubSession session, CancellationToken cancellationToken)
    {
        try
        {
            await RunSessionAsync(session, cancellationToken);

            if (session.WelcomeReceived is false)
            {
                logger.LogWarning("Миграция EventSub не состоялась: сокет по reconnect_url закрылся до session_welcome, работаем на прежней сессии {OldSessionId}",
                    session.OldSessionId);
            }
        }
        catch (Exception ex) when (session.WelcomeReceived is false)
        {
            logger.LogWarning(ex, "Миграция EventSub не состоялась: сокет по reconnect_url оборвался до session_welcome, работаем на прежней сессии {OldSessionId}",
                session.OldSessionId);
        }
        finally
        {
            if (session.WelcomeReceived is false)
            {
                DiscardMigration(session);
            }
        }
    }

    private async Task PumpSessionAsync(EventSubSession session, CancellationToken cancellationToken)
    {
        var ws = session.Socket;
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        if (session.Predecessor is null)
        {
            logger.LogInformation("Подключение EventSub WebSocket к {Url}", session.Uri.OriginalString);
        }
        else
        {
            logger.LogInformation("Подключение EventSub WebSocket к {Url} для миграции с сессии {OldSessionId}",
                session.Uri.OriginalString,
                session.OldSessionId);
        }

        await ws.ConnectAsync(session.Uri, cancellationToken);
        Interlocked.Exchange(ref session.LastKeepaliveTicks, DateTime.UtcNow.Ticks);

        var buffer = new byte[16 * 1024];
        var sb = new StringBuilder();
        var keepaliveTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        var keepaliveTask = MonitorKeepaliveAsync(session, keepaliveTimer, cancellationToken);

        try
        {
            while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        session.CloseStatus = result.CloseStatus;
                        session.CloseDescription = result.CloseStatusDescription;

                        logger.LogInformation("EventSub получил Close по сессии {SessionId}: {Status} {Description}",
                            DescribeSession(session),
                            result.CloseStatus,
                            result.CloseStatusDescription);

                        return;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                Interlocked.Exchange(ref session.LastKeepaliveTicks, DateTime.UtcNow.Ticks);

                var raw = sb.ToString();
                EventSubMessageDto? message;
                try
                {
                    message = JsonSerializer.Deserialize<EventSubMessageDto>(raw, JsonOptions);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "EventSub получил неразбираемое сообщение: {Raw}", raw);
                    continue;
                }

                if (message is null)
                {
                    continue;
                }

                await DispatchAsync(session, message, cancellationToken);
            }
        }
        finally
        {
            keepaliveTimer.Dispose();
            try
            {
                await keepaliveTask;
            }
            catch (OperationCanceledException)
            {
                // keepalive monitor stopped with the session
            }

            if (ws.State == WebSocketState.Open)
            {
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "client shutdown", CancellationToken.None);
                }
                catch
                {
                    // best-effort close; ignore failures during shutdown
                }
            }
        }
    }

    private async Task MonitorKeepaliveAsync(EventSubSession session, PeriodicTimer timer, CancellationToken cancellationToken)
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (session.Socket.State != WebSocketState.Open)
            {
                return;
            }

            var lastKeepalive = new DateTime(Interlocked.Read(ref session.LastKeepaliveTicks), DateTimeKind.Utc);
            var elapsed = DateTime.UtcNow - lastKeepalive;
            var limit = session.KeepaliveTimeout + KeepaliveGrace;

            if (elapsed > limit)
            {
                logger.LogWarning("Не получено keepalive {Elapsed} сек > порога {Limit} сек по сессии {SessionId}, форсируем reconnect",
                    elapsed.TotalSeconds,
                    limit.TotalSeconds,
                    DescribeSession(session));

                session.KeepaliveExpired = true;
                session.Socket.Abort();
                return;
            }
        }
    }

    private async Task DispatchAsync(EventSubSession session, EventSubMessageDto message, CancellationToken cancellationToken)
    {
        switch (message.Metadata.MessageType)
        {
            case "session_welcome":
                await HandleSessionWelcomeAsync(session, message, cancellationToken);
                break;

            case "session_keepalive":
                break;

            case "notification":
                await HandleNotificationAsync(message, cancellationToken);
                break;

            case "session_reconnect":
                await HandleSessionReconnectAsync(session, message, cancellationToken);
                break;

            case "revocation":
                await HandleRevocationAsync(message, cancellationToken);
                break;

            default:
                logger.LogWarning("EventSub: неизвестный message_type {Type} по сессии {SessionId}", message.Metadata.MessageType, DescribeSession(session));
                break;
        }
    }

    private async Task HandleSessionWelcomeAsync(EventSubSession session, EventSubMessageDto message, CancellationToken cancellationToken)
    {
        var dto = message.Payload.Deserialize<EventSubSessionPayloadDto>(JsonOptions);
        if (dto is null)
        {
            return;
        }

        if (dto.Session.KeepaliveTimeoutSeconds is { } seconds and > 0)
        {
            session.KeepaliveTimeout = TimeSpan.FromSeconds(seconds);
        }

        SessionId = dto.Session.Id;
        session.SessionId = dto.Session.Id;

        if (session.Predecessor is not { } predecessor)
        {
            logger.LogInformation("EventSub session_welcome: id={SessionId}, keepalive={Seconds} сек", dto.Session.Id, session.KeepaliveTimeout.TotalSeconds);
            await SafeRaiseAsync(OnSessionWelcome, new(dto.Session.Id, dto.Session.KeepaliveTimeoutSeconds), cancellationToken);
            return;
        }

        lock (_stateLock)
        {
            session.WelcomeReceived = true;
        }

        logger.LogInformation("EventSub сессия мигрировала: {OldSessionId} → {NewSessionId}, keepalive={Seconds} сек; старый сокет закрывается",
            session.OldSessionId, dto.Session.Id, session.KeepaliveTimeout.TotalSeconds);

        await CloseMigratedSocketAsync(predecessor);
        await SafeRaiseAsync(OnSessionReconnect, new(session.Uri.OriginalString, session.OldSessionId, dto.Session.Id), cancellationToken);
    }

    private async Task HandleNotificationAsync(EventSubMessageDto message, CancellationToken cancellationToken)
    {
        var subscriptionType = message.Metadata.SubscriptionType ?? string.Empty;
        var subscriptionVersion = message.Metadata.SubscriptionVersion ?? "1";
        await SafeRaiseAsync(OnNotification,
            new(subscriptionType, subscriptionVersion, message.Metadata.MessageId, message.Metadata.MessageTimestamp, message.Payload),
            cancellationToken);
    }

    private Task HandleSessionReconnectAsync(EventSubSession session, EventSubMessageDto message, CancellationToken cancellationToken)
    {
        var dto = message.Payload.Deserialize<EventSubSessionPayloadDto>(JsonOptions);
        if (dto is null
            || string.IsNullOrEmpty(dto.Session.ReconnectUrl)
            || Uri.TryCreate(dto.Session.ReconnectUrl, UriKind.Absolute, out var reconnectUri) is false)
        {
            logger.LogWarning("session_reconnect по сессии {SessionId} без пригодного reconnect_url, обычная реконнект-логика", DescribeSession(session));
            return Task.CompletedTask;
        }

        lock (_stateLock)
        {
            if (_pendingMigration is not null)
            {
                logger.LogWarning("session_reconnect по сессии {SessionId} пропущен: миграция сессии EventSub уже идёт", DescribeSession(session));
                return Task.CompletedTask;
            }

            var successor = new EventSubSession(reconnectUri, session, SessionId ?? string.Empty);
            _pendingMigration = successor;
            successor.Runner = RunMigrationAsync(successor, cancellationToken);
        }

        logger.LogInformation("EventSub session_reconnect по сессии {SessionId} → {Url}: открыт второй сокет, старый закроем после session_welcome",
            DescribeSession(session),
            dto.Session.ReconnectUrl);

        return Task.CompletedTask;
    }

    private EventSubSession? ClaimMigratedSession()
    {
        lock (_stateLock)
        {
            if (_pendingMigration is not { WelcomeReceived: true } migration)
            {
                return null;
            }

            _pendingMigration = null;
            return migration;
        }
    }

    private void DiscardMigration(EventSubSession session)
    {
        lock (_stateLock)
        {
            if (ReferenceEquals(_pendingMigration, session))
            {
                _pendingMigration = null;
            }
        }
    }

    private async Task AbortPendingMigrationAsync()
    {
        EventSubSession? pending;

        lock (_stateLock)
        {
            pending = _pendingMigration;
            _pendingMigration = null;
        }

        if (pending is null)
        {
            return;
        }

        try
        {
            pending.Socket.Abort();
            await pending.Runner;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Сокет незавершённой миграции EventSub закрыт с ошибкой");
        }
    }

    private async Task CloseMigratedSocketAsync(EventSubSession predecessor)
    {
        try
        {
            if (predecessor.Socket.State == WebSocketState.Open)
            {
                await predecessor.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "session migrated", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Старый сокет EventSub не принял закрытие после миграции");
        }
    }

    private async Task HandleRevocationAsync(EventSubMessageDto message, CancellationToken cancellationToken)
    {
        var dto = message.Payload.Deserialize<EventSubRevocationPayloadDto>(JsonOptions);
        if (dto is null)
        {
            return;
        }

        logger.LogWarning("EventSub revocation: id={Id}, type={Type}, status={Status}", dto.Subscription.Id, dto.Subscription.Type, dto.Subscription.Status);
        await SafeRaiseAsync(OnRevocation, new(dto.Subscription.Id, dto.Subscription.Type, dto.Subscription.Status), cancellationToken);
    }

    private async Task SafeRaiseAsync<TArgs>(EventSubAsyncHandler<TArgs>? handler, TArgs args, CancellationToken cancellationToken)
    {
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList().Cast<EventSubAsyncHandler<TArgs>>())
        {
            try
            {
                await subscriber(args, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Подписчик EventSub упал на типе {Type}", typeof(TArgs).Name);
            }
        }
    }

    private sealed class EventSubSession(Uri uri, EventSubSession? predecessor, string oldSessionId) : IDisposable
    {
        public long LastKeepaliveTicks = DateTime.UtcNow.Ticks;

        public Uri Uri { get; } = uri;

        public ClientWebSocket Socket { get; } = new();

        public EventSubSession? Predecessor { get; } = predecessor;

        public string OldSessionId { get; } = oldSessionId;

        public TimeSpan KeepaliveTimeout { get; set; } = DefaultKeepaliveTimeout;

        public bool KeepaliveExpired { get; set; }

        public bool WelcomeReceived { get; set; }

        public string SessionId { get; set; } = string.Empty;

        public WebSocketCloseStatus? CloseStatus { get; set; }

        public string? CloseDescription { get; set; }

        public Task Runner { get; set; } = Task.CompletedTask;

        public void Dispose()
        {
            Socket.Dispose();
        }
    }
}
