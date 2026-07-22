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
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
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
[Tags(tags: "Dashboard Optical")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/dashboard/optical")]
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

    [HttpGet(template: "drives")]
    public IActionResult GetOpticalDrives()
    {
        IEnumerable<object> drives = driveMonitor
            .GetDrives()
            .Select(selector: d => new
            {
                path = d.Path.TrimEnd(trimChar: Path.DirectorySeparatorChar),
                label = d.Label,
                open = !d.HasDisc,
                has_disc = d.HasDisc,
                disc_type = d.DiscType.ToString().ToLowerInvariant(),
            });

        return Ok(value: drives);
    }

    [HttpGet(template: "{drivePath}")]
    public async Task<IActionResult> GetDriveContents(string drivePath, CancellationToken ct)
    {
        DiscDrive? drive = FindDrive(drivePath: drivePath);
        if (drive is null)
            return NotFoundResponse(detail: $"No optical drive found at {drivePath}");

        if (!drive.HasDisc)
            return Ok(
                value: new
                {
                    path = drive.Path.TrimEnd(trimChar: Path.DirectorySeparatorChar),
                    label = drive.Label,
                    open = true,
                    has_disc = false,
                    disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                }
            );

        IDiscSource? source = discSourceFactory.CreateFor(type: drive.DiscType);
        if (source is null)
            return Ok(
                value: new
                {
                    path = drive.Path.TrimEnd(trimChar: Path.DirectorySeparatorChar),
                    label = drive.Label,
                    open = false,
                    has_disc = true,
                    disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                }
            );

        DiscInfo info = await source.ProbeAsync(drive: drive, ct: ct);

        return Ok(
            value: new
            {
                path = drive.Path.TrimEnd(trimChar: Path.DirectorySeparatorChar),
                label = info.DiscTitle ?? info.DiscLabel ?? drive.Label,
                open = false,
                has_disc = true,
                disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                disc = info,
            }
        );
    }

    [HttpPost(template: "{drivePath}/process")]
    public IActionResult ProcessMedia(string drivePath)
    {
        if (string.IsNullOrWhiteSpace(value: drivePath))
            return BadRequestResponse(detail: "Drive path is required");

        // Legacy stub — callers should use the /rip endpoint instead.
        // The old Module B ProcessMedia had DVD/CD stubs that logged
        // "not yet implemented" and returned. That same behaviour is
        // preserved here without pulling in dead code.
        return Ok(value: "Use /rip to start a rip job.");
    }

    [HttpPost(template: "{drivePath}/open")]
    public IActionResult OpenDrive(string drivePath)
    {
        if (string.IsNullOrWhiteSpace(value: drivePath))
            return BadRequestResponse(detail: "Drive path is required");

        bool success = Optical.OpenDrive(drivePath: drivePath);

        if (!success)
            return BadRequestResponse(detail: "Failed to open drive");

        return Ok(value: "Drive opened.");
    }

    [HttpPost(template: "{drivePath}/close")]
    public IActionResult CloseDrive(string drivePath)
    {
        if (string.IsNullOrWhiteSpace(value: drivePath))
            return BadRequestResponse(detail: "Drive path is required");

        bool success = Optical.CloseDrive(drivePath: drivePath);

        if (!success)
            return BadRequestResponse(detail: "Failed to close drive");

        return Ok(value: "Drive closed.");
    }

    [HttpPost(template: "{drivePath}/play/{playlistId}")]
    public async Task<IActionResult> PlayMedia(
        string drivePath,
        string playlistId,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(value: drivePath))
            return BadRequestResponse(detail: "Drive path is required");

        if (string.IsNullOrWhiteSpace(value: playlistId))
            return BadRequestResponse(detail: "Playlist ID is required");

        if (!int.TryParse(s: playlistId, result: out int titleIndex) || titleIndex < 0)
            return BadRequestResponse(detail: "playlistId must be a non-negative integer title index");

        DiscDrive? drive = FindDrive(drivePath: drivePath);
        if (drive is null)
            return NotFoundResponse(detail: $"No optical drive found at {drivePath}");

        if (!drive.HasDisc)
            return BadRequestResponse(detail: $"No disc loaded in drive {drivePath}");

        if (!sessionManager.CanStartSession(userId: User.UserId().ToString()))
            return ServiceUnavailableResponse(detail: "Maximum concurrent live sessions reached");

        ILiveSession session;
        try
        {
            session = await liveDiscSession.StartAsync(
                drive: drive,
                titleIndex: titleIndex,
                startPosition: TimeSpan.Zero,
                preferredQuality: null,
                ct: ct
            );
        }
        catch (InvalidOperationException ex)
        {
            return ServiceUnavailableResponse(detail: ex.Message);
        }

        sessionManager.RegisterSession(session: session, userId: User.UserId().ToString());
        discSessionRegistry.Register(
            drivePath: drive.Path.TrimEnd(trimChar: Path.DirectorySeparatorChar),
            sessionId: session.SessionId
        );

        string playlistUrl = $"/api/v1/streaming/live/sessions/{session.SessionId}/playlist.m3u8";
        LiveQuality quality = session.CurrentQuality;

        return Ok(
            value: new
            {
                session_id = session.SessionId,
                playlist_url = playlistUrl,
                quality_id = quality.Id,
                quality_label = quality.Label,
            }
        );
    }

    [HttpPost(template: "{drivePath}/stop")]
    public async Task<IActionResult> StopMedia(string drivePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value: drivePath))
            return BadRequestResponse(detail: "Drive path is required");

        string normalised = drivePath.TrimEnd(trimChar: Path.DirectorySeparatorChar);

        if (!discSessionRegistry.TryGet(drivePath: normalised, sessionId: out string sessionId))
            return NotFoundResponse(detail: $"No active live session for drive {drivePath}");

        await liveStreamingService.RemoveAsync(sessionId: sessionId);
        sessionManager.RemoveSession(sessionId: sessionId);
        discSessionRegistry.Remove(drivePath: normalised);

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
    [HttpGet(template: "{drivePath}/probe")]
    public async Task<IActionResult> ProbeDisc(string drivePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value: drivePath))
            return BadRequestResponse(detail: "Drive path is required");

        DiscDrive? drive = FindDrive(drivePath: drivePath);

        if (drive is null)
            return NotFoundResponse(detail: $"No optical drive found at {drivePath}");

        if (!drive.HasDisc || drive.DiscType == OpticalDiscType.None)
            return Ok(
                value: new
                {
                    drive_path = drive.Path,
                    label = drive.Label,
                    has_disc = false,
                    disc_type = drive.DiscType.ToString().ToLowerInvariant(),
                }
            );

        IDiscSource? source = discSourceFactory.CreateFor(type: drive.DiscType);
        if (source is null)
            return BadRequestResponse(detail: $"No reader registered for disc type {drive.DiscType} (yet)");

        DiscInfo info = await source.ProbeAsync(drive: drive, ct: ct);
        DiscIdentification identification = await identificationService.IdentifyAsync(disc: info, ct: ct);

        return Ok(
            value: new
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
    [HttpPost(template: "{drivePath}/resolve")]
    public async Task<IActionResult> ResolveDisc(string drivePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value: drivePath))
            return BadRequestResponse(detail: "Drive path is required");

        DiscDrive? drive = FindDrive(drivePath: drivePath);

        if (drive is null || !drive.HasDisc)
            return NotFoundResponse(detail: $"No disc loaded in {drivePath}");

        IDiscSource? source = discSourceFactory.CreateFor(type: drive.DiscType);
        if (source is null)
            return BadRequestResponse(detail: $"No reader registered for disc type {drive.DiscType}");

        DiscInfo info = await source.ProbeAsync(drive: drive, ct: ct);
        DiscIdentification identification = await identificationService.IdentifyAsync(disc: info, ct: ct);

        return Ok(
            value: new
            {
                drive_path = drive.Path,
                disc_duration_sec = info.MainTitleDurationSec,
                needs_manual = identification.NeedsManualAssignment,
                auto_apply = identification.AutoApply,
                candidates = identification
                    .Candidates.Take(count: 5)
                    .Select(selector: c => new
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
    [HttpPost(template: "{drivePath}/confirm")]
    public async Task<IActionResult> ConfirmDisc(
        string drivePath,
        [FromBody] DiscConfirmRequest request,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(value: drivePath))
            return BadRequestResponse(detail: "Drive path is required");

        if (string.IsNullOrWhiteSpace(value: request.TmdbId))
            return BadRequestResponse(detail: "TmdbId is required");

        if (string.IsNullOrWhiteSpace(value: request.RipOutputPath))
            return BadRequestResponse(detail: "RipOutputPath is required");

        string ripStagingRoot = Path.Combine(path1: AppFiles.TranscodePath, path2: "ripper");
        if (!RipStagingPath.IsWithinStaging(ripOutputPath: request.RipOutputPath, stagingRoot: ripStagingRoot))
            return BadRequestResponse(
                detail: "RipOutputPath must be inside the server rip staging directory"
            );

        if (!System.IO.File.Exists(path: request.RipOutputPath))
            return NotFoundResponse(detail: $"Rip output not found at {request.RipOutputPath}");

        await using MediaContext db = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        LibraryRepository libraryRepository = new(context: db, storageDriver: localStorageDriver);

        Folder? targetFolder = await libraryRepository.GetLibraryFolder(folderId: request.FolderId);
        if (targetFolder is null)
            return BadRequestResponse(
                detail: $"FolderId {request.FolderId} does not match any library folder"
            );

        Library? targetLibrary = await libraryRepository.GetLibraryByIdWithFolders(
            libraryId: request.LibraryId
        );
        if (targetLibrary is null)
            return BadRequestResponse(detail: $"LibraryId {request.LibraryId} does not match any library");

        CustomMetadata meta =
            request.MediaType == "tv"
                ? new(
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

        string folderRelative = BuildOutputPath(request: syntheticRequest, libraryType: targetLibrary.Type, titleIndex: 0, batchIndex: 0);

        IStorage folderStorage = storageFactory.For(
            folderId: targetFolder.Id,
            driverId: targetFolder.DriverId,
            subPath: string.Empty
        );

        string parentRelative = ParentRelative(folderRelative: folderRelative);
        if (!string.IsNullOrEmpty(value: parentRelative))
            await folderStorage.CreateDirectoryAsync(path: parentRelative, ct: ct);

        await using (FileStream src = new(path: request.RipOutputPath, mode: FileMode.Open, access: FileAccess.Read))
        await using (
            Stream dst = await folderStorage.OpenWriteAsync(path: folderRelative, overwrite: true, ct: ct)
        )
        {
            await src.CopyToAsync(destination: dst, cancellationToken: ct);
        }

        try
        {
            System.IO.File.Delete(path: request.RipOutputPath);
        }
        catch
        {
            // best effort
        }

        string watcherFolderHost = ResolveHostPath(storage: folderStorage, subPath: parentRelative);
        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new FileCreatedEvent
                {
                    FolderPath = watcherFolderHost,
                    LibraryId = targetLibrary.Id,
                    LibraryType = targetLibrary.Type,
                }
            );
        }

        return Ok(
            value: new
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
    [HttpPost(template: "{drivePath}/rip")]
    public async Task<IActionResult> RipDisc(
        string drivePath,
        [FromBody] RipRequest request,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(value: drivePath))
            return BadRequestResponse(detail: "Drive path is required");

        DiscDrive? drive = FindDrive(drivePath: drivePath);

        if (drive is null || !drive.HasDisc)
            return NotFoundResponse(detail: $"No disc loaded in {drivePath}");

        // For CD discs, an empty SelectedTitleIndices is fine — the endpoint
        // defaults to all probed tracks further below. Video discs must supply at least one.
        if (request.SelectedTitleIndices.Length == 0 && drive.DiscType != OpticalDiscType.Cd)
            return BadRequestResponse(detail: "At least one title must be selected");

        // Fail fast if the disc is DRM-locked the host can't read.
        IDiscSource? source = discSourceFactory.CreateFor(type: drive.DiscType);
        if (source is not null)
        {
            DiscInfo precheck = await source.ProbeAsync(drive: drive, ct: ct);
            if (precheck.Protection is not null)
                return BadRequestResponse(
                    detail: $"Cannot rip — disc is {precheck.Protection.Kind}-protected: {precheck.Protection.Message}"
                );
        }

        // Validate destination for RipAndEncode up front.
        Folder? targetFolder = null;
        Library? targetLibrary = null;
        if (request.Mode == RipMode.RipAndEncode)
        {
            await using MediaContext lookupContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            LibraryRepository libraryRepository = new(context: lookupContext, storageDriver: localStorageDriver);
            targetFolder = await libraryRepository.GetLibraryFolder(folderId: request.FolderId);
            if (targetFolder is null)
                return BadRequestResponse(
                    detail: $"FolderId {request.FolderId} does not match any library folder. "
                            + "RipAndEncode needs a real folder so the rip output lands somewhere "
                            + "the encoder can read it via the folder's driver."
                );
            targetLibrary = await libraryRepository.GetLibraryByIdWithFolders(libraryId: request.LibraryId);
            if (targetLibrary is null)
                return BadRequestResponse(
                    detail: $"LibraryId {request.LibraryId} does not match any library."
                );
        }

        string sanitisedDrive = drive
            .Path.TrimEnd(trimChar: Path.DirectorySeparatorChar)
            .Replace(oldValue: ":", newValue: "")
            .Replace(oldChar: Path.DirectorySeparatorChar, newChar: '_');
        string outputDir = Path.Combine(path1: AppFiles.TranscodePath, path2: "ripper", path3: sanitisedDrive);
        Directory.CreateDirectory(path: outputDir);

        // For audio CDs, default to all probed tracks when the caller sent
        // no SelectedTitleIndices (CD tracks don't map to video-title semantics).
        RipRequest enriched = request with
        {
            DiscType = drive.DiscType,
        };

        if (drive.DiscType == OpticalDiscType.Cd && enriched.SelectedTitleIndices.Length == 0)
        {
            IDiscSource? cdSource = discSourceFactory.CreateFor(type: OpticalDiscType.Cd);
            if (cdSource is not null)
            {
                DiscInfo cdInfo = await cdSource.ProbeAsync(drive: drive, ct: ct);
                if (cdInfo.AudioTracks is { Length: > 0 })
                {
                    enriched = enriched with
                    {
                        SelectedTitleIndices = cdInfo.AudioTracks.Select(selector: t => t.Index).ToArray(),
                    };
                }
            }
        }

        DiscRipJob job = new(
            request: enriched,
            outputDir: outputDir,
            targetFolderId: targetFolder?.Id,
            targetLibraryId: targetLibrary?.Id,
            targetLibraryType: targetLibrary?.Type
        );

        QueueRunner.Current!.Dispatcher.Dispatch(job: job);

        return Accepted(
            value: new
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
            .FirstOrDefault(predicate: d =>
                d.Path.TrimEnd(trimChar: Path.DirectorySeparatorChar)
                    .Equals(
                        value: drivePath.TrimEnd(trimChar: Path.DirectorySeparatorChar),
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    )
            );

    private static string BuildOutputPath(
        RipRequest request,
        string libraryType,
        int titleIndex,
        int batchIndex
    ) => RipOutputPathHelper.Build(request: request, libraryType: libraryType, titleIndex: titleIndex, batchIndex: batchIndex);

    private static string ParentRelative(string folderRelative)
    {
        int slash = folderRelative.LastIndexOf(value: '/');
        return slash <= 0 ? "" : folderRelative[..slash];
    }

    private static string ResolveHostPath(IStorage storage, string subPath)
    {
        try
        {
            return storage.GetFullPath(path: subPath);
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
