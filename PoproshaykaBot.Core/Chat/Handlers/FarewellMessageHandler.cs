using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Core.Chat.Handlers;

public sealed class FarewellMessageHandler : IEventHandler<BotLifecyclePhaseChanged>, IEventSubscriber, IDisposable
{
    private readonly IChannelProvider _channelProvider;
    private readonly AudienceTracker _audienceTracker;
    private readonly IChatMessenger _messenger;
    private readonly SettingsManager _settingsManager;
    private readonly ILogger<FarewellMessageHandler> _logger;
    private readonly IDisposable _subscription;

    public FarewellMessageHandler(
        IChannelProvider channelProvider,
        AudienceTracker audienceTracker,
        IChatMessenger messenger,
        SettingsManager settingsManager,
        IEventBus eventBus,
        ILogger<FarewellMessageHandler> logger)
    {
        _channelProvider = channelProvider;
        _audienceTracker = audienceTracker;
        _messenger = messenger;
        _settingsManager = settingsManager;
        _logger = logger;
        _subscription = eventBus.Subscribe(this);
    }

    public Task HandleAsync(BotLifecyclePhaseChanged @event, CancellationToken cancellationToken)
    {
        if (@event.Phase != BotLifecyclePhase.Disconnecting)
        {
            return Task.CompletedTask;
        }

        var channel = _channelProvider.Channel;

        if (string.IsNullOrWhiteSpace(channel))
        {
            _logger.LogDebug("Прощальные сообщения пропущены: бот не заходил в канал в этой сессии");
            return Task.CompletedTask;
        }

        var messages = new List<string>();

        var messageSettings = _settingsManager.Current.Twitch.Messages;
        var activeUsers = _audienceTracker.ActiveUserCount;

        if (!messageSettings.FarewellEnabled)
        {
            _logger.LogInformation("Коллективное прощание выключено настройкой Messages.FarewellEnabled");
        }
        else if (activeUsers == 0)
        {
            _logger.LogInformation("Коллективное прощание пропущено: AudienceTracker пуст – ни одного нефильтрованного сообщения за сессию");
        }
        else
        {
            var collectiveFarewell = _audienceTracker.CreateCollectiveFarewell();

            if (!string.IsNullOrWhiteSpace(collectiveFarewell))
            {
                messages.Add(collectiveFarewell);
                _logger.LogInformation("Сформировано коллективное прощание для {Count} зрителей", activeUsers);
            }
        }

        if (messageSettings.DisconnectionEnabled
            && !string.IsNullOrWhiteSpace(messageSettings.Disconnection))
        {
            messages.Add(messageSettings.Disconnection);
        }

        if (messages.Count == 0)
        {
            _logger.LogDebug("На фазе Disconnecting не сформировано ни одного прощального сообщения");
            return Task.CompletedTask;
        }

        var text = string.Join(" ", messages);

        try
        {
            _messenger.Send(text);
            _logger.LogInformation("Прощальное сообщение поставлено в очередь ChatSender ({Length} симв.)", text.Length);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось поставить прощальное сообщение в очередь ChatSender");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
