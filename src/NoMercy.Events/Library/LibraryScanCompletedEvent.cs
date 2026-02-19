namespace NoMercy.Events.Library;

public sealed class LibraryScanCompletedEvent : EventBase
{
    public override string Source => "LibraryScanner";

    public required Ulid LibraryId { get; init; }
    public required string LibraryName { get; init; }
    public required int ItemsFound { get; init; }
    public required TimeSpan Duration { get; init; }
}
