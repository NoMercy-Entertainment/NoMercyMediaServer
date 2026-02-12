namespace NoMercy.Events.Media;

public sealed class MediaRemovedEvent : EventBase
{
    public override string Source => "MediaProcessor";

    public required int MediaId { get; init; }
    public required string MediaType { get; init; }
    public required string Title { get; init; }
    public required Ulid LibraryId { get; init; }
}
