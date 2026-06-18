using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.EventHandlers;

/// <summary>
/// Consumer video-archiver workflow: when a newly-scanned movie or episode
/// lands in a library folder that has an <c>EncoderProfileFolder</c>
/// assignment, automatically queue a <c>VideoEncodeJob</c> for each video
/// file that hasn't been encoded yet.
///
/// Skips files whose expected encoded output directory already exists so
/// rescans don't re-encode. Folders without profile assignments are a no-op —
/// users can still manage encoding by hand through the dashboard.
/// </summary>
public class AutoEncodeSubscriber(
    IEventBus eventBus,
    ILogger<AutoEncodeSubscriber> logger,
    IStorage storage
) : IHostedService
{
    private readonly List<IDisposable> _subscriptions = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriptions.Add(
            eventBus.Subscribe<MediaFilesScannedEvent>((evt, ct) => HandleAsync(evt, ct))
        );
        logger.LogInformation("Auto-encode subscriber active");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (IDisposable subscription in _subscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not dispose auto-encode subscription");
            }
        }
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    private async Task HandleAsync(MediaFilesScannedEvent evt, CancellationToken ct)
    {
        try
        {
            await using MediaContext context = new();

            // All folders in this library that have an encoder profile attached.
            List<Folder> folders = await context
                .Folders.Include(f => f.EncoderProfileFolder)
                    .ThenInclude(epf => epf.EncoderProfile)
                .Include(f => f.FolderLibraries)
                .Where(f =>
                    f.FolderLibraries.Any(fl => fl.LibraryId == evt.LibraryId)
                    && f.EncoderProfileFolder.Any()
                )
                .ToListAsync(ct);

            if (folders.Count == 0)
            {
                logger.LogDebug(
                    "No folders with encoder profiles in library {LibraryId}; auto-encode skipped",
                    evt.LibraryId
                );
                return;
            }

            // Video files for this media id — match on movie id OR episode id.
            // Multiple sources per media (NFS + S3 + ...) is expected, so dedupe
            // by Filename and pick the best driver to read from. The encoder
            // publishes its output to every configured destination from a single
            // run, so we never need to encode the same content twice.
            List<VideoFile> videoFiles = await context
                .VideoFiles.Where(vf => vf.MovieId == evt.MediaId || vf.EpisodeId == evt.MediaId)
                .ToListAsync(ct);

            if (videoFiles.Count == 0)
            {
                logger.LogDebug(
                    "No video files yet for media {MediaId} — auto-encode skipped",
                    evt.MediaId
                );
                return;
            }

            JobDispatcher dispatcher = new();
            int dispatched = 0;

            // Pre-load drivers so we can rank sources by storage type
            // (local > nfs/smb > webdav > r2/s3) without N+1 queries.
            Dictionary<Ulid, string> driverTypeById = await context
                .Drivers.AsNoTracking()
                .ToDictionaryAsync(d => d.Id, d => d.Type, ct);

            foreach (
                (VideoFile bestSource, Folder folder) in SelectSourcesToEncode(
                    videoFiles,
                    folders,
                    driverTypeById
                )
            )
            {
                if (!string.IsNullOrEmpty(bestSource.Folder) && IsAlreadyEncoded(bestSource))
                    continue;

                string filePath =
                    bestSource.HostFolder.TrimEnd('/') + "/" + bestSource.Filename.TrimStart('/');
                dispatcher.DispatchJob<VideoEncodeJob>(
                    evt.LibraryId,
                    folder.Id,
                    evt.MediaId.ToString(),
                    filePath
                );
                dispatched++;
            }

            if (dispatched > 0)
            {
                logger.LogInformation(
                    "Auto-encode dispatched {Count} VideoEncodeJob(s) for media {MediaId}",
                    dispatched,
                    evt.MediaId
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-encode handler failed for media {MediaId}", evt.MediaId);
        }
    }

    /// <summary>
    /// Pure decision function: given the VideoFiles for one media and the
    /// available folders+driver mapping, return one (source, folder) pair per
    /// distinct filename — the best source ranked by driver type. Same media
    /// content stored on NFS + S3 gets exactly one encode dispatched, against
    /// the fastest source. Internal so the test project can pin every branch
    /// without spinning up a DB.
    /// </summary>
    internal static IEnumerable<(VideoFile File, Folder Folder)> SelectSourcesToEncode(
        IReadOnlyList<VideoFile> videoFiles,
        IReadOnlyList<Folder> folders,
        Dictionary<Ulid, string> driverTypeById
    )
    {
        return videoFiles
            .GroupBy(vf => vf.Filename)
            .Select(group =>
                group
                    .Select(file => new
                    {
                        File = file,
                        Folder = folders.FirstOrDefault(f =>
                            !string.IsNullOrEmpty(file.HostFolder)
                            && file.HostFolder.StartsWith(
                                f.Path,
                                StringComparison.OrdinalIgnoreCase
                            )
                        ),
                    })
                    .Where(x => x.Folder is not null)
                    .OrderBy(x => DriverPreference(driverTypeById, x.Folder!.DriverId))
                    .Select(x => (File: x.File, Folder: x.Folder!))
                    .FirstOrDefault()
            )
            .Where(pair => pair.File is not null && pair.Folder is not null);
    }

    // Lower number = preferred. Picks the lowest-latency / cheapest source
    // when the same media is mounted on multiple drivers.
    internal static int DriverPreference(Dictionary<Ulid, string> typeById, Ulid driverId)
    {
        if (!typeById.TryGetValue(driverId, out string? type))
            return 99;

        return type.ToLowerInvariant() switch
        {
            "local" => 0,
            "nfs" or "smb" or "cifs" => 1,
            "webdav" => 2,
            "s3" or "r2" => 3,
            _ => 50,
        };
    }

    /// <summary>
    /// Quick filesystem check — the encoder drops outputs under a sibling
    /// directory named after the media title. If that directory exists with
    /// any files in it, consider the file already encoded.
    /// </summary>
    private bool IsAlreadyEncoded(VideoFile file)
    {
        if (string.IsNullOrEmpty(file.HostFolder))
            return false;

        // The encoder writes into `{HostFolder}` itself for single-file
        // outputs (mkv/mp4/m4a) or into a `{HostFolder}/*.NoMercy/` subdir
        // for HLS. Either way, any *.m3u8 / *.mp4 / *.mkv / *.m4a sibling
        // of the source is a reasonable indicator the file is encoded.
        string sourceExt = Path.GetExtension(file.Filename).ToLowerInvariant();
        try
        {
            if (!storage.Exists(file.HostFolder))
                return false;

            // Any .NoMercy subdirectory counts as encoded.
            if (
                storage.List(file.HostFolder, "*.NoMercy", recursive: false).Any(e => e.IsDirectory)
            )
                return true;

            // Any master playlist alongside counts too.
            if (storage.List(file.HostFolder, "*.m3u8", recursive: false).Any(e => !e.IsDirectory))
                return true;

            _ = sourceExt;
            return false;
        }
        catch
        {
            return false;
        }
    }
}
