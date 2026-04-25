namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveSession : IAsyncDisposable
{
    string SessionId { get; }
    LiveSessionState State { get; }
    LiveQuality CurrentQuality { get; }
    double CurrentSpeed { get; }
    TimeSpan TranscodedPosition { get; }
    TimeSpan BufferAhead { get; }
    IAsyncEnumerable<Segment> Segments { get; }

    /// <summary>
    /// Tears down the current FFmpeg runner and spawns a new one starting at
    /// <paramref name="position"/>. The caller receives control back once the
    /// new runner is dispatched; segment flow resumes asynchronously.
    /// </summary>
    Task SeekAsync(TimeSpan position, CancellationToken ct);

    /// <summary>
    /// Tears down the current FFmpeg runner and spawns a new one using
    /// <paramref name="newQuality"/>. Resumes from the current playback
    /// position so the viewer does not jump backward.
    /// </summary>
    Task ChangeQualityAsync(string qualityId, LiveQuality newQuality, CancellationToken ct);
    void Suspend();
    void Resume();
    void ReportPlaybackPosition(TimeSpan position);

    /// <summary>
    /// Attaches the factory that <see cref="SeekAsync"/> uses to spawn a
    /// replacement runner. Called once by <see cref="LiveEncoder"/> immediately
    /// after the session is created.
    /// </summary>
    void AttachRunnerFactory(Func<TimeSpan, CancellationToken, Task> factory);
}
