using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Services.Http;
using PoproshaykaBot.WinForms.Settings;
using System.Net;

namespace PoproshaykaBot.WinForms;

// TODO: Смешение ответственностей
public sealed class UnifiedHttpServer(
    ChatHistoryManager chatHistoryManager,
    Router router,
    SseService sseService,
    int port = 8080)
    : IChatDisplay, IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private HttpListener _httpListener = CreateListener(port);
    private Task? _serverTask;

    public event Action<string>? LogMessage;

    public bool IsRunning { get; private set; }

    public Task StartAsync()
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (!_httpListener.IsListening)
            {
                _httpListener.Close();
                _httpListener = CreateListener(port);
            }

            _httpListener.Start();
            IsRunning = true;

            chatHistoryManager.RegisterChatDisplay(this);
            sseService.Start();
            _serverTask = HandleRequestsAsync();

            LogMessage?.Invoke($"HTTP сервер запущен на порту {port}");

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка запуска HTTP сервера: {ex.Message}");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            IsRunning = false;
            await _cancellationTokenSource.CancelAsync();

            chatHistoryManager.UnregisterChatDisplay(this);
            sseService.Stop();

            _httpListener.Stop();

            if (_serverTask != null)
            {
                await _serverTask;
            }

            LogMessage?.Invoke("HTTP сервер остановлен");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка остановки HTTP сервера: {ex.Message}");
        }
    }

    public void AddChatMessage(ChatMessageData chatMessage)
    {
        sseService.AddChatMessage(chatMessage);
    }

    public void ClearChat()
    {
        sseService.ClearChat();
    }

    public void NotifyChatSettingsChanged(ObsChatSettings settings)
    {
        sseService.NotifyChatSettingsChanged(settings);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    private static HttpListener CreateListener(int port)
    {
        var httpListener = new HttpListener();
        httpListener.Prefixes.Add($"http://localhost:{port}/");
        return httpListener;
    }

    private async ValueTask DisposeAsyncCore()
    {
        await StopAsync();
        _httpListener.Close();
        _cancellationTokenSource.Dispose();
    }

    private async Task HandleRequestsAsync()
    {
        while (IsRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                _ = Task.Run(() => ProcessRequest(context), _cancellationTokenSource.Token);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Ошибка обработки HTTP запроса: {ex.Message}");
            }
        }
    }

    private async Task ProcessRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            LogMessage?.Invoke($"HTTP запрос: {request.HttpMethod} {request.Url?.PathAndQuery}");
            await router.RouteAsync(context);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка обработки запроса: {ex.Message}");
            context.Response.StatusCode = 500;
            context.Response.Close();
        }
    }
}
