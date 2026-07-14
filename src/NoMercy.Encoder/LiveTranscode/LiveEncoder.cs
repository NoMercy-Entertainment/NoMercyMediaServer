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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.LiveTranscode;

public class LiveEncoder(
    ILiveQualitySelector qualitySelector,
    ISessionManager sessionManager,
    ILiveStreamingService streamingService,
    ILiveFfmpegRunner runner,
    EncoderOptions options,
    SpeedIndex speedIndex,
    IResourceBudget budget,
    ILogger<LiveEncoder> logger
) : ILiveEncoder
{
    public Task<ILiveSession> StartAsync(LiveEncodeRequest request, CancellationToken ct)
    {
        logger.LogInformation("Starting live session for {Path}", request.InputPath);

        if (!sessionManager.CanStartSession())
        {
            throw new InvalidOperationException("Maximum concurrent live sessions reached");
        }

        LiveQuality quality = ResolveQuality(request);

        string sessionId = Ulid.NewUlid().ToString();
        LiveSession session = new(sessionId, quality);

        // The default audio track (resolved from the library's language
        // preference) so the first spawn — and every seek/quality re-spawn that
        // reads it back — maps the viewer's language, not just the file's first.
        session.SetAudioStreamIndex(request.AudioStreamIndex);

        // Seed the playhead at the start position. Segments are absolutely indexed
        // so a resumed/seeked session's transcoded position is absolute too; if the
        // playhead stayed at zero, BufferAhead would read as the whole start offset
        // and the buffer-adaptive sweep could suspend the encoder before the client
        // has fetched its first segment. Segment fetches keep it current thereafter.
        session.ReportPlaybackPosition(request.StartPosition);
        session.SetState(LiveSessionState.Starting);

        // Flip to Transcoding eagerly so API consumers see the session as live
        // the moment StartAsync returns. The runner kicks in async — it will
        // downgrade to Error if FFmpeg fails to start.
        session.SetState(LiveSessionState.Transcoding);

        string outputDirectory = Path.Combine(
            options.ResolvedLiveTranscodeCachePath,
            $"lts-{sessionId}"
        );

        streamingService.Register(
            session,
            TimeSpan.FromSeconds(options.DefaultSegmentDurationSeconds),
            outputDirectory
        );

        streamingService.StampRequestContext(sessionId, request.CachedInfo, request.Client);

        // Build the run-input factory so SeekAsync / ChangeQualityAsync can
        // restart the runner without coupling LiveSession to LiveFfmpegRunner.
        // Quality is read from session.CurrentQuality at spawn time so that a
        // quality change performed before the factory fires uses the new value.
        async Task SpawnRunner(TimeSpan startPosition, CancellationToken runnerCt)
        {
            LiveRunInput runInput = new(
                InputPath: request.InputPath,
                OutputDirectory: outputDirectory,
                StartPosition: startPosition,
                Quality: session.CurrentQuality,
                SegmentDurationSeconds: options.DefaultSegmentDurationSeconds,
                Client: request.Client,
                SourceInfo: request.CachedInfo,
                CustomArguments: request.CustomArguments,
                ExtraInputArgs: request.ExtraInputArgs,
                // Read back at spawn time so an audio switch performed before the
                // factory fires (like a quality change) uses the new track.
                AudioStreamIndex: session.CurrentAudioStreamIndex,
                VideoOnly: request.VideoOnly
            );

            try
            {
                await runner.RunAsync(runInput, session, runnerCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on seek/dispose
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Live runner task faulted for session {SessionId}", sessionId);
                // Without this the session lingers in Running state forever and the reaper
                // can't tell a wedged runner apart from a live one.
                session.SetState(LiveSessionState.Error);
            }
        }

        session.AttachRunnerFactory(SpawnRunner);

        session.MarkTranscodeStart();
        _ = Task.Run(
            () => SpawnRunner(request.StartPosition, session.RunnerCancellation),
            CancellationToken.None
        );

        logger.LogInformation(
            "Live session {SessionId} started at {Quality}",
            sessionId,
            quality.Label
        );

        return Task.FromResult<ILiveSession>(session);
    }

    public Task<ILiveSession> StartAudioRenditionAsync(
        LiveEncodeRequest request,
        CancellationToken ct
    )
    {
        // No cap check: an audio child rides on a video session that already
        // passed the cap. It carries a placeholder quality — audio-only runs skip
        // the whole video block, so width/height/encoder are never read.
        LiveQuality audioQuality = new(
            Id: "audio",
            Label: "Audio",
            Width: 0,
            Height: 0,
            Codec: Codecs.VideoCodecType.H264,
            BitrateKbps: 128,
            Encoder: "aac",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 1.0,
            CanRealtime: true
        );

        string sessionId = Ulid.NewUlid().ToString();
        LiveSession session = new(sessionId, audioQuality);
        session.SetAudioStreamIndex(request.AudioStreamIndex);
        session.ReportPlaybackPosition(request.StartPosition);
        session.SetState(LiveSessionState.Transcoding);

        string outputDirectory = Path.Combine(
            options.ResolvedLiveTranscodeCachePath,
            $"lts-{sessionId}"
        );

        streamingService.Register(
            session,
            TimeSpan.FromSeconds(options.DefaultSegmentDurationSeconds),
            outputDirectory,
            isAudioRenditionChild: true
        );

        async Task SpawnRunner(TimeSpan startPosition, CancellationToken runnerCt)
        {
            LiveRunInput runInput = new(
                InputPath: request.InputPath,
                OutputDirectory: outputDirectory,
                StartPosition: startPosition,
                Quality: session.CurrentQuality,
                SegmentDurationSeconds: options.DefaultSegmentDurationSeconds,
                Client: request.Client,
                SourceInfo: request.CachedInfo,
                CustomArguments: request.CustomArguments,
                ExtraInputArgs: request.ExtraInputArgs,
                AudioStreamIndex: session.CurrentAudioStreamIndex,
                AudioRenditionOnly: true
            );

            try
            {
                await runner.RunAsync(runInput, session, runnerCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on seek/dispose
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Live audio runner faulted for session {SessionId}", sessionId);
                session.SetState(LiveSessionState.Error);
            }
        }

        session.AttachRunnerFactory(SpawnRunner);
        session.MarkTranscodeStart();
        _ = Task.Run(
            () => SpawnRunner(request.StartPosition, session.RunnerCancellation),
            CancellationToken.None
        );

        logger.LogInformation(
            "Live audio rendition {SessionId} started for stream 0:a:{Index}",
            sessionId,
            request.AudioStreamIndex
        );

        return Task.FromResult<ILiveSession>(session);
    }

    private LiveQuality ResolveQuality(LiveEncodeRequest request)
    {
        if (request.PreferredQuality is not null)
        {
            LiveQuality[] available = qualitySelector.GetAvailableQualities(
                request.CachedInfo,
                request.Client,
                speedIndex,
                budget
            );

            LiveQuality? match = available.FirstOrDefault(q => q.Id == request.PreferredQuality);
            if (match is not null)
                return match;
        }

        return qualitySelector.SelectOptimal(
            request.CachedInfo,
            request.Client,
            speedIndex,
            budget
        );
    }
}
