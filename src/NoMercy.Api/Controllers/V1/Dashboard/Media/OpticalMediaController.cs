using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaSources.OpticalMedia.Dto;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage;
using DriveMonitor = NoMercy.MediaSources.OpticalMedia.DriveMonitor;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags("Dashboard Optical")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/optical")]
public partial class OpticalMediaController(
    DiscSourceFactory discSourceFactory,
    IDiscMetadataResolver metadataResolver,
    IDriveMonitor driveMonitor,
    IDiscRipper discRipper,
    IStorageFactory storageFactory,
    IStorageDriver localStorageDriver
) : BaseController
{
    [HttpGet("drives")]
    public IActionResult GetOpticalDrives()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view optical drives");

        IEnumerable<DriveState> drives = Optical
            .GetOpticalDrives()
            .Select(drive => new DriveState
            {
                Path = drive.Key.TrimEnd(Path.DirectorySeparatorChar),
                Label = drive.Value,
                Open = drive.Value == null,
                MetaData = DriveMonitor.Contents.FirstOrDefault(x => x.Path == drive.Key),
            });

        return Ok(drives);
    }

    [HttpGet("{drivePath}")]
    public async Task<IActionResult> GetDriveContents(string drivePath)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view drive contents");

        MetaData? metadata = await DriveMonitor.GetDriveMetadata(drivePath);
        if (metadata == null)
            return NotFoundResponse("Drive metadata not found");

        return Ok(
            new DriveState
            {
                Open = false,
                Path = drivePath.TrimEnd(Path.DirectorySeparatorChar),
                Label = metadata.Title,
                MetaData = metadata,
            }
        );
    }

    [HttpPost("{drivePath}/process")]
    public IActionResult ProcessMedia(string drivePath, [FromBody] MediaProcessingRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to process media");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        _ = DriveMonitor.ProcessMedia(drivePath, request);

        return Ok("Processing started.");
    }

    [HttpPost("{drivePath}/open")]
    public IActionResult OpenDrive(string drivePath)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to open drive");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        bool success = Optical.OpenDrive(drivePath);

        if (!success)
            return BadRequestResponse("Failed to open drive");

        return Ok("Drive opened.");
    }

    [HttpPost("{drivePath}/close")]
    public IActionResult CloseDrive(string drivePath)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to close drive");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        bool success = Optical.CloseDrive(drivePath);

        if (!success)
            return BadRequestResponse("Failed to close drive");

        return Ok("Drive closed.");
    }

    [HttpPost("{drivePath}/play/{playlistId}")]
    public async Task<IActionResult> PlayMedia(
        string drivePath,
        string playlistId,
        CancellationTokenSource cancellationTokenSource
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to play media");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        if (string.IsNullOrWhiteSpace(playlistId))
            return BadRequestResponse("Playlist ID is required");

        await DriveMonitor.PlayMedia(drivePath, playlistId, cancellationTokenSource);

        return Ok("Playing media.");
    }

    [HttpPost("{drivePath}/stop")]
    public IActionResult StopMedia(string drivePath)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to stop media");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        _ = DriveMonitor.StopMedia();

        return Ok("Media stopped.");
    }

    /// <summary>
    /// New full probe via NoMercy.OpticalMedia: enumerates every playlist
    /// on the disc plus all viable TMDB candidates with confidence scores.
    /// Replaces the legacy single-title <c>GetDriveContents</c> response
    /// for callers that want to render a multi-title browse UI or pick
    /// between metadata candidates.
    /// </summary>
    [HttpGet("{drivePath}/probe")]
    public async Task<IActionResult> ProbeDisc(string drivePath, CancellationToken ct)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to probe optical drives");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        DiscDrive? drive = driveMonitor
            .GetDrives()
            .FirstOrDefault(d =>
                d.Path.TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(
                        drivePath.TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase
                    )
            );

        if (drive is null)
            return NotFoundResponse($"No optical drive found at {drivePath}");

        if (!drive.HasDisc || drive.DiscType == OpticalDiscType.None)
            return Ok(
                new
                {
                    drive_path = drive.Path,
                    label = drive.Label,
                    has_disc = false,
                    disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                }
            );

        IDiscSource? source = discSourceFactory.CreateFor(drive.DiscType);
        if (source is null)
            return BadRequestResponse($"No reader registered for disc type {drive.DiscType} (yet)");

        DiscInfo info = await source.ProbeAsync(drive, ct);
        MetadataMatch[] candidates = await metadataResolver.ResolveAsync(info, ct);

        return Ok(
            new
            {
                drive_path = drive.Path,
                label = drive.Label,
                has_disc = true,
                disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                disc = info,
                candidates,
            }
        );
    }

    /// <summary>
    /// Returns ranked TMDB candidates for the disc in the given drive.
    /// No side effects — purely read-only preview. Returns the top 5
    /// candidates ordered by confidence (highest first) so the dashboard
    /// can render a candidate picker before the user confirms.
    /// </summary>
    [HttpPost("{drivePath}/resolve")]
    public async Task<IActionResult> ResolveDisc(string drivePath, CancellationToken ct)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to resolve disc metadata");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        DiscDrive? drive = driveMonitor
            .GetDrives()
            .FirstOrDefault(d =>
                d.Path.TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(
                        drivePath.TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase
                    )
            );

        if (drive is null || !drive.HasDisc)
            return NotFoundResponse($"No disc loaded in {drivePath}");

        IDiscSource? source = discSourceFactory.CreateFor(drive.DiscType);
        if (source is null)
            return BadRequestResponse($"No reader registered for disc type {drive.DiscType}");

        DiscInfo info = await source.ProbeAsync(drive, ct);
        MetadataMatch[] candidates = await metadataResolver.ResolveAsync(info, ct);

        return Ok(
            new
            {
                drive_path = drive.Path,
                disc_duration_sec = info.MainTitleDurationSec,
                candidates = candidates
                    .Take(5)
                    .Select(m => new
                    {
                        tmdb_id = m.ExternalId,
                        media_type = m.Type.ToString().ToLowerInvariant(),
                        title = m.Title,
                        year = m.Year,
                        runtime = m.Runtime,
                        confidence = m.Confidence,
                        poster_url = m.PosterUrl,
                        backdrop_url = m.BackdropUrl,
                        season_number = m.SeasonNumber,
                        episode_number = m.EpisodeNumber,
                    }),
            }
        );
    }

    /// <summary>
    /// Applies the user's chosen TMDB match to the rip output. Renames/moves
    /// the raw rip file into the canonical media-library path and triggers a
    /// library refresh so the file is picked up by the import pipeline.
    /// </summary>
    [HttpPost("{drivePath}/confirm")]
    public async Task<IActionResult> ConfirmDisc(
        string drivePath,
        [FromBody] DiscConfirmRequest request,
        CancellationToken ct
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to confirm disc metadata");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        if (string.IsNullOrWhiteSpace(request.TmdbId))
            return BadRequestResponse("TmdbId is required");

        if (string.IsNullOrWhiteSpace(request.RipOutputPath))
            return BadRequestResponse("RipOutputPath is required");

        if (!System.IO.File.Exists(request.RipOutputPath))
            return NotFoundResponse($"Rip output not found at {request.RipOutputPath}");

        await using MediaContext db = new();
        LibraryRepository libraryRepository = new(db, localStorageDriver);

        Folder? targetFolder = await libraryRepository.GetLibraryFolder(request.FolderId);
        if (targetFolder is null)
            return BadRequestResponse(
                $"FolderId {request.FolderId} does not match any library folder"
            );

        Library? targetLibrary = await libraryRepository.GetLibraryByIdWithFolders(
            request.LibraryId
        );
        if (targetLibrary is null)
            return BadRequestResponse($"LibraryId {request.LibraryId} does not match any library");

        CustomMetadata meta =
            request.MediaType == "tv"
                ? new CustomMetadata(
                    Title: request.Title ?? string.Empty,
                    Year: request.Year,
                    Type: MediaType.TvShow,
                    PosterUrl: request.PosterUrl,
                    SeasonNumber: request.SeasonNumber ?? 1,
                    EpisodeStartNumber: request.EpisodeNumber ?? 1
                )
                : new CustomMetadata(
                    Title: request.Title ?? string.Empty,
                    Year: request.Year,
                    Type: MediaType.Movie,
                    PosterUrl: request.PosterUrl
                );

        RipRequest syntheticRequest = new(
            DrivePath: drivePath,
            SelectedTitleIndices: [0],
            MetadataId: request.TmdbId,
            Custom: meta,
            LibraryId: request.LibraryId,
            FolderId: request.FolderId,
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: [],
            Mode: RipMode.RipAndEncode
        );

        string folderRelative = BuildOutputPath(syntheticRequest, targetLibrary.Type, 0, 0);

        IStorage folderStorage = storageFactory.For(
            targetFolder.Id,
            targetFolder.DriverId,
            string.Empty
        );

        string parentRelative = ParentRelative(folderRelative);
        if (!string.IsNullOrEmpty(parentRelative))
            await folderStorage.CreateDirectoryAsync(parentRelative, ct);

        await using (FileStream src = new(request.RipOutputPath, FileMode.Open, FileAccess.Read))
        await using (
            System.IO.Stream dst = await folderStorage.OpenWriteAsync(
                folderRelative,
                overwrite: true,
                ct
            )
        )
        {
            await src.CopyToAsync(dst, ct);
        }

        try
        {
            System.IO.File.Delete(request.RipOutputPath);
        }
        catch
        {
            // best effort
        }

        string watcherFolderHost = ResolveHostPath(folderStorage, parentRelative);
        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new FileCreatedEvent
                {
                    FolderPath = watcherFolderHost,
                    LibraryId = targetLibrary.Id,
                    LibraryType = targetLibrary.Type,
                }
            );
        }

        return Ok(
            new
            {
                tmdb_id = request.TmdbId,
                media_type = request.MediaType,
                destination = folderRelative,
                library_refresh_triggered = EventBusProvider.IsConfigured,
            }
        );
    }

    /// <summary>
    /// Starts a stream-copy rip of the requested titles into MKV intermediates.
    /// Each rip emits a <c>DiscRipResult</c> the caller can inspect; failures
    /// surface AACS / BD+ / read-error classifications via <c>Error</c>. The
    /// resulting MKVs land in <c>{TranscodePath}/ripper/{Drive}/title_{N}.mkv</c>;
    /// downstream encoding is wired up by the caller (a <c>VideoEncodeJob</c>
    /// per output, in Phase E.2).
    /// </summary>
    [HttpPost("{drivePath}/rip")]
    public async Task<IActionResult> RipDisc(
        string drivePath,
        [FromBody] RipRequest request,
        CancellationToken ct
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to rip optical drives");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        if (request.SelectedTitleIndices.Length == 0)
            return BadRequestResponse("At least one title must be selected");

        DiscDrive? drive = driveMonitor
            .GetDrives()
            .FirstOrDefault(d =>
                d.Path.TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(
                        drivePath.TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase
                    )
            );

        if (drive is null || !drive.HasDisc)
            return NotFoundResponse($"No disc loaded in {drivePath}");

        // Fail fast if the disc is DRM-locked the host can't read. The
        // background rip task would otherwise spin up ffmpeg and fail with
        // an opaque "stream copy 0 bytes written" — much worse UX.
        IDiscSource? source = discSourceFactory.CreateFor(drive.DiscType);
        if (source is not null)
        {
            DiscInfo precheck = await source.ProbeAsync(drive, ct);
            if (precheck.Protection is not null)
                return BadRequestResponse(
                    $"Cannot rip — disc is {precheck.Protection.Kind}-protected: {precheck.Protection.Message}"
                );
        }

        // Validate the destination — when the user wants RipAndEncode the
        // controller has to know which folder + driver to upload the rip
        // into so the encoder can find the file via its driver. We also
        // need the library to know the media type (movie / tv / anime / music)
        // so we can compose a TMDB-style path the FileWatcher recognises.
        Folder? targetFolder = null;
        Library? targetLibrary = null;
        if (request.Mode == RipMode.RipAndEncode)
        {
            await using MediaContext lookupContext = new();
            LibraryRepository libraryRepository = new(lookupContext, localStorageDriver);
            targetFolder = await libraryRepository.GetLibraryFolder(request.FolderId);
            if (targetFolder is null)
                return BadRequestResponse(
                    $"FolderId {request.FolderId} does not match any library folder. "
                        + "RipAndEncode needs a real folder so the rip output lands somewhere "
                        + "the encoder can read it via the folder's driver."
                );
            targetLibrary = await libraryRepository.GetLibraryByIdWithFolders(request.LibraryId);
            if (targetLibrary is null)
                return BadRequestResponse(
                    $"LibraryId {request.LibraryId} does not match any library."
                );
        }

        // Local rip cache — ffmpeg always writes here first; for RipAndEncode
        // we then upload to the target folder via its driver. Per-drive
        // subdir keeps concurrent rips from different discs separated.
        string sanitisedDrive = drive
            .Path.TrimEnd(Path.DirectorySeparatorChar)
            .Replace(":", "")
            .Replace(Path.DirectorySeparatorChar, '_');
        string outputDir = Path.Combine(AppFiles.TranscodePath, "ripper", sanitisedDrive);
        Directory.CreateDirectory(outputDir);

        // Inject the resolved disc type so DiscRipper can pick the right
        // ffmpeg input shape — the body the client sent rarely includes one.
        RipRequest enriched = request with
        {
            DiscType = drive.DiscType,
        };

        // Probe the disc now (we already probed for DRM above, so cache the
        // info for auto-resolve). Only needed when the caller didn't supply
        // custom metadata — otherwise we use what they gave us.
        DiscInfo? discInfoForAutoResolve = null;
        if (enriched.Custom is null && source is not null)
        {
            try
            {
                discInfoForAutoResolve = await source.ProbeAsync(drive, ct);
            }
            catch
            {
                // Non-fatal — auto-resolve will simply be skipped.
            }
        }

        // Spawn the rip in the background. After each title rips successfully
        // we upload to the chosen folder via its driver, then publish a
        // FileCreatedEvent so the existing FileWatcher → MediaScan → AutoEncode
        // pipeline picks the new file up exactly as it would for a file
        // dropped into the library through any other channel.
        Folder? folderForBackground = targetFolder;
        Library? libraryForBackground = targetLibrary;
        DiscInfo? discInfoCapture = discInfoForAutoResolve;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    DiscRipResult[] results = await discRipper.RipAsync(
                        enriched,
                        outputDir,
                        CancellationToken.None
                    );

                    if (
                        enriched.Mode != RipMode.RipAndEncode
                        || folderForBackground is null
                        || libraryForBackground is null
                    )
                        return;

                    IStorage folderStorage = storageFactory.For(
                        folderForBackground.Id,
                        folderForBackground.DriverId,
                        string.Empty
                    );

                    DiscRipResult[] successes = results.Where(r => r.Success).ToArray();
                    int batchIndex = 0;
                    HashSet<string> notifiedFolders = new(StringComparer.OrdinalIgnoreCase);
                    foreach (DiscRipResult res in successes)
                    {
                        // When the caller supplied no custom metadata, attempt
                        // auto-resolution from TMDB. High-confidence matches are
                        // applied automatically; low-confidence matches write a
                        // pending-state sidecar for the user to resolve manually.
                        RipRequest effectiveRequest = enriched;
                        if (enriched.Custom is null && discInfoCapture is not null)
                        {
                            MetadataMatch[] candidates = await metadataResolver.ResolveAsync(
                                discInfoCapture,
                                CancellationToken.None
                            );
                            MetadataMatch? top = candidates.FirstOrDefault();

                            if (
                                top is not null
                                && top.Confidence >= TmdbDiscMatcher.AutoApplyThreshold
                            )
                            {
                                effectiveRequest = enriched with
                                {
                                    Custom = new CustomMetadata(
                                        Title: top.Title,
                                        Year: top.Year,
                                        Type: top.Type == MediaType.Movie
                                            ? MediaType.Movie
                                            : MediaType.TvShow,
                                        PosterUrl: top.PosterUrl,
                                        SeasonNumber: top.SeasonNumber,
                                        EpisodeStartNumber: top.EpisodeNumber
                                    ),
                                };
                            }
                            else if (top is not null)
                            {
                                string pendingPath = Path.Combine(
                                    outputDir,
                                    $"pending_{res.TitleIndex:D2}.json"
                                );
                                DiscRipPendingState pendingState = new(
                                    RipOutputPath: res.OutputPath,
                                    TitleIndex: res.TitleIndex,
                                    DrivePath: enriched.DrivePath,
                                    DiscDurationSec: discInfoCapture.MainTitleDurationSec,
                                    Candidates: candidates.Take(5).ToArray(),
                                    CreatedAt: DateTimeOffset.UtcNow
                                );
                                await System.IO.File.WriteAllTextAsync(
                                    pendingPath,
                                    System.Text.Json.JsonSerializer.Serialize(
                                        pendingState,
                                        new System.Text.Json.JsonSerializerOptions
                                        {
                                            WriteIndented = true,
                                        }
                                    ),
                                    CancellationToken.None
                                );
                                // Skip the move — the file stays in the rip cache
                                // until the user confirms via the /confirm endpoint.
                                continue;
                            }
                        }

                        string folderRelative = BuildOutputPath(
                            effectiveRequest,
                            libraryForBackground.Type,
                            res.TitleIndex,
                            batchIndex
                        );
                        batchIndex++;

                        // Make sure parent dirs exist on the destination driver.
                        string parentRelative = ParentRelative(folderRelative);
                        if (!string.IsNullOrEmpty(parentRelative))
                            await folderStorage.CreateDirectoryAsync(
                                parentRelative,
                                CancellationToken.None
                            );

                        await using (
                            FileStream src = new(res.OutputPath, FileMode.Open, FileAccess.Read)
                        )
                        await using (
                            System.IO.Stream dst = await folderStorage.OpenWriteAsync(
                                folderRelative,
                                overwrite: true,
                                CancellationToken.None
                            )
                        )
                        {
                            await src.CopyToAsync(dst, CancellationToken.None);
                        }

                        // Wake the FileWatcher pipeline. We use the
                        // driver-mounted (host-visible) folder path so
                        // MediaScan can crawl it the same way it would for
                        // a regular file-system event. For local-driver
                        // folders that's the absolute path; for NFS / S3
                        // it's the driver's mount root + sub-path.
                        string watcherFolderHost = ResolveHostPath(
                            folderStorage,
                            ParentRelative(folderRelative)
                        );
                        if (EventBusProvider.IsConfigured && notifiedFolders.Add(watcherFolderHost))
                        {
                            await EventBusProvider.Current.PublishAsync(
                                new FileCreatedEvent
                                {
                                    FolderPath = watcherFolderHost,
                                    LibraryId = libraryForBackground.Id,
                                    LibraryType = libraryForBackground.Type,
                                }
                            );
                        }

                        // Local rip cache served its purpose — drop it.
                        try
                        {
                            System.IO.File.Delete(res.OutputPath);
                        }
                        catch
                        {
                            // best effort; a stale cache file isn't fatal.
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"[optical-rip] background task failed for {drive.Path}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
                    );
                }
            },
            ct
        );

        return Accepted(
            new
            {
                drive_path = drive.Path,
                output_dir = outputDir,
                titles_queued = request.SelectedTitleIndices.Length,
                mode = request.Mode.ToString(),
            }
        );
    }

    /// <summary>
    /// Builds the folder-relative output path for a ripped title in the
    /// shape <see cref="MediaScan"/> can match against TMDB:
    /// movies → <c>{Title} ({Year})/{Title} ({Year}).mkv</c>;
    /// TV/anime → <c>{Title} ({Year})/Season {SS}/{Title} S{SS}E{EE}.mkv</c>.
    /// Falls back to <c>disc-rips/title_NN.mkv</c> when no Custom metadata
    /// was supplied — that lands as an unmatched file the user can rename
    /// manually in the dashboard.
    /// </summary>
    internal static string BuildOutputPath(
        RipRequest request,
        string libraryType,
        int titleIndex,
        int batchIndex
    )
    {
        CustomMetadata? meta = request.Custom;
        if (meta is null || string.IsNullOrWhiteSpace(meta.Title))
            return $"disc-rips/title_{titleIndex:D2}.mkv";

        string safeTitle = SanitizeForPath(meta.Title);
        string yearSuffix = meta.Year is { } year ? $" ({year})" : "";
        string showRoot = $"{safeTitle}{yearSuffix}";

        switch (libraryType)
        {
            case "tv":
            case "anime":
            {
                int season = meta.SeasonNumber ?? 1;
                int episode = (meta.EpisodeStartNumber ?? 1) + batchIndex;
                string seasonDir = $"Season {season:D2}";
                string fileName = $"{safeTitle} S{season:D2}E{episode:D2}.mkv";
                return $"{showRoot}/{seasonDir}/{fileName}";
            }
            case "movie":
            {
                // Multi-disc movies: append " - Disc N" so the user sees both
                // titles as siblings instead of fighting for one slot.
                string suffix = batchIndex == 0 ? "" : $" - Disc {batchIndex + 1}";
                return $"{showRoot}/{safeTitle}{yearSuffix}{suffix}.mkv";
            }
            default:
                return $"disc-rips/title_{titleIndex:D2}.mkv";
        }
    }

    private static string ParentRelative(string folderRelative)
    {
        int slash = folderRelative.LastIndexOf('/');
        return slash <= 0 ? "" : folderRelative[..slash];
    }

    /// <summary>
    /// Translates a folder-relative sub-path back to the host-visible path
    /// that the FileWatcher expects. Local storage exposes a real absolute
    /// path via <see cref="IStorage.GetFullPath"/>; remote drivers throw,
    /// in which case we fall back to the sub-path itself (which the watcher
    /// will treat as relative to its own scan root).
    /// </summary>
    private static string ResolveHostPath(IStorage storage, string subPath)
    {
        try
        {
            return storage.GetFullPath(subPath);
        }
        catch
        {
            return subPath;
        }
    }

    /// <summary>
    /// Strips characters that break common filesystems (NTFS, APFS) and the
    /// MediaScan regex. Keeps spaces, dots, parentheses, and dashes —
    /// matches the existing library naming convention seen in VideoFiles.
    /// </summary>
    private static string SanitizeForPath(string input)
    {
        string trimmed = InvalidFsChars().Replace(input, " ").Trim();
        return WhitespaceRun().Replace(trimmed, " ");
    }

    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")]
    private static partial Regex InvalidFsChars();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}

/// <summary>
/// Request body for <c>POST /optical/{drivePath}/confirm</c>.
/// The caller supplies the TMDB match they want applied along with the
/// rip output path, destination library/folder, and optional title metadata
/// for constructing the canonical file name.
/// </summary>
public record DiscConfirmRequest(
    string TmdbId,
    /// <summary>"movie" or "tv"</summary>
    string MediaType,
    string RipOutputPath,
    Ulid LibraryId,
    Ulid FolderId,
    string? Title = null,
    int? Year = null,
    string? PosterUrl = null,
    int? SeasonNumber = null,
    int? EpisodeNumber = null
);
