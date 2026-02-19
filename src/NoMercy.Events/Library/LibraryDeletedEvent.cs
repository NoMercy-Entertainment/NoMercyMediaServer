namespace NoMercy.Events.Library;

public sealed class LibraryDeletedEvent : EventBase
{
    public override string Source => "Library";

    public required Ulid LibraryId { get; init; }
    public required string LibraryName { get; init; }
}
