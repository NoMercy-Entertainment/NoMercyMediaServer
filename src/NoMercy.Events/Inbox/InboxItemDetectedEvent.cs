namespace NoMercy.Events.Inbox;

public sealed class InboxItemDetectedEvent : EventBase
{
    public override string Source => "Inbox";

    public required string Id { get; init; }
    public required string DetectedType { get; init; }
    public required string Confidence { get; init; }
    public required string Status { get; init; }
}
