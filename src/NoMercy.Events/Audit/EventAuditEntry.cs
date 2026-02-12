namespace NoMercy.Events.Audit;

public sealed class EventAuditEntry
{
    public required Guid EventId { get; init; }
    public required string EventType { get; init; }
    public required string Source { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Payload { get; init; }
}
