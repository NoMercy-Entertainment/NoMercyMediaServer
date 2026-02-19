namespace NoMercy.Events.FileWatcher;

public sealed class FileRenamedEvent : EventBase
{
    public override string Source => "FileWatcher";

    public required string OldFullPath { get; init; }
    public required string NewFullPath { get; init; }
    public required Ulid LibraryId { get; init; }
    public required string LibraryType { get; init; }
}
