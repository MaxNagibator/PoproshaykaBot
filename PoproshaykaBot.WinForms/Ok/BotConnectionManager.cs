using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Broadcast;
using PoproshaykaBot.WinForms.Chat;
using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;
using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace PoproshaykaBot.WinForms;

public sealed class BotConnectionManager : IDisposable
{
    private readonly TwitchClient _twitchClient;
    private readonly TwitchOAuthService _tokenService;
    private readonly SettingsManager _settingsManager;
    private readonly StatisticsCollector _statisticsCollector;
    private readonly ChatDecorationsProvider _chatDecorationsProvider;
    private readonly StreamStatusManager _streamStatusManager;
    private readonly BroadcastScheduler _broadcastScheduler;
    private readonly AudienceTracker _audienceTracker;
    private readonly TwitchChatMessenger _messenger;
    private readonly TwitchChatHandler _twitchChatHandler;
    private readonly ILogger<BotConnectionManager> _logger;

    private CancellationTokenSource? _cts;
    private Task? _connectionTask;
    private bool _disposed;

    public BotConnectionManager(
        TwitchClient twitchClient,
        TwitchOAuthService tokenService,
        SettingsManager settingsManager,
        StatisticsCollector statisticsCollector,
        ChatDecorationsProvider chatDecorationsProvider,
        StreamStatusManager streamStatusManager,
        BroadcastScheduler broadcastScheduler,
        AudienceTracker audienceTracker,
        TwitchChatMessenger messenger,
        TwitchChatHandler twitchChatHandler,
        ILogger<BotConnectionManager> logger)
    {
        _twitchClient = twitchClient;
        _tokenService = tokenService;
        _settingsManager = settingsManager;
        _statisticsCollector = statisticsCollector;
        _chatDecorationsProvider = chatDecorationsProvider;
        _streamStatusManager = streamStatusManager;
        _broadcastScheduler = broadcastScheduler;
        _audienceTracker = audienceTracker;
        _messenger = messenger;
        _twitchChatHandler = twitchChatHandler;
        _logger = logger;

        _streamStatusManager.StreamStatusChanged += UpdateStreamState;
        _streamStatusManager.MonitoringLogMessage += HandleMonitoringLogMessage;
        _streamStatusManager.ErrorOccurred += HandleStreamStatusError;

        _logger.LogDebug("Менеджер подключений бота инициализирован");
    }

    public event EventHandler<BotConnectionResult>? ConnectionCompleted;

    public event EventHandler<string>? ProgressChanged;

    public bool IsBusy => _connectionTask is { IsCompleted: false };

    public void StartConnection()
    {
        _logger.LogDebug("Попытка запуска подключения");

        if (IsBusy)
        {
            _logger.LogWarning("Попытка запуска подключения отклонена: процесс уже выполняется");
            throw new InvalidOperationException("Connection is already in progress");
        }

        _logger.LogInformation("Начат процесс подключения бота");

        _cts?.Dispose();
        _cts = new();

        _connectionTask = ConnectAsync(_cts.Token);
    }

    public void CancelConnection()
    {
        if (_cts == null || _cts.IsCancellationRequested)
        {
            return;
        }

        _logger.LogInformation("Пользователь запросил отмену подключения");
        _cts.Cancel();
    }

    public async Task StopAsync()
    {
        _logger.LogDebug("Инициализация процесса остановки бота (StopAsync)");

        if (_twitchClient.IsConnected)
        {
            var channel = _twitchChatHandler.Channel;

            if (!string.IsNullOrWhiteSpace(channel))
            {
                var messages = new List<string>();
                var collectiveFarewell = _audienceTracker.CreateCollectiveFarewell();

                if (!string.IsNullOrWhiteSpace(collectiveFarewell))
                {
                    _logger.LogDebug("Добавление коллективного прощания для канала {Channel}", channel);
                    messages.Add(collectiveFarewell);
                }

                var settings = _settingsManager.Current.Twitch;

                if (settings.Messages.DisconnectionEnabled
                    && !string.IsNullOrWhiteSpace(settings.Messages.Disconnection))
                {
                    _logger.LogDebug("Добавление стандартного сообщения об отключении для канала {Channel}", channel);
                    messages.Add(settings.Messages.Disconnection);
                }

                if (messages.Count > 0)
                {
                    var finalMessage = string.Join(" ", messages);
                    _logger.LogInformation("Отправка прощальных сообщений в канал {Channel}", channel);
                    _messenger.Send(channel, finalMessage);
                }
            }
        }

        try
        {
            if (_twitchClient.IsConnected)
            {
                _logger.LogInformation("Отключение клиента Twitch");
                _twitchClient.Disconnect();
            }

            _logger.LogDebug("Остановка мониторинга стрима");
            await _streamStatusManager.StopMonitoringAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Произошла ошибка при отключении клиента Twitch или остановке мониторинга");
            ProgressChanged?.Invoke(this, $"Ошибка при отключении: {exception.Message}");
        }

        try
        {
            _logger.LogDebug("Остановка сборщика статистики");
            await _statisticsCollector.StopAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Произошла ошибка при сохранении статистики");
            ProgressChanged?.Invoke(this, $"Ошибка сохранения статистики: {exception.Message}");
        }

        _twitchChatHandler.Reset();
        _logger.LogInformation("Бот успешно остановлен");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Освобождение ресурсов BotConnectionManager (Dispose)");

        _streamStatusManager.StreamStatusChanged -= UpdateStreamState;
        _streamStatusManager.MonitoringLogMessage -= HandleMonitoringLogMessage;
        _streamStatusManager.ErrorOccurred -= HandleStreamStatusError;

        CancelConnection();

        if (_twitchClient.IsConnected)
        {
            _logger.LogWarning("TwitchClient принудительно отключен в Dispose. Рекомендуется вызывать StopAsync перед уничтожением объекта.");
            _twitchClient.Disconnect();
        }

        _cts?.Dispose();
        _disposed = true;
    }

