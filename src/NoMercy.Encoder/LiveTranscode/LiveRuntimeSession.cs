namespace NoMercy.Encoder.LiveTranscode;

using System.Collections.Concurrent;

public sealed class LiveRuntimeSession : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, Segment> _segments = new();
    private readonly CancellationTokenSource _drainerCts = new();
    private int _highestIndex = -1;
    private int _isComplete;

    public ILiveSession Session { get; }
    public TimeSpan TargetSegmentDuration { get; }

    internal Task? DrainerTask { get; set; }

    public LiveRuntimeSession(ILiveSession session, TimeSpan targetSegmentDuration)
    {
        Session = session;
        TargetSegmentDuration = targetSegmentDuration;
    }

    public bool IsComplete => Volatile.Read(ref _isComplete) == 1;

    public int HighestSegmentIndex => Volatile.Read(ref _highestIndex);

    public bool TryGetSegment(int index, out Segment segment) =>
        _segments.TryGetValue(index, out segment!);

    /// <summary>
    /// Snapshot of buffered segments ordered by index. Safe to call concurrently
    /// with the drainer — callers see whatever segments were buffered at the
    /// moment of the snapshot.
    /// </summary>
    public IReadOnlyList<Segment> SnapshotSegments()
    {
        return _segments.Values.OrderBy(s => s.Index).ToList();
    }

    internal CancellationToken DrainerCancellation => _drainerCts.Token;

    internal void BufferSegment(Segment segment)
    {
        _segments[segment.Index] = segment;

        int current;
        do
        {
            current = Volatile.Read(ref _highestIndex);
            if (segment.Index <= current)
                break;
        } while (Interlocked.CompareExchange(ref _highestIndex, segment.Index, current) != current);
    }

    internal void MarkComplete() => Interlocked.Exchange(ref _isComplete, 1);

    public async ValueTask DisposeAsync()
    {
        try
        {
            _drainerCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        if (DrainerTask is not null)
        {
            try
            {
                await DrainerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }

        await Session.DisposeAsync().ConfigureAwait(false);
        _drainerCts.Dispose();
    }
}
