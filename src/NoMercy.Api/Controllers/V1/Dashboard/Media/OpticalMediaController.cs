// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.Helpers.Extensions;
using NoMercy.Authorization;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Live;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage;
using NoMercyQueue;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags("Dashboard Optical")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/optical")]
public class OpticalMediaController(
    DiscSourceFactory discSourceFactory,
    DiscIdentificationService identificationService,
    IDriveMonitor driveMonitor,
    IStorageFactory storageFactory,
    IStorageDriver localStorageDriver,
    IDbContextFactory<MediaContext> contextFactory,
    ILiveDiscSession liveDiscSession,
    ILiveStreamingService liveStreamingService,
    ISessionManager sessionManager,
    IDiscSessionRegistry discSessionRegistry
) : BaseController
{
    // ── Legacy endpoints (re-pointed to Module A) ──────────────────────────

    [HttpGet("drives")]
    public IActionResult GetOpticalDrives()
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view optical drives");

        IEnumerable<object> drives = driveMonitor
            .GetDrives()
            .Select(d => new
            {
                path = d.Path.TrimEnd(Path.DirectorySeparatorChar),
                label = d.Label,
                open = !d.HasDisc,
                has_disc = d.HasDisc,
                disc_type = d.DiscType.ToString().ToLowerInvariant(),
            });

        return Ok(drives);
    }

    [HttpGet("{drivePath}")]
    public async Task<IActionResult> GetDriveContents(string drivePath, CancellationToken ct)
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to view drive contents");

        DiscDrive? drive = FindDrive(drivePath);
        if (drive is null)
            return NotFoundResponse($"No optical drive found at {drivePath}");

        if (!drive.HasDisc)
            return Ok(
                new
                {
                    path = drive.Path.TrimEnd(Path.DirectorySeparatorChar),
                    label = drive.Label,
                    open = true,
                    has_disc = false,
                    disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                }
            );

        IDiscSource? source = discSourceFactory.CreateFor(drive.DiscType);
        if (source is null)
            return Ok(
                new
                {
                    path = drive.Path.TrimEnd(Path.DirectorySeparatorChar),
                    label = drive.Label,
                    open = false,
                    has_disc = true,
                    disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                }
            );

        DiscInfo info = await source.ProbeAsync(drive, ct);

        return Ok(
            new
            {
                path = drive.Path.TrimEnd(Path.DirectorySeparatorChar),
                label = info.DiscTitle ?? info.DiscLabel ?? drive.Label,
                open = false,
                has_disc = true,
                disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                disc = info,
            }
        );
    }

    [HttpPost("{drivePath}/process")]
    public IActionResult ProcessMedia(string drivePath)
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to process media");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        // Legacy stub — callers should use the /rip endpoint instead.
        // The old Module B ProcessMedia had DVD/CD stubs that logged
        // "not yet implemented" and returned. That same behaviour is
        // preserved here without pulling in dead code.
        return Ok("Use /rip to start a rip job.");
    }

    [HttpPost("{drivePath}/open")]
    public IActionResult OpenDrive(string drivePath)
    {
        if (!AuthPolicy.IsModerator(User))
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
        if (!AuthPolicy.IsModerator(User))
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
        CancellationToken ct
    )
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to play media");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        if (string.IsNullOrWhiteSpace(playlistId))
            return BadRequestResponse("Playlist ID is required");

        if (!int.TryParse(playlistId, out int titleIndex) || titleIndex < 0)
            return BadRequestResponse("playlistId must be a non-negative integer title index");

        DiscDrive? drive = FindDrive(drivePath);
        if (drive is null)
            return NotFoundResponse($"No optical drive found at {drivePath}");

        if (!drive.HasDisc)
            return BadRequestResponse($"No disc loaded in drive {drivePath}");

        if (!sessionManager.CanStartSession(User.UserId().ToString()))
            return ServiceUnavailableResponse("Maximum concurrent live sessions reached");

        ILiveSession session;
        try
        {
            session = await liveDiscSession.StartAsync(
                drive,
                titleIndex,
                TimeSpan.Zero,
                preferredQuality: null,
                ct
            );
        }
        catch (InvalidOperationException ex)
        {
            return ServiceUnavailableResponse(ex.Message);
        }

        sessionManager.RegisterSession(session, User.UserId().ToString());
        discSessionRegistry.Register(
            drive.Path.TrimEnd(Path.DirectorySeparatorChar),
            session.SessionId
        );

        string playlistUrl = $"/api/v1/streaming/live/sessions/{session.SessionId}/playlist.m3u8";
        LiveQuality quality = session.CurrentQuality;

        return Ok(
            new
            {
                session_id = session.SessionId,
                playlist_url = playlistUrl,
                quality_id = quality.Id,
                quality_label = quality.Label,
            }
        );
    }

    [HttpPost("{drivePath}/stop")]
    public async Task<IActionResult> StopMedia(string drivePath, CancellationToken ct)
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to stop media");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        string normalised = drivePath.TrimEnd(Path.DirectorySeparatorChar);

        if (!discSessionRegistry.TryGet(normalised, out string sessionId))
            return NotFoundResponse($"No active live session for drive {drivePath}");

        await liveStreamingService.RemoveAsync(sessionId);
        sessionManager.RemoveSession(sessionId);
        discSessionRegistry.Remove(normalised);

        return NoContent();
    }

    // ── New endpoints (Module A, unchanged) ───────────────────────────────

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
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to probe optical drives");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        DiscDrive? drive = FindDrive(drivePath);

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
        DiscIdentification identification = await identificationService.IdentifyAsync(info, ct);

        return Ok(
            new
            {
                drive_path = drive.Path,
                label = info.DiscTitle ?? info.DiscLabel ?? drive.Label,
                has_disc = true,
                disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                disc = info,
                candidates = identification.Candidates,
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
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to resolve disc metadata");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        DiscDrive? drive = FindDrive(drivePath);

        if (drive is null || !drive.HasDisc)
            return NotFoundResponse($"No disc loaded in {drivePath}");

        IDiscSource? source = discSourceFactory.CreateFor(drive.DiscType);
        if (source is null)
            return BadRequestResponse($"No reader registered for disc type {drive.DiscType}");

        DiscInfo info = await source.ProbeAsync(drive, ct);
        DiscIdentification identification = await identificationService.IdentifyAsync(info, ct);

        return Ok(
            new
            {
                drive_path = drive.Path,
                disc_duration_sec = info.MainTitleDurationSec,
                needs_manual = identification.NeedsManualAssignment,
                auto_apply = identification.AutoApply,
                candidates = identification
                    .Candidates.Take(5)
                    .Select(c => new
                    {
                        stable_id = c.StableId,
                        media_type = c.Type?.ToString().ToLowerInvariant(),
                        title = c.Title,
                        year = c.Year,
                        confidence = c.Confidence,
                        poster_url = c.PosterUrl,
                        backdrop_url = c.BackdropUrl,
                        season_number = c.SeasonNumber,
                        episode_number = c.EpisodeNumber,
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
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to confirm disc metadata");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        if (string.IsNullOrWhiteSpace(request.TmdbId))
            return BadRequestResponse("TmdbId is required");

        if (string.IsNullOrWhiteSpace(request.RipOutputPath))
            return BadRequestResponse("RipOutputPath is required");

        if (!System.IO.File.Exists(request.RipOutputPath))
            return NotFoundResponse($"Rip output not found at {request.RipOutputPath}");

        await using MediaContext db = await contextFactory.CreateDbContextAsync(ct);
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
            Stream dst = await folderStorage.OpenWriteAsync(
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
    /// Enqueues a durable <see cref="DiscRipJob"/> for the requested titles.
    /// Returns the job id the caller can use to poll rip progress on the
    /// <c>ripperHub</c>. Fails fast on DRM-locked discs and missing
    /// destination folders so the background job never starts in a doomed state.
    /// </summary>
    [HttpPost("{drivePath}/rip")]
    public async Task<IActionResult> RipDisc(
        string drivePath,
        [FromBody] RipRequest request,
        CancellationToken ct
    )
    {
        if (!AuthPolicy.IsModerator(User))
            return UnauthorizedResponse("You do not have permission to rip optical drives");

        if (string.IsNullOrWhiteSpace(drivePath))
            return BadRequestResponse("Drive path is required");

        DiscDrive? drive = FindDrive(drivePath);

        if (drive is null || !drive.HasDisc)
            return NotFoundResponse($"No disc loaded in {drivePath}");

        // For CD discs, an empty SelectedTitleIndices is fine — the endpoint
        // defaults to all probed tracks further below. Video discs must supply at least one.
        if (request.SelectedTitleIndices.Length == 0 && drive.DiscType != OpticalDiscType.Cd)
            return BadRequestResponse("At least one title must be selected");

        // Fail fast if the disc is DRM-locked the host can't read.
        IDiscSource? source = discSourceFactory.CreateFor(drive.DiscType);
        if (source is not null)
        {
            DiscInfo precheck = await source.ProbeAsync(drive, ct);
            if (precheck.Protection is not null)
                return BadRequestResponse(
                    $"Cannot rip — disc is {precheck.Protection.Kind}-protected: {precheck.Protection.Message}"
                );
        }

        // Validate destination for RipAndEncode up front.
        Folder? targetFolder = null;
        Library? targetLibrary = null;
        if (request.Mode == RipMode.RipAndEncode)
        {
            await using MediaContext lookupContext = await contextFactory.CreateDbContextAsync(ct);
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

        string sanitisedDrive = drive
            .Path.TrimEnd(Path.DirectorySeparatorChar)
            .Replace(":", "")
            .Replace(Path.DirectorySeparatorChar, '_');
        string outputDir = Path.Combine(AppFiles.TranscodePath, "ripper", sanitisedDrive);
        Directory.CreateDirectory(outputDir);

        // For audio CDs, default to all probed tracks when the caller sent
        // no SelectedTitleIndices (CD tracks don't map to video-title semantics).
        RipRequest enriched = request with
        {
            DiscType = drive.DiscType,
        };

        if (drive.DiscType == OpticalDiscType.Cd && enriched.SelectedTitleIndices.Length == 0)
        {
            IDiscSource? cdSource = discSourceFactory.CreateFor(OpticalDiscType.Cd);
            if (cdSource is not null)
            {
                DiscInfo cdInfo = await cdSource.ProbeAsync(drive, ct);
                if (cdInfo.AudioTracks is { Length: > 0 })
                {
                    enriched = enriched with
                    {
                        SelectedTitleIndices = cdInfo.AudioTracks.Select(t => t.Index).ToArray(),
                    };
                }
            }
        }

        DiscRipJob job = new(
            enriched,
            outputDir,
            targetFolder?.Id,
            targetLibrary?.Id,
            targetLibrary?.Type
        );

        QueueRunner.Current!.Dispatcher.Dispatch(job);

        return Accepted(
            new
            {
                job_id = job.JobId,
                drive_path = drive.Path,
                output_dir = outputDir,
                titles_queued = request.SelectedTitleIndices.Length,
                mode = request.Mode.ToString(),
            }
        );
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private DiscDrive? FindDrive(string drivePath) =>
        driveMonitor
            .GetDrives()
            .FirstOrDefault(d =>
                d.Path.TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(
                        drivePath.TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase
                    )
            );

    private static string BuildOutputPath(
        RipRequest request,
        string libraryType,
        int titleIndex,
        int batchIndex
    ) => RipOutputPathHelper.Build(request, libraryType, titleIndex, batchIndex);

    private static string ParentRelative(string folderRelative)
    {
        int slash = folderRelative.LastIndexOf('/');
        return slash <= 0 ? "" : folderRelative[..slash];
    }

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
}

/// <summary>
/// Request body for <c>POST /optical/{drivePath}/confirm</c>.
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
