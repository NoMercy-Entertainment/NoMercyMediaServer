using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Background service that sweeps active live sessions every 30 seconds and
/// disposes any session that has had no playlist or segment hit for longer than
/// <see cref="LiveSessionLimits.IdleTimeoutMinutes"/>. Temp directories are
/// cleaned by <see cref="ILiveStreamingService.RemoveAsync"/>.
/// </summary>
public class LiveSessionIdleReaper(
    ILiveStreamingService streamingService,
    ISessionManager sessionManager,
    LiveSessionLimits limits,
    ILogger<LiveSessionIdleReaper> logger
) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug(
            "LiveSessionIdleReaper started (idle timeout = {Min} min)",
            limits.IdleTimeoutMinutes
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await SweepAsync().ConfigureAwait(false);
        }
    }

    internal async Task SweepAsync()
    {
        DateTime cutoff = DateTime.UtcNow.AddMinutes(-limits.IdleTimeoutMinutes);

        List<string> toEvict = [];
        foreach (string sessionId in streamingService.ActiveSessionIds)
        {
            if (!streamingService.TryGetRuntime(sessionId, out LiveRuntimeSession runtime))
                continue;

            if (runtime.IsComplete)
                continue;

            if (runtime.LastAccess < cutoff)
            {
                toEvict.Add(sessionId);
            }
        }

        foreach (string sessionId in toEvict)
        {
            logger.LogInformation(
                "LiveSessionIdleReaper evicting idle session {SessionId}",
                sessionId
            );

            try
            {
                await streamingService.RemoveAsync(sessionId).ConfigureAwait(false);
                sessionManager.RemoveSession(sessionId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "LiveSessionIdleReaper failed to evict session {SessionId}",
                    sessionId
                );
            }
        }
    }
}
