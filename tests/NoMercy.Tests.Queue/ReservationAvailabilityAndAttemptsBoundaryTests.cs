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

using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// CRIT — EfQueueContextAdapter's ReserveJobQuery ignored AvailableAt and used
/// an off-by-one attempts boundary (<c>Attempts &lt;= maxAttempts</c>) that
/// disagreed with FailJob's dead-letter threshold (<c>Attempts &gt;= maxAttempts</c>),
/// letting a job at the max-attempts threshold be reserved and executed one
/// extra ("zombie") time before it was finally dead-lettered. These tests
/// drive the real <see cref="JobQueue.ReserveJob"/> against the real
/// <see cref="NoMercy.Queue.MediaServer.EfQueueContextAdapter"/>.
/// </summary>
[Trait("Category", "Unit")]
public class ReservationAvailabilityAndAttemptsBoundaryTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;

    public ReservationAvailabilityAndAttemptsBoundaryTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    [Fact]
    public void ReserveJob_AvailableAtInFuture_IsNotReserved()
    {
        JobQueue jobQueue = new(_adapter);
        QueueJob job = new()
        {
            Queue = "delayed",
            Payload = "future payload",
            AvailableAt = DateTime.UtcNow.AddMinutes(10),
            Attempts = 0,
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        QueueJobModel? reserved = jobQueue.ReserveJob("delayed", null);

        Assert.Null(reserved);
        QueueJob? untouched = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(untouched);
        Assert.Null(untouched.ReservedAt);
        Assert.Equal(0, untouched.Attempts);
    }

    [Fact]
    public void ReserveJob_AvailableAtInPast_IsReserved()
    {
        JobQueue jobQueue = new(_adapter);
        QueueJob job = new()
        {
            Queue = "delayed",
            Payload = "past payload",
            AvailableAt = DateTime.UtcNow.AddMinutes(-10),
            Attempts = 0,
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        QueueJobModel? reserved = jobQueue.ReserveJob("delayed", null);

        Assert.NotNull(reserved);
        Assert.Equal("past payload", reserved.Payload);
        Assert.NotNull(reserved.ReservedAt);
    }

    [Fact]
    public void ReserveJob_BackoffRescheduledJob_NotReservedUntilAvailableAtElapses()
    {
        JobQueue jobQueue = new(_adapter);
        QueueJob job = new()
        {
            Queue = "backoff",
            Payload = "backoff payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 1,
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = "backoff",
            Payload = "backoff payload",
            Attempts = 1,
        };
        jobQueue.ReleaseReservation(jobModel, TimeSpan.FromMinutes(10));

        QueueJobModel? reserved = jobQueue.ReserveJob("backoff", null);

        Assert.Null(reserved);
        QueueJob? deferred = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(deferred);
        Assert.Null(deferred.ReservedAt);
        Assert.True(deferred.AvailableAt > DateTime.UtcNow.AddMinutes(9));
    }

    [Fact]
    public void ReserveJob_JobAtMaxAttempts_IsNotReservedAgain_NoZombieExecution()
    {
        JobQueue jobQueue = new(_adapter, maxAttempts: 3);
        QueueJob job = new()
        {
            Queue = "boundary",
            Payload = "at-limit payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 3,
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        QueueJobModel? reserved = jobQueue.ReserveJob("boundary", null);

        Assert.Null(reserved);
        QueueJob? untouched = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(untouched);
        Assert.Null(untouched.ReservedAt);
        Assert.Equal(3, untouched.Attempts);
    }

    [Fact]
    public void ReserveJob_JobOneUnderMaxAttempts_IsReservedForFinalAttempt()
    {
        JobQueue jobQueue = new(_adapter, maxAttempts: 3);
        QueueJob job = new()
        {
            Queue = "boundary",
            Payload = "under-limit payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 2,
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        QueueJobModel? reserved = jobQueue.ReserveJob("boundary", null);

        Assert.NotNull(reserved);
        Assert.Equal(3, reserved.Attempts);
        Assert.NotNull(reserved.ReservedAt);
    }

    [Fact]
    public void ReserveJob_JobAtMaxAttemptsThenFailed_NeverExecutesBeyondBudget()
    {
        JobQueue jobQueue = new(_adapter, maxAttempts: 2);
        QueueJob job = new()
        {
            Queue = "budget",
            Payload = "budget payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        QueueJobModel? attempt1 = jobQueue.ReserveJob("budget", null);
        Assert.NotNull(attempt1);
        jobQueue.FailJob(attempt1, new InvalidOperationException("fail 1"));

        QueueJobModel? attempt2 = jobQueue.ReserveJob("budget", null);
        Assert.NotNull(attempt2);
        Assert.Equal(2, attempt2.Attempts);
        jobQueue.FailJob(attempt2, new InvalidOperationException("fail 2"));

        QueueJobModel? attempt3 = jobQueue.ReserveJob("budget", null);

        Assert.Null(attempt3);
        Assert.Empty(_context.QueueJobs);
        Assert.Single(_context.FailedJobs);
    }
}
