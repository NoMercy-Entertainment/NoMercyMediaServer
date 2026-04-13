namespace NoMercy.Encoder.DiscRipping;

public interface IDriveMonitor
{
    IAsyncEnumerable<DriveEvent> MonitorAsync(CancellationToken ct);

    IReadOnlyList<DiscDrive> GetDrives();
}
