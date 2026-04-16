namespace NoMercy.Encoder.LiveTranscode;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;

public class LiveEncoder(
    ILiveQualitySelector qualitySelector,
    ISessionManager sessionManager,
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

        LiveRunInput runInput = new(
            InputPath: request.InputPath,
            OutputDirectory: outputDirectory,
            StartPosition: request.StartPosition,
            Quality: quality,
            SegmentDurationSeconds: options.DefaultSegmentDurationSeconds
        );

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await runner
                        .RunAsync(runInput, session, session.RunnerCancellation)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Live runner task faulted for session {SessionId}",
                        sessionId
                    );
                }
            },
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
