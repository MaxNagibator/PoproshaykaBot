using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Chat;
using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Chat.Handlers;

public sealed class StatisticsTrackingHandler : IEventHandler<ChatMessageReceived>, IEventSubscriber, IDisposable
{
    private readonly IUserStatisticsRepository _userStatistics;
    private readonly IBotStatisticsRepository _botStatistics;
    private readonly ITargetChannelProvider _targetChannelProvider;
    private readonly IDisposable _subscription;

    public StatisticsTrackingHandler(
        IUserStatisticsRepository userStatistics,
        IBotStatisticsRepository botStatistics,
        ITargetChannelProvider targetChannelProvider,
        IEventBus eventBus)
    {
        _userStatistics = userStatistics;
        _botStatistics = botStatistics;
        _targetChannelProvider = targetChannelProvider;
        _subscription = eventBus.Subscribe(this);
    }

    public Task HandleAsync(ChatMessageReceived @event, CancellationToken cancellationToken)
    {
        if (@event.IsBot || !_targetChannelProvider.Current.RecordsUserData)
        {
            return Task.CompletedTask;
        }

        _userStatistics.TrackMessage(@event.UserId, @event.Username);
        _botStatistics.IncrementMessagesProcessed();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
