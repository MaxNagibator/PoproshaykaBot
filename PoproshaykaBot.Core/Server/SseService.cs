using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Server.Obs;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace PoproshaykaBot.Core.Server;

public sealed class SseService : IAsyncDisposable
{
    private readonly SettingsManager _settingsManager;
    private readonly ObsChatStore _obsChatStore;
    private readonly ILogger<SseService> _logger;
    private readonly SseChannelOptions _options;
    private readonly SseClientRegistry _registry;
    private readonly SseDropMetrics _metrics;

    private readonly Channel<SseEnvelope> _messageChannel;
    private readonly object _lifecycleLock = new();
    private readonly object _enqueueLock = new();

    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private Task? _keepAliveTask;
    private bool _isRunning;
    private TimeSpan _clientWriteTimeout = TimeSpan.FromSeconds(10);
    private ObsChatSettings? _chatSettings;

    public SseService(
        SettingsManager settingsManager,
        ObsChatStore obsChatStore,
        ILogger<SseService> logger,
        SseChannelOptions options,
        SseClientRegistry registry,
        SseDropMetrics metrics)
    {
        _settingsManager = settingsManager;
        _obsChatStore = obsChatStore;
        _logger = logger;
        _options = options;
        _registry = registry;
        _metrics = metrics;

        _messageChannel = Channel.CreateBounded<SseEnvelope>(new BoundedChannelOptions(_options.GlobalChannelCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
    }

    public long DroppedMessageCount => _metrics.GlobalDropCount;

    public long ClientDroppedMessageCount => _metrics.ClientDropCount;

    public bool IsRunning
    {
        get
        {
            lock (_lifecycleLock)
            {
                return _isRunning;
            }
        }
    }

    public void Start()
    {
        _logger.LogDebug("Инициализация запуска сервиса SSE");

        lock (_lifecycleLock)
        {
            if (_isRunning)
            {
                _logger.LogWarning("Сервис SSE уже запущен. Повторный запрос на запуск проигнорирован");
                return;
            }

            _cts = new();
            var keepAliveSeconds = Math.Max(5, _settingsManager.Current.Twitch.Infrastructure.SseKeepAliveSeconds);
            var writeTimeoutSeconds = Math.Max(1, _settingsManager.Current.Twitch.Infrastructure.SseClientWriteTimeoutSeconds);
            _clientWriteTimeout = TimeSpan.FromSeconds(writeTimeoutSeconds);

            var token = _cts.Token;
            _keepAliveTask = Task.Run(() => KeepAliveLoopAsync(keepAliveSeconds, token), CancellationToken.None);
            _broadcastTask = Task.Run(() => BroadcastLoopAsync(token), CancellationToken.None);

            _isRunning = true;
            _logger.LogInformation("Сервис SSE успешно запущен. Интервал keep-alive: {KeepAliveSeconds} сек", keepAliveSeconds);
        }
    }

    public async Task StopAsync()
    {
        _logger.LogDebug("Остановка сервиса SSE");

        CancellationTokenSource? cts;
        Task? broadcastTask;
        Task? keepAliveTask;

        lock (_lifecycleLock)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            cts = _cts;
            broadcastTask = _broadcastTask;
            keepAliveTask = _keepAliveTask;
        }

        await (cts?.CancelAsync() ?? Task.CompletedTask);

        try
        {
            await Task.WhenAll(broadcastTask ?? Task.CompletedTask, keepAliveTask ?? Task.CompletedTask);
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }

        var pipelines = _registry.DrainAll();
        _logger.LogInformation("Отключено {ClientCount} SSE клиентов при остановке сервиса", pipelines.Count);

        foreach (var pipeline in pipelines)
        {
            pipeline.Channel.Writer.TryComplete();
        }

        try
        {
            await Task.WhenAll(pipelines.Select(p => p.WriterTask ?? Task.CompletedTask));
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
    }

    public void RemoveClient(HttpResponse response)
    {
        if (!_registry.TryRemove(response, out var pipeline))
        {
            return;
        }

        pipeline.Channel.Writer.TryComplete();
        _logger.LogInformation("SSE клиент {ClientId} удалён по завершению соединения. Активных: {Count}", pipeline.ClientId, _registry.Count);
    }

    public bool AddClient(HttpResponse response)
    {
        _logger.LogDebug("Попытка регистрации нового SSE клиента");

        CancellationToken token;

        lock (_lifecycleLock)
        {
            if (!_isRunning || _cts is null)
            {
                _logger.LogWarning("Попытка зарегистрировать SSE клиента при остановленном сервисе. Подключение отклонено");
                return false;
            }

            token = _cts.Token;
        }

        try
        {
            var pipeline = new SseClientPipeline(response, _options.ClientChannelCapacity);
            var clientCount = _registry.Add(response, pipeline);
            pipeline.WriterTask = Task.Run(() => ClientWriterLoopAsync(pipeline, token), CancellationToken.None);

            _logger.LogInformation("Установлено SSE подключение {ClientId}. Всего активных клиентов: {ClientCount}", pipeline.ClientId, clientCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при установке нового SSE подключения");
            return false;
        }
    }

    public void AddChatMessage(ChatMessageData chatMessage)
    {
        _logger.LogDebug("Подготовка нового сообщения чата для отправки в SSE");

        try
        {
            var settings = _chatSettings ??= _obsChatStore.Load();
            var json = JsonSerializer.Serialize(DtoMapper.ToServerMessage(chatMessage, settings), ServerJsonOptions.Default);
            Enqueue(new("message", json));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сериализации или постановки в очередь сообщения чата");
        }
    }

    public void ClearChat()
    {
        _logger.LogInformation("Инициация события очистки чата для всех SSE клиентов");

        try
        {
            Enqueue(new("clear", "{}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка постановки в очередь события очистки чата");
        }
    }

    public void NotifyChatSettingsChanged(ObsChatSettings settings)
    {
        _chatSettings = null;
        _logger.LogDebug("Подготовка уведомления об изменении настроек чата");

        try
        {
            var cssSettings = ObsChatCssSettings.FromObsChatSettings(settings);
            var json = JsonSerializer.Serialize(cssSettings, ServerJsonOptions.Default);

            Enqueue(new("chat_settings_changed", json));
            _logger.LogInformation("Уведомление об изменении настроек чата поставлено в очередь на отправку");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подготовки уведомления о настройках чата");
        }
    }

    public void NotifyChatSettingsChangedRaw(ObsChatSettings settings)
    {
        _chatSettings = null;
        _logger.LogDebug("Подготовка raw-уведомления об изменении настроек чата");

        try
        {
            var json = JsonSerializer.Serialize(settings, ServerJsonOptions.WithColors);
            Enqueue(new("chat_settings_changed_raw", json));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подготовки raw-уведомления о настройках чата");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Освобождение ресурсов сервиса SSE (DisposeAsync)");
        await StopAsync();
        _cts?.Dispose();
    }

    internal static string BuildPingPayload(int intervalSeconds)
    {
        return JsonSerializer.Serialize(new PingPayload(intervalSeconds), ServerJsonOptions.Default);
    }

    private void Enqueue(SseEnvelope envelope)
    {
        bool willDrop;
        lock (_enqueueLock)
        {
            willDrop = _messageChannel.Reader.Count >= _options.GlobalChannelCapacity;

            if (!_messageChannel.Writer.TryWrite(envelope))
            {
                _logger.LogWarning("Не удалось записать сообщение в канал SSE. Очередь недоступна");
                return;
            }
        }

        if (!willDrop)
        {
            return;
        }

        var total = _metrics.IncrementGlobalDrops();

        if (total == 1 || total % _options.DropLogThrottle == 0)
        {
            _logger.LogWarning("SSE очередь переполнена ({Capacity}), теряем старые сообщения. Всего потерь: {Total}",
                _options.GlobalChannelCapacity,
                total);
        }
    }

    private async Task KeepAliveLoopAsync(int intervalSeconds, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        var pingPayload = BuildPingPayload(intervalSeconds);

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                Enqueue(new("ping", pingPayload));
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Цикл keep-alive остановлен штатно");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Неожиданная ошибка в цикле keep-alive SSE");
        }
    }

    private async Task BroadcastLoopAsync(CancellationToken token)
    {
        long lastNotifiedGlobalDrops = 0;
        long lastNotifiedClientDrops = 0;

        try
        {
            await foreach (var envelope in _messageChannel.Reader.ReadAllAsync(token))
            {
                var currentGlobalDrops = _metrics.GlobalDropCount;
                var currentClientDrops = _metrics.ClientDropCount;

                if (currentGlobalDrops - lastNotifiedGlobalDrops >= _options.DropNotifyThreshold
                    || currentClientDrops - lastNotifiedClientDrops >= _options.DropNotifyThreshold)
                {
                    var droppedJson = JsonSerializer.Serialize(new DroppedNotice(currentGlobalDrops, currentClientDrops),
                        ServerJsonOptions.Default);

                    var droppedBuffer = Encoding.UTF8.GetBytes(SseFormatter.Format(new("dropped", droppedJson)));
                    BroadcastBuffer(droppedBuffer);
                    lastNotifiedGlobalDrops = currentGlobalDrops;
                    lastNotifiedClientDrops = currentClientDrops;
                }

                var buffer = Encoding.UTF8.GetBytes(SseFormatter.Format(envelope));
                BroadcastBuffer(buffer);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Цикл рассылки SSE остановлен штатно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка в фоновом цикле рассылки SSE");
        }
    }

    private void BroadcastBuffer(byte[] buffer)
    {
        var snapshot = _registry.Snapshot();

        if (snapshot.Count == 0)
        {
            return;
        }

        foreach (var channel in snapshot.Select(pipeline => pipeline.Channel))
        {
            var willDrop = channel.Reader.Count >= _options.ClientChannelCapacity;

            if (!channel.Writer.TryWrite(buffer))
            {
                continue;
            }

            if (!willDrop)
            {
                continue;
            }

            var total = _metrics.IncrementClientDrops();

            if (total == 1 || total % _options.DropLogThrottle == 0)
            {
                _logger.LogWarning("Per-client SSE очередь переполнена ({Capacity}). Всего потерь по клиентам: {Total}",
                    _options.ClientChannelCapacity,
                    total);
            }
        }
    }

    private async Task ClientWriterLoopAsync(SseClientPipeline pipeline, CancellationToken token)
    {
        try
        {
            await foreach (var buffer in pipeline.Channel.Reader.ReadAllAsync(token))
            {
                using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                writeCts.CancelAfter(_clientWriteTimeout);

                try
                {
                    await pipeline.Response.Body.WriteAsync(buffer, writeCts.Token);
                    await pipeline.Response.Body.FlushAsync(writeCts.Token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogDebug(ex, "SSE клиент не принял данные за {Timeout} c – отключаю зависшего клиента",
                        _clientWriteTimeout.TotalSeconds);

                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Сбой записи в поток SSE. Клиент будет удалён");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // client cancelled or shutdown
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Неожиданная ошибка в цикле записи SSE-клиента");
        }
        finally
        {
            RemoveClient(pipeline.Response);
        }
    }

    private sealed record PingPayload(int IntervalSeconds);

    private sealed record DroppedNotice(long Count, long ClientCount);
}
