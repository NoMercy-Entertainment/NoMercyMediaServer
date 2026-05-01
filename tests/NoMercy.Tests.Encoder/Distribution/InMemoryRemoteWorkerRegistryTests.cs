using Moq;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Jobs;

namespace NoMercy.Tests.Encoder.Distribution;

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

    // ──────────────────────────────────────────────────────────────────────────
    // Health tracking — consecutive-failure cooldown
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RecordTaskOutcome_ConsecutiveFailures_PushesWorkerIntoCooldown()
    {
        DateTime now = new(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(10),
            cooldownDuration: TimeSpan.FromMinutes(2),
            clock: () => now
        );
        sut.Register(MakeWorker("flaky"));

        sut.RecordTaskOutcome("flaky", success: false);
        sut.RecordTaskOutcome("flaky", success: false);
        sut.RecordTaskOutcome("flaky", success: false);

        sut.GetActiveWorkers().Should().BeEmpty("3 consecutive failures must trigger cooldown");

        IReadOnlyList<WorkerHealthSnapshot> snapshot = sut.GetAllWorkersWithHealth();
        snapshot.Should().HaveCount(1);
        snapshot[0].ConsecutiveFailures.Should().Be(3);
        snapshot[0].CooldownUntilUtc.Should().NotBeNull();
    }

    [Fact]
    public void RecordTaskOutcome_SuccessClearsFailureCount()
    {
        DateTime now = new(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(10),
            cooldownDuration: TimeSpan.FromMinutes(2),
            clock: () => now
        );
        sut.Register(MakeWorker("recovering"));

        sut.RecordTaskOutcome("recovering", success: false);
        sut.RecordTaskOutcome("recovering", success: false);
        sut.RecordTaskOutcome("recovering", success: true);
        sut.RecordTaskOutcome("recovering", success: false);

        // Failure counter reset on success — latest single failure doesn't
        // push to cooldown.
        sut.GetActiveWorkers().Should().HaveCount(1);
    }

    [Fact]
    public void Cooldown_LiftsAutomaticallyAfterDuration()
    {
        DateTime now = new(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(10),
            cooldownDuration: TimeSpan.FromMinutes(2),
            clock: () => now
        );
        sut.Register(MakeWorker("w"));
        sut.RecordTaskOutcome("w", false);
        sut.RecordTaskOutcome("w", false);
        sut.RecordTaskOutcome("w", false);

        sut.GetActiveWorkers().Should().BeEmpty(); // In cooldown.

        // Advance past cooldown window + heartbeat refresh to keep it non-stale.
        now = now.AddMinutes(3);
        sut.Register(MakeWorker("w")); // Re-register refreshes LastSeenUtc AND clears cooldown per contract.

        sut.GetActiveWorkers().Should().HaveCount(1);
    }

    [Fact]
    public void Register_ClearsExistingCooldown()
    {
        // A worker that self-re-registers (e.g. after a restart) is claiming
        // to be healthy again. Wipe the cooldown so it returns to rotation.
        DateTime now = new(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(10),
            cooldownDuration: TimeSpan.FromMinutes(2),
            clock: () => now
        );
        sut.Register(MakeWorker("w"));
        sut.RecordTaskOutcome("w", false);
        sut.RecordTaskOutcome("w", false);
        sut.RecordTaskOutcome("w", false);

        sut.GetActiveWorkers().Should().BeEmpty();

        sut.Register(MakeWorker("w"));

        sut.GetActiveWorkers().Should().HaveCount(1, "re-register must clear cooldown");
    }

    [Fact]
    public void GetAllWorkersWithHealth_ReturnsCooledWorkers_ForDashboardVisibility()
    {
        // Hidden from dispatch ≠ hidden from the operator. Dashboard lists
        // everyone so the user can see "workerX is cooling down, 3 failures".
        DateTime now = new(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(10),
            cooldownDuration: TimeSpan.FromMinutes(2),
            clock: () => now
        );
        sut.Register(MakeWorker("healthy"));
        sut.Register(MakeWorker("cooling"));
        sut.RecordTaskOutcome("cooling", false);
        sut.RecordTaskOutcome("cooling", false);
        sut.RecordTaskOutcome("cooling", false);

        sut.GetActiveWorkers().Should().HaveCount(1);
        sut.GetAllWorkersWithHealth().Should().HaveCount(2);
    }

    [Fact]
    public void RecordTaskOutcome_UnknownWorker_IsNoOp()
    {
        InMemoryRemoteWorkerRegistry sut = new();
        Action act = () => sut.RecordTaskOutcome("never-registered", success: false);
        act.Should().NotThrow();
    }

    private static IRemoteWorker MakeWorker(string id)
    {
        Mock<IRemoteWorker> mock = new();
        mock.SetupGet(w => w.WorkerId).Returns(id);
        mock.Setup(w => w.GetAvailableBudget()).Returns(new ResourceBudgetSnapshot(0, 4, 0));
        return mock.Object;
    }
}
