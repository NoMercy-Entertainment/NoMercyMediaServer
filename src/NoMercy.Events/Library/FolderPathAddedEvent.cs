namespace NoMercy.Events.Library;

public sealed class FolderPathAddedEvent : EventBase
{
    public override string Source => "Library";

    public required Ulid RequestPath { get; init; }
    public required Ulid DriverId { get; init; }
    public required string SubPath { get; init; }
}
