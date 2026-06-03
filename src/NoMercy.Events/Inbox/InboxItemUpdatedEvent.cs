namespace NoMercy.Events.Inbox;

public sealed class InboxItemUpdatedEvent : EventBase
{
    public override string Source => "Inbox";

    public required string Id { get; init; }
    public required string Status { get; init; }
}
