using NoMercy.Encoder.Distribution;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// InMemoryTaskProgressStore caches the latest progress per task so the
/// dashboard can serve live progress for distributed encodes without
/// holding a SignalR connection to each worker. The bounded-map + stale-
/// eviction contract prevents disconnected workers from leaking forever.
/// </summary>
public class InMemoryTaskProgressStoreTests
{
    private static TaskProgressSnapshot Snap(
        string taskId,
        DateTime? receivedUtc = null,
        double percent = 50.0
    ) =>
        new(
            TaskId: taskId,
            WorkerId: "worker-1",
            PercentComplete: percent,
            CurrentFps: 30,
            CurrentSpeed: 1.5,
            CurrentStage: "encode",
            ElapsedSeconds: 100,
            EstimatedRemainingSeconds: 100,
            CurrentTimeSeconds: 50,
            DurationSeconds: 100,
            ReceivedAtUtc: receivedUtc ?? DateTime.UtcNow
        );

    [Fact]
    public void Get_UnknownTask_ReturnsNull()
    {
        InMemoryTaskProgressStore store = new();
        store.Get("missing").Should().BeNull();
    }

    [Fact]
    public void Update_ThenGet_ReturnsLatest()
    {
        InMemoryTaskProgressStore store = new();
        TaskProgressSnapshot snap = Snap("task-1", percent: 50);

        store.Update("task-1", snap);

        store.Get("task-1").Should().BeSameAs(snap);
    }

    [Fact]
    public void Update_Twice_OverwritesPrevious()
    {
        // Latest-wins semantics — store doesn't keep history.
        InMemoryTaskProgressStore store = new();
        store.Update("task-1", Snap("task-1", percent: 25));
        store.Update("task-1", Snap("task-1", percent: 75));

        store.Get("task-1")!.PercentComplete.Should().Be(75);
    }

    [Fact]
    public void GetAll_ReturnsAllRecentSnapshots()
    {
        InMemoryTaskProgressStore store = new();
        store.Update("a", Snap("a"));
        store.Update("b", Snap("b"));

        IReadOnlyList<TaskProgressSnapshot> all = store.GetAll();

        all.Should().HaveCount(2);
        all.Select(s => s.TaskId).Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void GetAll_FiltersOutStaleSnapshots()
    {
        // Snapshots older than 15 minutes shouldn't surface in GetAll —
        // they may still live in the map until an Update triggers eviction.
        InMemoryTaskProgressStore store = new();
        DateTime old = DateTime.UtcNow - TimeSpan.FromMinutes(30);
        store.Update("stale", Snap("stale", receivedUtc: old));
        store.Update("fresh", Snap("fresh"));

        IReadOnlyList<TaskProgressSnapshot> all = store.GetAll();

        all.Should().ContainSingle();
        all[0].TaskId.Should().Be("fresh");
    }

    [Fact]
    public void Get_StaleSnapshot_StillReturnsIt()
    {
        // Get is a direct lookup — staleness only filters GetAll. Callers
        // checking specific tasks see whatever is in the map.
        InMemoryTaskProgressStore store = new();
        DateTime old = DateTime.UtcNow - TimeSpan.FromHours(2);
        TaskProgressSnapshot snap = Snap("stale", receivedUtc: old);
        store.Update("stale", snap);

        store.Get("stale").Should().BeSameAs(snap);
    }

    [Fact]
    public void Update_OverMaxEntries_EvictsStale()
    {
        // Fill the store with stale entries + one fresh; the 501st update
        // triggers EvictStale which clears the old ones.
        InMemoryTaskProgressStore store = new();
        DateTime old = DateTime.UtcNow - TimeSpan.FromHours(1);
        for (int i = 0; i < 500; i++)
            store.Update($"stale-{i}", Snap($"stale-{i}", receivedUtc: old));

        // Trigger the eviction path by exceeding MaxEntries with a fresh entry.
        store.Update("fresh", Snap("fresh"));

        IReadOnlyList<TaskProgressSnapshot> all = store.GetAll();
        all.Should().ContainSingle("only the fresh entry survives the eviction sweep");
        all[0].TaskId.Should().Be("fresh");
    }

    [Fact]
    public void Update_OverMaxEntries_KeepsRecentEntries()
    {
        // If all entries are within the freshness window, none are evicted
        // even when count exceeds MaxEntries — they all stay.
        InMemoryTaskProgressStore store = new();
        for (int i = 0; i < 501; i++)
            store.Update($"task-{i}", Snap($"task-{i}"));

        store.GetAll().Count.Should().BeGreaterThan(500);
    }
}
