namespace NoMercy.Events.DriveMonitor;

public sealed class DriveStateChangedEvent : EventBase
{
    public override string Source => "DriveMonitor";

    public required object DriveStateData { get; init; }
}
