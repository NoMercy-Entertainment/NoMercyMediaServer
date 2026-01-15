using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

public class JobQueueTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly JobQueue _jobQueue;

    public JobQueueTests()
    {
        _context = TestQueueContextFactory.CreateInMemoryContext();
        _jobQueue = new(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void Enqueue_ValidJob_AddsJobToDatabase()
    {
        // Arrange
        QueueJob queueJob = new()
        {
            Queue = "test",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
            Priority = 1
        };

        // Act
        _jobQueue.Enqueue(queueJob);

        // Assert
        QueueJob? job = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(job);
        Assert.Equal("test", job.Queue);
        Assert.Equal("test payload", job.Payload);
        Assert.Equal(1, job.Priority);
    }

    [Fact]
    public void Enqueue_DuplicatePayload_DoesNotAddDuplicate()
    {
        // Arrange
        string payload = "duplicate payload";
        QueueJob job1 = new()
        {
            Queue = "test",
            Payload = payload,
            AvailableAt = DateTime.UtcNow
        };
        QueueJob job2 = new()
        {
            Queue = "test",
            Payload = payload,
            AvailableAt = DateTime.UtcNow
        };

        // Act
        _jobQueue.Enqueue(job1);
        _jobQueue.Enqueue(job2);

        // Assert
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(1, jobCount);
    }

    [Fact]
    public void Dequeue_WithJobs_ReturnsAndRemovesFirstJob()
    {
        // Arrange
        QueueJob job1 = new()
        {
            Queue = "test",
            Payload = "payload1",
            AvailableAt = DateTime.UtcNow
        };
        QueueJob job2 = new()
        {
            Queue = "test",
            Payload = "payload2",
            AvailableAt = DateTime.UtcNow
        };
        
        _jobQueue.Enqueue(job1);
        _jobQueue.Enqueue(job2);

        // Act
        QueueJob? dequeuedJob = _jobQueue.Dequeue();

        // Assert
        Assert.NotNull(dequeuedJob);
        Assert.Equal("payload1", dequeuedJob.Payload);
        
        int remainingJobs = _context.QueueJobs.Count();
        Assert.Equal(1, remainingJobs);
    }

    [Fact]
    public void Dequeue_EmptyQueue_ReturnsNull()
    {
        // Act
        QueueJob? dequeuedJob = _jobQueue.Dequeue();

        // Assert
        Assert.Null(dequeuedJob);
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
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act
        QueueJob? reservedJob = _jobQueue.ReserveJob("test-queue", null);

        // Assert
        Assert.NotNull(reservedJob);
        Assert.NotNull(reservedJob.ReservedAt);
        Assert.Equal(1, reservedJob.Attempts);
        Assert.Equal("test payload", reservedJob.Payload);
    }

    [Fact]
    public void ReserveJob_NoAvailableJobs_ReturnsNull()
    {
        // Act
        QueueJob? reservedJob = _jobQueue.ReserveJob("nonexistent-queue", null);

        // Assert
        Assert.Null(reservedJob);
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
            Attempts = 1
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act
        QueueJob? reservedJob = _jobQueue.ReserveJob("test-queue", null);

        // Assert
        Assert.Null(reservedJob);
    }

    [Fact]
    public void ReserveJob_JobExceedsMaxAttempts_DoesNotReserve()
    {
        // Arrange
        JobQueue jobQueue = new(_context, maxAttempts: 2);
        QueueJob job = new()
        {
            Queue = "test-queue",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 3 // Exceeds max attempts
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act
        QueueJob? reservedJob = jobQueue.ReserveJob("test-queue", null);

        // Assert
        Assert.Null(reservedJob);
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
            Attempts = 0
        };
        QueueJob highPriorityJob = new()
        {
            Queue = "test-queue",
            Payload = "high priority",
            AvailableAt = DateTime.UtcNow,
            Priority = 5,
            Attempts = 0
        };
        
        _context.QueueJobs.AddRange(lowPriorityJob, highPriorityJob);
        _context.SaveChanges();

        // Act
        QueueJob? reservedJob = _jobQueue.ReserveJob("test-queue", null);

        // Assert
        Assert.NotNull(reservedJob);
        Assert.Equal("high priority", reservedJob.Payload);
        Assert.Equal(5, reservedJob.Priority);
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
            Attempts = 1
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        InvalidOperationException exception = new("Test exception");

        // Act
        _jobQueue.FailJob(job, exception);

        // Assert
        QueueJob? updatedJob = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(updatedJob);
        Assert.Null(updatedJob.ReservedAt);
        
        // Should not create failed job record yet
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(0, failedJobCount);
    }

    [Fact]
    public void FailJob_ExceedsMaxAttempts_MovesToFailedJobs()
    {
        // Arrange
        JobQueue jobQueue = new(_context, maxAttempts: 2);
        QueueJob job = new()
        {
            Queue = "test-queue",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 2 // Equals max attempts
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        InvalidOperationException exception = new("Test exception");

        // Act
        jobQueue.FailJob(job, exception);

        // Assert
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(0, queueJobCount);
        
        FailedJob? failedJob = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(failedJob);
        Assert.Equal("test-queue", failedJob.Queue);
        Assert.Equal("test payload", failedJob.Payload);
        Assert.Contains("Test exception", failedJob.Exception);
    }

    [Fact]
    public void DeleteJob_ExistingJob_RemovesFromDatabase()
    {
        // Arrange
        QueueJob job = new()
        {
            Queue = "test-queue",
            Payload = "test payload",
            AvailableAt = DateTime.UtcNow
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act
        _jobQueue.DeleteJob(job);

        // Assert
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(0, jobCount);
    }

    [Fact]
    public void RequeueFailedJob_WithTypeMismatchBug_HandlesGracefully()
    {
        // This test documents a known bug in RequeueFailedJob method:
        // The method parameter is int but FailedJob.Id is long, causing Find() to fail
        // The method catches the exception internally and silently fails
        
        // Arrange
        FailedJob failedJob = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "test-queue",
            Payload = "test payload",
            Exception = "Test exception",
            FailedAt = DateTime.UtcNow
        };
        _context.FailedJobs.Add(failedJob);
        _context.SaveChanges();

        FailedJob? savedFailedJob = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(savedFailedJob);

        // Act - The method will silently fail due to the type mismatch bug
        _jobQueue.RequeueFailedJob((int)savedFailedJob.Id);
        
        // Assert - The job should still exist because the requeue failed silently
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(1, failedJobCount); // Job was not removed due to the bug
        
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(0, queueJobCount); // No queue job was created
    }

    [Fact]
    public void RequeueFailedJob_NonexistentJob_DoesNothing()
    {
        // Act
        _jobQueue.RequeueFailedJob(999);

        // Assert - Should not throw exception
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(0, queueJobCount);
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
            FailedAt = DateTime.UtcNow
        };
        FailedJob failedJob2 = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "queue2",
            Payload = "payload2",
            Exception = "Exception2",
            FailedAt = DateTime.UtcNow
        };
        
        _context.FailedJobs.AddRange(failedJob1, failedJob2);
        _context.SaveChanges();

        // Act
        _jobQueue.RetryFailedJobs();

        // Assert
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(0, failedJobCount);
        
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(2, queueJobCount);
        
        List<QueueJob> queueJobs = _context.QueueJobs.ToList();
        Assert.Contains(queueJobs, j => j is { Queue: "queue1", Payload: "payload1" });
        Assert.Contains(queueJobs, j => j is { Queue: "queue2", Payload: "payload2" });
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
            FailedAt = DateTime.UtcNow
        };
        FailedJob failedJob2 = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "queue2",
            Payload = "payload2",
            Exception = "Exception2",
            FailedAt = DateTime.UtcNow
        };
        
        _context.FailedJobs.AddRange(failedJob1, failedJob2);
        _context.SaveChanges();

        // Act
        _jobQueue.RetryFailedJobs(failedJob1.Id);

        // Assert
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(1, failedJobCount); // Only one should remain
        
        FailedJob? remainingFailedJob = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(remainingFailedJob);
        Assert.Equal("queue2", remainingFailedJob.Queue);
        
        QueueJob? queueJob = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(queueJob);
        Assert.Equal("queue1", queueJob.Queue);
        Assert.Equal("payload1", queueJob.Payload);
    }
}
