namespace NoMercy.Encoder.V3.LiveTranscode;

public interface ILiveSession : IAsyncDisposable
{
    string SessionId { get; }
    LiveSessionState State { get; }
    LiveQuality CurrentQuality { get; }
    double CurrentSpeed { get; }
    TimeSpan TranscodedPosition { get; }
    TimeSpan BufferAhead { get; }
    IAsyncEnumerable<Segment> Segments { get; }

    Task SeekAsync(TimeSpan position, CancellationToken ct);
    Task ChangeQualityAsync(string qualityId, CancellationToken ct);
    void Suspend();
    void Resume();
    void ReportPlaybackPosition(TimeSpan position);
}
