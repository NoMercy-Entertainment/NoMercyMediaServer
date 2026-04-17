namespace NoMercy.Tests.Encoder.Distribution;

using Moq;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Jobs;

public class InMemoryRemoteWorkerRegistryTests
{
    [Fact]
    public void Register_NewWorker_AppearsInActiveList()
    {
        InMemoryRemoteWorkerRegistry sut = new();
        sut.Register(MakeWorker("beast"));

        IReadOnlyList<IRemoteWorker> active = sut.GetActiveWorkers();

        active.Should().HaveCount(1);
        active[0].WorkerId.Should().Be("beast");
    }

    [Fact]
    public void Register_ExistingWorker_ReplacesEntry_NoDuplicates()
    {
        // Re-registration (worker restarted) must not grow the active list.
        InMemoryRemoteWorkerRegistry sut = new();
        sut.Register(MakeWorker("beast"));
        sut.Register(MakeWorker("beast"));

        sut.GetActiveWorkers().Should().HaveCount(1);
    }

    [Fact]
    public void Heartbeat_UnknownWorker_ReturnsFalse()
    {
        InMemoryRemoteWorkerRegistry sut = new();
        sut.Heartbeat("never-registered").Should().BeFalse();
    }

    [Fact]
    public void Heartbeat_KnownWorker_ReturnsTrue_AndKeepsActive()
    {
        // Tight stale threshold proves heartbeat keeps the worker from
        // being evicted as stale.
        DateTime t0 = new(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc);
        DateTime now = t0;
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromSeconds(10),
            clock: () => now
        );
        sut.Register(MakeWorker("beast"));

        // Advance past stale threshold, heartbeat, then advance again.
        now = t0.AddSeconds(9);
        sut.Heartbeat("beast").Should().BeTrue();
        now = t0.AddSeconds(15); // 6s past the refresh → still within 10s

        sut.GetActiveWorkers().Should().HaveCount(1);
    }

    [Fact]
    public void Unregister_RemovesWorker()
    {
        InMemoryRemoteWorkerRegistry sut = new();
        sut.Register(MakeWorker("w"));
        sut.Unregister("w").Should().BeTrue();
        sut.GetActiveWorkers().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveWorkers_EvictsStale()
    {
        DateTime now = new(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromSeconds(30),
            clock: () => now
        );
        sut.Register(MakeWorker("ghost"));

        now = now.AddSeconds(31);

        sut.GetActiveWorkers().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveWorkers_Snapshot_IsStable()
    {
        // Registry may churn mid-dispatch. The snapshot returned must be
        // an independent array so the caller iterates safely.
        InMemoryRemoteWorkerRegistry sut = new();
        sut.Register(MakeWorker("a"));
        sut.Register(MakeWorker("b"));

        IReadOnlyList<IRemoteWorker> snapshot = sut.GetActiveWorkers();

        // Mutate after snapshot — snapshot must not change.
        sut.Unregister("a");

        snapshot.Should().HaveCount(2);
    }

    private static IRemoteWorker MakeWorker(string id)
    {
        Mock<IRemoteWorker> mock = new();
        mock.SetupGet(w => w.WorkerId).Returns(id);
        mock.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 4, 0));
        return mock.Object;
    }
}
