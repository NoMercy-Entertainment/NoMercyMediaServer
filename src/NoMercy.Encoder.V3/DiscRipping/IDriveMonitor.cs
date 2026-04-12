namespace NoMercy.Encoder.V3.DiscRipping;

public interface IDriveMonitor
{
    IAsyncEnumerable<DriveEvent> MonitorAsync(CancellationToken ct);

    IReadOnlyList<DiscDrive> GetDrives();
}
