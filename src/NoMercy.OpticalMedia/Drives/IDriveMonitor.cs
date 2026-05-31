namespace NoMercy.OpticalMedia.Drives;

/// <summary>
/// Watches optical drives for insert/eject and surfaces them as a stream of
/// <see cref="DriveEvent"/>. Singleton: holds the last-seen drive set so it
/// can diff between iterations.
/// </summary>
public interface IDriveMonitor
{
    IReadOnlyList<DiscDrive> GetDrives();
    IAsyncEnumerable<DriveEvent> MonitorAsync(CancellationToken ct);
}
