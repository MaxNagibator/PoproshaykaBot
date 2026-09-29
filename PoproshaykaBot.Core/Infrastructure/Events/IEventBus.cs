namespace PoproshaykaBot.Core.Infrastructure.Events;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent;

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IEvent;

    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : IEvent;

    void ContinueAfterPublish(Func<Task> continuation);
}
