using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Models;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace PoproshaykaBot.WinForms;

public class StreamStatusManager : IAsyncDisposable
{
    private const int MaxReconnectAttempts = 5;
    private readonly EventSubWebsocketClient _eventSubClient;
    private readonly TwitchAPI _twitchApi;
    private readonly ILogger<StreamStatusManager> _logger;

    private readonly object _lockObj = new();
    private readonly CancellationTokenSource _disposeCts = new();

    private string? _broadcasterUserId;
    private bool _disposed;
    private bool _isMonitoring;
    private int _reconnectAttempts;
    private bool _stopRequested;
    private CancellationTokenSource? _reconnectCts;

    public StreamStatusManager(
        EventSubWebsocketClient eventSubClient,
        TwitchAPI twitchApi,
        ILogger<StreamStatusManager> logger)
    {
        _eventSubClient = eventSubClient;
        _twitchApi = twitchApi;
        _logger = logger;

        _eventSubClient.WebsocketConnected += OnWebsocketConnected;
        _eventSubClient.WebsocketDisconnected += OnWebsocketDisconnected;
        _eventSubClient.WebsocketReconnected += OnWebsocketReconnected;
        _eventSubClient.ErrorOccurred += OnErrorOccurred;

        _eventSubClient.StreamOnline += OnStreamOnline;
        _eventSubClient.StreamOffline += OnStreamOffline;
    }

    public event Action<string>? ErrorOccurred;
    public event Action<string>? MonitoringLogMessage;
    public event Action<StreamOnlineArgs>? StreamStarted;
    public event Action<StreamStatus>? StreamStatusChanged;
    public event Action<StreamOfflineArgs>? StreamStopped;

    public StreamStatus CurrentStatus { get; private set; } = StreamStatus.Unknown;
    public StreamInfo? CurrentStream { get; private set; }

    public async Task StartMonitoringAsync(string channelName)
    {
        _logger.LogDebug("Попытка запуска мониторинга для канала: {ChannelName}", channelName);

        lock (_lockObj)
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Мониторинг для канала {ChannelName} уже запущен или находится в процессе запуска", channelName);
                return;
            }

