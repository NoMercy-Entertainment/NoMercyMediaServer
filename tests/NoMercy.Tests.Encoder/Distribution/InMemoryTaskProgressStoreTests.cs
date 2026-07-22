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
        store.Get(taskId: "missing").Should().BeNull();
    }

    [Fact]
    public void Update_ThenGet_ReturnsLatest()
    {
        InMemoryTaskProgressStore store = new();
        TaskProgressSnapshot snap = Snap(taskId: "task-1", percent: 50);

        store.Update(taskId: "task-1", snapshot: snap);

        store.Get(taskId: "task-1").Should().BeSameAs(expected: snap);
    }

    [Fact]
    public void Update_Twice_OverwritesPrevious()
    {
        // Latest-wins semantics — store doesn't keep history.
        InMemoryTaskProgressStore store = new();
        store.Update(taskId: "task-1", snapshot: Snap(taskId: "task-1", percent: 25));
        store.Update(taskId: "task-1", snapshot: Snap(taskId: "task-1", percent: 75));

        store.Get(taskId: "task-1")!.PercentComplete.Should().Be(expected: 75);
    }

    [Fact]
    public void GetAll_ReturnsAllRecentSnapshots()
    {
        InMemoryTaskProgressStore store = new();
        store.Update(taskId: "a", snapshot: Snap(taskId: "a"));
        store.Update(taskId: "b", snapshot: Snap(taskId: "b"));

        IReadOnlyList<TaskProgressSnapshot> all = store.GetAll();

        all.Should().HaveCount(expected: 2);
        all.Select(selector: s => s.TaskId).Should().BeEquivalentTo(expectation: new[] { "a", "b" });
    }

    [Fact]
    public void GetAll_FiltersOutStaleSnapshots()
    {
        // Snapshots older than 15 minutes shouldn't surface in GetAll —
        // they may still live in the map until an Update triggers eviction.
        InMemoryTaskProgressStore store = new();
        DateTime old = DateTime.UtcNow - TimeSpan.FromMinutes(minutes: 30);
        store.Update(taskId: "stale", snapshot: Snap(taskId: "stale", receivedUtc: old));
        store.Update(taskId: "fresh", snapshot: Snap(taskId: "fresh"));

        IReadOnlyList<TaskProgressSnapshot> all = store.GetAll();

        all.Should().ContainSingle();
        all[index: 0].TaskId.Should().Be(expected: "fresh");
    }

    [Fact]
    public void Get_StaleSnapshot_StillReturnsIt()
    {
        // Get is a direct lookup — staleness only filters GetAll. Callers
        // checking specific tasks see whatever is in the map.
        InMemoryTaskProgressStore store = new();
        DateTime old = DateTime.UtcNow - TimeSpan.FromHours(hours: 2);
        TaskProgressSnapshot snap = Snap(taskId: "stale", receivedUtc: old);
        store.Update(taskId: "stale", snapshot: snap);

        store.Get(taskId: "stale").Should().BeSameAs(expected: snap);
    }

    [Fact]
    public void Update_OverMaxEntries_EvictsStale()
    {
        // Fill the store with stale entries + one fresh; the 501st update
        // triggers EvictStale which clears the old ones.
        InMemoryTaskProgressStore store = new();
        DateTime old = DateTime.UtcNow - TimeSpan.FromHours(hours: 1);
        for (int i = 0; i < 500; i++)
            store.Update(taskId: $"stale-{i}", snapshot: Snap(taskId: $"stale-{i}", receivedUtc: old));

        // Trigger the eviction path by exceeding MaxEntries with a fresh entry.
        store.Update(taskId: "fresh", snapshot: Snap(taskId: "fresh"));

        IReadOnlyList<TaskProgressSnapshot> all = store.GetAll();
        all.Should().ContainSingle(because: "only the fresh entry survives the eviction sweep");
        all[index: 0].TaskId.Should().Be(expected: "fresh");
    }

    [Fact]
    public void Update_OverMaxEntries_KeepsRecentEntries()
    {
        // If all entries are within the freshness window, none are evicted
        // even when count exceeds MaxEntries — they all stay.
        InMemoryTaskProgressStore store = new();
        for (int i = 0; i < 501; i++)
            store.Update(taskId: $"task-{i}", snapshot: Snap(taskId: $"task-{i}"));

        store.GetAll().Count.Should().BeGreaterThan(expected: 500);
    }
}
