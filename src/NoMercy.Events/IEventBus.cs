namespace NoMercy.Events;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent;

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IEvent;

    IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent;
}
