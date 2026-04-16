namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveStreamingService
{
    /// <summary>
    /// Registers a live session, starting a background task that drains its
    /// segment channel into an indexed buffer so any segment can be served
    /// non-sequentially by id.
    /// </summary>
    void Register(ILiveSession session, TimeSpan targetSegmentDuration);

    bool TryGetRuntime(string sessionId, out LiveRuntimeSession runtime);

    Task RemoveAsync(string sessionId);

    IReadOnlyCollection<string> ActiveSessionIds { get; }
}
