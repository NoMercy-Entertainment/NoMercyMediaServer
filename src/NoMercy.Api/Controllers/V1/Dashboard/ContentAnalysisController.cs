using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.V1.Dashboard;

/// <summary>
/// On-demand content-analysis probes. Useful when dialing in profiles
/// (does this source actually have letterbox bars?) or debugging the
/// auto-detection pipeline without kicking off a full encode.
/// </summary>
[ApiController]
[Tags("Dashboard Content Analysis")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/content-analysis")]
public class ContentAnalysisController(
    ICropDetector cropDetector,
    IDbContextFactory<MediaContext> contextFactory
) : BaseController
{
    /// <summary>
    /// Runs the crop detector against a VideoFile by id and returns the
    /// detected rectangle (or <c>should_crop=false</c> if the frame is
    /// already letterbox-free). Ffmpeg-bound — can take up to 60 seconds
    /// on large sources. Owner-only to avoid DoS-by-probe.
    /// </summary>
    [HttpGet("crop/{videoFileId}")]
    public async Task<IActionResult> DetectCrop(string videoFileId, CancellationToken ct)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse("Only the server owner can probe crop detection");

        if (!Ulid.TryParse(videoFileId, out Ulid fileId))
            return BadRequestResponse("Invalid video file id");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        VideoFile? file = await context.VideoFiles.FirstOrDefaultAsync(v => v.Id == fileId, ct);

        if (file is null)
            return NotFoundResponse("Video file not found");

        string path = Path.Combine(file.HostFolder, file.Filename);
        if (!System.IO.File.Exists(path))
            return NotFoundResponse($"Source file missing on disk: {path}");

        try
        {
            CropResult result = await cropDetector.DetectAsync(path, ct);
            return Ok(
                new
                {
                    should_crop = result.ShouldCrop,
                    width = result.Width,
                    height = result.Height,
                    x = result.X,
                    y = result.Y,
                }
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse($"Crop detection failed: {ex.Message}");
        }
    }
}
