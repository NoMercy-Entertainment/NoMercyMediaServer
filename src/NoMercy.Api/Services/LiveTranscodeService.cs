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
using NoMercy.NmSystem.Configuration;
using NoMercy.Storage;
using NoMercyQueue.Core.Resources;
using EncoderMediaInfo = NoMercy.Encoder.Analysis.MediaInfo;

namespace NoMercy.Api.Services;

/// <inheritdoc />
public class LiveTranscodeService(
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
    ILogger<LiveTranscodeService> logger,
    ILiveSessionTransport? transport = null
) : ILiveTranscodeService
{
    // How long a segment request blocks waiting for the encoder to produce the
    // requested (on-demand) segment before giving up, and how often it rechecks.
    private static readonly TimeSpan SegmentWaitTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SegmentPollInterval = TimeSpan.FromMilliseconds(200);

    public IReadOnlyList<LiveSessionDto> ListSessions()
    {
        IReadOnlyList<LiveSessionSnapshot> snapshots = streamingService.GetActiveSessions();
        return snapshots
            .Select(s => new LiveSessionDto(
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
            ))
            .ToList();
    }

    public async Task<LiveResult> StartSessionAsync(
        Guid userId,
        StartLiveSessionRequest request,
        string? deviceId,
        string? accessToken,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(request.VideoFileId))
            return LiveResult.BadRequest("video_file_id is required");

        if (request.ClientCaps is null)
            return LiveResult.BadRequest("client_caps is required");

        if (!Ulid.TryParse(request.VideoFileId, out Ulid videoFileId))
            return LiveResult.BadRequest("video_file_id is not a valid identifier");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        AuthorizedFile? resolved = await ResolveAuthorizedFileAsync(
            context,
            videoFileId,
            userId,
            accessToken,
            ct
        );
        if (resolved is null)
            return LiveResult.NotFound("Video file not found or you lack access");

        // Reconcile accounting against the live runtimes first: a crashed FFmpeg or
        // an abandoned tab can leave a session counted against the cap after its
        // runtime is gone — the "0 active but max reached" symptom.
        sessionManager.PruneDeadSessions(streamingService.ActiveSessionIds);

        if (!sessionManager.CanStartSession(userId.ToString()))
        {
            // Still at the cap with live sessions. Starting playback abandons the
            // previous stream without a clean stop (a reload or item switch), so
            // evict this user's stalest session to make room — the same way Plex and
            // Jellyfin supersede a replaced playback session rather than refusing.
            await EvictStalestUserSessionAsync(userId.ToString());

            if (!sessionManager.CanStartSession(userId.ToString()))
                return LiveResult.ServiceUnavailable("Maximum concurrent live sessions reached");
        }

        // The source codec is what drives the direct-play-vs-transcode decision, and
        // NoMercy's own HLS output is frequently HEVC 10-bit — the exact thing a
        // browser without HEVC needs transcoded — so the file MUST be probed, an
        // ".m3u8" name proves nothing about compatibility. A probe failure (backend
        // unreachable, transient NFS/S3 error, corrupt header) must NOT 500 the whole
        // playback: fall back to handing the client the file's own static URL. If it
        // is browser-playable it just plays; if not, the client surfaces a normal
        // playback error instead of a scary 500 on every start.
        EncoderMediaInfo mediaInfo;
        try
        {
            mediaInfo = await mediaAnalyzer.AnalyzeAsync(
                resolved.InputPath,
                resolved.ExtraInputArgs,
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Live analyze failed for {VideoFileId}; falling back to direct-play of the static source",
                videoFileId
            );
            return DirectPlayResult(
                videoFileId,
                resolved.File,
                "Source could not be analyzed; serving the original file"
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
            return DirectPlayResult(
                videoFileId,
                resolved.File,
                "File is compatible with client capabilities"
            );

        LiveEncodeRequest liveRequest = new(
            InputPath: resolved.InputPath,
            CachedInfo: mediaInfo,
            Client: clientCaps,
            StartPosition: TimeSpan.FromSeconds(Math.Max(0, request.StartTimeSeconds)),
            PreferredQuality: request.PreferredQuality,
            ExtraInputArgs: resolved.ExtraInputArgs
        );

        ILiveSession session;
        try
        {
            session = await liveEncoder.StartAsync(liveRequest, ct);
        }
        catch (EncoderRuntimeException ex)
        {
            return LiveResult.Encoder(ex.HttpStatusCode, ex.Shape);
        }
        catch (InvalidOperationException ex)
        {
            return LiveResult.ServiceUnavailable(ex.Message);
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

        return LiveResult.Ok(
            new StartLiveSessionResponse(session.SessionId, playlistUrl, quality.Id, quality.Label)
            {
                SelectedVariant = selectedVariant,
                ExpiresAt = expiresAt,
            }
        );
    }

    public LiveResult GetPlaylist(string sessionId)
    {
        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId);

        runtime.TouchLastAccess();
        string segmentUrlTemplate =
            $"/api/v1/streaming/live/sessions/{sessionId}/segment/{runtime.CurrentEpoch}/{{index}}.ts";
        LivePlaylistRequest request = new(
            SessionId: sessionId,
            Segments: runtime.SnapshotSegments(),
            TargetSegmentDuration: runtime.TargetSegmentDuration,
            IsComplete: runtime.IsComplete,
            SegmentUrlTemplate: segmentUrlTemplate,
            TotalDuration: runtime.CachedMediaInfo?.Duration
        );
        string playlist = playlistBuilder.Build(request);
        return LiveResult.Ok(playlist);
    }

    public async Task<LiveResult> GetSegmentAsync(
        string sessionId,
        string epoch,
        int index,
        CancellationToken ct
    )
    {
        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId);

        // No stale-epoch gate: the client holds one cached whole-runtime VOD
        // playlist whose segment URLs are minted once. A seek repositions the
        // encoder but must not invalidate those URLs — segments are absolutely
        // indexed, so the index alone identifies the content regardless of which
        // runner generation produced it. (The route still carries the epoch for
        // URL-shape compatibility; it is intentionally not matched here.)
        _ = epoch;

        // Pace transcoding from real client demand. The web client never posts a
        // playback position, so the only signal of where the viewer is comes from
        // which segment it is fetching. Feed that to the session as the playhead:
        // segments are absolutely indexed, so segment N sits at N×segDur. Without
        // this the session's BufferAhead is (absolute transcoded position − 0),
        // which the buffer-adaptive sweep reads as a permanently-full buffer and
        // suspends the encoder forever — the stall this fixes.
        double segmentSeconds =
            runtime.TargetSegmentDuration.TotalSeconds > 0
                ? runtime.TargetSegmentDuration.TotalSeconds
                : 6;
        runtime.Session.ReportPlaybackPosition(TimeSpan.FromSeconds(index * segmentSeconds));

        // The whole-runtime VOD playlist lists every segment up front, so hls.js
        // asks for the one at the playhead before the encoder has written it —
        // right after a seek (the client's beforeSeek repositions the encoder
        // concurrently), or when the transcode momentarily trails real time.
        // Block for the file to land rather than 404, which hls.js treats as a
        // fatal fragment-load error. Bounded so a genuinely dead encoder still
        // returns instead of hanging the request forever.
        // Deterministic on-disk path for this index, used as a fallback when the
        // in-memory buffer does not have the segment. After a seek the buffer is
        // cleared and the replacement runner writes segments to disk before the
        // drainer re-indexes them — serving from disk decouples delivery from that
        // bookkeeping so a produced segment is never withheld.
        string? diskPath = runtime.ScratchDirectory is { Length: > 0 } scratch
            ? storage.CombinePath(scratch, $"seg_{index:D5}.ts")
            : null;

        DateTime deadline = DateTime.UtcNow.Add(SegmentWaitTimeout);
        while (true)
        {
            if (
                runtime.TryGetSegment(index, out Segment segment)
                && storage.Exists(segment.FilePath)
            )
            {
                runtime.TouchLastAccess();
                Stream stream = storage.OpenRead(segment.FilePath);
                return LiveResult.Ok(stream);
            }

            if (diskPath is not null && storage.Exists(diskPath))
            {
                runtime.TouchLastAccess();
                Stream stream = storage.OpenRead(diskPath);
                return LiveResult.Ok(stream);
            }

            // The client is asking for a segment the encoder has not produced. If
            // the buffer-adaptive sweep suspended it (buffer looked full), wake it
            // now instead of waiting up to a full sweep interval — Resume is a
            // no-op unless the session is actually suspended, so this is safe to
            // call on every poll.
            if (runtime.Session.State == LiveSessionState.Buffered)
                runtime.Session.Resume();

            if (ct.IsCancellationRequested || DateTime.UtcNow >= deadline)
                return LiveResult.NotFound($"Segment {index} is not ready yet");

            try
            {
                await Task.Delay(SegmentPollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return LiveResult.NotFound($"Segment {index} is not ready yet");
            }

            runtime.TouchLastAccess();
        }
    }

    public LiveResult ReportPosition(string sessionId, ReportPositionRequest request)
    {
        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId);

        double clampedSeconds = Math.Max(0, request.TimeSeconds);
        runtime.Session.ReportPlaybackPosition(TimeSpan.FromSeconds(clampedSeconds));
        bool isPaused = runtime.Session.State == LiveSessionState.Buffered;
        return LiveResult.Ok(new ReportPositionResponse(clampedSeconds, isPaused));
    }

    public async Task<LiveResult> ChangeQualityAsync(
        string sessionId,
        ChangeQualityRequest request,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(request.QualityId))
            return LiveResult.BadRequest("quality_id is required");

        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId);

        if (runtime.CachedMediaInfo is null || runtime.ClientCapabilities is null)
            return LiveResult.ServiceUnavailable(
                "Session context not available for quality change"
            );

        LiveQuality[] available = qualitySelector.GetAvailableQualities(
            runtime.CachedMediaInfo,
            runtime.ClientCapabilities,
            speedIndex,
            budget
        );
        LiveQuality? newQuality = available.FirstOrDefault(q => q.Id == request.QualityId);
        if (newQuality is null)
            return LiveResult.NotFound(
                $"Quality '{request.QualityId}' is not available for this session"
            );

        await runtime.Session.ChangeQualityAsync(request.QualityId, newQuality, ct);
        runtime.TouchLastAccess();

        await PushIfTransportAsync(
                sessionId,
                new QualityChangedMessage(
                    NewQuality: newQuality,
                    Reason: QualityChangeReason.UserRequested,
                    SeekEpoch: runtime.CurrentEpoch
                ),
                ct
            )
            .ConfigureAwait(false);

        return LiveResult.Ok(new ChangeQualityResponse(newQuality.Id, newQuality.Label));
    }

    public async Task<LiveResult> SeekAsync(
        string sessionId,
        SeekRequest request,
        CancellationToken ct
    )
    {
        if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId);

        double clampedSeconds = Math.Max(0, request.PositionSeconds);
        await runtime.Session.SeekAsync(TimeSpan.FromSeconds(clampedSeconds), ct);
        runtime.TouchLastAccess();

        int firstSegmentIndex = runtime.HighestSegmentIndex + 1;
        await PushIfTransportAsync(
                sessionId,
                new SeekCompletedMessage(
                    NewPositionSeconds: clampedSeconds,
                    FirstSegmentIndex: firstSegmentIndex,
                    SeekEpoch: runtime.CurrentEpoch
                ),
                ct
            )
            .ConfigureAwait(false);

        return LiveResult.Ok(new SeekResponse(clampedSeconds));
    }

    public async Task EndSessionAsync(string sessionId, CancellationToken ct)
    {
        await PushIfTransportAsync(
                sessionId,
                new SessionEndedMessage(Reason: SessionEndReason.ClientDisconnected),
                ct
            )
            .ConfigureAwait(false);

        // LiveStreamingService.RemoveAsync now owns the complete teardown
        // (runtime disposal + session-manager removal + tombstone) atomically,
        // so callers no longer need a separate RemoveSession call.
        await streamingService.RemoveAsync(sessionId);
    }

    private LiveResult SessionGoneOrNotFound(string sessionId) =>
        streamingService.WasRecentlyRemoved(sessionId)
            ? LiveResult.Gone("Live session has ended or expired")
            : LiveResult.NotFound("Live session not found");

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

    // The route DynamicStaticFilesMiddleware serves from, built exactly as
    // VideoPlaylistResponseDto does: /{folder-ULID}{sub-path}{/filename}. HostFolder
    // is the physical disk path (e.g. M:/…) and is NOT a valid route — the middleware
    // parses the first segment as a folder ULID, so a raw disk path 404s.
    private static string BuildServedUrl(VideoFile file) =>
        $"/{file.Share}{file.Folder}{file.Filename}";

    // Evict the caller's least-recently-accessed session. LastAccess is bumped on
    // every playlist/segment fetch, so an abandoned stream (reload/switch) is the
    // stalest and gets reclaimed first, leaving a genuinely active second-device
    // session alone. Any owned session with no live runtime is dropped outright.
    private async Task EvictStalestUserSessionAsync(string userId)
    {
        string? stalestId = null;
        DateTime stalestAccess = DateTime.MaxValue;

        foreach (string sessionId in sessionManager.GetUserSessionIds(userId))
        {
            if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
            {
                sessionManager.RemoveSession(sessionId);
                continue;
            }

            if (runtime.LastAccess < stalestAccess)
            {
                stalestAccess = runtime.LastAccess;
                stalestId = sessionId;
            }
        }

        if (stalestId is not null)
            await streamingService.RemoveAsync(stalestId);
    }

    private LiveResult DirectPlayResult(Ulid videoFileId, VideoFile file, string reason)
    {
        string url = BuildServedUrl(file);
        logger.LogInformation("Direct-play for {VideoFileId}: {Url}", videoFileId, url);
        return LiveResult.Ok(
            new StartLiveSessionResponse(
                SessionId: string.Empty,
                PlaylistUrl: string.Empty,
                QualityId: string.Empty,
                QualityLabel: string.Empty
            )
            {
                Mode = "direct",
                DirectStreamUrl = url,
                DirectPlayReason = reason,
            }
        );
    }

    private async Task<AuthorizedFile?> ResolveAuthorizedFileAsync(
        MediaContext context,
        Ulid videoFileId,
        Guid userId,
        string? accessToken,
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

        (string inputPath, string[]? extraInputArgs) = await ResolveInputAsync(
            context,
            file,
            accessToken,
            ct
        );
        return new(file, inputPath, extraInputArgs);
    }

    // Resolve the source to something ffprobe/ffmpeg can actually open across every
    // storage backend. A library file's HostFolder is a driver-scope-relative key
    // (e.g. "Anime/Anime/Show/Ep") on a backend (NFS/SMB/S3/local) that ffmpeg often
    // cannot read directly — its own NFS client, for instance, fails where the
    // server's driver succeeds. So the transcoder self-ingests over the server's own
    // internal HTTP serving port: that path already streams every backend correctly
    // (it is what the browser plays), so ffmpeg only ever speaks plain HTTP. Media
    // serving is authenticated, hence the caller's bearer token. Non-library sources
    // (disc rips, absolute paths — no folder ULID) keep their direct filesystem path.
    private async Task<(string InputPath, string[]? ExtraInputArgs)> ResolveInputAsync(
        MediaContext context,
        VideoFile file,
        string? accessToken,
        CancellationToken ct
    )
    {
        string localPath = storage.CombinePath(file.HostFolder, file.Filename);

        if (accessToken is null || !Ulid.TryParse(file.Share, out Ulid folderId))
            return (localPath, null);

        bool folderExists = await context
            .Folders.AsNoTracking()
            .AnyAsync(f => f.Id == folderId, ct);
        if (!folderExists)
            return (localPath, null);

        int httpPort = RuntimeServerSettings.Current.InternalServerPort + 1;
        string url = $"http://127.0.0.1:{httpPort}{BuildServedUrlEncoded(file)}";
        string[] headers = ["-headers", $"Authorization: Bearer {accessToken}\r\n"];
        return (url, headers);
    }

    // Percent-encode each path segment (spaces, commas, apostrophes, parentheses)
    // while preserving the '/' separators DynamicStaticFilesMiddleware splits on and
    // then unescapes.
    private static string BuildServedUrlEncoded(VideoFile file) =>
        string.Join('/', BuildServedUrl(file).Split('/').Select(Uri.EscapeDataString));

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

    private sealed record AuthorizedFile(
        VideoFile File,
        string InputPath,
        string[]? ExtraInputArgs
    );
}
