namespace NoMercy.Encoder.LiveTranscode;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

public class LiveSession : ILiveSession
{
    private readonly Channel<Segment> _segmentChannel = Channel.CreateUnbounded<Segment>();

    // Protects concurrent seeks: only one seek can manipulate the runner CTS at a time.
    private readonly SemaphoreSlim _seekLock = new(1, 1);

    // Tracks the current runner's cancellation source. Replaced on each seek.
    private CancellationTokenSource _runnerCts = new();

    // Linked to _runnerCts; cancelled when the whole session is disposed.
    private readonly CancellationTokenSource _sessionCts = new();

    private int _state = (int)LiveSessionState.Starting;
    private long _playbackPositionTicks;
    private TimeSpan _transcodedPosition;
    private double _currentSpeed;

    // Injected by LiveEncoder after construction via AttachRunnerFactory.
    private Func<TimeSpan, CancellationToken, Task>? _runnerFactory;

    /// <summary>
    /// Fires when the CURRENT runner should terminate. Replaced on each seek.
    /// The outer session lifetime is tracked via <see cref="_sessionCts"/>.
    /// </summary>
    public CancellationToken RunnerCancellation => _runnerCts.Token;

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

    public void AttachRunnerFactory(Func<TimeSpan, CancellationToken, Task> factory)
    {
        _runnerFactory = factory;
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

    /// <summary>
    /// Cancels the current FFmpeg runner, resets position state, then spawns
    /// a new runner from <paramref name="position"/> via the attached factory.
    /// </summary>
    public async Task SeekAsync(TimeSpan position, CancellationToken ct)
    {
        await _seekLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _state, (int)LiveSessionState.Seeking);

            // Tear down existing runner
            CancellationTokenSource oldCts = _runnerCts;
            try
            {
                await oldCts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }
            finally
            {
                oldCts.Dispose();
            }

            // Reset position bookkeeping
            Interlocked.Exchange(ref _playbackPositionTicks, position.Ticks);
            _transcodedPosition = position;

            // Create a new CTS for the replacement runner, linked to session lifetime
            _runnerCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);

            // Spawn new runner if a factory is wired up
            if (_runnerFactory is not null)
            {
                Volatile.Write(ref _state, (int)LiveSessionState.Transcoding);

                _ = Task.Run(
                    () => _runnerFactory(position, _runnerCts.Token),
                    CancellationToken.None
                );
            }
        }
        finally
        {
            _seekLock.Release();
        }
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
        try
        {
            _sessionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        try
        {
            _runnerCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        Volatile.Write(ref _state, (int)LiveSessionState.Ended);
        _segmentChannel.Writer.TryComplete();

        _seekLock.Dispose();
        _runnerCts.Dispose();
        _sessionCts.Dispose();
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
