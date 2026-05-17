using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PoproshaykaBot.Core.Obs;

public sealed class ObsWebSocketClient(ILogger<ObsWebSocketClient> logger) : IObsWebSocketClient
{
    private const string InputVolumeMetersEventType = "InputVolumeMeters";

    private const int OpHello = 0;
    private const int OpIdentify = 1;
    private const int OpIdentified = 2;
    private const int OpEvent = 5;
    private const int OpRequest = 6;
    private const int OpRequestResponse = 7;
    private const int EventSubscriptionScenes = 1 << 2;
    private const int EventSubscriptionInputs = 1 << 3;
    private const int EventSubscriptionOutputs = 1 << 6;
    private const int EventSubscriptionInputVolumeMeters = 1 << 16;

    private const int DashboardEventSubscriptions =
        EventSubscriptionScenes | EventSubscriptionInputs | EventSubscriptionOutputs | EventSubscriptionInputVolumeMeters;

    private static readonly Action<ILogger, string, int, Exception?> LogConnecting =
        LoggerMessage.Define<string, int>(LogLevel.Information,
            new(1, nameof(LogConnecting)),
            "Подключение к OBS WebSocket {Host}:{Port}");

    private static readonly Action<ILogger, string, string, Exception?> LogConnected =
        LoggerMessage.Define<string, string>(LogLevel.Information,
            new(2, nameof(LogConnected)),
            "OBS WebSocket подключен. OBS {ObsVersion}, obs-websocket {ObsWebSocketVersion}");

