namespace NoMercy.Events.Playback;

public sealed class PlaybackProgressEvent : EventBase
{
    public override string Source => "Playback";

    public required Guid UserId { get; init; }
    public required int MediaId { get; init; }
    public string? MediaIdentifier { get; init; }
    public required TimeSpan Position { get; init; }
    public required TimeSpan Duration { get; init; }
}
