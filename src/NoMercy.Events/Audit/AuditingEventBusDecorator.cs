namespace NoMercy.Events.Audit;

public class AuditingEventBusDecorator : IEventBus
{
    private readonly IEventBus _inner;
    private readonly EventAuditLog _auditLog;

    public AuditingEventBusDecorator(IEventBus inner, EventAuditLog auditLog)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    }

    public EventAuditLog AuditLog => _auditLog;

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent
    {
        _auditLog.Record(@event, typeof(TEvent).Name);
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
