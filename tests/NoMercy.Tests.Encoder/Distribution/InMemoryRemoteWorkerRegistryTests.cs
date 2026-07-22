// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
        sut.Register(worker: MakeWorker(id: "beast"));

        IReadOnlyList<IRemoteWorker> active = sut.GetActiveWorkers();

        active.Should().HaveCount(expected: 1);
        active[index: 0].WorkerId.Should().Be(expected: "beast");
    }

    [Fact]
    public void Register_ExistingWorker_ReplacesEntry_NoDuplicates()
    {
        // Re-registration (worker restarted) must not grow the active list.
        InMemoryRemoteWorkerRegistry sut = new();
        sut.Register(worker: MakeWorker(id: "beast"));
        sut.Register(worker: MakeWorker(id: "beast"));

        sut.GetActiveWorkers().Should().HaveCount(expected: 1);
    }

    [Fact]
    public void Heartbeat_UnknownWorker_ReturnsFalse()
    {
        InMemoryRemoteWorkerRegistry sut = new();
        sut.Heartbeat(workerId: "never-registered").Should().BeFalse();
    }

    [Fact]
    public void Heartbeat_KnownWorker_ReturnsTrue_AndKeepsActive()
    {
        // Tight stale threshold proves heartbeat keeps the worker from
        // being evicted as stale.
        DateTime t0 = new(year: 2026, month: 4, day: 17, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);
        DateTime now = t0;
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromSeconds(seconds: 10),
            clock: () => now
        );
        sut.Register(worker: MakeWorker(id: "beast"));

        // Advance past stale threshold, heartbeat, then advance again.
        now = t0.AddSeconds(value: 9);
        sut.Heartbeat(workerId: "beast").Should().BeTrue();
        now = t0.AddSeconds(value: 15); // 6s past the refresh → still within 10s

        sut.GetActiveWorkers().Should().HaveCount(expected: 1);
    }

    [Fact]
    public void Unregister_RemovesWorker()
    {
        InMemoryRemoteWorkerRegistry sut = new();
        sut.Register(worker: MakeWorker(id: "w"));
        sut.Unregister(workerId: "w").Should().BeTrue();
        sut.GetActiveWorkers().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveWorkers_EvictsStale()
    {
        DateTime now = new(year: 2026, month: 4, day: 17, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromSeconds(seconds: 30),
            clock: () => now
        );
        sut.Register(worker: MakeWorker(id: "ghost"));

        now = now.AddSeconds(value: 31);

        sut.GetActiveWorkers().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveWorkers_Snapshot_IsStable()
    {
        // Registry may churn mid-dispatch. The snapshot returned must be
        // an independent array so the caller iterates safely.
        InMemoryRemoteWorkerRegistry sut = new();
        sut.Register(worker: MakeWorker(id: "a"));
        sut.Register(worker: MakeWorker(id: "b"));

        IReadOnlyList<IRemoteWorker> snapshot = sut.GetActiveWorkers();

        // Mutate after snapshot — snapshot must not change.
        sut.Unregister(workerId: "a");

        snapshot.Should().HaveCount(expected: 2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Health tracking — consecutive-failure cooldown
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RecordTaskOutcome_ConsecutiveFailures_PushesWorkerIntoCooldown()
    {
        DateTime now = new(year: 2026, month: 4, day: 17, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(minutes: 10),
            cooldownDuration: TimeSpan.FromMinutes(minutes: 2),
            clock: () => now
        );
        sut.Register(worker: MakeWorker(id: "flaky"));

        sut.RecordTaskOutcome(workerId: "flaky", success: false);
        sut.RecordTaskOutcome(workerId: "flaky", success: false);
        sut.RecordTaskOutcome(workerId: "flaky", success: false);

        sut.GetActiveWorkers().Should().BeEmpty(because: "3 consecutive failures must trigger cooldown");

        IReadOnlyList<WorkerHealthSnapshot> snapshot = sut.GetAllWorkersWithHealth();
        snapshot.Should().HaveCount(expected: 1);
        snapshot[index: 0].ConsecutiveFailures.Should().Be(expected: 3);
        snapshot[index: 0].CooldownUntilUtc.Should().NotBeNull();
    }

    [Fact]
    public void RecordTaskOutcome_SuccessClearsFailureCount()
    {
        DateTime now = new(year: 2026, month: 4, day: 17, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(minutes: 10),
            cooldownDuration: TimeSpan.FromMinutes(minutes: 2),
            clock: () => now
        );
        sut.Register(worker: MakeWorker(id: "recovering"));

        sut.RecordTaskOutcome(workerId: "recovering", success: false);
        sut.RecordTaskOutcome(workerId: "recovering", success: false);
        sut.RecordTaskOutcome(workerId: "recovering", success: true);
        sut.RecordTaskOutcome(workerId: "recovering", success: false);

        // Failure counter reset on success — latest single failure doesn't
        // push to cooldown.
        sut.GetActiveWorkers().Should().HaveCount(expected: 1);
    }

    [Fact]
    public void Cooldown_LiftsAutomaticallyAfterDuration()
    {
        DateTime now = new(year: 2026, month: 4, day: 17, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(minutes: 10),
            cooldownDuration: TimeSpan.FromMinutes(minutes: 2),
            clock: () => now
        );
        sut.Register(worker: MakeWorker(id: "w"));
        sut.RecordTaskOutcome(workerId: "w", success: false);
        sut.RecordTaskOutcome(workerId: "w", success: false);
        sut.RecordTaskOutcome(workerId: "w", success: false);

        sut.GetActiveWorkers().Should().BeEmpty(); // In cooldown.

        // Advance past cooldown window + heartbeat refresh to keep it non-stale.
        now = now.AddMinutes(value: 3);
        sut.Register(worker: MakeWorker(id: "w")); // Re-register refreshes LastSeenUtc AND clears cooldown per contract.

        sut.GetActiveWorkers().Should().HaveCount(expected: 1);
    }

    [Fact]
    public void Register_ClearsExistingCooldown()
    {
        // A worker that self-re-registers (e.g. after a restart) is claiming
        // to be healthy again. Wipe the cooldown so it returns to rotation.
        DateTime now = new(year: 2026, month: 4, day: 17, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(minutes: 10),
            cooldownDuration: TimeSpan.FromMinutes(minutes: 2),
            clock: () => now
        );
        sut.Register(worker: MakeWorker(id: "w"));
        sut.RecordTaskOutcome(workerId: "w", success: false);
        sut.RecordTaskOutcome(workerId: "w", success: false);
        sut.RecordTaskOutcome(workerId: "w", success: false);

        sut.GetActiveWorkers().Should().BeEmpty();

        sut.Register(worker: MakeWorker(id: "w"));

        sut.GetActiveWorkers().Should().HaveCount(expected: 1, because: "re-register must clear cooldown");
    }

    [Fact]
    public void GetAllWorkersWithHealth_ReturnsCooledWorkers_ForDashboardVisibility()
    {
        // Hidden from dispatch ≠ hidden from the operator. Dashboard lists
        // everyone so the user can see "workerX is cooling down, 3 failures".
        DateTime now = new(year: 2026, month: 4, day: 17, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);
        InMemoryRemoteWorkerRegistry sut = new(
            staleAfter: TimeSpan.FromMinutes(minutes: 10),
            cooldownDuration: TimeSpan.FromMinutes(minutes: 2),
            clock: () => now
        );
        sut.Register(worker: MakeWorker(id: "healthy"));
        sut.Register(worker: MakeWorker(id: "cooling"));
        sut.RecordTaskOutcome(workerId: "cooling", success: false);
        sut.RecordTaskOutcome(workerId: "cooling", success: false);
        sut.RecordTaskOutcome(workerId: "cooling", success: false);

        sut.GetActiveWorkers().Should().HaveCount(expected: 1);
        sut.GetAllWorkersWithHealth().Should().HaveCount(expected: 2);
    }

    [Fact]
    public void RecordTaskOutcome_UnknownWorker_IsNoOp()
    {
        InMemoryRemoteWorkerRegistry sut = new();
        Action act = () => sut.RecordTaskOutcome(workerId: "never-registered", success: false);
        act.Should().NotThrow();
    }

    private static IRemoteWorker MakeWorker(string id)
    {
        Mock<IRemoteWorker> mock = new();
        mock.SetupGet(expression: w => w.WorkerId).Returns(value: id);
        mock.Setup(expression: w => w.GetAvailableBudget()).Returns(value: new ResourceBudgetSnapshot(AvailableGpuSlots: 0, AvailableCpuThreads: 4, GpuUtilization: 0));
        return mock.Object;
    }
}
