namespace NoMercy.Encoder.LiveTranscode;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NoMercy.Storage;

/// <summary>
/// Singleton that holds live transcode runtime state — maps a session id to
/// its <see cref="ILiveSession"/> plus a background task draining the async
/// segment stream into an indexed buffer so the HTTP layer can serve any
/// requested segment id without re-enumerating the source channel.
/// </summary>
public class LiveStreamingService(ILogger<LiveStreamingService> logger, IStorage storage)
    : ILiveStreamingService
{
    private readonly ConcurrentDictionary<string, LiveRuntimeSession> _runtimes = new();

    public IReadOnlyCollection<string> ActiveSessionIds => _runtimes.Keys.ToList();

    public void Register(
        ILiveSession session,
        TimeSpan targetSegmentDuration,
        string? scratchDirectory = null
    )
    {
        LiveRuntimeSession runtime = new(session, targetSegmentDuration, scratchDirectory);
        if (!_runtimes.TryAdd(session.SessionId, runtime))
        {
            throw new InvalidOperationException(
                $"Live session '{session.SessionId}' is already registered"
            );
        }

        runtime.DrainerTask = Task.Run(() => DrainAsync(runtime));
        logger.LogDebug("Registered live session {SessionId}", session.SessionId);
    }

    public bool TryGetRuntime(string sessionId, out LiveRuntimeSession runtime)
    {
        return _runtimes.TryGetValue(sessionId, out runtime!);
    }

    public async Task RemoveAsync(string sessionId)
    {
        if (_runtimes.TryRemove(sessionId, out LiveRuntimeSession? runtime))
        {
            logger.LogDebug("Removing live session {SessionId}", sessionId);
            await runtime.DisposeAsync().ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(runtime.ScratchDirectory))
            {
                TryDeleteScratch(runtime.ScratchDirectory, sessionId);
            }
        }
    }

    private void TryDeleteScratch(string scratchDirectory, string sessionId)
    {
        try
        {
            if (storage.Exists(scratchDirectory))
            {
                storage.DeleteDirectory(scratchDirectory, recursive: true);
                logger.LogDebug(
                    "Deleted live session scratch {Dir} for {SessionId}",
                    scratchDirectory,
                    sessionId
                );
            }
        }
        catch (Exception ex)
        {
            // Best-effort: a segment might still be held open by Windows until
            // the FFmpeg handle fully releases. The temp dir will be cleaned
            // up on the next server start or by the OS temp sweep.
            logger.LogWarning(
                ex,
                "Could not delete scratch {Dir} for live session {SessionId}",
                scratchDirectory,
                sessionId
            );
        }
    }

    private async Task DrainAsync(LiveRuntimeSession runtime)
    {
        try
        {
            await foreach (
                Segment segment in runtime.Session.Segments.WithCancellation(
                    runtime.DrainerCancellation
                )
            )
            {
                runtime.BufferSegment(segment);
            }

            runtime.MarkComplete();
            logger.LogDebug(
                "Drainer for {SessionId} completed — buffered {Count} segments",
                runtime.Session.SessionId,
                runtime.HighestSegmentIndex + 1
            );
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Drainer for live session {SessionId} failed",
                runtime.Session.SessionId
            );
            runtime.MarkComplete();
        }
    }
}
