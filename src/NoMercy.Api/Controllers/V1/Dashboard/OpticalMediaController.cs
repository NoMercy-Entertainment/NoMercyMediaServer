using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.MediaSources.OpticalMedia;
using NoMercy.MediaSources.OpticalMedia.Dto;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Optical")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/optical")]
public class OpticalMediaController : BaseController
{
    [HttpGet("drives")]
    public IActionResult GetOpticalDrives()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view optical drives");

        IEnumerable<DriveState> drives = Optical.GetOpticalDrives()
            .Select(drive => new DriveState
            {
                Path = drive.Key.TrimEnd(Path.DirectorySeparatorChar),
                Label = drive.Value,
                Open = drive.Value == null,
                MetaData = DriveMonitor.Contents.FirstOrDefault(x => x.Path == drive.Key)
            });

        return Ok(drives);
    }

    [HttpGet("{drivePath}")]
    public async Task<IActionResult> GetDriveContents(string drivePath)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view drive contents");

        MetaData? metadata = await DriveMonitor.GetDriveMetadata(drivePath);
        if (metadata == null) return NotFound("Drive metadata not found.");

        return Ok(new DriveState
        {
            Open = false,
            Path = drivePath.TrimEnd(Path.DirectorySeparatorChar),
            Label = metadata.Title,
            MetaData = metadata
        });
    }

    [HttpPost("{drivePath}/process")]
    public IActionResult ProcessMedia(string drivePath, [FromBody] MediaProcessingRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to process media");

        if (string.IsNullOrWhiteSpace(drivePath)) return BadRequest("Drive path is required.");

        _ = DriveMonitor.ProcessMedia(drivePath, request);

        return Ok("Processing started.");
    }

    [HttpPost("{drivePath}/open")]
    public IActionResult OpenDrive(string drivePath)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to open drive");

        if (string.IsNullOrWhiteSpace(drivePath)) return BadRequest("Drive path is required.");

        bool success = Optical.OpenDrive(drivePath);

        if (!success) return BadRequest("Failed to open drive.");

        return Ok("Drive opened.");
    }

    [HttpPost("{drivePath}/close")]
    public IActionResult CloseDrive(string drivePath)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to close drive");

        if (string.IsNullOrWhiteSpace(drivePath)) return BadRequest("Drive path is required.");

        bool success = Optical.CloseDrive(drivePath);

        if (!success) return BadRequest("Failed to close drive.");

        return Ok("Drive closed.");
    }

    [HttpPost("{drivePath}/play/{playlistId}")]
    public async Task<IActionResult> PlayMedia(string drivePath, string playlistId,
        CancellationTokenSource cancellationTokenSource)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to play media");

        if (string.IsNullOrWhiteSpace(drivePath)) return BadRequest("Drive path is required.");

        if (string.IsNullOrWhiteSpace(playlistId)) return BadRequest("PlaylistId is required.");

        await DriveMonitor.PlayMedia(drivePath, playlistId, cancellationTokenSource);

        return Ok("Playing media.");
    }

    [HttpPost("{drivePath}/stop")]
    public IActionResult StopMedia(string drivePath)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to stop media");

        if (string.IsNullOrWhiteSpace(drivePath)) return BadRequest("Drive path is required.");

        _ = DriveMonitor.StopMedia();

        return Ok("Media stopped.");
    }
}