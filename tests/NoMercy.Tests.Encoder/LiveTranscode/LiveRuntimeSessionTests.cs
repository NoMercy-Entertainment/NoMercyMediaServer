using Moq;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// Concurrent-state contract for <see cref="LiveRuntimeSession"/>. This is
/// the per-session buffer the live-transcode HTTP handler reads from while
/// the FFmpeg drainer writes into it. If buffering, completion, or the
/// idle-reaper's LastAccess get the threading wrong, the symptoms are:
///
/// • Stale segments served back to the client (CompareExchange loop broken).
/// • Idle reaper killing an active session (TouchLastAccess race).
/// • IsComplete flipping back to false (write order).
/// • SnapshotSegments missing buffered entries (ConcurrentDictionary misuse).
/// • DisposeAsync hanging when the drainer task never registered.
///
/// Each test pins one of those failure modes with a deterministic call shape.
/// </summary>
public class LiveRuntimeSessionTests
{
    private static Mock<ILiveSession> SessionMock()
    {
        Mock<ILiveSession> session = new();
        session.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
        return session;
    }

    private static LiveRuntimeSession Build(
        Mock<ILiveSession>? session = null,
        TimeSpan? target = null,
        string? scratchDir = null
    ) =>
        new(
            (session ?? SessionMock()).Object,
            target ?? TimeSpan.FromSeconds(4),
            scratchDirectory: scratchDir
        );

    private static Segment Seg(int index, double startSec = 0, double durSec = 4) =>
        new(
            Index: index,
            StartTime: TimeSpan.FromSeconds(startSec),
            Duration: TimeSpan.FromSeconds(durSec),
            FilePath: $"/scratch/segment-{index}.ts",
            SizeBytes: 1024
        );

    // ── Construction state ───────────────────────────────────────────────────

    [Fact]
    public void Newly_constructed_session_has_no_segments_and_not_complete()
    {
        LiveRuntimeSession runtime = Build();

        runtime.IsComplete.Should().BeFalse();
        runtime.HighestSegmentIndex.Should().Be(-1);
        runtime.SnapshotSegments().Should().BeEmpty();
    }

    [Fact]
    public void Newly_constructed_session_carries_target_duration_and_scratch_dir()
    {
        LiveRuntimeSession runtime = Build(
            target: TimeSpan.FromSeconds(6),
            scratchDir: "/tmp/live-1"
        );

        runtime.TargetSegmentDuration.Should().Be(TimeSpan.FromSeconds(6));
        runtime.ScratchDirectory.Should().Be("/tmp/live-1");
    }

    [Fact]
    public void CachedMediaInfo_and_ClientCapabilities_default_to_null()
    {
        // Set externally by LiveEncoder after probe completes — must default null.
        LiveRuntimeSession runtime = Build();

        runtime.CachedMediaInfo.Should().BeNull();
        runtime.ClientCapabilities.Should().BeNull();
    }

    // ── BufferSegment + HighestSegmentIndex ──────────────────────────────────

    [Fact]
    public void BufferSegment_first_segment_sets_highest_to_segment_index()
    {
        LiveRuntimeSession runtime = Build();
        InvokeBuffer(runtime, Seg(0));

        runtime.HighestSegmentIndex.Should().Be(0);
    }

    [Fact]
    public void BufferSegment_monotonic_indices_raise_highest()
    {
        LiveRuntimeSession runtime = Build();
        InvokeBuffer(runtime, Seg(0));
        InvokeBuffer(runtime, Seg(1));
        InvokeBuffer(runtime, Seg(2));

        runtime.HighestSegmentIndex.Should().Be(2);
    }

    [Fact]
    public void BufferSegment_out_of_order_lower_index_does_not_lower_highest()
    {
        // Drainer can write segments out-of-order on retry. Highest must NOT
        // drop back — clients use HighestSegmentIndex to know what's available.
        LiveRuntimeSession runtime = Build();
        InvokeBuffer(runtime, Seg(0));
        InvokeBuffer(runtime, Seg(5));
        InvokeBuffer(runtime, Seg(2));

        runtime.HighestSegmentIndex.Should().Be(5);
    }

    [Fact]
    public void BufferSegment_replacing_segment_at_same_index_keeps_highest_unchanged()
    {
        // CompareExchange loop must short-circuit when new == current.
        LiveRuntimeSession runtime = Build();
        InvokeBuffer(runtime, Seg(3));
        InvokeBuffer(runtime, Seg(3, startSec: 12, durSec: 5)); // replacement

        runtime.HighestSegmentIndex.Should().Be(3);
    }

    // ── TryGetSegment ────────────────────────────────────────────────────────

