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

public class JobQueueTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public JobQueueTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(context: _adapter);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    [Fact]
    public void Enqueue_ValidJob_AddsJobToDatabase()
    {
        // Arrange
        QueueJobModel queueJob = new()
        {
            Queue = "test",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
        };

        // Act
        _jobQueue.Enqueue(queueJob: queueJob);

        // Assert
        QueueJob? job = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(@object: job);
        Assert.Equal(expected: "test", actual: job.Queue);
        Assert.Equal(expected: "test payload", actual: job.Payload);
        Assert.Equal(expected: 1, actual: job.Priority);
    }

    [Fact]
    public void Enqueue_DuplicatePayload_DoesNotAddDuplicate()
    {
        // Arrange
        string payload = "duplicate payload";
        QueueJobModel job1 = new()
        {
            Queue = "test",
            Payload = payload,
            AvailableAt = DateTime.UtcNow,
        };
        QueueJobModel job2 = new()
        {
            Queue = "test",
            Payload = payload,
            AvailableAt = DateTime.UtcNow,
        };

        // Act
        _jobQueue.Enqueue(queueJob: job1);
        _jobQueue.Enqueue(queueJob: job2);

        // Assert
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 1, actual: jobCount);
    }

    [Fact]
    public void Dequeue_WithJobs_ReturnsAndRemovesFirstJob()
    {
        // Arrange
        QueueJobModel job1 = new()
        {
            Queue = "test",
            Payload = "payload1",
            AvailableAt = DateTime.UtcNow,
        };
        QueueJobModel job2 = new()
        {
            Queue = "test",
            Payload = "payload2",
            AvailableAt = DateTime.UtcNow,
        };

        _jobQueue.Enqueue(queueJob: job1);
        _jobQueue.Enqueue(queueJob: job2);

        // Act
        QueueJobModel? dequeuedJob = _jobQueue.Dequeue();

        // Assert
        Assert.NotNull(@object: dequeuedJob);
        Assert.Equal(expected: "payload1", actual: dequeuedJob.Payload);

        int remainingJobs = _context.QueueJobs.Count();
        Assert.Equal(expected: 1, actual: remainingJobs);
    }

    [Fact]
    public void Dequeue_EmptyQueue_ReturnsNull()
    {
        // Act
        QueueJobModel? dequeuedJob = _jobQueue.Dequeue();

        // Assert
        Assert.Null(@object: dequeuedJob);
    }

    [Fact]
    public void ReserveJob_AvailableJob_ReservesAndReturnsJob()
    {
        // Arrange
        QueueJob job = new()
        {
            Queue = "test-queue",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        // Act
        QueueJobModel? reservedJob = _jobQueue.ReserveJob(name: "test-queue", currentJobId: null);

        // Assert
        Assert.NotNull(@object: reservedJob);
        Assert.NotNull(value: reservedJob.ReservedAt);
        Assert.Equal(expected: 1, actual: reservedJob.Attempts);
        Assert.Equal(expected: "test payload", actual: reservedJob.Payload);
    }

    [Fact]
    public void ReserveJob_NoAvailableJobs_ReturnsNull()
    {
        // Act
        QueueJobModel? reservedJob = _jobQueue.ReserveJob(name: "nonexistent-queue", currentJobId: null);

        // Assert
        Assert.Null(@object: reservedJob);
    }

    [Fact]
    public void ReserveJob_JobAlreadyReserved_DoesNotReserveAgain()
    {
        // Arrange
        QueueJob job = new()
        {
            Queue = "test-queue",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow, // Already reserved
            Attempts = 1,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        // Act
        QueueJobModel? reservedJob = _jobQueue.ReserveJob(name: "test-queue", currentJobId: null);

        // Assert
        Assert.Null(@object: reservedJob);
    }

    [Fact]
    public void ReserveJob_JobExceedsMaxAttempts_DoesNotReserve()
    {
        // Arrange
        JobQueue jobQueue = new(context: _adapter, maxAttempts: 2);
        QueueJob job = new()
        {
            Queue = "test-queue",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 3, // Exceeds max attempts
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        // Act
        QueueJobModel? reservedJob = jobQueue.ReserveJob(name: "test-queue", currentJobId: null);

        // Assert
        Assert.Null(@object: reservedJob);
    }

    [Fact]
    public void ReserveJob_MultipleJobs_ReturnsHighestPriority()
    {
        // Arrange
        QueueJob lowPriorityJob = new()
        {
            Queue = "test-queue",
            Payload = "low priority",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0,
        };
        QueueJob highPriorityJob = new()
        {
            Queue = "test-queue",
            Payload = "high priority",
            AvailableAt = DateTime.UtcNow,
            Priority = 5,
            Attempts = 0,
        };

        _context.QueueJobs.AddRange(entities: [lowPriorityJob, highPriorityJob]);
        _context.SaveChanges();

        // Act
        QueueJobModel? reservedJob = _jobQueue.ReserveJob(name: "test-queue", currentJobId: null);

        // Assert
        Assert.NotNull(@object: reservedJob);
        Assert.Equal(expected: "high priority", actual: reservedJob.Payload);
        Assert.Equal(expected: 5, actual: reservedJob.Priority);
    }

    [Fact]
    public void FailJob_WithinMaxAttempts_UnreservesJob()
    {
        // Arrange
        QueueJob job = new()
        {
            Queue = "test-queue",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 1,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        InvalidOperationException exception = new(message: "Test exception");

        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = job.Queue,
            Payload = job.Payload,
            AvailableAt = job.AvailableAt,
            ReservedAt = job.ReservedAt,
            Attempts = job.Attempts,
        };

        // Act
        _jobQueue.FailJob(queueJob: jobModel, exception: exception);

        // Assert
        QueueJob? updatedJob = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(@object: updatedJob);
        Assert.Null(value: updatedJob.ReservedAt);

        // Should not create failed job record yet
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(expected: 0, actual: failedJobCount);
    }

    [Fact]
    public void FailJob_ExceedsMaxAttempts_MovesToFailedJobs()
    {
        // Arrange
        JobQueue jobQueue = new(context: _adapter, maxAttempts: 2);
        QueueJob job = new()
        {
            Queue = "test-queue",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 2, // Equals max attempts
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        InvalidOperationException exception = new(message: "Test exception");

        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = job.Queue,
            Payload = job.Payload,
            AvailableAt = job.AvailableAt,
            ReservedAt = job.ReservedAt,
            Attempts = job.Attempts,
        };

        // Act
        jobQueue.FailJob(queueJob: jobModel, exception: exception);

        // Assert
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: queueJobCount);

        FailedJob? failedJob = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(@object: failedJob);
        Assert.Equal(expected: "test-queue", actual: failedJob.Queue);
        Assert.Equal(expected: "test payload", actual: failedJob.Payload);
        Assert.Contains(expectedSubstring: "Test exception", actualString: failedJob.Exception);
    }

    [Fact]
    public void DeleteJob_ExistingJob_RemovesFromDatabase()
    {
        // Arrange
        QueueJob job = new()
        {
            Queue = "test-queue",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
        };
        _context.QueueJobs.Add(entity: job);
        _context.SaveChanges();

        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = job.Queue,
            Payload = job.Payload,
            AvailableAt = job.AvailableAt,
        };

        // Act
        _jobQueue.DeleteJob(queueJob: jobModel);

        // Assert
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: jobCount);
    }

    [Fact]
    public void RequeueFailedJob_MovesFailedJobBackToQueue()
    {
        // Arrange
        FailedJob failedJob = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "test-queue",
            Payload = "test payload",
            Exception = "Test exception",
            FailedAt = DateTime.UtcNow,
        };
        _context.FailedJobs.Add(entity: failedJob);
        _context.SaveChanges();

        FailedJob? savedFailedJob = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(@object: savedFailedJob);

        // Act
        _jobQueue.RequeueFailedJob(failedJobId: (int)savedFailedJob.Id);

        // Assert - The failed job should be removed and a new queue job created
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(expected: 0, actual: failedJobCount);

        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 1, actual: queueJobCount);

        QueueJob? requeuedJob = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(@object: requeuedJob);
        Assert.Equal(expected: "test-queue", actual: requeuedJob.Queue);
        Assert.Equal(expected: "test payload", actual: requeuedJob.Payload);
        Assert.Equal(expected: 0, actual: requeuedJob.Attempts);
    }

    [Fact]
    public void RequeueFailedJob_NonexistentJob_DoesNothing()
    {
        // Act
        _jobQueue.RequeueFailedJob(failedJobId: 999);

        // Assert - Should not throw exception
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: queueJobCount);
    }

    [Fact]
    public void RetryFailedJobs_AllFailedJobs_MovesAllBackToQueue()
    {
        // Arrange
        FailedJob failedJob1 = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "queue1",
            Payload = "payload1",
            Exception = "Exception1",
            FailedAt = DateTime.UtcNow,
        };
        FailedJob failedJob2 = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "queue2",
            Payload = "payload2",
            Exception = "Exception2",
            FailedAt = DateTime.UtcNow,
        };

        _context.FailedJobs.AddRange(entities: [failedJob1, failedJob2]);
        _context.SaveChanges();

        // Act
        _jobQueue.RetryFailedJobs();

        // Assert
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(expected: 0, actual: failedJobCount);

        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 2, actual: queueJobCount);

        List<QueueJob> queueJobs = _context.QueueJobs.ToList();
        Assert.Contains(collection: queueJobs, filter: j => j is { Queue: "queue1", Payload: "payload1" });
        Assert.Contains(collection: queueJobs, filter: j => j is { Queue: "queue2", Payload: "payload2" });
    }

    [Fact]
    public void RetryFailedJobs_SpecificFailedJob_MovesOnlyThatJobBackToQueue()
    {
        // Arrange
        FailedJob failedJob1 = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "queue1",
            Payload = "payload1",
            Exception = "Exception1",
            FailedAt = DateTime.UtcNow,
        };
        FailedJob failedJob2 = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "queue2",
            Payload = "payload2",
            Exception = "Exception2",
            FailedAt = DateTime.UtcNow,
        };

        _context.FailedJobs.AddRange(entities: [failedJob1, failedJob2]);
        _context.SaveChanges();

        // Act
        _jobQueue.RetryFailedJobs(failedJobId: failedJob1.Id);

        // Assert
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(expected: 1, actual: failedJobCount); // Only one should remain

        FailedJob? remainingFailedJob = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(@object: remainingFailedJob);
        Assert.Equal(expected: "queue2", actual: remainingFailedJob.Queue);

        QueueJob? queueJob = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(@object: queueJob);
        Assert.Equal(expected: "queue1", actual: queueJob.Queue);
        Assert.Equal(expected: "payload1", actual: queueJob.Payload);
    }
}
