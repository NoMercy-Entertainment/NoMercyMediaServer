namespace NoMercy.Events.Encoding;

public sealed class EncoderProgressBroadcastEvent : EventBase
{
    public override string Source => "Encoder";

    public required object ProgressData { get; init; }
}
