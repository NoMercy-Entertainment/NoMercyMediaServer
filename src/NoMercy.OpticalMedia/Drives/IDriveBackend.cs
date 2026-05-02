namespace NoMercy.OpticalMedia.Drives;

/// <summary>
/// OS-specific source of drive state and insert/eject events.
/// <see cref="DriveMonitor"/> picks one at DI time:
/// WMI on Windows where available, polling fallback otherwise.
/// </summary>
public interface IDriveBackend : IAsyncDisposable
{
    IReadOnlyList<DiscDrive> GetDrives();
    IAsyncEnumerable<DriveEvent> ListenAsync(CancellationToken ct);
}
