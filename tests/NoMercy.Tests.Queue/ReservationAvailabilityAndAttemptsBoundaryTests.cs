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
[Trait(name: "Category", value: "Unit")]
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
        JobQueue jobQueue = new(context: _adapter);
        QueueJob job = new()
        {
            Queue = "delayed",
            Payload = "future payload",
            AvailableAt = DateTime.UtcNow.AddMinutes(value: 10),
            Attempts = 0,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        QueueJobModel? reserved = jobQueue.ReserveJob(name: "delayed", currentJobId: null);

        Assert.Null(@object: reserved);
        QueueJob? untouched = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(@object: untouched);
        Assert.Null(value: untouched.ReservedAt);
        Assert.Equal(expected: 0, actual: untouched.Attempts);
    }

    [Fact]
    public void ReserveJob_AvailableAtInPast_IsReserved()
    {
        JobQueue jobQueue = new(context: _adapter);
        QueueJob job = new()
        {
            Queue = "delayed",
            Payload = "past payload",
            AvailableAt = DateTime.UtcNow.AddMinutes(value: -10),
            Attempts = 0,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        QueueJobModel? reserved = jobQueue.ReserveJob(name: "delayed", currentJobId: null);

        Assert.NotNull(@object: reserved);
        Assert.Equal(expected: "past payload", actual: reserved.Payload);
        Assert.NotNull(value: reserved.ReservedAt);
    }

    [Fact]
    public void ReserveJob_BackoffRescheduledJob_NotReservedUntilAvailableAtElapses()
    {
        JobQueue jobQueue = new(context: _adapter);
        QueueJob job = new()
        {
            Queue = "backoff",
            Payload = "backoff payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 1,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = "backoff",
            Payload = "backoff payload",
            Attempts = 1,
        };
        jobQueue.ReleaseReservation(job: jobModel, availableAfter: TimeSpan.FromMinutes(minutes: 10));

        QueueJobModel? reserved = jobQueue.ReserveJob(name: "backoff", currentJobId: null);

        Assert.Null(@object: reserved);
        QueueJob? deferred = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(@object: deferred);
        Assert.Null(value: deferred.ReservedAt);
        Assert.True(condition: deferred.AvailableAt > DateTime.UtcNow.AddMinutes(value: 9));
    }

    [Fact]
    public void ReserveJob_JobAtMaxAttempts_IsNotReservedAgain_NoZombieExecution()
    {
        JobQueue jobQueue = new(context: _adapter, maxAttempts: 3);
        QueueJob job = new()
        {
            Queue = "boundary",
            Payload = "at-limit payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 3,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        QueueJobModel? reserved = jobQueue.ReserveJob(name: "boundary", currentJobId: null);

        Assert.Null(@object: reserved);
        QueueJob? untouched = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(@object: untouched);
        Assert.Null(value: untouched.ReservedAt);
        Assert.Equal(expected: 3, actual: untouched.Attempts);
    }

    [Fact]
    public void ReserveJob_JobOneUnderMaxAttempts_IsReservedForFinalAttempt()
    {
        JobQueue jobQueue = new(context: _adapter, maxAttempts: 3);
        QueueJob job = new()
        {
            Queue = "boundary",
            Payload = "under-limit payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 2,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        QueueJobModel? reserved = jobQueue.ReserveJob(name: "boundary", currentJobId: null);

        Assert.NotNull(@object: reserved);
        Assert.Equal(expected: 3, actual: reserved.Attempts);
        Assert.NotNull(value: reserved.ReservedAt);
    }

    [Fact]
    public void ReserveJob_JobAtMaxAttemptsThenFailed_NeverExecutesBeyondBudget()
    {
        JobQueue jobQueue = new(context: _adapter, maxAttempts: 2);
        QueueJob job = new()
        {
            Queue = "budget",
            Payload = "budget payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        QueueJobModel? attempt1 = jobQueue.ReserveJob(name: "budget", currentJobId: null);
        Assert.NotNull(@object: attempt1);
        jobQueue.FailJob(queueJob: attempt1, exception: new InvalidOperationException(message: "fail 1"));

        QueueJobModel? attempt2 = jobQueue.ReserveJob(name: "budget", currentJobId: null);
        Assert.NotNull(@object: attempt2);
        Assert.Equal(expected: 2, actual: attempt2.Attempts);
        jobQueue.FailJob(queueJob: attempt2, exception: new InvalidOperationException(message: "fail 2"));

        QueueJobModel? attempt3 = jobQueue.ReserveJob(name: "budget", currentJobId: null);

        Assert.Null(@object: attempt3);
        Assert.Empty(collection: _context.QueueJobs);
        Assert.Single(collection: _context.FailedJobs);
    }
}
