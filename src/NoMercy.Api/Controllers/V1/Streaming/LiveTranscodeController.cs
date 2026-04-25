using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Streaming.Dtos;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Helpers.Extensions;
using EncoderMediaInfo = NoMercy.Encoder.Analysis.MediaInfo;

namespace NoMercy.Api.Controllers.V1.Streaming;

[ApiController]
[Tags("Streaming")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/streaming/live")]
public class LiveTranscodeController(
    ILiveEncoder liveEncoder,
    ILiveStreamingService streamingService,
    ILivePlaylistBuilder playlistBuilder,
    ISessionManager sessionManager,
    IMediaAnalyzer mediaAnalyzer,
    IDbContextFactory<MediaContext> contextFactory,
    LiveSessionLimits sessionLimits
) : BaseController
{
    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession(
        [FromBody] StartLiveSessionRequest request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to stream media");

        if (string.IsNullOrWhiteSpace(request.VideoFileId))
            return BadRequestResponse("video_file_id is required");

        if (request.ClientCaps is null)
            return BadRequestResponse("client_caps is required");

        if (!Ulid.TryParse(request.VideoFileId, out Ulid videoFileId))
            return BadRequestResponse("video_file_id is not a valid identifier");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);

        AuthorizedFile? resolved = await ResolveAuthorizedFileAsync(
            context,
            videoFileId,
            userId,
            ct
        );

        if (resolved is null)
            return NotFoundResponse("Video file not found or you lack access");

        if (!sessionManager.CanStartSession(userId.ToString()))
            return ServiceUnavailableResponse("Maximum concurrent live sessions reached");

        EncoderMediaInfo mediaInfo;
        try
        {
            mediaInfo = await mediaAnalyzer.AnalyzeAsync(resolved.InputPath, ct);
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse(
                $"Could not analyze input for live transcode: {ex.Message}"
            );
        }

        LiveEncodeRequest liveRequest = new(
            InputPath: resolved.InputPath,
            CachedInfo: mediaInfo,
            Client: ToClientCapabilities(request.ClientCaps),
            StartPosition: TimeSpan.FromSeconds(Math.Max(0, request.StartTimeSeconds)),
            PreferredQuality: request.PreferredQuality
        );

        ILiveSession session;
        try
        {
            session = await liveEncoder.StartAsync(liveRequest, ct);
        }
        catch (EncoderRuntimeException ex)
        {
            return StatusCode(ex.HttpStatusCode, ex.Shape);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceUnavailableResponse(ex.Message);
        }

        // LiveEncoder.StartAsync already registers the session with the
        // session manager + streaming service. We just re-tag it with the
        // user id so the per-user session cap works.
        sessionManager.RegisterSession(session, userId.ToString());

        string playlistUrl = $"/api/v1/streaming/live/sessions/{session.SessionId}/playlist.m3u8";

        LiveQuality quality = session.CurrentQuality;
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(sessionLimits.IdleTimeoutMinutes);

        SelectedVariantDto selectedVariant = new(
            Codec: quality.Codec.ToString(),
            Width: quality.Width,
            Height: quality.Height,
            BitrateKbps: quality.BitrateKbps
        );

        return Ok(
            new StartLiveSessionResponse(
                session.SessionId,
                playlistUrl,
                quality.Id,
                quality.Label
            )
            {
                SelectedVariant = selectedVariant,
                ExpiresAt = expiresAt,
            }
        );
    }

    [HttpGet("sessions/{sessionId}/playlist.m3u8")]
    public IActionResult GetPlaylist(string sessionId)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to stream media");

        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return NotFoundResponse("Live session not found");

        runtime.TouchLastAccess();

        string segmentUrlTemplate =
            $"/api/v1/streaming/live/sessions/{sessionId}/segment/{{index}}.ts";

        LivePlaylistRequest request = new(
            SessionId: sessionId,
            Segments: runtime.SnapshotSegments(),
            TargetSegmentDuration: runtime.TargetSegmentDuration,
            IsComplete: runtime.IsComplete,
            SegmentUrlTemplate: segmentUrlTemplate
        );

        string playlist = playlistBuilder.Build(request);
        return Content(playlist, "application/vnd.apple.mpegurl");
    }

    [HttpGet("sessions/{sessionId}/segment/{index:int}.ts")]
    public IActionResult GetSegment(string sessionId, int index)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to stream media");

        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return NotFoundResponse("Live session not found");

        if (!runtime.TryGetSegment(index, out Segment segment))
            return NotFoundResponse($"Segment {index} is not ready yet");

        if (!System.IO.File.Exists(segment.FilePath))
            return NotFoundResponse($"Segment {index} file missing on disk");

        runtime.TouchLastAccess();

        Response.Headers["Accept-Ranges"] = "bytes";
        FileStream stream = System.IO.File.OpenRead(segment.FilePath);
        return File(stream, "video/mp2t", enableRangeProcessing: true);
    }

    [HttpPost("sessions/{sessionId}/position")]
    public IActionResult ReportPosition(string sessionId, [FromBody] ReportPositionRequest request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to stream media");

        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return NotFoundResponse("Live session not found");

        double clampedSeconds = Math.Max(0, request.TimeSeconds);
        runtime.Session.ReportPlaybackPosition(TimeSpan.FromSeconds(clampedSeconds));

        bool isPaused = runtime.Session.State == LiveSessionState.Buffered;

        return Ok(new ReportPositionResponse(clampedSeconds, isPaused));
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> EndSession(string sessionId)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to stream media");

        await streamingService.RemoveAsync(sessionId);
        sessionManager.RemoveSession(sessionId);
        return NoContent();
    }

    private static ClientCapabilities ToClientCapabilities(ClientCapabilitiesDto dto)
    {
        return new(
            SupportedVideoCodecs: dto.VideoCodecs ?? [],
            SupportedAudioCodecs: dto.AudioCodecs ?? [],
            SupportedContainers: dto.Containers ?? [],
            MaxWidth: dto.MaxWidth,
            MaxHeight: dto.MaxHeight,
            SupportsHdr: dto.SupportsHdr,
            Supports10Bit: dto.Supports10Bit,
            MaxBitrateKbps: dto.MaxBitrateKbps
        );
    }

    /// <summary>
    /// Resolves a <see cref="VideoFile"/> by id and verifies the caller has
    /// access to the library containing the file. Returns null when the file
    /// doesn't exist, is unlinked from any media, or the user is not in the
    /// owning library's LibraryUsers set.
    /// </summary>
    private static async Task<AuthorizedFile?> ResolveAuthorizedFileAsync(
        MediaContext context,
        Ulid videoFileId,
        Guid userId,
        CancellationToken ct
    )
    {
        VideoFile? file = await context
            .VideoFiles.AsNoTracking()
            .FirstOrDefaultAsync(vf => vf.Id == videoFileId, ct);

        if (file is null)
            return null;

        bool allowed = await UserHasAccessAsync(context, file, userId, ct);
        if (!allowed)
            return null;

        string inputPath = Path.Combine(file.HostFolder, file.Filename);
        return new(file, inputPath);
    }

    private static async Task<bool> UserHasAccessAsync(
        MediaContext context,
        VideoFile file,
        Guid userId,
        CancellationToken ct
    )
    {
        if (file.MovieId is int movieId)
        {
            bool fromMovie = await context.Movies.AnyAsync(
                m => m.Id == movieId && m.Library.LibraryUsers.Any(u => u.UserId == userId),
                ct
            );
            if (fromMovie)
                return true;
        }

        if (file.EpisodeId is int episodeId)
        {
            bool fromEpisode = await context.Episodes.AnyAsync(
                e => e.Id == episodeId && e.Tv.Library.LibraryUsers.Any(u => u.UserId == userId),
                ct
            );
            if (fromEpisode)
                return true;
        }

        return false;
    }

    private sealed record AuthorizedFile(VideoFile File, string InputPath);
}
