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
    ILiveIngestKeyStore ingestKeyStore,
    ILogger<LiveTranscodeService> logger,
    ILiveSessionTransport? transport = null
) : ILiveTranscodeService
{
    // How long a segment request blocks waiting for the encoder to produce the
    // requested (on-demand) segment before giving up, and how often it rechecks.
    private static readonly TimeSpan SegmentWaitTimeout = TimeSpan.FromSeconds(seconds: 20);
    private static readonly TimeSpan SegmentPollInterval = TimeSpan.FromMilliseconds(milliseconds: 200);

    public IReadOnlyList<LiveSessionDto> ListSessions()
    {
        IReadOnlyList<LiveSessionSnapshot> snapshots = streamingService.GetActiveSessions();
        return snapshots
            .Select(selector: s => new LiveSessionDto(
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
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(value: request.VideoFileId))
            return LiveResult.BadRequest(message: "video_file_id is required");

        if (request.ClientCaps is null)
            return LiveResult.BadRequest(message: "client_caps is required");

        if (!Ulid.TryParse(base32: request.VideoFileId, ulid: out Ulid videoFileId))
            return LiveResult.BadRequest(message: "video_file_id is not a valid identifier");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        AuthorizedFile? resolved = await ResolveAuthorizedFileAsync(
            context: context,
            videoFileId: videoFileId,
            userId: userId,
            ct: ct
        );
        if (resolved is null)
            return LiveResult.NotFound(message: "Video file not found or you lack access");

        // Reconcile accounting against the live runtimes first: a crashed FFmpeg or
        // an abandoned tab can leave a session counted against the cap after its
        // runtime is gone — the "0 active but max reached" symptom.
        sessionManager.PruneDeadSessions(aliveSessionIds: streamingService.ActiveSessionIds);

        if (!sessionManager.CanStartSession(userId: userId.ToString()))
        {
            // Still at the cap with live sessions. Starting playback abandons the
            // previous stream without a clean stop (a reload or item switch), so
            // evict this user's stalest session to make room — the same way Plex and
            // Jellyfin supersede a replaced playback session rather than refusing.
            await EvictStalestUserSessionAsync(userId: userId.ToString());

            if (!sessionManager.CanStartSession(userId: userId.ToString()))
                return LiveResult.ServiceUnavailable(message: "Maximum concurrent live sessions reached");
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
                filePath: resolved.InputPath,
                extraInputArgs: resolved.ExtraInputArgs,
                ct: ct
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Live analyze failed for {VideoFileId}; falling back to direct-play of the static source",
                args: videoFileId
            );
            return DirectPlayResult(
                videoFileId: videoFileId,
                file: resolved.File,
                reason: "Source could not be analyzed; serving the original file"
            );
        }

        DeviceCapabilities? deviceCaps = null;
        if (deviceId is not null)
        {
            deviceCaps =
                capabilityRegistry.Get(deviceId: deviceId)
                ?? await capabilityRegistry.LoadFromDbAsync(deviceId: deviceId, ct: ct);
        }

        ClientCapabilities clientCaps = variantSelector.ApplyDeviceCaps(
            client: ToClientCapabilities(dto: request.ClientCaps),
            deviceCaps: deviceCaps
        );

        if (deviceCaps is not null)
            logger.LogInformation(
                message: "Live session for device {DeviceId}: caps channels={Ch} ramTier={Tier}", args: [deviceId, deviceCaps.MaxAudioChannels, deviceCaps.RamTier]
            );

        PlaybackDecision playbackDecision;
        try
        {
            playbackDecision = decisionEngine.Decide(media: mediaInfo, client: clientCaps);
        }
        catch (Exception ex)
        {
            logger.LogWarning(exception: ex, message: "PlaybackDecisionEngine.Decide threw; falling back to transcode");
            playbackDecision = new(Action: PlaybackAction.TranscodeVideo, Reason: "Decision engine error", DirectStreamUrl: null);
        }

        if (playbackDecision.Action == PlaybackAction.DirectPlay)
            return DirectPlayResult(
                videoFileId: videoFileId,
                file: resolved.File,
                reason: "File is compatible with client capabilities"
            );

        // The client picks the audio track from the episode's own language list and
        // passes the ISO code. Absent (older client), fall back to English so a
        // Japanese-default file still opens in English.
        List<string> preferredLanguages = request.AudioLanguage is { Length: > 0 } lang
            ? [lang]
            : ["eng"];

        // A NoMercy-encoded file already ships each audio track as a separate
        // browser-ready HLS rendition, so the session transcodes video only and its
        // master playlist points the player at those renditions — instant switching,
        // no audio re-encode.
        List<LiveAudioRendition> fileRenditions = BuildAudioRenditions(
            file: resolved.File,
            preferred: preferredLanguages[index: 0]
        );
        bool useFileRenditions = fileRenditions.Count > 0;

        // A raw source (a lossless remux, disc rip — no pre-encoded renditions) with
        // its own audio: transcode video-only and spin up a per-language audio-only
        // child so every language lands in the menu, each transcoded to AAC. Only
        // when the source has no renditions AND has audio streams to expose.
        bool rawMultiAudio = !useFileRenditions && mediaInfo.AudioStreams.Count > 0;

        int audioStreamIndex = LiveAudioSelector.Select(audioStreams: mediaInfo.AudioStreams, preferredIso6391: preferredLanguages);

        LiveEncodeRequest liveRequest = new(
            InputPath: resolved.InputPath,
            CachedInfo: mediaInfo,
            Client: clientCaps,
            StartPosition: TimeSpan.FromSeconds(value: Math.Max(val1: 0, val2: request.StartTimeSeconds)),
            PreferredQuality: request.PreferredQuality,
            ExtraInputArgs: resolved.ExtraInputArgs,
            AudioStreamIndex: audioStreamIndex,
            // Video-only whenever audio comes from a separate track set (existing
            // renditions or per-language children); muxed only for the last-resort
            // single-track fallback.
            VideoOnly: useFileRenditions || rawMultiAudio
        );

        ILiveSession session;
        try
        {
            session = await liveEncoder.StartAsync(request: liveRequest, ct: ct);
        }
        catch (EncoderRuntimeException ex)
        {
            return LiveResult.Encoder(statusCode: ex.HttpStatusCode, shape: ex.Shape);
        }
        catch (InvalidOperationException ex)
        {
            return LiveResult.ServiceUnavailable(message: ex.Message);
        }

        sessionManager.RegisterSession(session: session, userId: userId.ToString());

        // Tie the self-ingest key to the session so it dies on teardown, not just
        // on its absolute-lifetime backstop. Null for local/disc sources that read
        // straight off the filesystem and never touch the HTTP serving port.
        if (resolved.IngestKey is not null)
            ingestKeyStore.BindSession(key: resolved.IngestKey, sessionId: session.SessionId);

        // Assemble the master's audio list: the file's own renditions when it has
        // them, otherwise a per-language live audio child for a raw source. Either
        // way the runtime carries the list and the client loads the master; a source
        // with no audio at all loads the plain (video-only) media playlist.
        List<LiveAudioRendition> masterRenditions =
            useFileRenditions ? fileRenditions
            : rawMultiAudio
                ? await StartAudioChildrenAsync(
                    parentSessionId: session.SessionId,
                    baseRequest: liveRequest,
                    audioStreams: mediaInfo.AudioStreams,
                    defaultAudioIndex: audioStreamIndex,
                    ct: ct
                )
            : [];
        bool useMaster = masterRenditions.Count > 0;

        if (useMaster)
            streamingService.StampAudioRenditions(sessionId: session.SessionId, renditions: masterRenditions);

        string playlistUrl = useMaster
            ? $"/api/v1/streaming/live/sessions/{session.SessionId}/master.m3u8"
            : $"/api/v1/streaming/live/sessions/{session.SessionId}/playlist.m3u8";
        LiveQuality quality = session.CurrentQuality;
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(value: sessionLimits.IdleTimeoutMinutes);
        SelectedVariantDto selectedVariant = new(
            Codec: quality.Codec.ToString(),
            Width: quality.Width,
            Height: quality.Height,
            BitrateKbps: quality.BitrateKbps
        );

        return LiveResult.Ok(
            payload: new StartLiveSessionResponse(SessionId: session.SessionId, PlaylistUrl: playlistUrl, QualityId: quality.Id, QualityLabel: quality.Label)
            {
                SelectedVariant = selectedVariant,
                ExpiresAt = expiresAt,
            }
        );
    }

    public LiveResult GetMasterPlaylist(string sessionId)
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId: sessionId);

        runtime.TouchLastAccess();
        LiveQuality quality = runtime.Session.CurrentQuality;

        // The video variant URI is relative so the client resolves it against the
        // master's own URL; the media playlist and its segment URLs are unchanged.
        LiveMasterPlaylistRequest request = new(
            VideoPlaylistUri: "playlist.m3u8",
            Width: quality.Width,
            Height: quality.Height,
            BitrateKbps: quality.BitrateKbps,
            AudioRenditions: runtime.AudioRenditions
        );
        string master = playlistBuilder.BuildMaster(request: request);
        return LiveResult.Ok(payload: master);
    }

    public LiveResult GetPlaylist(string sessionId)
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId: sessionId);

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
        string playlist = playlistBuilder.Build(request: request);
        return LiveResult.Ok(payload: playlist);
    }

    public async Task<LiveResult> GetSegmentAsync(
        string sessionId,
        string epoch,
        int index,
        CancellationToken ct
    )
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId: sessionId);

        // No stale-epoch gate: the client holds one cached whole-runtime VOD
        // playlist whose segment URLs are minted once. A seek repositions the
        // encoder but must not invalidate those URLs — segments are absolutely
        // indexed, so the index alone identifies the content regardless of which
        // runner generation produced it. (The route still carries the epoch for
        // URL-shape compatibility; it is intentionally not matched here.)
        _ = epoch;

        // Pace transcoding from real client demand as a FALLBACK. A client that
        // reports its true position (LiveTranscodeHub.ReportPlayhead) owns the
        // playhead; this segment-request-derived estimate only applies while no
        // such authoritative report is current — see LiveSession.ReportPlaybackPosition.
        // Absent an authoritative report, the only signal of where the viewer is
        // comes from which segment it is fetching, and the player prefetches well
        // ahead of that — feeding it in unconditionally used to make BufferAhead
        // read as permanently low and let the encoder race to the end of the file.
        // Segments are absolutely indexed, so segment N sits at N×segDur.
        double segmentSeconds =
            runtime.TargetSegmentDuration.TotalSeconds > 0
                ? runtime.TargetSegmentDuration.TotalSeconds
                : 6;
        runtime.Session.ReportPlaybackPosition(
            position: TimeSpan.FromSeconds(value: index * segmentSeconds),
            authoritative: false
        );

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
            ? storage.CombinePath(parent: scratch, child: $"seg_{index:D5}.ts")
            : null;

        DateTime deadline = DateTime.UtcNow.Add(value: SegmentWaitTimeout);
        while (true)
        {
            if (
                runtime.TryGetSegment(index: index, segment: out Segment segment)
                && storage.Exists(path: segment.FilePath)
            )
            {
                runtime.TouchLastAccess();
                Stream stream = storage.OpenRead(path: segment.FilePath);
                return LiveResult.Ok(payload: stream);
            }

            if (diskPath is not null && storage.Exists(path: diskPath))
            {
                runtime.TouchLastAccess();
                Stream stream = storage.OpenRead(path: diskPath);
                return LiveResult.Ok(payload: stream);
            }

            // The client is asking for a segment the encoder has not produced. If
            // the buffer-adaptive sweep suspended it (buffer looked full), wake it
            // now instead of waiting up to a full sweep interval — Resume is a
            // no-op unless the session is actually suspended, so this is safe to
            // call on every poll.
            if (runtime.Session.State == LiveSessionState.Buffered)
                runtime.Session.Resume();

            if (ct.IsCancellationRequested || DateTime.UtcNow >= deadline)
                return LiveResult.NotFound(message: $"Segment {index} is not ready yet");

            try
            {
                await Task.Delay(delay: SegmentPollInterval, cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                return LiveResult.NotFound(message: $"Segment {index} is not ready yet");
            }

            runtime.TouchLastAccess();
        }
    }

    public LiveResult ReportPosition(string sessionId, ReportPositionRequest request)
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId: sessionId);

        double clampedSeconds = Math.Max(val1: 0, val2: request.TimeSeconds);
        runtime.Session.ReportPlaybackPosition(
            position: TimeSpan.FromSeconds(value: clampedSeconds),
            authoritative: true
        );
        bool isPaused = runtime.Session.State == LiveSessionState.Buffered;
        return LiveResult.Ok(payload: new ReportPositionResponse(PositionSeconds: clampedSeconds, IsPaused: isPaused));
    }

    public LiveResult ReportBufferHealth(string sessionId, ReportBufferHealthRequest request)
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId: sessionId);

        double clampedBufferedSeconds = Math.Max(val1: 0, val2: request.BufferedSeconds);
        double clampedBandwidthKbps = Math.Max(val1: 0, val2: request.ObservedBandwidthKbps);

        runtime.Session.ReportClientBufferHealth(
            bufferedAhead: TimeSpan.FromSeconds(value: clampedBufferedSeconds),
            observedBandwidthKbps: (int)clampedBandwidthKbps
        );
        runtime.TouchLastAccess();

        return LiveResult.Ok(
            payload: new ReportBufferHealthResponse(BufferedSeconds: clampedBufferedSeconds, ObservedBandwidthKbps: clampedBandwidthKbps)
        );
    }

    public async Task<LiveResult> ChangeQualityAsync(
        string sessionId,
        ChangeQualityRequest request,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(value: request.QualityId))
            return LiveResult.BadRequest(message: "quality_id is required");

        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId: sessionId);

        if (runtime.CachedMediaInfo is null || runtime.ClientCapabilities is null)
            return LiveResult.ServiceUnavailable(
                message: "Session context not available for quality change"
            );

        LiveQuality[] available = qualitySelector.GetAvailableQualities(
            input: runtime.CachedMediaInfo,
            client: runtime.ClientCapabilities,
            speeds: speedIndex,
            budget: budget
        );
        LiveQuality? newQuality = available.FirstOrDefault(predicate: q => q.Id == request.QualityId);
        if (newQuality is null)
            return LiveResult.NotFound(
                message: $"Quality '{request.QualityId}' is not available for this session"
            );

        await runtime.Session.ChangeQualityAsync(qualityId: request.QualityId, newQuality: newQuality, ct: ct);
        runtime.TouchLastAccess();

        await PushIfTransportAsync(
                sessionId: sessionId,
                message: new QualityChangedMessage(
                    NewQuality: newQuality,
                    Reason: QualityChangeReason.UserRequested,
                    SeekEpoch: runtime.CurrentEpoch
                ),
                ct: ct
            )
            .ConfigureAwait(continueOnCapturedContext: false);

        return LiveResult.Ok(payload: new ChangeQualityResponse(QualityId: newQuality.Id, QualityLabel: newQuality.Label));
    }

    public async Task<LiveResult> SeekAsync(
        string sessionId,
        SeekRequest request,
        CancellationToken ct
    )
    {
        if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            return SessionGoneOrNotFound(sessionId: sessionId);

        double clampedSeconds = Math.Max(val1: 0, val2: request.PositionSeconds);
        int targetIndex = SegmentIndexAt(runtime: runtime, positionSeconds: clampedSeconds);

        // Fast seek. The target segment has already been transcoded, so the client
        // can pull it over HTTP immediately. Re-spawning ffmpeg at the target would
        // kill the runner's forward lead and re-encode ground already covered: the
        // slow path, and the reason a "rewatch" seek used to stall. Skip it — just
        // move the reported playhead so the buffer-adaptive sweep paces from the new
        // position (it suspends the encoder if the seek left it far ahead, and a
        // later request past the transcoded frontier resumes it there). The check
        // reads the on-disk scratch too, not just the current runner's in-memory
        // buffer, because segment files persist across runner generations while the
        // buffer is cleared on every (re)spawn — without the disk check, every seek
        // after the first would re-encode content already sitting on disk. A target
        // that is genuinely absent — a forward jump past everything transcoded, or a
        // gap left by an earlier jump — falls through to a real re-spawn below.
        if (SegmentAlreadyTranscoded(runtime: runtime, index: targetIndex))
        {
            runtime.Session.ReportPlaybackPosition(
                position: TimeSpan.FromSeconds(value: clampedSeconds),
                authoritative: true
            );
            runtime.TouchLastAccess();

            await PushIfTransportAsync(
                    sessionId: sessionId,
                    message: new SeekCompletedMessage(
                        NewPositionSeconds: clampedSeconds,
                        FirstSegmentIndex: targetIndex,
                        SeekEpoch: runtime.CurrentEpoch
                    ),
                    ct: ct
                )
                .ConfigureAwait(continueOnCapturedContext: false);

            return LiveResult.Ok(payload: new SeekResponse(PositionSeconds: clampedSeconds));
        }

        await runtime.Session.SeekAsync(position: TimeSpan.FromSeconds(value: clampedSeconds), ct: ct);
        runtime.TouchLastAccess();

        // targetIndex, not HighestSegmentIndex + 1: the runtime buffer is no
        // longer wiped on seek (see LiveSession.SeekAsync), so HighestSegmentIndex
        // can be an unrelated older maximum from before the seek. targetIndex is
        // the index the coverage-aware respawn (LiveEncoder.SpawnRunner via
        // LiveGapPlanner) actually targets and the client should fetch next.
        int firstSegmentIndex = targetIndex;
        await PushIfTransportAsync(
                sessionId: sessionId,
                message: new SeekCompletedMessage(
                    NewPositionSeconds: clampedSeconds,
                    FirstSegmentIndex: firstSegmentIndex,
                    SeekEpoch: runtime.CurrentEpoch
                ),
                ct: ct
            )
            .ConfigureAwait(continueOnCapturedContext: false);

        return LiveResult.Ok(payload: new SeekResponse(PositionSeconds: clampedSeconds));
    }

    // Absolute HLS segment index a wall-clock position maps to. Mirrors the
    // indexing GetSegmentAsync and the ffmpeg arg builder use so a seek's
    // "is it already transcoded" check lines up with what the client requests.
    private static int SegmentIndexAt(LiveRuntimeSession runtime, double positionSeconds)
    {
        double segmentSeconds =
            runtime.TargetSegmentDuration.TotalSeconds > 0
                ? runtime.TargetSegmentDuration.TotalSeconds
                : 6;
        return (int)(positionSeconds / segmentSeconds);
    }

    // Whether the segment at <paramref name="index"/> has already been produced
    // and is servable right now — in the current runner's in-memory buffer, or on
    // disk from any runner generation this session has spawned. Mirrors the two
    // sources GetSegmentAsync serves from, so a fast seek only skips the re-spawn
    // when the client is guaranteed to get the segment without one.
    private bool SegmentAlreadyTranscoded(LiveRuntimeSession runtime, int index)
    {
        if (runtime.TryGetSegment(index: index, segment: out Segment buffered) && storage.Exists(path: buffered.FilePath))
            return true;

        return runtime.ScratchDirectory is { Length: > 0 } scratch
            && storage.Exists(path: storage.CombinePath(parent: scratch, child: $"seg_{index:D5}.ts"));
    }

    public async Task EndSessionAsync(string sessionId, CancellationToken ct)
    {
        await PushIfTransportAsync(
                sessionId: sessionId,
                message: new SessionEndedMessage(Reason: SessionEndReason.ClientDisconnected),
                ct: ct
            )
            .ConfigureAwait(continueOnCapturedContext: false);

        // LiveStreamingService.RemoveAsync now owns the complete teardown
        // (runtime disposal + session-manager removal + tombstone) atomically,
        // so callers no longer need a separate RemoveSession call.
        await streamingService.RemoveAsync(sessionId: sessionId);

        // Kill the file's self-ingest key the moment the session ends so no
        // spent key survives on its absolute-lifetime backstop.
        ingestKeyStore.RevokeSession(sessionId: sessionId);
    }

    private LiveResult SessionGoneOrNotFound(string sessionId) =>
        streamingService.WasRecentlyRemoved(sessionId: sessionId)
            ? LiveResult.Gone(message: "Live session has ended or expired")
            : LiveResult.NotFound(message: "Live session not found");

    private async Task PushIfTransportAsync(string sessionId, object message, CancellationToken ct)
    {
        if (transport is null)
            return;

        try
        {
            await transport.SendToClientAsync(sessionId: sessionId, message: message, ct: ct).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                exception: ex,
                message: "Transport push failed for {Event} on session {SessionId}", args: [message.GetType().Name, sessionId]
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

    // The file's own pre-encoded HLS audio renditions, mapped to client-facing
    // URLs the session's master playlist references. Each rendition's FileName is
    // a folder-relative HLS path (e.g. "/audio_eng_aac/audio_eng_aac.m3u8"); the
    // served URL joins it to the file's base folder exactly as the static playlist
    // response does. The viewer's preferred language opens by default; the rest
    // stay available in the player's audio menu. Empty when the source has no
    // renditions (raw/disc), which routes the session to the muxed media playlist.
    private static List<LiveAudioRendition> BuildAudioRenditions(VideoFile file, string preferred)
    {
        List<IAudio> tracks =
            file.Metadata?.Audio?.Where(predicate: a =>
                    !string.IsNullOrWhiteSpace(value: a.FileName)
                    && a.FileName.EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                .ToList()
            ?? [];

        if (tracks.Count == 0)
            return [];

        int defaultIndex = tracks.FindIndex(match: a =>
            LiveAudioSelector.LanguageMatches(streamLanguage: a.Language, preferredIso6391: preferred)
        );
        if (defaultIndex < 0)
            defaultIndex = 0;

        string baseFolder = $"/{file.Share}{file.Folder}";
        return tracks
            .Select(
                selector: (a, index) =>
                    new LiveAudioRendition(
                        Language: a.Language,
                        Uri: EncodeServedPath(path: $"{baseFolder}{a.FileName}"),
                        IsDefault: index == defaultIndex
                    )
            )
            .ToList();
    }

    // Percent-encode each path segment (spaces, commas, apostrophes) while keeping
    // the '/' separators DynamicStaticFilesMiddleware splits on and unescapes.
    private static string EncodeServedPath(string path) =>
        string.Join(separator: '/', values: path.Split(separator: '/').Select(selector: Uri.EscapeDataString));

    // Spawn one audio-only transcode per source language for a raw source, and
    // return the master's audio list pointing at each child's media playlist. The
    // viewer's language (defaultAudioIndex, already resolved) opens by default;
    // every language stays switchable because each child runs its own AAC transcode
    // sharing the video's segment boundaries. A child that fails to start is
    // skipped rather than sinking the whole session. The children are registered
    // for cascade disposal so they never outlive the video.
    private async Task<List<LiveAudioRendition>> StartAudioChildrenAsync(
        string parentSessionId,
        LiveEncodeRequest baseRequest,
        IReadOnlyList<AudioStreamInfo> audioStreams,
        int defaultAudioIndex,
        CancellationToken ct
    )
    {
        List<LiveAudioRendition> renditions = [];
        List<string> childSessionIds = [];

        for (int index = 0; index < audioStreams.Count; index++)
        {
            LiveEncodeRequest childRequest = baseRequest with { AudioStreamIndex = index };

            ILiveSession child;
            try
            {
                child = await liveEncoder.StartAudioRenditionAsync(request: childRequest, ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    exception: ex,
                    message: "Failed to start live audio child for stream 0:a:{Index} of session {SessionId}", args: [index, parentSessionId]
                );
                continue;
            }

            childSessionIds.Add(item: child.SessionId);
            renditions.Add(
                item: new LiveAudioRendition(
                    Language: audioStreams[index: index].Language ?? "und",
                    Uri: $"/api/v1/streaming/live/sessions/{child.SessionId}/playlist.m3u8",
                    IsDefault: index == defaultAudioIndex
                )
            );
        }

        streamingService.StampChildAudioSessions(sessionId: parentSessionId, childSessionIds: childSessionIds);
        return renditions;
    }

    // Evict the caller's least-recently-accessed session. LastAccess is bumped on
    // every playlist/segment fetch, so an abandoned stream (reload/switch) is the
    // stalest and gets reclaimed first, leaving a genuinely active second-device
    // session alone. Any owned session with no live runtime is dropped outright.
    private async Task EvictStalestUserSessionAsync(string userId)
    {
        string? stalestId = null;
        DateTime stalestAccess = DateTime.MaxValue;

        foreach (string sessionId in sessionManager.GetUserSessionIds(userId: userId))
        {
            if (!streamingService.TryGetRuntime(sessionId: sessionId, runtime: out LiveRuntimeSession runtime))
            {
                sessionManager.RemoveSession(sessionId: sessionId);
                continue;
            }

            if (runtime.LastAccess < stalestAccess)
            {
                stalestAccess = runtime.LastAccess;
                stalestId = sessionId;
            }
        }

        if (stalestId is not null)
            await streamingService.RemoveAsync(sessionId: stalestId);
    }

    private LiveResult DirectPlayResult(Ulid videoFileId, VideoFile file, string reason)
    {
        string url = BuildServedUrl(file: file);
        logger.LogInformation(message: "Direct-play for {VideoFileId}: {Url}", args: [videoFileId, url]);
        return LiveResult.Ok(
            payload: new StartLiveSessionResponse(
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
        CancellationToken ct
    )
    {
        VideoFile? file = await context
            .VideoFiles.AsNoTracking()
            .Include(navigationPropertyPath: vf => vf.Metadata)
            .FirstOrDefaultAsync(predicate: vf => vf.Id == videoFileId, cancellationToken: ct);
        if (file is null)
            return null;

        bool allowed = await UserHasAccessAsync(context: context, file: file, userId: userId, ct: ct);
        if (!allowed)
            return null;

        (string inputPath, string[]? extraInputArgs, string? ingestKey) = await ResolveInputAsync(
            context: context,
            file: file,
            ct: ct
        );
        return new(File: file, InputPath: inputPath, ExtraInputArgs: extraInputArgs, IngestKey: ingestKey);
    }

    // Resolve the source to something ffprobe/ffmpeg can actually open across every
    // storage backend. A library file's HostFolder is a driver-scope-relative key
    // (e.g. "Anime/Anime/Show/Ep") on a backend (NFS/SMB/S3/local) that ffmpeg often
    // cannot read directly — its own NFS client, for instance, fails where the
    // server's driver succeeds. So the transcoder self-ingests over the server's own
    // internal HTTP serving port: that path already streams every backend correctly
    // (it is what the browser plays), so ffmpeg only ever speaks plain HTTP. The
    // serving port is authenticated, so ffmpeg carries a single-use ingest key
    // scoped to this one file (not the viewer's bearer): it stays out of ffmpeg's
    // argv and outlives a short access token so long transcodes never 401 mid-run.
    // Non-library sources (disc rips, absolute paths — no folder ULID) keep their
    // direct filesystem path and no key.
    private async Task<(
        string InputPath,
        string[]? ExtraInputArgs,
        string? IngestKey
    )> ResolveInputAsync(MediaContext context, VideoFile file, CancellationToken ct)
    {
        string localPath = storage.CombinePath(parent: file.HostFolder, child: file.Filename);

        if (!Ulid.TryParse(base32: file.Share, ulid: out Ulid folderId))
            return (localPath, null, null);

        bool folderExists = await context
            .Folders.AsNoTracking()
            .AnyAsync(predicate: f => f.Id == folderId, cancellationToken: ct);
        if (!folderExists)
            return (localPath, null, null);

        string servedPath = BuildServedUrl(file: file);
        string ingestKey = ingestKeyStore.Issue(servedPath: servedPath);
        int httpPort = RuntimeServerSettings.Current.InternalServerPort + 1;
        string url = $"http://127.0.0.1:{httpPort}{EncodeServedPath(path: servedPath)}";
        string[] headers = ["-headers", $"X-NoMercy-Ingest-Key: {ingestKey}\r\n"];
        return (url, headers, ingestKey);
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
                predicate: m => m.Id == movieId && m.Library.LibraryUsers.Any(u => u.UserId == userId),
                cancellationToken: ct
            );
            if (fromMovie)
                return true;
        }

        if (file.EpisodeId is int episodeId)
        {
            bool fromEpisode = await context.Episodes.AnyAsync(
                predicate: e => e.Id == episodeId && e.Tv.Library.LibraryUsers.Any(u => u.UserId == userId),
                cancellationToken: ct
            );
            if (fromEpisode)
                return true;
        }

        return false;
    }

    private sealed record AuthorizedFile(
        VideoFile File,
        string InputPath,
        string[]? ExtraInputArgs,
        string? IngestKey
    );
}
