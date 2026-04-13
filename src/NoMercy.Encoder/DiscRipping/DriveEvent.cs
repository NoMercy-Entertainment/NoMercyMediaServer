namespace NoMercy.Encoder.DiscRipping;

public enum DriveEventType
{
    DiscInserted,
    DiscEjected,
    DriveAdded,
    DriveRemoved,
}

public record DriveEvent(DriveEventType Type, DiscDrive Drive);
