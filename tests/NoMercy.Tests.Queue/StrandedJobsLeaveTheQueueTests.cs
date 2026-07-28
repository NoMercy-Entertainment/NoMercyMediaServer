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
/// A job that can never run again must leave the queue.
/// <para>
/// The reserve query skips a row once <c>Attempts</c> reaches the maximum, and
/// both orphan-recovery passes only ever scan rows that are still RESERVED. A
/// row that runs out of attempts while unreserved therefore falls between them:
/// invisible to the worker, invisible to recovery, and still holding the queue
/// position it was dispatched into. A restart mid-encode produces exactly that
/// row — the boot pass clears <c>ReservedAt</c> and leaves <c>Attempts</c>
/// alone — which is how the first episode of a season ended up parked at the
/// head of the encoder queue while the rest of the season encoded past it.
/// </para>
/// <para>
/// The coordinator behind that episode is the second half: it waits on its
/// children writing outcome rows, and a child that dies writes none, so it
/// re-queued itself every poll interval for as long as the server ran.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class StrandedJobsLeaveTheQueueTests : IDisposable
{
    private const byte MaxAttempts = 3;

    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;

    public StrandedJobsLeaveTheQueueTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    private int Enqueue(string payload, byte attempts, int? parentJobId = null)
    {
        QueueJob row = new()
        {
            Queue = "encoder",
            Payload = payload,
            Priority = 4,
            Attempts = attempts,
            AvailableAt = new(2026, 7, 27, 23, 10, 21, DateTimeKind.Utc),
            CreatedAt = new(2026, 7, 27, 23, 10, 21, DateTimeKind.Utc),
            ParentJobId = parentJobId,
        };
        _context.QueueJobs.Add(row);
        _context.SaveChanges();
        return row.Id;
    }

    private JobQueue Queue() => new(_adapter, MaxAttempts);

    [Fact]
    public void A_job_out_of_attempts_and_holding_no_reservation_is_dead_lettered()
    {
        Enqueue("S01E01", MaxAttempts);

        Queue().FailStrandedJobs().Should().Be(1);

        _context.QueueJobs.Should().BeEmpty();
        _context.FailedJobs.Should().ContainSingle(job => job.Payload == "S01E01");
    }

    [Fact]
    public void A_job_with_attempts_left_is_not_touched()
    {
        // The guard on the sweep. Two of three attempts used is a job waiting
        // for its next try, not a dead one, and removing it would silently drop
        // work the queue is still going to do.
        Enqueue("S01E02", MaxAttempts - 1);

        Queue().FailStrandedJobs().Should().Be(0);

        _context.QueueJobs.Should().ContainSingle();
        _context.FailedJobs.Should().BeEmpty();
    }

    [Fact]
    public void A_reserved_job_is_never_swept_however_many_attempts_it_has_used()
    {
        // A reservation means a worker is on it right now. Encodes hold one for
        // hours, and the attempt that reserved it is already counted, so an
        // encoder job at the limit is the NORMAL shape of a running encode.
        QueueJob running = new()
        {
            Queue = "encoder",
            Payload = "S01E03",
            Priority = 4,
            Attempts = MaxAttempts,
            ReservedAt = new(2026, 7, 27, 23, 11, 0, DateTimeKind.Utc),
            AvailableAt = new(2026, 7, 27, 23, 10, 21, DateTimeKind.Utc),
            CreatedAt = new(2026, 7, 27, 23, 10, 21, DateTimeKind.Utc),
        };
        _context.QueueJobs.Add(running);
        _context.SaveChanges();

        Queue().FailStrandedJobs().Should().Be(0);

        _context.QueueJobs.Should().ContainSingle();
    }

    [Fact]
    public void A_stranded_child_takes_its_coordinator_down_with_it()
    {
        int coordinatorId = Enqueue("coordinator S01E09", attempts: 0);
        Enqueue("bundle-0 of S01E09", MaxAttempts, parentJobId: coordinatorId);

        Queue().FailStrandedJobs();

        // Both gone: the child because it is out of attempts, the coordinator
        // because the child it is waiting on is never going to report.
        _context.QueueJobs.Should().BeEmpty();
        _context
            .FailedJobs.Select(job => job.Payload)
            .Should()
            .BeEquivalentTo("bundle-0 of S01E09", "coordinator S01E09");
    }

    [Fact]
    public void A_child_that_fails_its_last_attempt_takes_its_coordinator_down_too()
    {
        // Same rule reached by the ordinary route: the child threw rather than
        // being stranded by a restart. Either way the coordinator is waiting on
        // an outcome row that will never be written.
        int coordinatorId = Enqueue("coordinator S02E04", attempts: 0);
        int childId = Enqueue("bundle-0 of S02E04", MaxAttempts, parentJobId: coordinatorId);

        QueueJobModel child = _adapter.FindJob(childId)!;
        Queue().FailJob(child, new InvalidOperationException("ffmpeg exited 1"));

        _context.QueueJobs.Should().BeEmpty();
        _context
            .FailedJobs.Select(job => job.Payload)
            .Should()
            .BeEquivalentTo("bundle-0 of S02E04", "coordinator S02E04");
    }
}
