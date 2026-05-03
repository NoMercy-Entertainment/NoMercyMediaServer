using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaSources.OpticalMedia;
using NoMercy.MediaSources.OpticalMedia.Dto;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Live;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using DriveMonitor = NoMercy.MediaSources.OpticalMedia.DriveMonitor;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Optical")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/optical")]
public class OpticalMediaController(
    DiscSourceFactory discSourceFactory,
    IDiscMetadataResolver metadataResolver,
    IDriveMonitor driveMonitor,
    IDiscRipper discRipper,
    JobDispatcher jobDispatcher,
    ILiveDiscSession liveDiscSession
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

        // Resolve the rip output dir under the per-drive ripper folder. Phase
        // A.9 will move this to IStorage; for now use AppFiles to mirror the
        // existing legacy DriveMonitor.PlayDvd / PlayCd output path.
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

        // Spawn the rip in the background — the caller polls progress via
        // SignalR (Phase E.3) or the encoding history endpoints once each
        // ripped MKV is enqueued as a VideoEncodeJob below.
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

                    // Only chain into the encoder for the default rip-and-encode
                    // mode. RipToRaw leaves the MKV in the ripper folder for
                    // the user to grab manually.
                    if (enriched.Mode != RipMode.RipAndEncode)
                        return;

                    foreach (DiscRipResult res in results.Where(r => r.Success))
                    {
                        // The rip output sits on the local drive's transcode
                        // path — pass sourceDriverId=null so JobDispatcher
                        // routes it through the default local IStorage.
                        jobDispatcher.DispatchJob<VideoEncodeJob>(
                            enriched.LibraryId,
                            enriched.FolderId,
                            id: $"disc:{drive.Path.TrimEnd('\\')}:{res.TitleIndex}",
                            inputFile: res.OutputPath,
                            sourceDriverId: null
                        );
                    }
                }
                catch (Exception ex)
                {
                    // The DiscRipper logs internally — keep the task body
                    // resilient so a rip crash doesn't take down the host.
                    _ = ex;
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
}
