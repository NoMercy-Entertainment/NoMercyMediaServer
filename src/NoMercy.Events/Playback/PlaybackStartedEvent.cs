namespace NoMercy.Events.Playback;

public sealed class PlaybackStartedEvent : EventBase
{
    public override string Source => "Playback";

    public required Guid UserId { get; init; }
    public required int MediaId { get; init; }
    public string? MediaIdentifier { get; init; }
    public required string MediaType { get; init; }
    public string? DeviceId { get; init; }
}
