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
        session.SetState(LiveSessionState.Starting);

        sessionManager.RegisterSession(session);

        // Flip to Transcoding eagerly so API consumers see the session as live
        // the moment StartAsync returns. The runner kicks in async — it will
        // downgrade to Error if FFmpeg fails to start.
        session.SetState(LiveSessionState.Transcoding);

        string outputDirectory = Path.Combine(options.ResolvedLiveTranscodeCachePath, sessionId);

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
                SourceInfo: request.CachedInfo
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