    private void UpdateStreamState(StreamStatus status)
    {
        var settings = _settingsManager.Current.Twitch;
        var channel = _twitchChatHandler.Channel;

        _logger.LogInformation("Статус стрима изменился на {StreamStatus} для канала {Channel}", status, channel);

        if (status == StreamStatus.Online)
        {
            if (settings.AutoBroadcast.AutoBroadcastEnabled && !_broadcastScheduler.IsActive)
            {
                if (!string.IsNullOrWhiteSpace(channel))
                {
                    _logger.LogInformation("Запуск планировщика автоматической рассылки для канала {Channel}", channel);
                    _broadcastScheduler.Start(channel);
                    ReportProgress("🔴 Стрим онлайн. Автоматически запускаю рассылку.");

                    if (settings.AutoBroadcast.StreamStatusNotificationsEnabled
                        && !string.IsNullOrEmpty(settings.AutoBroadcast.StreamStartMessage))
                    {
                        _logger.LogDebug("Отправка уведомления о начале стрима в канал {Channel}", channel);
                        _messenger.Send(channel, settings.AutoBroadcast.StreamStartMessage);
                    }
                }
            }
        }
        else if (status == StreamStatus.Offline)
        {
            if (settings.AutoBroadcast.AutoBroadcastEnabled && _broadcastScheduler.IsActive)
            {
                _logger.LogInformation("Остановка планировщика автоматической рассылки для канала {Channel}", channel);
                _broadcastScheduler.Stop();
                ReportProgress("⚫ Стрим офлайн. Автоматически останавливаю рассылку.");

                if (settings.AutoBroadcast.StreamStatusNotificationsEnabled
                    && !string.IsNullOrEmpty(settings.AutoBroadcast.StreamStopMessage)
                    && !string.IsNullOrWhiteSpace(channel))
                {
                    _logger.LogDebug("Отправка уведомления об окончании стрима в канал {Channel}", channel);
                    _messenger.Send(channel, settings.AutoBroadcast.StreamStopMessage);
                }
            }
        }
    }

    private void HandleMonitoringLogMessage(string msg)
    {
        _logger.LogDebug("Лог мониторинга стрима: {MonitoringMessage}", msg);
        ProgressChanged?.Invoke(this, $"[Monitoring] {msg}");
    }

