namespace NoMercy.Encoder.LiveTranscode;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

public class LiveSession : ILiveSession
{
    private readonly Channel<Segment> _segmentChannel = Channel.CreateUnbounded<Segment>();

    private int _state = (int)LiveSessionState.Starting;
    private long _playbackPositionTicks;
    private TimeSpan _transcodedPosition;
    private double _currentSpeed;

    public string SessionId { get; }
    public LiveSessionState State => (LiveSessionState)Volatile.Read(ref _state);
    public LiveQuality CurrentQuality { get; private set; }
    public double CurrentSpeed => _currentSpeed;
    public TimeSpan TranscodedPosition => _transcodedPosition;
    public TimeSpan BufferAhead =>
        _transcodedPosition - new TimeSpan(Interlocked.Read(ref _playbackPositionTicks));

    public IAsyncEnumerable<Segment> Segments => ReadSegmentsAsync();

    public LiveSession(string sessionId, LiveQuality quality)
    {
        SessionId = sessionId;
        CurrentQuality = quality;
    }

    // Called by the encoder to push completed segments into the channel
    internal void PushSegment(Segment segment)
    {
        _transcodedPosition = segment.StartTime + segment.Duration;
        _segmentChannel.Writer.TryWrite(segment);
    }

    internal void SetState(LiveSessionState state) => Volatile.Write(ref _state, (int)state);

    internal void SetSpeed(double speed) => _currentSpeed = speed;

    internal void Complete() => _segmentChannel.Writer.Complete();

    public Task SeekAsync(TimeSpan position, CancellationToken ct)
    {
        Volatile.Write(ref _state, (int)LiveSessionState.Seeking);
        Interlocked.Exchange(ref _playbackPositionTicks, position.Ticks);
        _transcodedPosition = position;
        return Task.CompletedTask;
    }

    public Task ChangeQualityAsync(string qualityId, CancellationToken ct)
    {
        Volatile.Write(ref _state, (int)LiveSessionState.ChangingQuality);
        return Task.CompletedTask;
    }

    public void Suspend()
    {
        Interlocked.CompareExchange(
            ref _state,
            (int)LiveSessionState.Buffered,
            (int)LiveSessionState.Transcoding
        );
    }

    public void Resume()
    {
        Interlocked.CompareExchange(
            ref _state,
            (int)LiveSessionState.Transcoding,
            (int)LiveSessionState.Buffered
        );
    }

    public void ReportPlaybackPosition(TimeSpan position) =>
        Interlocked.Exchange(ref _playbackPositionTicks, position.Ticks);

    public ValueTask DisposeAsync()
    {
        Volatile.Write(ref _state, (int)LiveSessionState.Ended);
        _segmentChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private async IAsyncEnumerable<Segment> ReadSegmentsAsync(
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await foreach (Segment segment in _segmentChannel.Reader.ReadAllAsync(ct))
        {
            yield return segment;
        }
    }
}
