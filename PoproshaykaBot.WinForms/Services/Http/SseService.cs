using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Timer = System.Threading.Timer;

namespace PoproshaykaBot.WinForms.Services.Http;

public sealed class SseService(SettingsManager settingsManager) : IChatDisplay, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private readonly List<HttpListenerResponse> _sseClients = [];
    private Timer? _ssePingTimer;
    private bool _isRunning;

    // TODO: Нормальное логирование
    public event Action<string>? LogMessage;

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        var keepAliveSeconds = Math.Max(5, settingsManager.Current.Twitch.Infrastructure.SseKeepAliveSeconds);

        _ssePingTimer = new(_ =>
        {
            try
            {
                SendSseToAllClients(": keep-alive");
            }
            catch
            {
            }
        }, null, TimeSpan.FromSeconds(keepAliveSeconds), TimeSpan.FromSeconds(keepAliveSeconds));

        _isRunning = true;
    }

    public void Stop()
    {
        _isRunning = false;
        _ssePingTimer?.Dispose();
        _ssePingTimer = null;

        lock (_sseClients)
        {
            foreach (var client in _sseClients.ToList())
            {
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Не удалось отключить sse-клиент {client.RedirectLocation}. Ошибка: {ex.Message}");
                }
            }

            _sseClients.Clear();
        }
    }

    public void AddClient(HttpListenerResponse response)
    {
        try
        {
            response.ContentType = "text/event-stream";
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            response.Headers.Add("Access-Control-Allow-Origin", "*");

            lock (_sseClients)
            {
                _sseClients.Add(response);
            }

            LogMessage?.Invoke("Новое SSE подключение установлено");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка SSE подключения: {ex.Message}");
        }
    }

    public void AddChatMessage(ChatMessageData chatMessage)
    {
        try
        {
            var messageData = new
            {
                type = "message",
                message = DtoMapper.ToServerMessage(chatMessage),
            };

            var json = JsonSerializer.Serialize(messageData, JsonSerializerOptions);
            SendSseToAllClients(json);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка отправки сообщения через SSE: {ex.Message}");
        }
    }

    public void ClearChat()
    {
        try
        {
            var clearData = new { type = "clear" };
            var json = JsonSerializer.Serialize(clearData);
            SendSseToAllClients(json);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка очистки чата через SSE: {ex.Message}");
        }
    }

    public void NotifyChatSettingsChanged(ObsChatSettings settings)
    {
        try
        {
            var cssSettings = ObsChatCssSettings.FromObsChatSettings(settings);

            var sseMessage = new
            {
                type = "chat_settings_changed",
                settings = cssSettings,
            };

            var json = JsonSerializer.Serialize(sseMessage, JsonSerializerOptions);

            int clientCount;
            lock (_sseClients)
            {
                clientCount = _sseClients.Count;
                if (clientCount == 0)
                {
                    return;
                }
            }

            SendSseToAllClients(json);
            LogMessage?.Invoke($"Отправлено уведомление об изменении настроек чата {clientCount} клиентам");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка отправки уведомления о настройках чата: {ex.Message}");
        }
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }

    private void SendSseToAllClients(string data)
    {
        lock (_sseClients)
        {
            var disconnectedClients = new List<HttpListenerResponse>();

            foreach (var client in _sseClients)
            {
                try
                {
                    var isComment = data.StartsWith(":");
                    var payload = isComment ? data + "\n\n" : $"data: {data}\n\n";
                    var buffer = Encoding.UTF8.GetBytes(payload);
                    client.OutputStream.Write(buffer, 0, buffer.Length);
                    client.OutputStream.Flush();
                }
                catch
                {
                    disconnectedClients.Add(client);
                }
            }

            foreach (var client in disconnectedClients)
            {
                _sseClients.Remove(client);
                client.Close();
            }

            if (disconnectedClients.Count > 0)
            {
                LogMessage?.Invoke($"Удалено {disconnectedClients.Count} отключившихся SSE клиентов");
            }
        }
    }
}
