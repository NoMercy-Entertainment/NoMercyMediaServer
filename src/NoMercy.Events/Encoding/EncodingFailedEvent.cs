namespace NoMercy.Events.Encoding;

public sealed class EncodingFailedEvent : EventBase
{
    public override string Source => "Encoder";

    public required int JobId { get; init; }
    public required string InputPath { get; init; }
    public required string ErrorMessage { get; init; }
    public string? ExceptionType { get; init; }
}
