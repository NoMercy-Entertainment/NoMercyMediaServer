namespace NoMercy.Events.FileWatcher;

public sealed class FileDeletedEvent : EventBase
{
    public override string Source => "FileWatcher";

    public required string FullPath { get; init; }
    public required Ulid LibraryId { get; init; }
    public required string LibraryType { get; init; }
}
