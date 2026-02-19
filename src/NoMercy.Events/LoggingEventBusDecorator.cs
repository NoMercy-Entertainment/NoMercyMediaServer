namespace NoMercy.Events;

public class LoggingEventBusDecorator : IEventBus
{
    private readonly IEventBus _inner;
    private readonly Action<string> _log;

    public LoggingEventBusDecorator(IEventBus inner, Action<string> log)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent
    {
        string eventTypeName = typeof(TEvent).Name;
        _log($"[Event] {eventTypeName} | Source={@event.Source} | EventId={@event.EventId} | Timestamp={@event.Timestamp:O}");

        await _inner.PublishAsync(@event, ct);
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IEvent
    {
        return _inner.Subscribe(handler);
    }

    public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        return _inner.Subscribe(handler);
    }
}