            _isMonitoring = true;
            _stopRequested = false;
        }

        if (string.IsNullOrWhiteSpace(channelName))
        {
            _logger.LogWarning("Не удалось запустить мониторинг: имя канала пустое");
            ErrorOccurred?.Invoke("Имя канала не может быть пустым");
            SetMonitoringStopped();
            return;
        }

        var userId = await GetBroadcasterUserIdAsync(channelName);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Не удалось запустить мониторинг: не удалось получить ID пользователя для канала {ChannelName}", channelName);
            SetMonitoringStopped();
            return;
        }

        _broadcasterUserId = userId;
        _reconnectAttempts = 0;

        try
        {
            await RefreshCurrentStatusAsync();

            MonitoringLogMessage?.Invoke("Подключение к EventSub WebSocket...");
            _logger.LogDebug("Подключение к EventSub WebSocket для BroadcasterId: {BroadcasterId}", _broadcasterUserId);

            var connected = await _eventSubClient.ConnectAsync();

            if (!connected)
            {
                var errorMessage = "Не удалось подключиться к EventSub WebSocket";
                _logger.LogError("Ошибка подключения к EventSub WebSocket для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
                ErrorOccurred?.Invoke(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogInformation("Соединение EventSub инициировано для BroadcasterId: {BroadcasterId}. Ожидание подтверждения...", _broadcasterUserId);
            MonitoringLogMessage?.Invoke("Ожидание подтверждения подключения...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при запуске мониторинга для канала {ChannelName}", channelName);
            ErrorOccurred?.Invoke($"Ошибка запуска мониторинга: {ex.Message}");
            SetMonitoringStopped();
            throw;
        }
    }

    public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            _logger.LogDebug("Вызван StopMonitoringAsync, но объект уже уничтожен (Disposed)");
            return;
        }

        _logger.LogDebug("Остановка мониторинга для BroadcasterId: {BroadcasterId}", _broadcasterUserId);

        try
        {
            lock (_lockObj)
            {
                _stopRequested = true;
                CancelAndResetReconnectToken();
            }

            MonitoringLogMessage?.Invoke("Отключение от EventSub WebSocket...");

            if (cancellationToken == CancellationToken.None)
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _eventSubClient.DisconnectAsync(timeoutCts.Token);
            }
            else
            {
                await _eventSubClient.DisconnectAsync(cancellationToken);
            }

            CurrentStatus = StreamStatus.Unknown;
            SetMonitoringStopped();

            _logger.LogInformation("Мониторинг стрима успешно остановлен для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
            MonitoringLogMessage?.Invoke("Мониторинг стрима остановлен");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при остановке мониторинга для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
            ErrorOccurred?.Invoke($"Ошибка остановки мониторинга: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Уничтожение ресурсов StreamStatusManager");
        _disposed = true;

        lock (_lockObj)
        {
            _stopRequested = true;
            CancelAndResetReconnectToken();
        }

        await _disposeCts.CancelAsync();
        _disposeCts.Dispose();

        await StopMonitoringAsync();

        _eventSubClient.WebsocketConnected -= OnWebsocketConnected;
        _eventSubClient.WebsocketDisconnected -= OnWebsocketDisconnected;
        _eventSubClient.WebsocketReconnected -= OnWebsocketReconnected;
        _eventSubClient.ErrorOccurred -= OnErrorOccurred;

        _eventSubClient.StreamOnline -= OnStreamOnline;
        _eventSubClient.StreamOffline -= OnStreamOffline;

        GC.SuppressFinalize(this);
    }

    public async Task RefreshCurrentStatusAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_broadcasterUserId))
            {
                _logger.LogDebug("Обновление статуса пропущено: BroadcasterUserId пуст");
                return;
            }

            _logger.LogDebug("Запрос текущего статуса стрима через API для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
            var response = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: [_broadcasterUserId]);

            var isOnline = response?.Streams is { Length: > 0 };
            var newStatus = isOnline ? StreamStatus.Online : StreamStatus.Offline;

            if (CurrentStatus == StreamStatus.Online && newStatus == StreamStatus.Offline)
            {
                _logger.LogWarning("Обнаружена задержка Twitch API для BroadcasterId {BroadcasterId}: локальный статус Online, но API вернул Offline", _broadcasterUserId);
            }
            else if (CurrentStatus != newStatus)
            {
                _logger.LogInformation("Статус стрима изменился с {OldStatus} на {NewStatus} для BroadcasterId {BroadcasterId}", CurrentStatus, newStatus, _broadcasterUserId);
                CurrentStatus = newStatus;
                StreamStatusChanged?.Invoke(CurrentStatus);
            }

            if (isOnline)
            {
                var stream = response!.Streams[0];

                CurrentStream = new()
                {
                    Id = stream.Id,
                    UserId = stream.UserId,
                    UserLogin = stream.UserLogin,
                    UserName = stream.UserName,
                    GameId = stream.GameId,
                    GameName = stream.GameName,
                    Title = stream.Title,
                    Language = stream.Language,
                    ViewerCount = stream.ViewerCount,
                    StartedAt = stream.StartedAt,
                    ThumbnailUrl = stream.ThumbnailUrl,
                    Tags = stream.Tags ?? [],
                    IsMature = stream.IsMature,
                };

                _logger.LogDebug("Метаданные стрима обновлены для BroadcasterId {BroadcasterId}. StreamId: {StreamId}, Игра: {GameName}", _broadcasterUserId, stream.Id, stream.GameName);
            }
            else
            {
                CurrentStream = null;
            }

            MonitoringLogMessage?.Invoke(isOnline
                ? "Текущий статус: онлайн (по данным API)"
                : "Текущий статус: офлайн (по данным API)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось обновить текущий статус стрима для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
            ErrorOccurred?.Invoke($"Ошибка получения текущего статуса стрима: {ex.Message}");
        }
    }

    private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
    {
        _reconnectAttempts = 0;
        _logger.LogInformation("EventSub WebSocket подключен. SessionId: {SessionId}, IsRequestedReconnect: {IsRequestedReconnect}", _eventSubClient.SessionId, e.IsRequestedReconnect);
        MonitoringLogMessage?.Invoke($"EventSub WebSocket подключен (Session: {_eventSubClient.SessionId})");

        if (!e.IsRequestedReconnect && !string.IsNullOrEmpty(_broadcasterUserId))
        {
            await CreateEventSubSubscriptions();
        }
    }

    private async Task OnWebsocketDisconnected(object sender, EventArgs e)
    {
        _logger.LogWarning("EventSub WebSocket отключен. Disposed: {IsDisposed}, StopRequested: {IsStopRequested}", _disposed, _stopRequested);
        MonitoringLogMessage?.Invoke("EventSub WebSocket отключен");
        CurrentStatus = StreamStatus.Unknown;

        if (_disposed || _stopRequested)
        {
            return;
        }

        if (_reconnectAttempts < MaxReconnectAttempts)
        {
            _reconnectAttempts++;
            var delayMs = (int)(1000 * Math.Pow(2, _reconnectAttempts - 1));

            _logger.LogWarning("Попытка переподключения EventSub {Attempt}/{MaxAttempts} через {DelayMs}мс...", _reconnectAttempts, MaxReconnectAttempts, delayMs);
            MonitoringLogMessage?.Invoke($"Попытка переподключения {_reconnectAttempts}/{MaxReconnectAttempts} через {delayMs / 1000} сек...");

            CancellationToken token;
            lock (_lockObj)
            {
                CancelAndResetReconnectToken();
                _reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
                token = _reconnectCts.Token;
            }

            try
            {
                await Task.Delay(delayMs, token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Ожидание переподключения отменено для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
                return;
            }

            try
            {
                var success = await _eventSubClient.ReconnectAsync();

                if (success)
                {
                    _reconnectAttempts = 0;
                    _logger.LogInformation("Успешное переподключение EventSub WebSocket (попытка {Attempt})", _reconnectAttempts);
                    MonitoringLogMessage?.Invoke("Переподключение успешно");
                }
                else
                {
                    _logger.LogWarning("Не удалось переподключиться к EventSub WebSocket (попытка {Attempt}/{MaxAttempts})", _reconnectAttempts, MaxReconnectAttempts);
                    ErrorOccurred?.Invoke($"Не удалось переподключиться (попытка {_reconnectAttempts}/{MaxReconnectAttempts})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Исключение при попытке переподключения {Attempt}/{MaxAttempts}", _reconnectAttempts, MaxReconnectAttempts);
                ErrorOccurred?.Invoke($"Ошибка переподключения (попытка {_reconnectAttempts}/{MaxReconnectAttempts}): {ex.Message}");
            }
        }
        else
        {
            _logger.LogError("Превышено максимальное количество попыток переподключения ({MaxAttempts}) для EventSub WebSocket. Остановка.", MaxReconnectAttempts);
            ErrorOccurred?.Invoke($"Превышено максимальное количество попыток переподключения ({MaxReconnectAttempts}). Мониторинг стрима остановлен.");

            lock (_lockObj)
            {
                _stopRequested = true;
            }

            SetMonitoringStopped();
        }
    }

    private Task OnWebsocketReconnected(object sender, EventArgs e)
    {
        if (!_stopRequested)
        {
            _logger.LogInformation("EventSub WebSocket неявно переподключен. SessionId: {SessionId}", _eventSubClient.SessionId);
            MonitoringLogMessage?.Invoke($"EventSub WebSocket переподключен (Session: {_eventSubClient.SessionId})");
        }

        return Task.CompletedTask;
    }

    private Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
    {
        _logger.LogError("Внутренняя ошибка EventSub WebSocket: {ErrorMessage}", e.Message);
        ErrorOccurred?.Invoke($"Ошибка EventSub WebSocket: {e.Message}");
        return Task.CompletedTask;
    }

    private async Task OnStreamOnline(object sender, StreamOnlineArgs e)
    {
        _logger.LogInformation("Событие стрима ONLINE получено через EventSub для BroadcasterId: {BroadcasterId}, Тип: {EventType}", _broadcasterUserId, e.Notification.Payload.Event.Type);
        MonitoringLogMessage?.Invoke($"🔴 Стрим запущен (EventSub): {e.Notification.Payload.Event.Type}");

        if (CurrentStatus != StreamStatus.Online)
        {
            CurrentStatus = StreamStatus.Online;
            StreamStatusChanged?.Invoke(CurrentStatus);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Запуск опроса метаданных стрима для BroadcasterId: {BroadcasterId}", _broadcasterUserId);

                for (var i = 0; i < 6; i++)
                {
                    _disposeCts.Token.ThrowIfCancellationRequested();

                    await RefreshCurrentStatusAsync();

                    if (CurrentStream != null)
                    {
                        _logger.LogInformation("Метаданные стрима успешно получены из API для BroadcasterId: {BroadcasterId} на попытке {Attempt}", _broadcasterUserId, i + 1);
                        MonitoringLogMessage?.Invoke("Метаданные стрима успешно получены из API");
                        StreamStatusChanged?.Invoke(CurrentStatus);
                        break;
                    }

                    if (CurrentStatus != StreamStatus.Online)
                    {
                        _logger.LogDebug("Стрим больше не онлайн, прерывание опроса метаданных для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
                        break;
                    }

                    var delaySeconds = 5 * (i + 1);
                    _logger.LogWarning("Метаданные еще не доступны в API. Повтор через {DelaySeconds}с (Попытка {Attempt}/6)...", delaySeconds, i + 1);
                    MonitoringLogMessage?.Invoke($"Метаданные еще не доступны в API. Повтор через {delaySeconds} сек (попытка {i + 1}/6)...");

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _disposeCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Опрос метаданных стрима отменен из-за завершения работы менеджера");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Непредвиденная ошибка во время фонового опроса метаданных стрима");
            }
        }, _disposeCts.Token);

        StreamStarted?.Invoke(e);
    }

    private Task OnStreamOffline(object sender, StreamOfflineArgs e)
    {
        _logger.LogInformation("Событие стрима OFFLINE получено через EventSub для BroadcasterId: {BroadcasterId}", _broadcasterUserId);

        var oldStatus = CurrentStatus;
        CurrentStatus = StreamStatus.Offline;
        MonitoringLogMessage?.Invoke("⚫ Стрим завершен (EventSub)");
        CurrentStream = null;

        if (oldStatus != CurrentStatus)
        {
            StreamStatusChanged?.Invoke(CurrentStatus);
        }

        StreamStopped?.Invoke(e);
        return Task.CompletedTask;
    }

    private async Task<string?> GetBroadcasterUserIdAsync(string channelName)
    {
        _logger.LogDebug("Получение Broadcaster User ID для канала: {ChannelName}", channelName);

        if (string.IsNullOrEmpty(channelName))
        {
            _logger.LogWarning("Невозможно получить User ID: имя канала пустое");
            ErrorOccurred?.Invoke("Имя канала не может быть пустым");
            return null;
        }

        try
        {
            MonitoringLogMessage?.Invoke($"Получение ID пользователя для канала: {channelName}");

            var users = await _twitchApi.Helix.Users.GetUsersAsync(logins: [channelName]);

            if (users?.Users == null || users.Users.Length == 0)
            {
                _logger.LogWarning("Пользователь не найден для канала: {ChannelName}", channelName);
                ErrorOccurred?.Invoke($"Пользователь с именем '{channelName}' не найден");
                return null;
            }

            var userId = users.Users.First().Id;
            _logger.LogInformation("Канал {ChannelName} успешно разрешен в User ID {UserId}", channelName, userId);
            MonitoringLogMessage?.Invoke($"ID пользователя получен: {userId}");

            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении Broadcaster User ID для канала: {ChannelName}", channelName);
            ErrorOccurred?.Invoke($"Ошибка получения ID пользователя: {ex.Message}");
            return null;
        }
    }

    private async Task CreateEventSubSubscriptions()
    {
        if (string.IsNullOrEmpty(_broadcasterUserId))
        {
            _logger.LogWarning("Невозможно создать подписки EventSub: BroadcasterUserId пуст");
            return;
        }

        _logger.LogDebug("Создание подписок EventSub для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
        MonitoringLogMessage?.Invoke("Создание подписок EventSub...");

        var subscriptionsCreated = 0;

        try
        {
            var onlineCondition = new Dictionary<string, string>
            {
                { "broadcaster_user_id", _broadcasterUserId },
            };

            MonitoringLogMessage?.Invoke("Создание подписки stream.online...");

            var response = await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync("stream.online",
                "1",
                onlineCondition,
                EventSubTransportMethod.Websocket,
                _eventSubClient.SessionId);

            subscriptionsCreated++;
            var subscriptionId = response.Subscriptions?.FirstOrDefault()?.Id;

            _logger.LogInformation("Успешно создана подписка 'stream.online'. SubscriptionId: {SubscriptionId}, SessionId: {SessionId}", subscriptionId, _eventSubClient.SessionId);
            MonitoringLogMessage?.Invoke($"Подписка на stream.online создана (ID: {subscriptionId})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании подписки 'stream.online' для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
            ErrorOccurred?.Invoke($"Ошибка создания подписки stream.online: {ex.Message}");
        }

        try
        {
            var offlineCondition = new Dictionary<string, string>
            {
                { "broadcaster_user_id", _broadcasterUserId },
            };

            MonitoringLogMessage?.Invoke("Создание подписки stream.offline...");

            var response = await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync("stream.offline",
                "1",
                offlineCondition,
                EventSubTransportMethod.Websocket,
                _eventSubClient.SessionId);

            subscriptionsCreated++;
            var subscriptionId = response.Subscriptions?.FirstOrDefault()?.Id;

            _logger.LogInformation("Успешно создана подписка 'stream.offline'. SubscriptionId: {SubscriptionId}, SessionId: {SessionId}", subscriptionId, _eventSubClient.SessionId);
            MonitoringLogMessage?.Invoke($"Подписка на stream.offline создана (ID: {subscriptionId})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании подписки 'stream.offline' для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
            ErrorOccurred?.Invoke($"Ошибка создания подписки stream.offline: {ex.Message}");
        }

        if (subscriptionsCreated == 2)
        {
            _logger.LogInformation("Все необходимые подписки EventSub успешно созданы для BroadcasterId: {BroadcasterId}", _broadcasterUserId);
            MonitoringLogMessage?.Invoke("Все подписки EventSub созданы успешно");
        }
        else
        {
            _logger.LogWarning("Успешно создано только {CreatedCount}/2 подписок EventSub для BroadcasterId: {BroadcasterId}", subscriptionsCreated, _broadcasterUserId);
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

    private void SetMonitoringStopped()
    {
        lock (_lockObj)
        {
            _isMonitoring = false;
        }
    }
}
