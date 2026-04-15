namespace NoMercy.Encoder.LiveTranscode;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Hardware;

public class LiveEncoder(
    ILiveQualitySelector qualitySelector,
    ISessionManager sessionManager,
    SpeedIndex speedIndex,
    IResourceBudget budget,
    ILogger<LiveEncoder> logger
) : ILiveEncoder
{
    public Task<ILiveSession> StartAsync(LiveEncodeRequest request, CancellationToken ct)
    {
        logger.LogInformation("Starting live session for {Path}", request.InputPath);

        // Check session limits
        if (!sessionManager.CanStartSession())
        {
            throw new InvalidOperationException("Maximum concurrent live sessions reached");
        }

        // Select quality
        LiveQuality quality;
        if (request.PreferredQuality is not null)
        {
            LiveQuality[] available = qualitySelector.GetAvailableQualities(
                request.CachedInfo,
                request.Client,
                speedIndex,
                budget
            );
            quality =
                available.FirstOrDefault(q => q.Id == request.PreferredQuality)
                ?? qualitySelector.SelectOptimal(
                    request.CachedInfo,
                    request.Client,
                    speedIndex,
                    budget
                );
        }
        else
        {
            quality = qualitySelector.SelectOptimal(
                request.CachedInfo,
                request.Client,
                speedIndex,
                budget
            );
        }

        // Create session
        string sessionId = Ulid.NewUlid().ToString();
        LiveSession session = new(sessionId, quality);
        session.SetState(LiveSessionState.Starting);

        // Register
        sessionManager.RegisterSession(session);

        logger.LogInformation(
            "Live session {SessionId} started at {Quality}",
            sessionId,
            quality.Label
        );

        session.SetState(LiveSessionState.Transcoding);

        return Task.FromResult<ILiveSession>(session);
    }
}
