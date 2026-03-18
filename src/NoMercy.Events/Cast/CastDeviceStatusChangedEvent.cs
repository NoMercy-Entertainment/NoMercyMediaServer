namespace NoMercy.Events.Cast;

public sealed class CastDeviceStatusChangedEvent : EventBase
{
    public override string Source => "ChromeCast";

    public required string EventType { get; init; }
    public required object StatusData { get; init; }
}
