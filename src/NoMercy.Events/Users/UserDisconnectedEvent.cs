namespace NoMercy.Events.Users;

public sealed class UserDisconnectedEvent : EventBase
{
    public override string Source => "SignalR";

    public required Guid UserId { get; init; }
    public required string ConnectionId { get; init; }
}
