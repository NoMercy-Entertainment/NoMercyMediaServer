namespace NoMercy.Events.Encoding;

public sealed class EncodingProgressEvent : EventBase
{
    public override string Source => "Encoder";

    public required int JobId { get; init; }
    public required double Percentage { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? Estimated { get; init; }
}
