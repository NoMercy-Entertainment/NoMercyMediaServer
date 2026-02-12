namespace NoMercy.Events.Media;

public sealed class MediaDiscoveredEvent : EventBase
{
    public override string Source => "MediaScanner";

    public required string FilePath { get; init; }
    public required Ulid LibraryId { get; init; }
    public string? DetectedType { get; init; }
}
