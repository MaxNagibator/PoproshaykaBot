using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.Core.Broadcast;

public sealed class StreamStatusBroadcastHandler :
    IEventHandler<StreamWentOnline>,
    IEventHandler<StreamWentOffline>,
    IEventHandler<BotLifecyclePhaseChanged>,
    IEventSubscriber,
    IDisposable
{
    private readonly IBroadcastScheduler _scheduler;
    private readonly IChatMessenger _messenger;
    private readonly IStreamStatus _streamStatus;
    private readonly ITargetChannelProvider _targetChannelProvider;
    private readonly SettingsManager _settingsManager;
    private readonly ILogger<StreamStatusBroadcastHandler> _logger;
    private readonly IDisposable _onlineSubscription;
    private readonly IDisposable _offlineSubscription;
    private readonly IDisposable _phaseSubscription;

    private bool _botConnected;
    private bool _pendingStartNotification;

    public StreamStatusBroadcastHandler(
        IBroadcastScheduler scheduler,
        IChatMessenger messenger,
        IStreamStatus streamStatus,
        ITargetChannelProvider targetChannelProvider,
        SettingsManager settingsManager,
        IEventBus eventBus,
        ILogger<StreamStatusBroadcastHandler> logger)
    {
        _scheduler = scheduler;
        _messenger = messenger;
        _streamStatus = streamStatus;
        _targetChannelProvider = targetChannelProvider;
        _settingsManager = settingsManager;
        _logger = logger;

        _onlineSubscription = eventBus.Subscribe<StreamWentOnline>(this);
        _offlineSubscription = eventBus.Subscribe<StreamWentOffline>(this);
        _phaseSubscription = eventBus.Subscribe<BotLifecyclePhaseChanged>(this);
    }

    public Task HandleAsync(BotLifecyclePhaseChanged @event, CancellationToken cancellationToken)
    {
        var wasConnected = _botConnected;
        _botConnected = @event.Phase == BotLifecyclePhase.Connected;

        if (wasConnected || !_botConnected)
        {
            return Task.CompletedTask;
        }

        var sendNotification = _pendingStartNotification;
        _pendingStartNotification = false;

        if (_streamStatus.CurrentStatus != StreamStatus.Online)
        {
            return Task.CompletedTask;
        }

        var channel = _targetChannelProvider.Current.Login;

        if (string.IsNullOrWhiteSpace(channel))
        {
            return Task.CompletedTask;
        }

        return StartBroadcast(channel, sendNotification);
    }

    public Task HandleAsync(StreamWentOnline @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Обработка события стрима ONLINE для канала {Channel}", @event.Channel);

        if (!_botConnected)
        {
            if (@event.IsCatchUp)
            {
                _logger.LogDebug("Бот не подключён – рассылка не запускается, уведомление не отправляется");
            }
            else
            {
                _pendingStartNotification = true;
                _logger.LogDebug("Бот не подключён – рассылка не запускается, уведомление отложено до подключения бота");
            }

            return Task.CompletedTask;
        }

        return StartBroadcast(@event.Channel, !@event.IsCatchUp);
    }

    public Task HandleAsync(StreamWentOffline @event, CancellationToken cancellationToken)
    {
        var settings = _settingsManager.Current.Twitch;
        var channel = @event.Channel;

        _logger.LogInformation("Обработка события стрима OFFLINE для канала {Channel}", channel);

        _pendingStartNotification = false;

        if (@event.IsCatchUp)
        {
            _logger.LogDebug("Офлайн-снимок при подключении (catch-up) – рассылка не останавливается и стоп-сообщение не отправляется");
            return Task.CompletedTask;
        }

        if (!_botConnected)
        {
            _logger.LogDebug("Бот не подключён – обработка офлайн пропущена");
            return Task.CompletedTask;
        }

        if (settings.AutoBroadcast.AutoBroadcastEnabled && _scheduler.IsActive)
        {
            _logger.LogInformation("Остановка планировщика автоматической рассылки для канала {Channel}", channel);
            _scheduler.Stop();
            _logger.LogInformation("⚫ Стрим офлайн. Автоматически останавливаю рассылку.");
        }

        if (settings.AutoBroadcast.StreamStatusNotificationsEnabled
            && !string.IsNullOrEmpty(settings.AutoBroadcast.StreamStopMessage))
        {
            _logger.LogDebug("Отправка уведомления об окончании стрима в канал {Channel}", channel);
            _messenger.Send(settings.AutoBroadcast.StreamStopMessage);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _onlineSubscription.Dispose();
        _offlineSubscription.Dispose();
        _phaseSubscription.Dispose();
    }

    private Task StartBroadcast(string channel, bool sendNotification)
    {
        var settings = _settingsManager.Current.Twitch;

        if (settings.AutoBroadcast.AutoBroadcastEnabled && !_scheduler.IsActive)
        {
            _logger.LogInformation("Запуск планировщика автоматической рассылки для канала {Channel}", channel);
            _scheduler.Start(channel);
            _logger.LogInformation("🔴 Стрим онлайн. Автоматически запускаю рассылку.");
        }

        if (sendNotification
            && settings.AutoBroadcast.StreamStatusNotificationsEnabled
            && !string.IsNullOrEmpty(settings.AutoBroadcast.StreamStartMessage))
        {
            _logger.LogDebug("Отправка уведомления о начале стрима в канал {Channel}", channel);
            _messenger.Send(settings.AutoBroadcast.StreamStartMessage);
        }

        return Task.CompletedTask;
    }
}
