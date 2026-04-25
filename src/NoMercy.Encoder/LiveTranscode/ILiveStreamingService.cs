namespace NoMercy.Encoder.LiveTranscode;

using NoMercy.Encoder.Analysis;

public interface ILiveStreamingService
{
    /// <summary>
    /// Registers a live session, starting a background task that drains its
    /// segment channel into an indexed buffer so any segment can be served
    /// non-sequentially by id. When <paramref name="scratchDirectory"/> is
    /// provided, <see cref="RemoveAsync"/> will best-effort delete it after
    /// disposing the session.
    /// </summary>
    void Register(
        ILiveSession session,
        TimeSpan targetSegmentDuration,
        string? scratchDirectory = null
    );

    /// <summary>
    /// Stores the original media analysis context on an already-registered
    /// runtime so the quality-change endpoint can enumerate available qualities
    /// without re-probing the file.
    /// </summary>
    void StampRequestContext(string sessionId, MediaInfo mediaInfo, ClientCapabilities client);

    bool TryGetRuntime(string sessionId, out LiveRuntimeSession runtime);

    Task RemoveAsync(string sessionId);

    IReadOnlyCollection<string> ActiveSessionIds { get; }

    /// <summary>
    /// Returns a point-in-time snapshot of all currently registered live sessions.
    /// Safe to call concurrently — enumerates the underlying dictionary under a
    /// consistent view and projects each runtime into an immutable snapshot record.
    /// </summary>
    IReadOnlyList<LiveSessionSnapshot> GetActiveSessions();
}
