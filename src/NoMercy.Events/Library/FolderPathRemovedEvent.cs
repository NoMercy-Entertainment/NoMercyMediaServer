namespace NoMercy.Events.Library;

public sealed class FolderPathRemovedEvent : EventBase
{
    public override string Source => "Library";

    public required Ulid RequestPath { get; init; }
}
