namespace NoMercy.Encoder.V3.LiveTranscode;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

public class LiveSession : ILiveSession
{
    private readonly Channel<Segment> _segmentChannel = Channel.CreateUnbounded<Segment>();

    private LiveSessionState _state = LiveSessionState.Starting;
    private TimeSpan _playbackPosition;
    private TimeSpan _transcodedPosition;
    private double _currentSpeed;

    public string SessionId { get; }
    public LiveSessionState State => _state;
    public LiveQuality CurrentQuality { get; private set; }
    public double CurrentSpeed => _currentSpeed;
    public TimeSpan TranscodedPosition => _transcodedPosition;
    public TimeSpan BufferAhead => _transcodedPosition - _playbackPosition;

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

    internal void SetState(LiveSessionState state) => _state = state;

    internal void SetSpeed(double speed) => _currentSpeed = speed;

    internal void Complete() => _segmentChannel.Writer.Complete();

    public Task SeekAsync(TimeSpan position, CancellationToken ct)
    {
        _state = LiveSessionState.Seeking;
        _playbackPosition = position;
        _transcodedPosition = position;
        return Task.CompletedTask;
    }

    public Task ChangeQualityAsync(string qualityId, CancellationToken ct)
    {
        _state = LiveSessionState.ChangingQuality;
        return Task.CompletedTask;
    }

    public void Suspend()
    {
        if (_state == LiveSessionState.Transcoding)
            _state = LiveSessionState.Buffered;
    }

    public void Resume()
    {
        if (_state == LiveSessionState.Buffered)
            _state = LiveSessionState.Transcoding;
    }

    public void ReportPlaybackPosition(TimeSpan position) => _playbackPosition = position;

    public ValueTask DisposeAsync()
    {
        _state = LiveSessionState.Ended;
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
