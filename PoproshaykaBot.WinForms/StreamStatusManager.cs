using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;
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
    private readonly SettingsManager _settingsManager;
    private readonly TwitchOAuthService _oauthService;
    private string? _broadcasterUserId;
    private bool _disposed;
    private bool _isInitialized;
    private int _reconnectAttempts;
    private bool _stopRequested;
    private CancellationTokenSource? _reconnectCts;

    public StreamStatusManager(
        EventSubWebsocketClient eventSubClient,
        TwitchAPI twitchApi,
        SettingsManager settingsManager,
        TwitchOAuthService oauthService)
    {
        _eventSubClient = eventSubClient;
        _twitchApi = twitchApi;
        _settingsManager = settingsManager;
        _oauthService = oauthService;

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

    public Task InitializeAsync(string clientId, string accessToken)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            throw new ArgumentException("Client ID не может быть пустым", nameof(clientId));
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new ArgumentException("Access Token не может быть пустым", nameof(accessToken));
        }

        _broadcasterUserId = null;

        _twitchApi.Settings.ClientId = clientId;
        _twitchApi.Settings.AccessToken = accessToken;

        _stopRequested = false;
        _isInitialized = true;
        CurrentStatus = StreamStatus.Unknown;
        CurrentStream = null;
        return Task.CompletedTask;
    }

    public async Task StartMonitoringAsync(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            ErrorOccurred?.Invoke("Имя канала не может быть пустым");
            return;
        }

        if (!await EnsureAuthenticatedAsync())
        {
            return;
        }

        var userId = await GetBroadcasterUserIdAsync(channelName);

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        _broadcasterUserId = userId;
        _reconnectAttempts = 0;
        _stopRequested = false;

        try
        {
            await RefreshCurrentStatusAsync();

            MonitoringLogMessage?.Invoke("Подключение к EventSub WebSocket...");

            var connected = await _eventSubClient.ConnectAsync();

            if (!connected)
            {
                var errorMessage = "Не удалось подключиться к EventSub WebSocket";
                ErrorOccurred?.Invoke(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            MonitoringLogMessage?.Invoke("Ожидание подтверждения подключения...");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Ошибка запуска мониторинга: {ex.Message}");
            throw;
        }
    }

    public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _stopRequested = true;
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = null;
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
            MonitoringLogMessage?.Invoke("Мониторинг стрима остановлен");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Ошибка остановки мониторинга: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopRequested = true;
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;

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
                return;
            }

            if (!await EnsureAuthenticatedAsync())
            {
                return;
            }

            var response = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: [_broadcasterUserId]);
            var isOnline = response?.Streams != null && response.Streams.Length > 0;
            var newStatus = isOnline ? StreamStatus.Online : StreamStatus.Offline;

            if (CurrentStatus == StreamStatus.Online && newStatus == StreamStatus.Offline)
            {
                // API тормозит
            }
            else if (CurrentStatus != newStatus)
            {
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
            }
            else
            {
                CurrentStream = null;
            }

            MonitoringLogMessage?.Invoke(isOnline
                ? "Текущий статус: онлайн (по данным API)"
                : "Текущий статус: офлайн (по данным API)");
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke($"Ошибка получения текущего статуса стрима: {exception.Message}");
        }
    }

    private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
    {
        _reconnectAttempts = 0;
        MonitoringLogMessage?.Invoke($"EventSub WebSocket подключен (Session: {_eventSubClient.SessionId})");

        if (!e.IsRequestedReconnect && !string.IsNullOrEmpty(_broadcasterUserId))
        {
            await CreateEventSubSubscriptions();
        }
    }

    private async Task OnWebsocketDisconnected(object sender, EventArgs e)
    {
        MonitoringLogMessage?.Invoke("EventSub WebSocket отключен");
        CurrentStatus = StreamStatus.Unknown;

        if (_disposed || _stopRequested)
        {
            return;
        }

        if (_reconnectAttempts < MaxReconnectAttempts)
        {
            _reconnectAttempts++;
            var delay = 1000 * Math.Pow(2, _reconnectAttempts - 1);

            MonitoringLogMessage?.Invoke($"Попытка переподключения {_reconnectAttempts}/{MaxReconnectAttempts} через {delay / 1000:F0} сек...");
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = new();

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delay), _reconnectCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                var success = await _eventSubClient.ReconnectAsync();

                if (success)
                {
                    _reconnectAttempts = 0;
                    MonitoringLogMessage?.Invoke("Переподключение успешно");
                }
                else
                {
                    ErrorOccurred?.Invoke($"Не удалось переподключиться (попытка {_reconnectAttempts}/{MaxReconnectAttempts})");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Ошибка переподключения (попытка {_reconnectAttempts}/{MaxReconnectAttempts}): {ex.Message}");
            }
        }
        else if (_reconnectAttempts >= MaxReconnectAttempts)
        {
            ErrorOccurred?.Invoke($"Превышено максимальное количество попыток переподключения ({MaxReconnectAttempts}). Мониторинг стрима остановлен.");
            _stopRequested = true;
        }
    }

    private Task OnWebsocketReconnected(object sender, EventArgs e)
    {
        if (!_stopRequested)
        {
            MonitoringLogMessage?.Invoke($"EventSub WebSocket переподключен (Session: {_eventSubClient.SessionId})");
        }

        return Task.CompletedTask;
    }

    private Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
    {
        ErrorOccurred?.Invoke($"Ошибка EventSub WebSocket: {e.Message}");
        return Task.CompletedTask;
    }

    private async Task OnStreamOnline(object sender, StreamOnlineArgs e)
    {
        MonitoringLogMessage?.Invoke($"🔴 Стрим запущен (EventSub): {e.Notification.Payload.Event.Type}");

        if (CurrentStatus != StreamStatus.Online)
        {
            CurrentStatus = StreamStatus.Online;
            StreamStatusChanged?.Invoke(CurrentStatus);
        }

        _ = Task.Run(async () =>
        {
            for (var i = 0; i < 6; i++)
            {
                await RefreshCurrentStatusAsync();

                if (CurrentStream != null)
                {
                    MonitoringLogMessage?.Invoke("Метаданные стрима успешно получены из API");
                    StreamStatusChanged?.Invoke(CurrentStatus);
                    break;
                }

                if (CurrentStatus != StreamStatus.Online)
                {
                    break;
                }

                var delaySeconds = 5 * (i + 1);
                MonitoringLogMessage?.Invoke($"Метаданные еще не доступны в API. Повтор через {delaySeconds} сек (попытка {i + 1}/6)...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        });

        StreamStarted?.Invoke(e);
    }

    private Task OnStreamOffline(object sender, StreamOfflineArgs e)
    {
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

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        var settings = _settingsManager.Current.Twitch;

        if (string.IsNullOrEmpty(settings.ClientId))
        {
            ErrorOccurred?.Invoke("Client ID не настроен в настройках.");
            return false;
        }

        var token = await _oauthService.GetValidTokenOrRefreshAsync();
        if (string.IsNullOrEmpty(token))
        {
            ErrorOccurred?.Invoke("Токен доступа отсутствует или недействителен. Пожалуйста, авторизуйтесь в настройках.");
            return false;
        }

        _twitchApi.Settings.ClientId = settings.ClientId;
        _twitchApi.Settings.AccessToken = token;
        return true;
    }

    private async Task<string?> GetBroadcasterUserIdAsync(string channelName)
    {
        if (string.IsNullOrEmpty(channelName))
        {
            ErrorOccurred?.Invoke("Имя канала не может быть пустым");
            return null;
        }

        if (!await EnsureAuthenticatedAsync())
        {
            return null;
        }

        try
        {
            MonitoringLogMessage?.Invoke($"Получение ID пользователя для канала: {channelName}");

            var users = await _twitchApi.Helix.Users.GetUsersAsync(logins: [channelName]);

            if (users?.Users == null || users.Users.Length == 0)
            {
                ErrorOccurred?.Invoke($"Пользователь с именем '{channelName}' не найден");
                return null;
            }

            var userId = users.Users.First().Id;
            MonitoringLogMessage?.Invoke($"ID пользователя получен: {userId}");
            return userId;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Ошибка получения ID пользователя: {ex.Message}");
            return null;
        }
    }

    private async Task CreateEventSubSubscriptions()
    {
        if (string.IsNullOrEmpty(_broadcasterUserId))
        {
            return;
        }

        if (!await EnsureAuthenticatedAsync())
        {
            return;
        }

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
            MonitoringLogMessage?.Invoke($"Подписка на stream.online создана (ID: {response.Subscriptions?.FirstOrDefault()?.Id})");
        }
        catch (Exception ex)
        {
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
            MonitoringLogMessage?.Invoke($"Подписка на stream.offline создана (ID: {response.Subscriptions?.FirstOrDefault()?.Id})");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Ошибка создания подписки stream.offline: {ex.Message}");
        }

        if (subscriptionsCreated == 2)
        {
            MonitoringLogMessage?.Invoke("Все подписки EventSub созданы успешно");
        }
    }
}
