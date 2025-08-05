using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;
using System.Net;
using System.Text;
using System.Text.Json;

namespace PoproshaykaBot.WinForms;

// TODO: Смешение ответственностей
public class UnifiedHttpServer : IChatDisplay, IDisposable
{
    private readonly HttpListener _httpListener;
    private readonly ChatHistoryManager _chatHistoryManager;
    private readonly List<HttpListenerResponse> _sseClients = [];
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly int _port;
    private TaskCompletionSource<string>? _oauthCodeTask;
    private bool _isRunning;
    private Task? _serverTask;

    public UnifiedHttpServer(ChatHistoryManager chatHistoryManager, int port = 8080)
    {
        _chatHistoryManager = chatHistoryManager;
        _port = port;
        _httpListener = new();
        _cancellationTokenSource = new();

        _httpListener.Prefixes.Add($"http://localhost:{port}/");
    }

    public event Action<string>? LogMessage;

    public Task StartAsync()
    {
        if (_isRunning)
        {
            return Task.CompletedTask;
        }

        try
        {
            _httpListener.Start();
            _isRunning = true;

            _chatHistoryManager.RegisterChatDisplay(this);
            _serverTask = HandleRequestsAsync();

            LogMessage?.Invoke($"HTTP сервер запущен на порту {_port}");

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
        if (_isRunning == false)
        {
            return;
        }

        try
        {
            _isRunning = false;
            await _cancellationTokenSource.CancelAsync();

            _chatHistoryManager.UnregisterChatDisplay(this);

            _httpListener.Stop();

            if (_serverTask != null)
            {
                await _serverTask;
            }

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

            LogMessage?.Invoke("HTTP сервер остановлен");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка остановки HTTP сервера: {ex.Message}");
        }
    }

    public Task<string> WaitForOAuthCodeAsync()
    {
        _oauthCodeTask = new();
        return _oauthCodeTask.Task;
    }

    public void AddChatMessage(ChatMessageData chatMessage)
    {
        try
        {
            var messageData = new
            {
                type = "message",
                message = new
                {
                    username = chatMessage.DisplayName,
                    message = chatMessage.Message,
                    timestamp = chatMessage.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    messageType = chatMessage.MessageType.ToString(),
                    emotes = chatMessage.Emotes.Select(e => new
                        {
                            id = e.Id,
                            name = e.Name,
                            imageUrl = e.ImageUrl,
                            startIndex = e.StartIndex,
                            endIndex = e.EndIndex,
                        })
                        .ToArray(),
                    badges = chatMessage.Badges.Select(b => new
                        {
                            type = b.Key,
                            version = b.Value,
                            imageUrl = chatMessage.BadgeUrls.TryGetValue($"{b.Key}/{b.Value}", out var url) ? url : "",
                        })
                        .ToArray(),
                },
            };

            var json = JsonSerializer.Serialize(messageData);
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

    public void Dispose()
    {
        StopAsync().Wait();
        _httpListener?.Close();
        _cancellationTokenSource?.Dispose();
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

            var json = JsonSerializer.Serialize(sseMessage);

            lock (_sseClients)
            {
                if (_sseClients.Count == 0)
                {
                    LogMessage?.Invoke("Нет подключенных SSE клиентов для отправки уведомления о настройках");
                    return;
                }
            }

            SendSseToAllClients(json);
            LogMessage?.Invoke($"Отправлено уведомление об изменении настроек чата {_sseClients.Count} клиентам");
        }
        catch (JsonException ex)
        {
            LogMessage?.Invoke($"Ошибка сериализации настроек для SSE: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка отправки уведомления о настройках чата: {ex.Message}");
        }
    }

    private async Task HandleRequestsAsync()
    {
        while (_isRunning && _cancellationTokenSource.Token.IsCancellationRequested == false)
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

    private void ProcessRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            LogMessage?.Invoke($"HTTP запрос: {request.HttpMethod} {request.Url?.PathAndQuery}");

            switch (request.Url?.AbsolutePath)
            {
                case "/":
                    HandleOAuthCallback(context);
                    break;

                case "/chat":
                    ServeObsOverlay(response);
                    break;

                case "/events":
                    HandleSseConnection(response);
                    break;

                case "/api/history":
                    ServeHistory(response);
                    break;

                case "/api/chat-settings":
                    ServeChatSettings(response);
                    break;

                default:
                    response.StatusCode = 404;
                    response.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка обработки запроса: {ex.Message}");

            context.Response.StatusCode = 500;
            context.Response.Close();
        }
    }

    private void HandleOAuthCallback(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var code = request.QueryString["code"];
            var error = request.QueryString["error"];

            if (string.IsNullOrEmpty(code) == false)
            {
                _oauthCodeTask?.SetResult(code);

                var successHtml =
                    """
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>Авторизация успешна</title>
                        <style>
                            body { font-family: Arial, sans-serif; text-align: center; margin-top: 50px; }
                            .success { color: green; font-size: 24px; }
                        </style>
                    </head>
                    <body>
                        <div class='success'>✓ Авторизация успешна!</div>
                        <p>Вы можете закрыть это окно и вернуться к приложению.</p>
                    </body>
                    </html>
                    """;

                var buffer = Encoding.UTF8.GetBytes(successHtml);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else if (string.IsNullOrEmpty(error) == false)
            {
                _oauthCodeTask?.SetException(new InvalidOperationException($"OAuth ошибка: {error}"));

                var errorHtml =
                    $$"""

                      <!DOCTYPE html>
                      <html>
                      <head>
                          <title>Ошибка авторизации</title>
                          <style>
                              body { font-family: Arial, sans-serif; text-align: center; margin-top: 50px; }
                              .error { color: red; font-size: 24px; }
                          </style>
                      </head>
                      <body>
                          <div class='error'>✗ Ошибка авторизации</div>
                          <p>Ошибка: {{error}}</p>
                          <p>Попробуйте еще раз.</p>
                      </body>
                      </html>
                      """;

                var buffer = Encoding.UTF8.GetBytes(errorHtml);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                response.StatusCode = 400;
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка обработки OAuth callback: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    private void ServeObsOverlay(HttpListenerResponse response)
    {
        try
        {
            var html = GetObsOverlayHtml();
            var buffer = Encoding.UTF8.GetBytes(html);

            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка отдачи OBS overlay: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    // TODO: Удалить избыточные сообщения при обновлении настроек
    // TODO: Поправить отображение бейджей и эмодзи в историчных сообщениях
    private string GetObsOverlayHtml()
    {
        return """
               <!DOCTYPE html>
               <html>
               <head>
                   <title>PoproshaykaBot Chat Overlay</title>
                   <meta charset='utf-8'>
                   <style>
                       :root {
                           --chat-bg-color: rgba(0, 0, 0, 0.7);
                           --chat-text-color: #ffffff;
                           --chat-username-color: #9146ff;
                           --chat-system-color: #ffcc00;
                           --chat-timestamp-color: #999999;
                           --chat-font-family: Arial, sans-serif;
                           --chat-font-size: 14px;
                           --chat-font-weight: normal;
                           --chat-padding: 5px;
                           --chat-margin: 5px 0;
                           --chat-border-radius: 5px;
                           --chat-animation-duration: 0.3s;

                           --emote-size: 28px;
                           --badge-size: 18px;
                       }

                       body {
                           background: transparent;
                           font-family: var(--chat-font-family);
                           font-size: var(--chat-font-size);
                           font-weight: var(--chat-font-weight);
                           margin: 0;
                           padding: var(--chat-padding);
                           color: var(--chat-text-color);
                       }

                       .message {
                           margin: var(--chat-margin);
                           animation: slideIn var(--chat-animation-duration) ease-out;
                           padding: var(--chat-padding);
                           border-radius: var(--chat-border-radius);
                           background: var(--chat-bg-color);
                       }

                       .message.no-animation {
                           animation: none;
                       }

                       @keyframes slideIn {
                           from { transform: translateX(-100%); opacity: 0; }
                           to { transform: translateX(0); opacity: 1; }
                       }

                       .username {
                           font-weight: bold;
                           color: var(--chat-username-color);
                       }

                       .timestamp {
                           color: var(--chat-timestamp-color);
                           font-size: 0.8em;
                           margin-right: 5px;
                       }

                       .message-text {
                           color: var(--chat-text-color);
                       }

                       .system-message {
                           color: var(--chat-system-color);
                           font-style: italic;
                       }

                       .badge {
                           width: var(--badge-size);
                           height: var(--badge-size);
                           margin-right: 2px;
                           vertical-align: middle;
                           border-radius: 2px;
                       }

                       .emote {
                           height: var(--emote-size);
                           vertical-align: middle;
                           margin: 0 1px;
                       }
                   </style>
               </head>
               <body>
                   <div id='chat'></div>
                   <script>
                       const chatContainer = document.getElementById('chat');
                       let maxMessages = 50;
                       let showTimestamp = true;
                       let enableAnimations = true;

                       const eventSource = new EventSource('/events');

                       eventSource.onmessage = function(event) {
                           try {
                               const data = JSON.parse(event.data);
                               if (data.type === 'message') {
                                   addMessage(data.message);
                               } else if (data.type === 'clear') {
                                   clearChat();
                               } else if (data.type === 'chat_settings_changed') {
                                   updateChatSettings(data.settings);
                               }
                           } catch (e) {
                               console.error('Ошибка парсинга SSE данных:', e);
                           }
                       };

                       eventSource.onerror = function(event) {
                           console.error('SSE ошибка:', event);
                       };

                       function addMessage(message, isHistoryMessage = false) {
                           const messageDiv = document.createElement('div');
                           messageDiv.className = 'message';

                           if (isHistoryMessage || !enableAnimations) {
                               messageDiv.classList.add('no-animation');
                           }

                           const timestamp = new Date(message.timestamp).toLocaleTimeString();
                           const isSystemMessage = message.messageType !== 'UserMessage';

                           let timestampHtml = showTimestamp ? `<span class='timestamp'>${timestamp}</span>` : '';

                           if (isSystemMessage) {
                               messageDiv.innerHTML = `
                                   ${timestampHtml}
                                   <span class='system-message'>${message.message}</span>
                               `;
                           } else {
                               const badgesHtml = renderBadges(message.badges || []);
                               const messageWithEmotes = renderMessageWithEmotes(message.message, message.emotes || []);

                               messageDiv.innerHTML = `
                                   ${timestampHtml}
                                   ${badgesHtml}
                                   <span class='username'>${message.username}:</span>
                                   <span class='message-text'> ${messageWithEmotes}</span>
                               `;
                           }

                           chatContainer.appendChild(messageDiv);

                           while (chatContainer.children.length > maxMessages) {
                               chatContainer.removeChild(chatContainer.firstChild);
                           }

                           chatContainer.scrollTop = chatContainer.scrollHeight;
                       }

                       function clearChat() {
                           chatContainer.innerHTML = '';
                       }

                       function renderBadges(badges) {
                           if (!badges || badges.length === 0) return '';

                           return badges.map(badge => {
                               if (!badge.imageUrl) return '';
                               return `<img src="${badge.imageUrl}" alt="${badge.type}" title="${badge.type} ${badge.version}" class="badge">`;
                           }).join('');
                       }

                       function renderMessageWithEmotes(message, emotes) {
                           if (!emotes || emotes.length === 0) return escapeHtml(message);

                           const sortedEmotes = emotes.sort((a, b) => b.startIndex - a.startIndex);

                           let result = message;
                           for (const emote of sortedEmotes) {
                               if (emote.imageUrl && emote.startIndex >= 0 && emote.endIndex >= emote.startIndex) {
                                   const before = result.substring(0, emote.startIndex);
                                   const after = result.substring(emote.endIndex + 1);
                                   const emoteImg = `<img src="${emote.imageUrl}" alt="${emote.name}" title="${emote.name}" class="emote">`;
                                   result = before + emoteImg + after;
                               }
                           }

                           return escapeHtml(result, true); // true = не экранировать HTML теги img
                       }

                       function escapeHtml(text, preserveImgTags = false) {
                           if (preserveImgTags) {
                               const imgTags = [];
                               text = text.replace(/<img[^>]*>/g, (match) => {
                                   imgTags.push(match);
                                   return `__IMG_PLACEHOLDER_${imgTags.length - 1}__`;
                               });

                               text = text.replace(/&/g, '&amp;')
                                         .replace(/</g, '&lt;')
                                         .replace(/>/g, '&gt;')
                                         .replace(/"/g, '&quot;')
                                         .replace(/'/g, '&#039;');

                               text = text.replace(/__IMG_PLACEHOLDER_(\d+)__/g, (match, index) => {
                                   return imgTags[parseInt(index)];
                               });

                               return text;
                           }

                           return text.replace(/&/g, '&amp;')
                                     .replace(/</g, '&lt;')
                                     .replace(/>/g, '&gt;')
                                     .replace(/"/g, '&quot;')
                                     .replace(/'/g, '&#039;');
                       }

                       function updateChatSettings(settings) {
                           const root = document.documentElement;

                           if (settings.backgroundColor) root.style.setProperty('--chat-bg-color', settings.backgroundColor);
                           if (settings.textColor) root.style.setProperty('--chat-text-color', settings.textColor);
                           if (settings.usernameColor) root.style.setProperty('--chat-username-color', settings.usernameColor);
                           if (settings.systemMessageColor) root.style.setProperty('--chat-system-color', settings.systemMessageColor);
                           if (settings.timestampColor) root.style.setProperty('--chat-timestamp-color', settings.timestampColor);
                           if (settings.fontFamily) root.style.setProperty('--chat-font-family', settings.fontFamily);
                           if (settings.fontSize) root.style.setProperty('--chat-font-size', settings.fontSize);
                           if (settings.fontWeight) root.style.setProperty('--chat-font-weight', settings.fontWeight);
                           if (settings.padding) root.style.setProperty('--chat-padding', settings.padding);
                           if (settings.margin) root.style.setProperty('--chat-margin', settings.margin);
                           if (settings.borderRadius) root.style.setProperty('--chat-border-radius', settings.borderRadius);
                           if (settings.animationDuration) root.style.setProperty('--chat-animation-duration', settings.animationDuration);

                           if (settings.emoteSize) root.style.setProperty('--emote-size', settings.emoteSize);
                           if (settings.badgeSize) root.style.setProperty('--badge-size', settings.badgeSize);

                           if (settings.maxMessages !== undefined) maxMessages = settings.maxMessages;
                           if (settings.showTimestamp !== undefined) showTimestamp = settings.showTimestamp;
                           if (settings.enableAnimations !== undefined) enableAnimations = settings.enableAnimations;

                           console.log('Настройки чата обновлены:', settings);
                       }

                       fetch('/api/chat-settings')
                           .then(response => response.json())
                           .then(settings => {
                               updateChatSettings(settings);
                           })
                           .catch(error => console.error('Ошибка загрузки настроек чата:', error));

                       fetch('/api/history')
                           .then(response => response.json())
                           .then(messages => {
                               messages.forEach(message => addMessage(message, true));
                           })
                           .catch(error => console.error('Ошибка загрузки истории:', error));
                   </script>
               </body>
               </html>
               """;
    }

    private void HandleSseConnection(HttpListenerResponse response)
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

    private void ServeHistory(HttpListenerResponse response)
    {
        try
        {
            var history = _chatHistoryManager.GetHistory()
                .TakeLast(10)
                .Select(msg => new
                {
                    username = msg.DisplayName,
                    message = msg.Message,
                    timestamp = msg.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    messageType = msg.MessageType.ToString(),
                });

            var json = JsonSerializer.Serialize(history);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка отдачи истории: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    private void ServeChatSettings(HttpListenerResponse response)
    {
        try
        {
            var settings = SettingsManager.Current.Twitch.ObsChat;
            var cssSettings = ObsChatCssSettings.FromObsChatSettings(settings);

            var json = JsonSerializer.Serialize(cssSettings);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json; charset=utf-8";
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Cache-Control", "no-cache");
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);

            LogMessage?.Invoke("Настройки чата успешно отданы клиенту");
        }
        catch (JsonException ex)
        {
            LogMessage?.Invoke($"Ошибка сериализации настроек чата: {ex.Message}");
            response.StatusCode = 500;
            WriteErrorResponse(response, "Ошибка обработки настроек");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка отдачи настроек чата: {ex.Message}");
            response.StatusCode = 500;
            WriteErrorResponse(response, "Внутренняя ошибка сервера");
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Ошибка закрытия ответа: {ex.Message}");
            }
        }
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
                    var message = $"data: {data}\n\n";
                    var buffer = Encoding.UTF8.GetBytes(message);
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

    private void WriteErrorResponse(HttpListenerResponse response, string errorMessage)
    {
        try
        {
            var errorObj = new { error = errorMessage };
            var json = JsonSerializer.Serialize(errorObj);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка записи ошибки в ответ: {ex.Message}");
        }
    }
}
