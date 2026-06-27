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
using Microsoft.Extensions.Logging;
using NoMercy.Api.Controllers.V1.Streaming.Dtos;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Devices;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.LiveTranscode.Protocol;
using NoMercy.Helpers.Extensions;
using NoMercy.Authorization;
using NoMercy.Resources;
using NoMercy.Storage;
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
    ILiveQualitySelector qualitySelector,
    IPlaybackDecisionEngine decisionEngine,
    SpeedIndex speedIndex,
    IResourceBudget budget,
    IDbContextFactory<MediaContext> contextFactory,
    LiveSessionLimits sessionLimits,
    IStorage storage,
    IDeviceCapabilityRegistry capabilityRegistry,
    IDeviceAwareVariantSelector variantSelector,
    ILogger<LiveTranscodeController> logger,
    ILiveSessionTransport? transport = null
) : BaseController
{
    [HttpGet("sessions")]
    public IActionResult ListSessions()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view live sessions");

        IReadOnlyList<LiveSessionSnapshot> snapshots = streamingService.GetActiveSessions();

        IEnumerable<LiveSessionDto> dtos = snapshots.Select(s => new LiveSessionDto(
            SessionId: s.SessionId,
            State: s.State.ToString(),
            QualityId: s.QualityId,
            QualityLabel: s.QualityLabel,
            Width: s.Width,
            Height: s.Height,
            BitrateKbps: s.BitrateKbps,
            PositionSeconds: s.PositionSeconds,
            BufferAheadSeconds: s.BufferAheadSeconds,
            SegmentCount: s.SegmentCount,
            IsComplete: s.IsComplete,
            LastAccess: s.LastAccess
        ));

        return Ok(dtos);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession(
        [FromBody] StartLiveSessionRequest request,
        [FromQuery] string? deviceId = null,
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
            storage,
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

        DeviceCapabilities? deviceCaps = null;
        if (deviceId is not null)
        {
            deviceCaps =
                capabilityRegistry.Get(deviceId)
                ?? await capabilityRegistry.LoadFromDbAsync(deviceId, ct);
        }

        ClientCapabilities clientCaps = variantSelector.ApplyDeviceCaps(
            ToClientCapabilities(request.ClientCaps),
            deviceCaps
        );

        if (deviceCaps is not null)
            logger.LogInformation(
                "Live session for device {DeviceId}: caps channels={Ch} ramTier={Tier}",
                deviceId,
                deviceCaps.MaxAudioChannels,
                deviceCaps.RamTier
            );

        PlaybackDecision playbackDecision;
        try
        {
            playbackDecision = decisionEngine.Decide(mediaInfo, clientCaps);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PlaybackDecisionEngine.Decide threw; falling back to transcode");
            playbackDecision = new(PlaybackAction.TranscodeVideo, "Decision engine error", null);
        }

        if (playbackDecision.Action == PlaybackAction.DirectPlay)
        {
            string directUrl = $"/{resolved.File.HostFolder}/{resolved.File.Filename}";

            logger.LogInformation("Direct-play for {VideoFileId}: {Url}", videoFileId, directUrl);

            return Ok(
                new StartLiveSessionResponse(
                    SessionId: string.Empty,
                    PlaylistUrl: string.Empty,
                    QualityId: string.Empty,
                    QualityLabel: string.Empty
                )
                {
                    Mode = "direct",
                    DirectStreamUrl = directUrl,
                    DirectPlayReason = "File is compatible with client capabilities",
                }
            );
        }

        LiveEncodeRequest liveRequest = new(
            InputPath: resolved.InputPath,
            CachedInfo: mediaInfo,
            Client: clientCaps,
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
            new StartLiveSessionResponse(session.SessionId, playlistUrl, quality.Id, quality.Label)
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

        if (!storage.Exists(segment.FilePath))
            return NotFoundResponse($"Segment {index} file missing on disk");

        runtime.TouchLastAccess();

        Response.Headers["Accept-Ranges"] = "bytes";
        Stream stream = storage.OpenRead(segment.FilePath);
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

    [HttpPost("sessions/{sessionId}/quality")]
    public async Task<IActionResult> ChangeQuality(
        string sessionId,
        [FromBody] ChangeQualityRequest request,
        CancellationToken ct = default
    )
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to stream media");

        if (string.IsNullOrWhiteSpace(request.QualityId))
            return BadRequestResponse("quality_id is required");

        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return NotFoundResponse("Live session not found");

        if (runtime.CachedMediaInfo is null || runtime.ClientCapabilities is null)
            return ServiceUnavailableResponse("Session context not available for quality change");

        LiveQuality[] available = qualitySelector.GetAvailableQualities(
            runtime.CachedMediaInfo,
            runtime.ClientCapabilities,
            speedIndex,
            budget
        );

        LiveQuality? newQuality = available.FirstOrDefault(q => q.Id == request.QualityId);
        if (newQuality is null)
            return NotFoundResponse(
                $"Quality '{request.QualityId}' is not available for this session"
            );

        await runtime.Session.ChangeQualityAsync(request.QualityId, newQuality, ct);

        runtime.TouchLastAccess();

        await PushIfTransportAsync(
                sessionId,
                new QualityChangedMessage(
                    NewQuality: newQuality,
                    Reason: QualityChangeReason.UserRequested
                ),
                ct
            )
            .ConfigureAwait(false);

        return Ok(new ChangeQualityResponse(newQuality.Id, newQuality.Label));
    }

    [HttpPost("sessions/{sessionId}/seek")]
    public async Task<IActionResult> Seek(
        string sessionId,
        [FromBody] SeekRequest request,
        CancellationToken ct = default
    )
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to stream media");

        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return NotFoundResponse("Live session not found");

        double clampedSeconds = Math.Max(0, request.PositionSeconds);

        await runtime.Session.SeekAsync(TimeSpan.FromSeconds(clampedSeconds), ct);

        runtime.TouchLastAccess();

        int firstSegmentIndex = runtime.HighestSegmentIndex + 1;
        await PushIfTransportAsync(
                sessionId,
                new SeekCompletedMessage(
                    NewPositionSeconds: clampedSeconds,
                    FirstSegmentIndex: firstSegmentIndex
                ),
                ct
            )
            .ConfigureAwait(false);

        return Ok(new SeekResponse(clampedSeconds));
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> EndSession(string sessionId)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to stream media");

        await PushIfTransportAsync(
                sessionId,
                new SessionEndedMessage(Reason: SessionEndReason.ClientDisconnected),
                HttpContext.RequestAborted
            )
            .ConfigureAwait(false);

        await streamingService.RemoveAsync(sessionId);
        sessionManager.RemoveSession(sessionId);
        return NoContent();
    }

    private async Task PushIfTransportAsync(string sessionId, object message, CancellationToken ct)
    {
        if (transport is null)
            return;

        try
        {
            await transport.SendToClientAsync(sessionId, message, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Transport push failed for {Event} on session {SessionId}",
                message.GetType().Name,
                sessionId
            );
        }
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
            MaxBitrateKbps: dto.MaxBitrateKbps,
            MaxAudioChannels: dto.MaxAudioChannels > 0 ? dto.MaxAudioChannels : 2
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
        IStorage fileStorage,
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

        string inputPath = fileStorage.CombinePath(file.HostFolder, file.Filename);
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
