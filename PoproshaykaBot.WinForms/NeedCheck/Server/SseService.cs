using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;
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

    private readonly List<HttpResponse> _sseClients = [];

    private readonly Channel<string> _messageChannel = Channel.CreateUnbounded<string>(new()
    {
        SingleReader = true,
    });

    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private Task? _keepAliveTask;
    private volatile bool _isRunning;

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

    public async Task StopAsync()
    {
        logger.LogDebug("Остановка сервиса SSE");

        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;

        await (_cts?.CancelAsync() ?? Task.CompletedTask);

        try
        {
            await Task.WhenAll(_broadcastTask ?? Task.CompletedTask, _keepAliveTask ?? Task.CompletedTask);
        }
        catch (OperationCanceledException)
        {
        }

        lock (_sseClients)
        {
            logger.LogInformation("Отключено {ClientCount} SSE клиентов при остановке сервиса", _sseClients.Count);
            _sseClients.Clear();
        }
    }

    public void RemoveClient(HttpResponse response)
    {
        lock (_sseClients)
        {
            _sseClients.Remove(response);
            logger.LogDebug("SSE клиент удалён по завершению соединения. Активных: {Count}", _sseClients.Count);
        }
    }

    public void AddClient(HttpResponse response)
    {
        logger.LogDebug("Попытка регистрации нового SSE клиента");

        try
        {
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
        await StopAsync();
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

                List<HttpResponse> activeClients;
                lock (_sseClients)
                {
                    if (_sseClients.Count == 0)
                    {
                        continue;
                    }

                    activeClients = _sseClients.ToList();
                }

                var results = await Task.WhenAll(activeClients.Select(async client =>
                {
                    try
                    {
                        await client.Body.WriteAsync(buffer, token);
                        await client.Body.FlushAsync(token);
                        return (client, ok: true);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Сбой записи в поток SSE. Клиент помечается на удаление");
                        return (client, ok: false);
                    }
                }));

                var disconnected = results.Where(r => !r.ok).Select(r => r.client).ToList();

                if (disconnected.Count == 0)
                {
                    continue;
                }

                lock (_sseClients)
                {
                    foreach (var client in disconnected)
                    {
                        _sseClients.Remove(client);
                    }
                }

                logger.LogInformation("Удалено {DisconnectedCount} отключившихся SSE клиентов. Оставшиеся активные клиенты: {RemainingCount}",
                    disconnected.Count, _sseClients.Count);
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
