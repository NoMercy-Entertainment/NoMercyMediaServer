namespace NoMercy.Events;

public abstract class EventBase : IEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public abstract string Source { get; }
}
