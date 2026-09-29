using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace PoproshaykaBot.Core.Tests.Twitch.EventSub;

internal sealed class FakeEventSubEndpoint : IAsyncDisposable
{
    public FakeEventSubEndpoint()
    {
        var port = GetFreePort();
        Uri = new($"ws://localhost:{port}/eventsub/");
        _listener.Prefixes.Add($"http://localhost:{port}/eventsub/");
        _listener.Start();
        _acceptTask = Task.Run(AcceptAsync);
    }

    public Uri Uri { get; }

    public Task<WebSocket> Connected => _connected.Task;

    public Task<WebSocketCloseStatus?> ClosedByClient => _closedByClient.Task;

    public async Task SendAsync(string json)
    {
        var socket = await _connected.Task;
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task CloseFromServerAsync()
    {
        var socket = await _connected.Task;
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "server close", CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Close();

        try
        {
            await _acceptTask;
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or HttpListenerException or ObjectDisposedException)
        {
        }

        _socket?.Dispose();
        _cts.Dispose();
    }

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<WebSocket> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<WebSocketCloseStatus?> _closedByClient = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _acceptTask;

    private WebSocket? _socket;

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task AcceptAsync()
    {
        try
        {
            var context = await _listener.GetContextAsync();
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            _socket = webSocketContext.WebSocket;
            _connected.TrySetResult(_socket);

            var buffer = new byte[8 * 1024];

            while (_socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(buffer, _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _closedByClient.TrySetResult(result.CloseStatus);
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or HttpListenerException or ObjectDisposedException)
        {
        }
        finally
        {
            _connected.TrySetCanceled();
            _closedByClient.TrySetResult(null);
        }
    }
}
