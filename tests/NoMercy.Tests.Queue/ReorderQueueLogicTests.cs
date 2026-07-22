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
[Trait(name: "Category", value: "Queue")]
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
        List<QueueJob> pendingJobs = allJobs.Where(predicate: j => j.ReservedAt == null).ToList();

        HashSet<int> requestedSet = orderedJobIds.ToHashSet();

        List<QueueJob> reordered =
        [
            .. orderedJobIds
                .Select(selector: id => pendingJobs.FirstOrDefault(predicate: j => j.Id == id))
                .Where(predicate: j => j is not null)
                .Cast<QueueJob>(),
            .. pendingJobs.Where(predicate: j => !requestedSet.Contains(item: j.Id)),
        ];

        int basePriority = reordered.Count;
        for (int i = 0; i < reordered.Count; i++)
            reordered[index: i].Priority = basePriority - i;

        return reordered;
    }

    // =========================================================================
    // Tests
    // =========================================================================

    [Fact]
    public void ReorderQueue_PendingItems_AreReturnedInRequestedOrder()
    {
        List<QueueJob> jobs = [MakeJob(id: 1), MakeJob(id: 2), MakeJob(id: 3)];

        List<QueueJob> result = RunReorder(allJobs: jobs, orderedJobIds: [3, 1, 2]);

        Assert.Equal(expected: [3, 1, 2], actual: result.Select(selector: j => j.Id));
    }

    [Fact]
    public void ReorderQueue_PendingItems_HaveDescendingPriorityMatchingOrder()
    {
        List<QueueJob> jobs = [MakeJob(id: 10), MakeJob(id: 20), MakeJob(id: 30)];

        List<QueueJob> result = RunReorder(allJobs: jobs, orderedJobIds: [30, 10, 20]);

        // First item must have highest priority.
        Assert.True(condition: result[index: 0].Priority > result[index: 1].Priority);
        Assert.True(condition: result[index: 1].Priority > result[index: 2].Priority);
        // basePriority = count = 3, so slots are 3, 2, 1.
        Assert.Equal(expected: 3, actual: result[index: 0].Priority);
        Assert.Equal(expected: 2, actual: result[index: 1].Priority);
        Assert.Equal(expected: 1, actual: result[index: 2].Priority);
    }

    [Fact]
    public void ReorderQueue_RunningItems_AreNotIncludedInReorderedList()
    {
        List<QueueJob> jobs = [MakeJob(id: 1), MakeJob(id: 99, reservedAt: DateTime.UtcNow), MakeJob(id: 2)];

        List<QueueJob> result = RunReorder(allJobs: jobs, orderedJobIds: [2, 1]);

        Assert.DoesNotContain(collection: result, filter: j => j.Id == 99);
        Assert.Equal(expected: [2, 1], actual: result.Select(selector: j => j.Id));
    }

    [Fact]
    public void ReorderQueue_RunningItems_PriorityIsNotModified()
    {
        QueueJob running = MakeJob(id: 99, reservedAt: DateTime.UtcNow);
        running.Priority = 999; // sentinel

        List<QueueJob> jobs = [MakeJob(id: 1), running, MakeJob(id: 2)];

        RunReorder(allJobs: jobs, orderedJobIds: [2, 1]);

        // Running job object was never touched by the reorder.
        Assert.Equal(expected: 999, actual: running.Priority);
    }

    [Fact]
    public void ReorderQueue_UnknownIds_AreIgnored()
    {
        List<QueueJob> jobs = [MakeJob(id: 1), MakeJob(id: 2)];

        // 999 doesn't exist in the queue — should be silently skipped.
        List<QueueJob> result = RunReorder(allJobs: jobs, orderedJobIds: [999, 2, 1]);

        Assert.Equal(expected: [2, 1], actual: result.Select(selector: j => j.Id));
    }

    [Fact]
    public void ReorderQueue_MissingIdsFromRequest_AreAppendedAtEnd()
    {
        List<QueueJob> jobs = [MakeJob(id: 1), MakeJob(id: 2), MakeJob(id: 3), MakeJob(id: 4)];

        // Only 3 and 1 are requested; 2 and 4 should trail in original order.
        List<QueueJob> result = RunReorder(allJobs: jobs, orderedJobIds: [3, 1]);

        Assert.Equal(expected: 3, actual: result[index: 0].Id);
        Assert.Equal(expected: 1, actual: result[index: 1].Id);
        List<int> tail = result.Skip(count: 2).Select(selector: j => j.Id).ToList();
        Assert.Equal(expected: [2, 4], actual: tail);
    }

    [Fact]
    public void ReorderQueue_EmptyRequest_LeavesAllPendingInOriginalOrder()
    {
        List<QueueJob> jobs = [MakeJob(id: 1), MakeJob(id: 2), MakeJob(id: 3)];

        List<QueueJob> result = RunReorder(allJobs: jobs, orderedJobIds: []);

        Assert.Equal(expected: 3, actual: result.Count);
        Assert.Equal(expected: [1, 2, 3], actual: result.Select(selector: j => j.Id));
    }

    [Fact]
    public void ReorderQueue_AllRunning_ReturnsEmptyList()
    {
        List<QueueJob> jobs =
        [
            MakeJob(id: 1, reservedAt: DateTime.UtcNow),
            MakeJob(id: 2, reservedAt: DateTime.UtcNow),
        ];

        List<QueueJob> result = RunReorder(allJobs: jobs, orderedJobIds: [1, 2]);

        Assert.Empty(collection: result);
    }
}
