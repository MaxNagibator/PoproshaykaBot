using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Channels;

namespace PoproshaykaBot.WinForms.Services.Http;

public sealed class SseService(SettingsManager settingsManager, ILogger<SseService> logger) : IChatDisplay, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private readonly List<HttpListenerResponse> _sseClients = [];

    private readonly Channel<string> _messageChannel = Channel.CreateUnbounded<string>(new()
    {
        SingleReader = true,
    });

    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private Task? _keepAliveTask;
    private bool _isRunning;

    public void Start()
    {
        logger.LogDebug("Инициализация запуска сервиса SSE");

        if (_isRunning)
        {
            logger.LogWarning("Сервис SSE уже запущен. Повторный запрос на запуск проигнорирован");
            return;
        }

        _cts = new();
        var keepAliveSeconds = Math.Max(5, settingsManager.Current.Twitch.Infrastructure.SseKeepAliveSeconds);

        _keepAliveTask = Task.Run(() => KeepAliveLoopAsync(keepAliveSeconds, _cts.Token));
        _broadcastTask = Task.Run(() => BroadcastLoopAsync(_cts.Token));

        _isRunning = true;
        logger.LogInformation("Сервис SSE успешно запущен. Интервал keep-alive: {KeepAliveSeconds} сек.", keepAliveSeconds);
    }

    public void Stop()
    {
        logger.LogDebug("Остановка сервиса SSE");

        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;

        _cts?.Cancel();

        lock (_sseClients)
        {
            foreach (var client in _sseClients)
            {
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Не удалось корректно закрыть соединение SSE клиента при остановке");
                }
            }

            logger.LogInformation("Отключено {ClientCount} SSE клиентов при остановке сервиса", _sseClients.Count);
            _sseClients.Clear();
        }
    }

    public void AddClient(HttpListenerResponse response)
    {
        logger.LogDebug("Попытка регистрации нового SSE клиента");

        try
        {
            response.ContentType = "text/event-stream";
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            response.Headers.Add("Access-Control-Allow-Origin", "*");

            int clientCount;
            lock (_sseClients)
            {
                _sseClients.Add(response);
                clientCount = _sseClients.Count;
            }

            logger.LogInformation("Установлено новое SSE подключение. Всего активных клиентов: {ClientCount}", clientCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при установке нового SSE подключения");
        }
    }

    public void AddChatMessage(ChatMessageData chatMessage)
    {
        logger.LogDebug("Подготовка нового сообщения чата для отправки в SSE");

        try
        {
            var messageData = new
            {
                type = "message",
                message = DtoMapper.ToServerMessage(chatMessage),
            };

            var json = JsonSerializer.Serialize(messageData, JsonSerializerOptions);
            EnqueueMessage(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка сериализации или постановки в очередь сообщения чата");
        }
    }

    public void ClearChat()
    {
        logger.LogInformation("Инициация события очистки чата для всех SSE клиентов");

        try
        {
            var clearData = new { type = "clear" };
            var json = JsonSerializer.Serialize(clearData);
            EnqueueMessage(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка постановки в очередь события очистки чата");
        }
    }

    public void NotifyChatSettingsChanged(ObsChatSettings settings)
    {
        logger.LogDebug("Подготовка уведомления об изменении настроек чата");

        try
        {
            var cssSettings = ObsChatCssSettings.FromObsChatSettings(settings);

            var sseMessage = new
            {
                type = "chat_settings_changed",
                settings = cssSettings,
            };

            var json = JsonSerializer.Serialize(sseMessage, JsonSerializerOptions);

            lock (_sseClients)
            {
                if (_sseClients.Count == 0)
                {
                    logger.LogDebug("Нет подключенных SSE клиентов. Уведомление о настройках пропущено");
                    return;
                }
            }

            EnqueueMessage(json);
            logger.LogInformation("Уведомление об изменении настроек чата поставлено в очередь на отправку");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка подготовки уведомления о настройках чата");
        }
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Освобождение ресурсов сервиса SSE (DisposeAsync)");
        Stop();

        if (_broadcastTask != null && _keepAliveTask != null)
        {
            try
            {
                await Task.WhenAll(_broadcastTask, _keepAliveTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts?.Dispose();
    }

    private void EnqueueMessage(string data)
    {
        if (!_messageChannel.Writer.TryWrite(data))
        {
            logger.LogWarning("Не удалось записать сообщение в канал SSE. Очередь недоступна");
        }
    }

    private async Task KeepAliveLoopAsync(int intervalSeconds, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                EnqueueMessage(": keep-alive");
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Цикл keep-alive остановлен штатно");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Неожиданная ошибка в цикле keep-alive SSE");
        }
    }

    private async Task BroadcastLoopAsync(CancellationToken token)
    {
        try
        {
            await foreach (var data in _messageChannel.Reader.ReadAllAsync(token))
            {
                var isComment = data.StartsWith(':');
                var payload = isComment ? data + "\n\n" : $"data: {data}\n\n";
                var buffer = Encoding.UTF8.GetBytes(payload);

                List<HttpListenerResponse> activeClients;
                lock (_sseClients)
                {
                    if (_sseClients.Count == 0)
                    {
                        continue;
                    }

                    activeClients = _sseClients.ToList();
                }

                var disconnectedClients = new ConcurrentBag<HttpListenerResponse>();

                var writeTasks = activeClients.Select(async client =>
                {
                    try
                    {
                        await client.OutputStream.WriteAsync(buffer, token);
                        await client.OutputStream.FlushAsync(token);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Сбой записи в поток SSE. Клиент помечается на удаление");
                        disconnectedClients.Add(client);
                    }
                });

                await Task.WhenAll(writeTasks);

                if (disconnectedClients.IsEmpty)
                {
                    continue;
                }

                lock (_sseClients)
                {
                    foreach (var client in disconnectedClients)
                    {
                        _sseClients.Remove(client);
                        try
                        {
                            client.Close();
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, "Ошибка при закрытии потока отключенного клиента");
                        }
                    }
                }

                logger.LogInformation("Удалено {DisconnectedCount} отключившихся SSE клиентов. Оставшиеся активные клиенты: {RemainingCount}",
                    disconnectedClients.Count, _sseClients.Count);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Цикл рассылки SSE остановлен штатно");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Критическая ошибка в фоновом цикле рассылки SSE");
        }
    }
}
