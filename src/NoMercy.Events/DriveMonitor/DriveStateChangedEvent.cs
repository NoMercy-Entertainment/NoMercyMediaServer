namespace NoMercy.Events.DriveMonitor;

public sealed class DriveStateChangedEvent : EventBase
{
    public override string Source => "DriveMonitor";

    public required DriveStatePayload DriveStateData { get; init; }
}
