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
/// Stopping the server is not something a job did wrong.
/// <para>
/// <c>ReserveJob</c> charges an attempt the moment it hands a job out, so a
/// worker that disappears mid-job leaves that charge behind. The runner clears
/// every reservation as it comes up, and while that left <c>Attempts</c> where
/// the reservation put it, three restarts across the life of one long encode
/// retired the file for good: the reserve query skips it from then on, nothing
/// is written to FailedJobs, and no line is logged. A whole season lost its
/// first episode that way and the queue kept reporting it as merely queued.
/// </para>
/// <para>
/// Only real failures spend attempts now — an exception out of the handler, or
/// a reservation left hanging while the server is still running. An
/// interruption is counted on its own, with a ceiling that exists for one case:
/// a job that takes the process down every time it runs.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class ARestartDoesNotSpendAnAttemptTests : IDisposable
{
    private const byte MaxAttempts = 3;
    private const byte MaxInterruptions = 10;

    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;

    public ARestartDoesNotSpendAnAttemptTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    private int Enqueue(string payload, byte attempts, byte interruptions, bool reserved)
    {
        QueueJob row = new()
        {
            Queue = "encoder",
            Payload = payload,
            Priority = 4,
            Attempts = attempts,
            Interruptions = interruptions,
            ReservedAt = reserved ? new DateTime(2026, 7, 27, 23, 11, 0, DateTimeKind.Utc) : null,
            AvailableAt = new(2026, 7, 27, 23, 10, 21, DateTimeKind.Utc),
            CreatedAt = new(2026, 7, 27, 23, 10, 21, DateTimeKind.Utc),
        };
        _context.QueueJobs.Add(row);
        _context.SaveChanges();
        return row.Id;
    }

    private JobQueue Queue() => new(_adapter, MaxAttempts, null, MaxInterruptions);

    private QueueJob Reload(int id) => _context.QueueJobs.Single(job => job.Id == id);

    [Fact]
    public void Releasing_an_interrupted_reservation_refunds_the_attempt_it_charged()
    {
        int id = Enqueue("S01E01", attempts: 1, interruptions: 0, reserved: true);

        Queue().ResetAllReservedJobs();

        QueueJob job = Reload(id);
        job.Attempts.Should().Be(0, "the job never got to fail — the process went away");
        job.Interruptions.Should().Be(1);
        job.ReservedAt.Should().BeNull();
    }

    [Fact]
    public void A_job_that_was_not_reserved_is_left_alone()
    {
        // The guard. Blanket-clearing every row would hand a refund to jobs
        // that had genuinely failed and were waiting out their next try.
        int id = Enqueue("S01E02", attempts: 2, interruptions: 0, reserved: false);

        Queue().ResetAllReservedJobs();

        QueueJob job = Reload(id);
        job.Attempts.Should().Be(2);
        job.Interruptions.Should().Be(0);
    }

    [Fact]
    public void Three_restarts_in_a_row_leave_the_job_exactly_where_it_started()
    {
        // The reported bug, end to end. Under the old behavior this job was
        // unreservable by the third pass and never ran again.
        int id = Enqueue("S01E09", attempts: 0, interruptions: 0, reserved: false);
        JobQueue queue = Queue();

        for (int restart = 0; restart < 3; restart++)
        {
            QueueJobModel reserved = queue.ReserveJob("encoder", null)!;
            reserved.Should().NotBeNull("an interrupted job must stay reservable");
            queue.ResetAllReservedJobs();
        }

        QueueJob job = Reload(id);
        job.Attempts.Should().Be(0);
        job.Interruptions.Should().Be(3);

        queue.ReserveJob("encoder", null).Should().NotBeNull();
    }

    [Fact]
    public void A_job_that_keeps_killing_the_process_still_stops_eventually()
    {
        // The one case the interruption count exists for. Without a ceiling a
        // job that crashes the host on every run is re-queued by every boot
        // pass forever, taking the server down with it each time.
        Enqueue("poison", attempts: 0, interruptions: MaxInterruptions, reserved: false);

        Queue().FailStrandedJobs().Should().Be(1);

        _context.QueueJobs.Should().BeEmpty();
        _context.FailedJobs.Should().ContainSingle(job => job.Payload == "poison");
    }
}
