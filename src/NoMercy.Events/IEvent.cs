namespace NoMercy.Events;

public interface IEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
    string Source { get; }
}
