namespace NoMercy.Encoder.V3.DiscRipping;

public enum DriveEventType
{
    DiscInserted,
    DiscEjected,
    DriveAdded,
    DriveRemoved,
}

public record DriveEvent(DriveEventType Type, DiscDrive Drive);
