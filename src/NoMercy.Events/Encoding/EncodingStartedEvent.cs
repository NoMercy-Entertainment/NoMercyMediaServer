namespace NoMercy.Events.Encoding;

public sealed class EncodingStartedEvent : EventBase
{
    public override string Source => "Encoder";

    public required int JobId { get; init; }
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public required string ProfileName { get; init; }
}
