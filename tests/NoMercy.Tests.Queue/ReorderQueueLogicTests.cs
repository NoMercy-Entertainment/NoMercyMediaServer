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

using NoMercy.Database.Models.Queue;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Phase 8.2 Step 1 — Queue reorder endpoint.
///
/// Tests the in-memory reorder algorithm extracted verbatim from
/// TasksController.ReorderQueue. No DB, no DI, no HTTP — pure logic.
/// </summary>
[Trait("Category", "Queue")]
public class ReorderQueueLogicTests
{
    // =========================================================================
    // Helpers — mirrors the exact algorithm in TasksController.ReorderQueue
    // =========================================================================

    private static QueueJob MakeJob(
        int id,
        string queue = "encoder",
        DateTime? reservedAt = null
    ) =>
        new()
        {
            Id = id,
            Queue = queue,
            Payload = "{}",
            Priority = 0,
            ReservedAt = reservedAt,
            AvailableAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

    /// <summary>
    /// Runs the same reorder algorithm as the controller and returns the
    /// reordered pending list with updated Priority values.
    /// </summary>
    private static List<QueueJob> RunReorder(List<QueueJob> allJobs, List<int> orderedJobIds)
    {
        List<QueueJob> pendingJobs = allJobs.Where(j => j.ReservedAt == null).ToList();

        HashSet<int> requestedSet = orderedJobIds.ToHashSet();

        List<QueueJob> reordered =
        [
            .. orderedJobIds
                .Select(id => pendingJobs.FirstOrDefault(j => j.Id == id))
                .Where(j => j is not null)
                .Cast<QueueJob>(),
            .. pendingJobs.Where(j => !requestedSet.Contains(j.Id)),
        ];

        int basePriority = reordered.Count;
        for (int i = 0; i < reordered.Count; i++)
            reordered[i].Priority = basePriority - i;

        return reordered;
    }

    // =========================================================================
    // Tests
    // =========================================================================

    [Fact]
    public void ReorderQueue_PendingItems_AreReturnedInRequestedOrder()
    {
        List<QueueJob> jobs = [MakeJob(1), MakeJob(2), MakeJob(3)];

        List<QueueJob> result = RunReorder(jobs, [3, 1, 2]);

        Assert.Equal([3, 1, 2], result.Select(j => j.Id));
    }

    [Fact]
    public void ReorderQueue_PendingItems_HaveDescendingPriorityMatchingOrder()
    {
        List<QueueJob> jobs = [MakeJob(10), MakeJob(20), MakeJob(30)];

        List<QueueJob> result = RunReorder(jobs, [30, 10, 20]);

        // First item must have highest priority.
        Assert.True(result[0].Priority > result[1].Priority);
        Assert.True(result[1].Priority > result[2].Priority);
        // basePriority = count = 3, so slots are 3, 2, 1.
        Assert.Equal(3, result[0].Priority);
        Assert.Equal(2, result[1].Priority);
        Assert.Equal(1, result[2].Priority);
    }

    [Fact]
    public void ReorderQueue_RunningItems_AreNotIncludedInReorderedList()
    {
        List<QueueJob> jobs = [MakeJob(1), MakeJob(99, reservedAt: DateTime.UtcNow), MakeJob(2)];

        List<QueueJob> result = RunReorder(jobs, [2, 1]);

        Assert.DoesNotContain(result, j => j.Id == 99);
        Assert.Equal([2, 1], result.Select(j => j.Id));
    }

    [Fact]
    public void ReorderQueue_RunningItems_PriorityIsNotModified()
    {
        QueueJob running = MakeJob(99, reservedAt: DateTime.UtcNow);
        running.Priority = 999; // sentinel

        List<QueueJob> jobs = [MakeJob(1), running, MakeJob(2)];

        RunReorder(jobs, [2, 1]);

        // Running job object was never touched by the reorder.
        Assert.Equal(999, running.Priority);
    }

    [Fact]
    public void ReorderQueue_UnknownIds_AreIgnored()
    {
        List<QueueJob> jobs = [MakeJob(1), MakeJob(2)];

        // 999 doesn't exist in the queue — should be silently skipped.
        List<QueueJob> result = RunReorder(jobs, [999, 2, 1]);

        Assert.Equal([2, 1], result.Select(j => j.Id));
    }

    [Fact]
    public void ReorderQueue_MissingIdsFromRequest_AreAppendedAtEnd()
    {
        List<QueueJob> jobs = [MakeJob(1), MakeJob(2), MakeJob(3), MakeJob(4)];

        // Only 3 and 1 are requested; 2 and 4 should trail in original order.
        List<QueueJob> result = RunReorder(jobs, [3, 1]);

        Assert.Equal(3, result[0].Id);
        Assert.Equal(1, result[1].Id);
        List<int> tail = result.Skip(2).Select(j => j.Id).ToList();
        Assert.Equal([2, 4], tail);
    }

    [Fact]
    public void ReorderQueue_EmptyRequest_LeavesAllPendingInOriginalOrder()
    {
        List<QueueJob> jobs = [MakeJob(1), MakeJob(2), MakeJob(3)];

        List<QueueJob> result = RunReorder(jobs, []);

        Assert.Equal(3, result.Count);
        Assert.Equal([1, 2, 3], result.Select(j => j.Id));
    }

    [Fact]
    public void ReorderQueue_AllRunning_ReturnsEmptyList()
    {
        List<QueueJob> jobs =
        [
            MakeJob(1, reservedAt: DateTime.UtcNow),
            MakeJob(2, reservedAt: DateTime.UtcNow),
        ];

        List<QueueJob> result = RunReorder(jobs, [1, 2]);

        Assert.Empty(result);
    }
}
