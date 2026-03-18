namespace NoMercy.Events.FileWatcher;

public sealed class FileCreatedEvent : EventBase
{
    public override string Source => "FileWatcher";

    public required string FolderPath { get; init; }
    public required Ulid LibraryId { get; init; }
    public required string LibraryType { get; init; }
}
