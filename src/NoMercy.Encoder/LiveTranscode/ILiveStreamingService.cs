namespace NoMercy.Encoder.LiveTranscode;

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

    bool TryGetRuntime(string sessionId, out LiveRuntimeSession runtime);

    Task RemoveAsync(string sessionId);

    IReadOnlyCollection<string> ActiveSessionIds { get; }
}
