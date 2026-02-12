namespace NoMercy.Events.Encoding;

public sealed class EncodingCompletedEvent : EventBase
{
    public override string Source => "Encoder";

    public required int JobId { get; init; }
    public required string OutputPath { get; init; }
    public required TimeSpan Duration { get; init; }
}
