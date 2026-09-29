using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;

namespace PoproshaykaBot.Core.Statistics;

public sealed class CommandUsageStreamResetHandler : IEventHandler<StreamWentOnline>, IEventSubscriber, IDisposable
{
    private readonly CommandUsageRepository _repository;
    private readonly ILogger<CommandUsageStreamResetHandler> _logger;
    private readonly IDisposable _subscription;

    public CommandUsageStreamResetHandler(
        CommandUsageRepository repository,
        IEventBus eventBus,
        ILogger<CommandUsageStreamResetHandler> logger)
    {
        _repository = repository;
        _logger = logger;
        _subscription = eventBus.Subscribe(this);
    }

    public Task HandleAsync(StreamWentOnline @event, CancellationToken cancellationToken)
    {
        var streamId = @event.Stream?.Id ?? string.Empty;

        if (_repository.ResetStreamCounters(streamId))
        {
            var reportedStreamId = string.IsNullOrEmpty(streamId) ? "–" : streamId;
            _logger.LogInformation("Счётчики команд за стрим обнулены (стрим {StreamId})", reportedStreamId);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
