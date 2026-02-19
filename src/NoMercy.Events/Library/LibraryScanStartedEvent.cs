namespace NoMercy.Events.Library;

public sealed class LibraryScanStartedEvent : EventBase
{
    public override string Source => "LibraryScanner";

    public required Ulid LibraryId { get; init; }
    public required string LibraryName { get; init; }
}
