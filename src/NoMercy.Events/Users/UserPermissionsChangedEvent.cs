namespace NoMercy.Events.Users;

public sealed class UserPermissionsChangedEvent : EventBase
{
    public override string Source => "Users";

    public required Guid UserId { get; init; }
    public required Guid ChangedBy { get; init; }
}