    private static readonly Action<ILogger, string, Exception?> LogObsEvent =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new(3, nameof(LogObsEvent)),
            "OBS WebSocket event: {EventType}");

    private static readonly Action<ILogger, int, Exception?> LogIgnoredMessage =
        LoggerMessage.Define<int>(LogLevel.Debug,
            new(4, nameof(LogIgnoredMessage)),
            "OBS WebSocket сообщение op={Op} проигнорировано");

    private readonly ConcurrentDictionary<string, TaskCompletionSource<ObsRequestResponse>> _pending = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _syncLock = new();

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    public event EventHandler<ObsWebSocketEventArgs>? EventReceived;

    public bool IsConnected
    {
        get
        {
            lock (_syncLock)
            {
                return _socket?.State == WebSocketState.Open;
            }
        }
    }

    public async Task<ObsConnectionSnapshot> ConnectAsync(ObsConnectionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        await DisconnectAsync(cancellationToken).ConfigureAwait(false);

        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol("obswebsocket.json");

        CancellationTokenSource? receiveCts = null;

        try
        {
            LogConnecting(logger, options.Host, options.Port, null);

            await socket.ConnectAsync(options.Uri, cancellationToken).ConfigureAwait(false);
            var hello = await ReceiveTextMessageAsync(socket, cancellationToken).ConfigureAwait(false);
            var snapshot = await IdentifyAsync(socket, hello, options, cancellationToken).ConfigureAwait(false);

            receiveCts = new();

            lock (_syncLock)
            {
                _socket?.Dispose();
                _receiveCts?.Dispose();
                _socket = socket;
                _receiveCts = receiveCts;
                _receiveTask = Task.Run(() => ReceiveLoopAsync(socket, receiveCts.Token), CancellationToken.None);
            }

            LogConnected(logger, snapshot.ObsVersion ?? string.Empty, snapshot.ObsWebSocketVersion ?? string.Empty, null);

            return snapshot;
        }
        catch
        {
            receiveCts?.Dispose();
            socket.Dispose();
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        ClientWebSocket? socket;
        CancellationTokenSource? cts;
        Task? receiveTask;

        lock (_syncLock)
        {
            socket = _socket;
            cts = _receiveCts;
            receiveTask = _receiveTask;
        }

        if (socket is null)
        {
            return;
        }

        try
        {
            await (cts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client shutdown", cancellationToken).ConfigureAwait(false);
            }

            if (receiveTask is not null)
            {
                await receiveTask.ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            logger.LogDebug(exception, "OBS WebSocket отключен во время закрытия соединения");
        }
        finally
        {
            CompletePending(new InvalidOperationException("OBS WebSocket отключен."));
            ReleaseConnection(socket, cts, receiveTask);
        }
    }

    public async Task<JsonElement?> SendRequestAsync(string requestType, object? requestData, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestType);

        var socket = GetOpenSocket();
        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<ObsRequestResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pending.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException("Не удалось зарегистрировать OBS WebSocket запрос.");
        }

        try
        {
            object payload = requestData is null
                ? new { op = OpRequest, d = new { requestType, requestId } }
                : new { op = OpRequest, d = new { requestType, requestId, requestData } };

            await SendJsonAsync(socket, payload, cancellationToken).ConfigureAwait(false);

            var response = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!response.Result)
            {
                throw new ObsRequestException(requestType, response.Code, response.Comment);
            }

            return response.ResponseData;
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _sendLock.Dispose();
    }

    private static object CreateIdentifyData(JsonElement helloData, string password, int rpcVersion)
    {
        if (!helloData.TryGetProperty("authentication", out var authentication))
        {
            return new { rpcVersion, eventSubscriptions = DashboardEventSubscriptions };
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("OBS WebSocket требует пароль.");
        }

        var challenge = authentication.GetProperty("challenge").GetString() ?? string.Empty;
        var salt = authentication.GetProperty("salt").GetString() ?? string.Empty;
        var auth = CreateAuthenticationString(password, salt, challenge);

        return new { rpcVersion, authentication = auth, eventSubscriptions = DashboardEventSubscriptions };
    }

    private static string CreateAuthenticationString(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    private static async Task<string> ReceiveTextMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException("OBS WebSocket закрыл соединение.");
            }

            await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
    }

    private async Task<ObsConnectionSnapshot> IdentifyAsync(
        ClientWebSocket socket,
        string helloJson,
        ObsConnectionOptions options,
        CancellationToken cancellationToken)
    {
        using var helloDocument = JsonDocument.Parse(helloJson);
        var helloRoot = helloDocument.RootElement;
        if (helloRoot.GetProperty("op").GetInt32() != OpHello)
        {
            throw new InvalidOperationException("OBS WebSocket не прислал Hello-сообщение.");
        }

        var helloData = helloRoot.GetProperty("d");
        var rpcVersion = Math.Min(1, helloData.GetProperty("rpcVersion").GetInt32());
        var identifyData = CreateIdentifyData(helloData, options.Password, rpcVersion);

        await SendJsonAsync(socket, new { op = OpIdentify, d = identifyData }, cancellationToken).ConfigureAwait(false);

        var identifiedJson = await ReceiveTextMessageAsync(socket, cancellationToken).ConfigureAwait(false);
        using var identifiedDocument = JsonDocument.Parse(identifiedJson);
        if (identifiedDocument.RootElement.GetProperty("op").GetInt32() != OpIdentified)
        {
            throw new InvalidOperationException("OBS WebSocket не подтвердил Identify-сообщение.");
        }

        return new(true,
            GetOptionalString(helloData, "obsStudioVersion"),
            GetOptionalString(helloData, "obsWebSocketVersion"),
            null);
    }

    private ClientWebSocket GetOpenSocket()
    {
        lock (_syncLock)
        {
            var socket = _socket;
            if (socket?.State == WebSocketState.Open)
            {
                return socket;
            }
        }

        throw new InvalidOperationException("OBS WebSocket не подключен.");
    }

    private void ReleaseConnection(
        ClientWebSocket? socket,
        CancellationTokenSource? cts,
        Task? receiveTask)
    {
        lock (_syncLock)
        {
            if (ReferenceEquals(_receiveTask, receiveTask))
            {
                _receiveTask = null;
            }

            if (ReferenceEquals(_receiveCts, cts))
            {
                _receiveCts?.Dispose();
                _receiveCts = null;
                cts = null;
            }

            if (ReferenceEquals(_socket, socket))
            {
                _socket?.Dispose();
                _socket = null;
                socket = null;
            }
        }

        cts?.Dispose();
        socket?.Dispose();
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var json = await ReceiveTextMessageAsync(socket, cancellationToken).ConfigureAwait(false);
                DispatchMessage(json);
            }
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OBS WebSocket: соединение прервано");
            CompletePending(exception);
        }
    }

    private void DispatchMessage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var op = root.GetProperty("op").GetInt32();

        switch (op)
        {
            case OpRequestResponse:
                DispatchRequestResponse(root.GetProperty("d"));
                break;

            case OpEvent:
                DispatchEvent(root.GetProperty("d"));
                break;

            default:
                LogIgnoredMessage(logger, op, null);
                break;
        }
    }

    private void DispatchEvent(JsonElement data)
    {
        var eventType = GetOptionalString(data, "eventType") ?? string.Empty;
        if (!string.Equals(eventType, InputVolumeMetersEventType, StringComparison.Ordinal))
        {
            LogObsEvent(logger, eventType, null);
        }

        var handler = EventReceived;
        if (handler is null)
        {
            return;
        }

        var evt = new ObsWebSocketEventArgs(eventType,
            data.TryGetProperty("eventIntent", out var eventIntent) && eventIntent.ValueKind == JsonValueKind.Number
                ? eventIntent.GetInt32()
                : 0,
            data.TryGetProperty("eventData", out var eventData) ? eventData.Clone() : null);

        try
        {
            handler(this, evt);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OBS WebSocket: ошибка обработки события {EventType}", eventType);
        }
    }

    private void DispatchRequestResponse(JsonElement data)
    {
        var requestId = data.GetProperty("requestId").GetString();
        if (requestId is null || !_pending.TryGetValue(requestId, out var completion))
        {
            return;
        }

        var status = data.GetProperty("requestStatus");
        var response = new ObsRequestResponse(status.GetProperty("result").GetBoolean(),
            status.GetProperty("code").GetInt32(),
            GetOptionalString(status, "comment"),
            data.TryGetProperty("responseData", out var responseData) ? responseData.Clone() : null);

        completion.TrySetResult(response);
    }

    private async Task SendJsonAsync(ClientWebSocket socket, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var buffer = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void CompletePending(Exception exception)
    {
        foreach (var pending in _pending)
        {
            pending.Value.TrySetException(exception);
        }

        _pending.Clear();
    }

    private sealed record ObsRequestResponse(bool Result, int Code, string? Comment, JsonElement? ResponseData);
}
