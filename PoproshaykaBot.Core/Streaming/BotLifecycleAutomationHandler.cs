using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Core.Streaming;

public sealed class BotLifecycleAutomationHandler :
    IEventHandler<StreamWentOnline>,
    IEventHandler<StreamWentOffline>,
    IEventHandler<BotLifecyclePhaseChanged>,
    IEventSubscriber,
    IDisposable
{
    private readonly IBotConnectionController _connectionManager;
    private readonly SettingsManager _settingsManager;
    private readonly IEventBus _eventBus;
    private readonly ILogger<BotLifecycleAutomationHandler> _logger;
    private readonly IDisposable _onlineSubscription;
    private readonly IDisposable _offlineSubscription;
    private readonly IDisposable _phaseSubscription;

    private BotLifecyclePhase _phase = BotLifecyclePhase.Idle;
    private volatile bool _isStreamOnline;

    public BotLifecycleAutomationHandler(
        IBotConnectionController connectionManager,
        SettingsManager settingsManager,
        IEventBus eventBus,
        ILogger<BotLifecycleAutomationHandler> logger)
    {
        _connectionManager = connectionManager;
        _settingsManager = settingsManager;
        _eventBus = eventBus;
        _logger = logger;

        _onlineSubscription = eventBus.Subscribe<StreamWentOnline>(this);
        _offlineSubscription = eventBus.Subscribe<StreamWentOffline>(this);
        _phaseSubscription = eventBus.Subscribe<BotLifecyclePhaseChanged>(this);
    }

    public Task HandleAsync(BotLifecyclePhaseChanged @event, CancellationToken cancellationToken)
    {
        _phase = @event.Phase;
        return Task.CompletedTask;
    }

    public Task HandleAsync(StreamWentOnline @event, CancellationToken cancellationToken)
    {
        _isStreamOnline = true;

        var settings = _settingsManager.Current.Twitch.BotLifecycleAutomation;

        if (!settings.AutoConnectOnStreamOnline)
        {
            return Task.CompletedTask;
        }

        if (!IsDisconnectedPhase(_phase))
        {
            _logger.LogDebug("Авто-подключение пропущено: текущая фаза {Phase}", _phase);
            return Task.CompletedTask;
        }

        if (_connectionManager.IsBusy)
        {
            _logger.LogDebug("Авто-подключение пропущено: BotConnectionManager занят");
            return Task.CompletedTask;
        }

        try
        {
            _logger.LogInformation("🔴 Стрим онлайн ({Channel}) – автоматически подключаю бота", @event.Channel);
            _connectionManager.StartConnection();
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Не удалось запустить авто-подключение бота");
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(StreamWentOffline @event, CancellationToken cancellationToken)
    {
        _isStreamOnline = false;

        var settings = _settingsManager.Current.Twitch.BotLifecycleAutomation;

        if (!settings.AutoDisconnectOnStreamOffline)
        {
            return Task.CompletedTask;
        }

        if (@event.IsCatchUp)
        {
            _logger.LogDebug("Авто-отключение пропущено: офлайн-снимок при подключении бота (catch-up), а не настоящий переход стрима в офлайн");
            return Task.CompletedTask;
        }

        if (_phase != BotLifecyclePhase.Connected)
        {
            _logger.LogDebug("Авто-отключение пропущено: текущая фаза {Phase}", _phase);
            return Task.CompletedTask;
        }

        _logger.LogInformation("⚫ Стрим офлайн ({Channel}) – автоматически отключаю бота", @event.Channel);

        _eventBus.ContinueAfterPublish(StopBotAsync);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _onlineSubscription.Dispose();
        _offlineSubscription.Dispose();
        _phaseSubscription.Dispose();
    }

    private async Task StopBotAsync()
    {
        if (_isStreamOnline)
        {
            _logger.LogInformation("Авто-отключение отменено: стрим снова онлайн");
            return;
        }

        try
        {
            await _connectionManager.StopAsync(BotStopMode.Graceful);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка авто-отключения бота");
        }
    }

    private static bool IsDisconnectedPhase(BotLifecyclePhase phase)
    {
        return phase is BotLifecyclePhase.Idle
            or BotLifecyclePhase.Disconnected
            or BotLifecyclePhase.Cancelled
            or BotLifecyclePhase.Failed;
    }
}