    [Fact]
    public void TryGetSegment_returns_buffered_segment_by_index()
    {
        LiveRuntimeSession runtime = Build();
        Segment original = Seg(7, startSec: 28);
        InvokeBuffer(runtime, original);

        bool found = runtime.TryGetSegment(7, out Segment retrieved);

        found.Should().BeTrue();
        retrieved.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void TryGetSegment_returns_false_for_missing_index()
    {
        LiveRuntimeSession runtime = Build();
        InvokeBuffer(runtime, Seg(0));

        bool found = runtime.TryGetSegment(99, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetSegment_returns_replacement_after_BufferSegment_overwrites_index()
    {
        // Drainer occasionally rewrites a segment (e.g. re-encode on quality change).
        // The dictionary must yield the LATEST value, not the original.
        LiveRuntimeSession runtime = Build();
        InvokeBuffer(runtime, Seg(2, startSec: 8, durSec: 4));
        Segment replacement = Seg(2, startSec: 8, durSec: 5);
        InvokeBuffer(runtime, replacement);

        runtime.TryGetSegment(2, out Segment seg);
        seg.Should().BeEquivalentTo(replacement);
    }

    // ── SnapshotSegments ─────────────────────────────────────────────────────

    [Fact]
    public void SnapshotSegments_returns_segments_sorted_by_index()
    {
        // ConcurrentDictionary enumeration is unordered — Snapshot MUST sort.
        // The playlist generator relies on this for HLS sequence numbering.
        LiveRuntimeSession runtime = Build();
        InvokeBuffer(runtime, Seg(5));
        InvokeBuffer(runtime, Seg(1));
        InvokeBuffer(runtime, Seg(3));
        InvokeBuffer(runtime, Seg(0));

        IReadOnlyList<Segment> snapshot = runtime.SnapshotSegments();

        snapshot.Select(s => s.Index).Should().Equal(0, 1, 3, 5);
    }

    [Fact]
    public void SnapshotSegments_returns_independent_copy_of_buffer()
    {
        // Snapshot is a List<Segment>, not a live view — adding more after
        // snapshot must NOT mutate the returned list.
        LiveRuntimeSession runtime = Build();
        InvokeBuffer(runtime, Seg(0));
        IReadOnlyList<Segment> snapshot = runtime.SnapshotSegments();

        InvokeBuffer(runtime, Seg(1));

        snapshot.Should().HaveCount(1);
        snapshot[0].Index.Should().Be(0);
    }

    // ── MarkComplete + IsComplete ────────────────────────────────────────────

    [Fact]
    public void IsComplete_flips_to_true_after_MarkComplete()
    {
        LiveRuntimeSession runtime = Build();

        runtime.IsComplete.Should().BeFalse();
        InvokeMarkComplete(runtime);
        runtime.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void MarkComplete_is_idempotent()
    {
        // Drainer may call MarkComplete more than once on retry paths — must
        // never flip back to false.
        LiveRuntimeSession runtime = Build();
        InvokeMarkComplete(runtime);
        InvokeMarkComplete(runtime);

        runtime.IsComplete.Should().BeTrue();
    }

    // ── TouchLastAccess + LastAccess ─────────────────────────────────────────

    [Fact]
    public void LastAccess_initialized_to_construction_time()
    {
        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        LiveRuntimeSession runtime = Build();
        DateTime after = DateTime.UtcNow.AddSeconds(1);

        runtime.LastAccess.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void TouchLastAccess_advances_LastAccess_forward()
    {
        LiveRuntimeSession runtime = Build();
        DateTime initial = runtime.LastAccess;
        // Windows DateTime.UtcNow resolution is ~15.6ms. 50ms guarantees a tick.
        Thread.Sleep(50);

        runtime.TouchLastAccess();

        runtime.LastAccess.Should().BeAfter(initial);
    }

    [Fact]
    public void LastAccess_is_always_utc_kind()
    {
        // Idle reaper compares with DateTime.UtcNow — wrong Kind makes that
        // comparison silently lose 1-hour-of-DST worth of accuracy.
        LiveRuntimeSession runtime = Build();
        runtime.LastAccess.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ── DisposeAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_with_no_drainer_task_completes_cleanly()
    {
        Mock<ILiveSession> session = SessionMock();
        LiveRuntimeSession runtime = Build(session);

        await runtime.DisposeAsync();

        session.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_awaits_drainer_task_and_disposes_inner_session()
    {
        Mock<ILiveSession> session = SessionMock();
        LiveRuntimeSession runtime = Build(session);

        TaskCompletionSource<bool> drainerStarted = new();
        Task drainer = Task.Run(async () =>
        {
            drainerStarted.SetResult(true);
            await Task.Delay(Timeout.Infinite, runtime.DrainerCancellation);
        });
        SetDrainerTask(runtime, drainer);

        await drainerStarted.Task;
        await runtime.DisposeAsync();

        drainer.IsCompleted.Should().BeTrue();
        session.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_swallows_OperationCanceledException_from_drainer()
    {
        Mock<ILiveSession> session = SessionMock();
        LiveRuntimeSession runtime = Build(session);

        Task drainer = Task.Run(() => Task.Delay(Timeout.Infinite, runtime.DrainerCancellation));
        SetDrainerTask(runtime, drainer);

        // DisposeAsync must not bubble the OperationCanceledException from
        // the drainer task — that exception is expected on shutdown.
        Func<Task> act = () => runtime.DisposeAsync().AsTask();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_called_twice_does_not_throw_ObjectDisposedException()
    {
        // Idle reaper + outer container both call DisposeAsync — second call
        // must be safe (catches ObjectDisposedException internally).
        Mock<ILiveSession> session = SessionMock();
        LiveRuntimeSession runtime = Build(session);

        await runtime.DisposeAsync();
        Func<Task> act = () => runtime.DisposeAsync().AsTask();
        await act.Should().NotThrowAsync();
    }

    // ── Reflection helpers ───────────────────────────────────────────────────
    //
    // BufferSegment, MarkComplete, and DrainerTask are internal but live in the
    // production assembly which we don't grant InternalsVisibleTo. Reflect into
    // them — these are the actual seams the FFmpeg drainer hits and they're
    // exactly what we want to pin.

    private static void InvokeBuffer(LiveRuntimeSession runtime, Segment segment) =>
        typeof(LiveRuntimeSession)
            .GetMethod(
                "BufferSegment",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .Invoke(runtime, [segment]);

    private static void InvokeMarkComplete(LiveRuntimeSession runtime) =>
        typeof(LiveRuntimeSession)
            .GetMethod(
                "MarkComplete",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .Invoke(runtime, []);

    private static void SetDrainerTask(LiveRuntimeSession runtime, Task task) =>
        typeof(LiveRuntimeSession)
            .GetProperty(
                "DrainerTask",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .SetValue(runtime, task);
}
