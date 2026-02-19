namespace NoMercy.Events.Users;

public sealed class UserAuthenticatedEvent : EventBase
{
    public override string Source => "Auth";

    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
}
