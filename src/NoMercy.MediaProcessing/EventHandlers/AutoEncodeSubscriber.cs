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
    private readonly IStorage _storage = storage;
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

            foreach (VideoFile file in videoFiles)
            {
                // Skip if already encoded — the encoded output lands under a
                // `.NoMercy` sibling directory next to the source. Cheap FS
                // check that keeps rescans idempotent.
                if (!string.IsNullOrEmpty(file.Folder) && IsAlreadyEncoded(file))
                    continue;

                Folder? folder = folders.FirstOrDefault(f =>
                    !string.IsNullOrEmpty(file.HostFolder)
                    && file.HostFolder.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase)
                );
                if (folder is null)
                    continue;

                string filePath = Path.Combine(file.HostFolder, file.Filename);
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
            if (!_storage.Exists(file.HostFolder))
                return false;

            // Any .NoMercy subdirectory counts as encoded.
            if (
                _storage
                    .List(file.HostFolder, "*.NoMercy", recursive: false)
                    .Any(e => e.IsDirectory)
            )
                return true;

            // Any master playlist alongside counts too.
            if (_storage.List(file.HostFolder, "*.m3u8", recursive: false).Any(e => !e.IsDirectory))
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
