using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Statistics;

namespace PoproshaykaBot.Core.Statistics;

public sealed class StreamSessionHistoryRecorder :
    IEventHandler<StreamSessionCompleted>,
    IEventSubscriber,
    IDisposable
{
    private readonly StreamSessionHistoryStore _store;
    private readonly ILogger<StreamSessionHistoryRecorder> _logger;
    private readonly IDisposable _subscription;

    public StreamSessionHistoryRecorder(
        StreamSessionHistoryStore store,
        IEventBus eventBus,
        ILogger<StreamSessionHistoryRecorder> logger)
    {
        _store = store;
        _logger = logger;
        _subscription = eventBus.Subscribe(this);
    }

    public Task HandleAsync(StreamSessionCompleted @event, CancellationToken cancellationToken)
    {
        try
        {
            _store.Append(@event.Session);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось сохранить запись истории стрима для канала {Channel}", @event.Session.Channel);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