    private void HandleStreamStatusError(string err)
    {
        _logger.LogError("Произошла ошибка EventSub: {EventSubError}", err);
        ProgressChanged?.Invoke(this, $"Ошибка EventSub: {err}");
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        try
        {
            ReportProgress("Получение токена доступа...");
            _logger.LogDebug("Запрос токена доступа");

            var accessToken = await _tokenService.GetAccessTokenAsync(ct);

            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Не удалось получить токен доступа (токен пуст или null)");
                throw new InvalidOperationException("Не удалось получить токен доступа. Проверьте настройки OAuth.");
            }

            ReportProgress("Инициализация подключения...");
            var settings = _settingsManager.Current.Twitch;

            _logger.LogInformation("Инициализация клиента Twitch для бота {BotUsername} на канале {Channel}", settings.BotUsername, settings.Channel);

            var credentials = new ConnectionCredentials(settings.BotUsername, accessToken);
            _twitchClient.Initialize(credentials, settings.Channel);

            ReportProgress("Подключение к серверу Twitch...");
            _logger.LogDebug("Подключение к IRC-серверу Twitch");
            _twitchClient.Connect();

            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;

            while (!_twitchClient.IsConnected && DateTime.UtcNow - startTime < timeout)
            {
                ct.ThrowIfCancellationRequested();
                ReportProgress("Ожидание подтверждения подключения...");
                _logger.LogDebug("Ожидание подтверждения подключения к Twitch. Прошло: {ElapsedMilliseconds}мс", (DateTime.UtcNow - startTime).TotalMilliseconds);
                await Task.Delay(500, ct);
            }

            if (!_twitchClient.IsConnected)
            {
                _logger.LogError("Превышено время ожидания подключения к Twitch ({TimeoutSeconds}с)", timeout.TotalSeconds);
                throw new TimeoutException("Превышено время ожидания подключения к Twitch");
            }

            ReportProgress("Подключение установлено успешно");
            _logger.LogInformation("Успешное подключение к каналу Twitch {Channel}", settings.Channel);

            ReportProgress("Инициализация статистики...");
            _logger.LogDebug("Запуск сборщика статистики");
            await _statisticsCollector.StartAsync();
            _statisticsCollector.ResetBotStartTime();

            ReportProgress("Загрузка эмодзи и бэйджей...");
            _logger.LogDebug("Загрузка декораций чата (эмодзи и бэйджи)");
            await _chatDecorationsProvider.LoadAsync();

            _logger.LogInformation("Успешно загружено {GlobalEmotesCount} глобальных эмодзи и {GlobalBadgeSetsCount} наборов глобальных бэйджей",
                _chatDecorationsProvider.GlobalEmotesCount,
                _chatDecorationsProvider.GlobalBadgeSetsCount);

            ReportProgress($"Загружено {_chatDecorationsProvider.GlobalEmotesCount} глобальных эмодзи и {_chatDecorationsProvider.GlobalBadgeSetsCount} типов глобальных бэйджей");

            ReportProgress("Инициализация мониторинга стрима...");
            await InitializeStreamMonitoringAsync(settings);

            _logger.LogInformation("Процесс подключения бота успешно завершен");
            ConnectionCompleted?.Invoke(this, BotConnectionResult.Success());
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Процесс подключения бота был отменен");
            ConnectionCompleted?.Invoke(this, BotConnectionResult.Cancelled());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Произошла ошибка в процессе подключения бота");
            ReportProgress($"Ошибка подключения: {exception.Message}");
            ConnectionCompleted?.Invoke(this, BotConnectionResult.Failed(exception));
        }
    }

    private async Task InitializeStreamMonitoringAsync(TwitchSettings settings)
    {
        try
        {
            _logger.LogDebug("Инициализация мониторинга стрима для канала {Channel}", settings.Channel);

            if (string.IsNullOrEmpty(settings.ClientId))
            {
                _logger.LogWarning("Мониторинг стрима недоступен: не настроен Client ID");
                ReportProgress("Client ID не установлен. Мониторинг стрима недоступен.");
                return;
            }

            if (string.IsNullOrEmpty(settings.AccessToken))
            {
                _logger.LogWarning("Мониторинг стрима недоступен: не настроен Access Token");
                ReportProgress("Access Token не установлен. Мониторинг стрима недоступен.");
                return;
            }

            await _streamStatusManager.StartMonitoringAsync(settings.Channel);
            _logger.LogInformation("Мониторинг стрима успешно запущен для канала {Channel}", settings.Channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации мониторинга стрима для канала {Channel}", settings.Channel);
            ReportProgress($"Ошибка инициализации мониторинга стрима: {ex.Message}");
        }
    }

    private void ReportProgress(string message)
    {
        ProgressChanged?.Invoke(this, message);
    }
}

public class BotConnectionResult
{
    private BotConnectionResult(BotConnectionStatus status, Exception? exception = null)
    {
        Status = status;
        Exception = exception;
    }

    public Exception? Exception { get; }

    public bool IsSuccess => Status == BotConnectionStatus.Success;

    public bool IsCancelled => Status == BotConnectionStatus.Cancelled;

    public bool IsFailed => Status == BotConnectionStatus.Failed;

    private BotConnectionStatus Status { get; }

    public static BotConnectionResult Success()
    {
        return new(BotConnectionStatus.Success);
    }

    public static BotConnectionResult Cancelled()
    {
        return new(BotConnectionStatus.Cancelled);
    }

    public static BotConnectionResult Failed(Exception exception)
    {
        return new(BotConnectionStatus.Failed, exception);
    }
}

public enum BotConnectionStatus
{
    Success,
    Cancelled,
    Failed,
}
