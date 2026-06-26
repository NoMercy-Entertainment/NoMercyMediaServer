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

using FluentAssertions;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Verifies that child jobs whose parent has been moved to FailedJobs are
/// themselves skipped (moved to FailedJobs with a synthetic exception) rather
/// than executed, so orphaned output is never produced.
/// </summary>
public class ParentJobFailedSkipTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public ParentJobFailedSkipTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(_adapter);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    [Fact]
    public void ReserveJob_WhenParentInFailedJobs_SkipsChildAndMovesToFailed()
    {
        // Arrange: persist a parent failure. The payload contains "Id":1, to
        // match the IsParentFailed string-search pattern.
        const int parentJobId = 1;
        _context.FailedJobs.Add(
            new FailedJob
            {
                Queue = "encoder",
                Payload = $"{{\"Id\":{parentJobId},\"Type\":\"VideoEncodeJob\"}}",
                Exception = "{\"Message\":\"disk full\"}",
                FailedAt = DateTime.UtcNow,
            }
        );
        _context.SaveChanges();

        // Enqueue a child job referencing that parent.
        QueueJobModel childJob = new()
        {
            Queue = "encoder-task",
            Payload = $"{{\"Id\":42,\"ParentJobId\":{parentJobId}}}",
            AvailableAt = DateTime.UtcNow,
            ParentJobId = parentJobId,
            GroupTag = "group-abc",
        };
        _jobQueue.Enqueue(childJob);
        _context.QueueJobs.Count().Should().Be(1);

        // Act: ReserveJob should detect the failed parent and skip the child.
        QueueJobModel? reserved = _jobQueue.ReserveJob("encoder-task", null);

        // Assert: ReserveJob returns null (skipped).
        reserved.Should().BeNull("child should be skipped when its parent is in FailedJobs");

        // The child should have been removed from QueueJobs...
        _context
            .QueueJobs.Count()
            .Should()
            .Be(0, "child job should be removed from the queue after skip");

        // ...and a synthetic FailedJob entry should have been added.
        FailedJob? skippedEntry = _context.FailedJobs.FirstOrDefault(failedJob =>
            failedJob.Exception.Contains("Skipped: parent job")
        );
        skippedEntry.Should().NotBeNull("a failed-by-parent entry should appear in FailedJobs");
        skippedEntry!.Queue.Should().Be("encoder-task");
    }

    [Fact]
    public void ReserveJob_WhenParentNotFailed_ReturnsChildNormally()
    {
        // Arrange: no failed parent — healthy encode run.
        const int parentJobId = 99;

        QueueJobModel childJob = new()
        {
            Queue = "encoder-task",
            Payload = $"{{\"Id\":10,\"ParentJobId\":{parentJobId}}}",
            AvailableAt = DateTime.UtcNow,
            ParentJobId = parentJobId,
            GroupTag = "group-xyz",
        };
        _jobQueue.Enqueue(childJob);

        // Act
        QueueJobModel? reserved = _jobQueue.ReserveJob("encoder-task", null);

        // Assert: normal reservation — child runs.
        reserved
            .Should()
            .NotBeNull("child should be reserved when its parent is NOT in FailedJobs");
        reserved!.ParentJobId.Should().Be(parentJobId);
    }

    [Fact]
    public void ReserveJob_WholeJobWithNoParent_ReturnsNormally()
    {
        // Arrange: a top-level (non-decomposed) encode job with no ParentJobId.
        QueueJobModel topLevelJob = new()
        {
            Queue = "encoder",
            Payload = "{\"Id\":5,\"Type\":\"VideoEncodeJob\"}",
            AvailableAt = DateTime.UtcNow,
        };
        _jobQueue.Enqueue(topLevelJob);

        // Act
        QueueJobModel? reserved = _jobQueue.ReserveJob("encoder", null);

        // Assert: top-level job is reserved normally.
        reserved.Should().NotBeNull();
        reserved!.ParentJobId.Should().BeNull();
    }
}
