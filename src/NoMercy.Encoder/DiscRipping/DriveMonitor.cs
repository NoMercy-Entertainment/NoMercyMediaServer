using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace NoMercy.Encoder.DiscRipping;

/// <summary>
/// Polls the OS for optical-drive state every few seconds and emits
/// <see cref="DriveEvent"/>s when a disc is inserted, ejected, or a drive
/// is added / removed. Polling is the least-intrusive option and works
/// identically on Windows, Linux, and macOS without native interop.
///
/// Uses <see cref="System.IO.DriveInfo.GetDrives"/> filtered by
/// <see cref="System.IO.DriveType.CDRom"/>. Drive identity is the drive
/// root path — when that changes between polls the monitor emits the
/// corresponding event type.
/// </summary>
public class DriveMonitor(ILogger<DriveMonitor> logger) : IDriveMonitor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    public IReadOnlyList<DiscDrive> GetDrives()
    {
        List<DiscDrive> drives = [];
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.CDRom)
                continue;

            bool ready = drive.IsReady;
            string label = ready ? drive.VolumeLabel : string.Empty;
            OpticalDiscType type = ready ? ClassifyDisc(drive) : OpticalDiscType.None;

            drives.Add(new(drive.Name, label, ready, type));
        }
        return drives;
    }

    public async IAsyncEnumerable<DriveEvent> MonitorAsync(
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        Dictionary<string, DiscDrive> previous = GetDrives().ToDictionary(d => d.Path);
        logger.LogInformation(
            "Drive monitor started — tracking {Count} optical drive(s)",
            previous.Count
        );

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            Dictionary<string, DiscDrive> current = GetDrives().ToDictionary(d => d.Path);

            // Drives added since last poll.
            foreach (DiscDrive added in current.Values.Where(d => !previous.ContainsKey(d.Path)))
            {
                yield return new(DriveEventType.DriveAdded, added);
                if (added.HasDisc)
                    yield return new(DriveEventType.DiscInserted, added);
            }

            // Drives removed since last poll.
            foreach (DiscDrive removed in previous.Values.Where(d => !current.ContainsKey(d.Path)))
            {
                yield return new(DriveEventType.DriveRemoved, removed);
            }

            // Disc state changes on known drives.
            foreach (DiscDrive now in current.Values)
            {
                if (!previous.TryGetValue(now.Path, out DiscDrive? before))
                    continue;
                if (before.HasDisc == now.HasDisc)
                    continue;

                yield return new(
                    now.HasDisc ? DriveEventType.DiscInserted : DriveEventType.DiscEjected,
                    now
                );
            }

            previous = current;
        }
    }

    /// <summary>
    /// Best-effort disc-type classification. True Blu-ray detection requires
    /// reading the UDF filesystem (BDMV folder presence) which DriveInfo
    /// doesn't expose; for an MVP we rely on the volume size — Blu-rays are
    /// typically 25GB+ while DVDs cap at 9GB. When TotalSize can't be read
    /// the drive is reported as DVD.
    /// </summary>
    private static OpticalDiscType ClassifyDisc(DriveInfo drive)
    {
        try
        {
            long gigabytes = drive.TotalSize / 1_000_000_000;
            if (gigabytes > 10)
                return OpticalDiscType.BluRay;
            if (gigabytes > 0)
                return OpticalDiscType.Dvd;
        }
        catch
        {
            // TotalSize throws on some drive states — fall through.
        }
        return OpticalDiscType.Dvd;
    }
}
