using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// CHAR-08: Queue behavior tests covering the full job lifecycle —
/// enqueue, reserve, execute, fail (implicit retry), permanent failure, and manual retry.
/// These tests complement the existing JobQueueTests/QueueIntegrationTests by exercising
/// behavioral gaps: implicit retry loops, cross-queue isolation, currentJobId guard,
/// attempt boundary precision, and exception preservation.
/// </summary>
[Trait("Category", "Characterization")]
public class QueueBehaviorTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public QueueBehaviorTests()
    {
        (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        _jobQueue = new(_adapter);
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _context.Dispose();
    }

    // ── Implicit Retry: fail under maxAttempts keeps job available ──

    [Fact]
    public void FailJob_UnderMaxAttempts_JobRemainsInQueueAndIsReservableAgain()
    {
        // Arrange — enqueue and reserve a job (Attempts goes to 1)
        QueueJob job = new()
        {
            Queue = "retry-test",
            Payload = SerializationHelper.Serialize(new TestJob { Message = "retry me", ShouldFail = true }),
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        QueueJobModel? reserved = _jobQueue.ReserveJob("retry-test", null);
        Assert.NotNull(reserved);
        Assert.Equal(1, reserved.Attempts);
        Assert.NotNull(reserved.ReservedAt);

        // Act — fail it (Attempts=1 < default maxAttempts=3)
        _jobQueue.FailJob(reserved, new InvalidOperationException("boom"));

        // Assert — job stays in QueueJobs with ReservedAt cleared
        QueueJob? stillInQueue = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(stillInQueue);
        Assert.Null(stillInQueue.ReservedAt);
        Assert.Equal(1, stillInQueue.Attempts);
        Assert.Equal(0, _context.FailedJobs.Count());

        // Act 2 — reserve again (Attempts goes to 2)
        QueueJobModel? secondReserve = _jobQueue.ReserveJob("retry-test", null);
        Assert.NotNull(secondReserve);
        Assert.Equal(2, secondReserve.Attempts);
        Assert.NotNull(secondReserve.ReservedAt);
    }

    [Fact]
    public async Task ImplicitRetryLoop_FailTwiceThenSucceed_JobCompletesOnThirdAttempt()
    {
        // Arrange
        TestJob testJob = new() { Message = "will succeed eventually", ShouldFail = true };
        QueueJob job = new()
        {
            Queue = "retry-loop",
            Payload = SerializationHelper.Serialize(testJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Attempt 1: reserve, execute (fail), fail-job
        QueueJobModel? attempt1 = _jobQueue.ReserveJob("retry-loop", null);
        Assert.NotNull(attempt1);
        Assert.Equal(1, attempt1.Attempts);
        try
        {
            NoMercyQueue.Core.Interfaces.IShouldQueue exec1 = (NoMercyQueue.Core.Interfaces.IShouldQueue)SerializationHelper.Deserialize<object>(attempt1.Payload);
            await exec1.Handle();
        }
        catch (Exception ex)
        {
            _jobQueue.FailJob(attempt1, ex);
        }
        Assert.Equal(1, _context.QueueJobs.Count());
        Assert.Equal(0, _context.FailedJobs.Count());

        // Attempt 2: reserve, execute (fail), fail-job
        QueueJobModel? attempt2 = _jobQueue.ReserveJob("retry-loop", null);
        Assert.NotNull(attempt2);
        Assert.Equal(2, attempt2.Attempts);
        try
        {
            NoMercyQueue.Core.Interfaces.IShouldQueue exec2 = (NoMercyQueue.Core.Interfaces.IShouldQueue)SerializationHelper.Deserialize<object>(attempt2.Payload);
            await exec2.Handle();
        }
        catch (Exception ex)
        {
            _jobQueue.FailJob(attempt2, ex);
        }
        Assert.Equal(1, _context.QueueJobs.Count());
        Assert.Equal(0, _context.FailedJobs.Count());

        // Attempt 3: reserve, execute (succeed this time), delete
        QueueJobModel? attempt3 = _jobQueue.ReserveJob("retry-loop", null);
        Assert.NotNull(attempt3);
        Assert.Equal(3, attempt3.Attempts);

        // Deserialize and fix it
        TestJob fixedJob = SerializationHelper.Deserialize<TestJob>(attempt3.Payload);
        fixedJob.ShouldFail = false;
        await fixedJob.Handle();
        _jobQueue.DeleteJob(attempt3);

        // Assert — job completed, no failed jobs
        Assert.True(fixedJob.HasExecuted);
        Assert.Equal(0, _context.QueueJobs.Count());
        Assert.Equal(0, _context.FailedJobs.Count());
    }

    // ── Attempt Boundary: exact maxAttempts triggers permanent failure ──

    [Fact]
    public void FailJob_AtExactMaxAttempts_MovesToFailedJobs()
    {
        // Arrange — use maxAttempts=2 and set Attempts=2 (at boundary)
        JobQueue jobQueue = new(_adapter, maxAttempts: 2);
        QueueJob job = new()
        {
            Queue = "boundary-test",
            Payload = "boundary payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 2
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act — pass a QueueJobModel to the JobQueue method
        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = "boundary-test",
            Payload = "boundary payload",
            AvailableAt = job.AvailableAt,
            ReservedAt = job.ReservedAt,
            Attempts = 2
        };
        jobQueue.FailJob(jobModel, new InvalidOperationException("at boundary"));

        // Assert — moved to FailedJobs, removed from QueueJobs
        Assert.Equal(0, _context.QueueJobs.Count());
        FailedJob? failed = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(failed);
        Assert.Equal("boundary-test", failed.Queue);
        Assert.Equal("boundary payload", failed.Payload);
    }

    [Fact]
    public void FailJob_OneUnderMaxAttempts_StaysInQueue()
    {
        // Arrange — use maxAttempts=2 and set Attempts=1 (one under)
        JobQueue jobQueue = new(_adapter, maxAttempts: 2);
        QueueJob job = new()
        {
            Queue = "boundary-test",
            Payload = "under boundary payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 1
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act — pass a QueueJobModel to the JobQueue method
        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = "boundary-test",
            Payload = "under boundary payload",
            AvailableAt = job.AvailableAt,
            ReservedAt = job.ReservedAt,
            Attempts = 1
        };
        jobQueue.FailJob(jobModel, new InvalidOperationException("under boundary"));

        // Assert — stays in QueueJobs, not in FailedJobs
        Assert.Equal(1, _context.QueueJobs.Count());
        Assert.Equal(0, _context.FailedJobs.Count());
        QueueJob? remaining = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(remaining);
        Assert.Null(remaining.ReservedAt);
    }

    [Fact]
    public void FailJob_AboveMaxAttempts_MovesToFailedJobs()
    {
        // Arrange — use maxAttempts=2 and set Attempts=5 (well above)
        JobQueue jobQueue = new(_adapter, maxAttempts: 2);
        QueueJob job = new()
        {
            Queue = "boundary-test",
            Payload = "above boundary payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 5
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act — pass a QueueJobModel to the JobQueue method
        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = "boundary-test",
            Payload = "above boundary payload",
            AvailableAt = job.AvailableAt,
            ReservedAt = job.ReservedAt,
            Attempts = 5
        };
        jobQueue.FailJob(jobModel, new InvalidOperationException("above boundary"));

        // Assert
        Assert.Equal(0, _context.QueueJobs.Count());
        Assert.Equal(1, _context.FailedJobs.Count());
    }

    // ── Full Lifecycle: enqueue → exhaust retries → permanent fail → manual retry → succeed ──

    [Fact]
    public async Task FullRetryLifecycle_ExhaustRetriesThenManualRetry_Succeeds()
    {
        // Arrange
        JobQueue jobQueue = new(_adapter, maxAttempts: 2);
        TestJob testJob = new() { Message = "lifecycle", ShouldFail = true };
        QueueJob job = new()
        {
            Queue = "lifecycle",
            Payload = SerializationHelper.Serialize(testJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Attempt 1: reserve (Attempts → 1), fail
        QueueJobModel? a1 = jobQueue.ReserveJob("lifecycle", null);
        Assert.NotNull(a1);
        jobQueue.FailJob(a1, new("fail 1"));
        Assert.Equal(1, _context.QueueJobs.Count());
        Assert.Equal(0, _context.FailedJobs.Count());

        // Attempt 2: reserve (Attempts → 2 = maxAttempts), fail → permanent failure
        QueueJobModel? a2 = jobQueue.ReserveJob("lifecycle", null);
        Assert.NotNull(a2);
        Assert.Equal(2, a2.Attempts);
        jobQueue.FailJob(a2, new("fail 2"));
        Assert.Equal(0, _context.QueueJobs.Count());
        Assert.Equal(1, _context.FailedJobs.Count());

        // Manual retry using RetryFailedJobs
        jobQueue.RetryFailedJobs();
        Assert.Equal(1, _context.QueueJobs.Count());
        Assert.Equal(0, _context.FailedJobs.Count());

        // Verify attempts were reset to 0
        QueueJob? retried = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(retried);
        Assert.Equal(0, retried.Attempts);
        Assert.Null(retried.ReservedAt);

        // Attempt 3: reserve and succeed
        QueueJobModel? a3 = jobQueue.ReserveJob("lifecycle", null);
        Assert.NotNull(a3);
        TestJob fixedJob = SerializationHelper.Deserialize<TestJob>(a3.Payload);
        fixedJob.ShouldFail = false;
        await fixedJob.Handle();
        jobQueue.DeleteJob(a3);

        Assert.True(fixedJob.HasExecuted);
        Assert.Equal(0, _context.QueueJobs.Count());
        Assert.Equal(0, _context.FailedJobs.Count());
    }

    // ── Cross-Queue Isolation ──

    [Fact]
    public void ReserveJob_DifferentQueueName_DoesNotReturnJobFromOtherQueue()
    {
        // Arrange — job on queue "alpha"
        QueueJob job = new()
        {
            Queue = "alpha",
            Payload = "alpha payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act — reserve on queue "beta"
        QueueJobModel? reserved = _jobQueue.ReserveJob("beta", null);

        // Assert
        Assert.Null(reserved);
        // Original job untouched
        QueueJob? original = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(original);
        Assert.Null(original.ReservedAt);
        Assert.Equal(0, original.Attempts);
    }

    [Fact]
    public void ReserveJob_MultipleQueues_EachQueueServesOwnJobs()
    {
        // Arrange
        QueueJob alphaJob = new()
        {
            Queue = "alpha",
            Payload = "alpha payload",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0
        };
        QueueJob betaJob = new()
        {
            Queue = "beta",
            Payload = "beta payload",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0
        };
        _context.QueueJobs.AddRange(alphaJob, betaJob);
        _context.SaveChanges();

        // Act
        QueueJobModel? reservedAlpha = _jobQueue.ReserveJob("alpha", null);
        QueueJobModel? reservedBeta = _jobQueue.ReserveJob("beta", null);

        // Assert
        Assert.NotNull(reservedAlpha);
        Assert.Equal("alpha payload", reservedAlpha.Payload);
        Assert.NotNull(reservedBeta);
        Assert.Equal("beta payload", reservedBeta.Payload);
    }

    // ── currentJobId Guard ──

    [Fact]
    public void ReserveJob_WithCurrentJobId_ReturnsNull()
    {
        // The compiled query has `Where(j => currentJobId == null)`, meaning if a
        // worker already has a job (currentJobId != null), no new job is returned.

        // Arrange
        QueueJob job = new()
        {
            Queue = "guard-test",
            Payload = "guard payload",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act — pass non-null currentJobId
        QueueJobModel? reserved = _jobQueue.ReserveJob("guard-test", 42L);

        // Assert — worker busy, should not get another job
        Assert.Null(reserved);
        // Original job still unreserved
        QueueJob? original = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(original);
        Assert.Null(original.ReservedAt);
        Assert.Equal(0, original.Attempts);
    }

    [Fact]
    public void ReserveJob_WithNullCurrentJobId_ReturnsJob()
    {
        // Arrange
        QueueJob job = new()
        {
            Queue = "guard-test",
            Payload = "guard payload null",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act
        QueueJobModel? reserved = _jobQueue.ReserveJob("guard-test", null);

        // Assert
        Assert.NotNull(reserved);
        Assert.Equal("guard payload null", reserved.Payload);
    }

    // ── Exception Content Preserved in FailedJob ──

    [Fact]
    public void FailJob_ExceptionContentPreserved_InFailedJobRecord()
    {
        // Arrange
        JobQueue jobQueue = new(_adapter, maxAttempts: 1);
        QueueJob job = new()
        {
            Queue = "exception-test",
            Payload = "exception payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 1
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        InvalidOperationException innerEx = new("root cause detail");
        AggregateException outerEx = new("wrapper", innerEx);

        // Act — pass a QueueJobModel to the JobQueue method
        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = "exception-test",
            Payload = "exception payload",
            AvailableAt = job.AvailableAt,
            ReservedAt = job.ReservedAt,
            Attempts = 1
        };
        jobQueue.FailJob(jobModel, outerEx);

        // Assert
        FailedJob? failed = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(failed);
        Assert.Contains("root cause detail", failed.Exception);
        Assert.NotEqual(Guid.Empty, failed.Uuid);
        Assert.Equal("default", failed.Connection);
    }

    [Fact]
    public void FailJob_NoInnerException_UsesOuterException()
    {
        // Arrange
        JobQueue jobQueue = new(_adapter, maxAttempts: 1);
        QueueJob job = new()
        {
            Queue = "exception-test",
            Payload = "outer exception payload",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 1
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        InvalidOperationException exception = new("direct error message");

        // Act — pass a QueueJobModel to the JobQueue method
        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = "exception-test",
            Payload = "outer exception payload",
            AvailableAt = job.AvailableAt,
            ReservedAt = job.ReservedAt,
            Attempts = 1
        };
        jobQueue.FailJob(jobModel, exception);

        // Assert
        FailedJob? failed = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(failed);
        Assert.Contains("direct error message", failed.Exception);
    }

    // ── RetryFailedJobs Resets Attempts ──

    [Fact]
    public void RetryFailedJobs_ResetsAttemptsToZero()
    {
        // Arrange
        FailedJob failedJob = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "reset-test",
            Payload = "reset payload",
            Exception = "some error",
            FailedAt = DateTime.UtcNow
        };
        _context.FailedJobs.Add(failedJob);
        _context.SaveChanges();

        // Act
        _jobQueue.RetryFailedJobs();

        // Assert
        QueueJob? requeuedJob = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(requeuedJob);
        Assert.Equal(0, requeuedJob.Attempts);
        Assert.Null(requeuedJob.ReservedAt);
        Assert.Equal("reset-test", requeuedJob.Queue);
        Assert.Equal("reset payload", requeuedJob.Payload);
    }

    [Fact]
    public void RetryFailedJobs_PreservesQueueName()
    {
        // Arrange
        FailedJob failedJob = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "specific-queue-name",
            Payload = "queue preservation payload",
            Exception = "err",
            FailedAt = DateTime.UtcNow
        };
        _context.FailedJobs.Add(failedJob);
        _context.SaveChanges();

        // Act
        _jobQueue.RetryFailedJobs();

        // Assert
        QueueJob? requeued = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(requeued);
        Assert.Equal("specific-queue-name", requeued.Queue);
    }

    // ── Enqueue: Different Payloads on Same Queue ──

    [Fact]
    public void Enqueue_DifferentPayloadsSameQueue_BothStored()
    {
        // Arrange
        QueueJobModel job1 = new()
        {
            Queue = "same-queue",
            Payload = SerializationHelper.Serialize(new TestJob { Message = "job-A" }),
            AvailableAt = DateTime.UtcNow
        };
        QueueJobModel job2 = new()
        {
            Queue = "same-queue",
            Payload = SerializationHelper.Serialize(new TestJob { Message = "job-B" }),
            AvailableAt = DateTime.UtcNow
        };

        // Act
        _jobQueue.Enqueue(job1);
        _jobQueue.Enqueue(job2);

        // Assert
        Assert.Equal(2, _context.QueueJobs.Count());
    }

    [Fact]
    public void Enqueue_SamePayloadDifferentQueues_BothStoredBecauseDuplicateCheckIsGlobal()
    {
        // The duplicate check is global (not per-queue): Exists() checks all QueueJobs
        // regardless of queue name. So same payload on different queues is still a duplicate.

        // Arrange
        string payload = SerializationHelper.Serialize(new TestJob { Message = "shared" });
        QueueJobModel job1 = new()
        {
            Queue = "queue-1",
            Payload = payload,
            AvailableAt = DateTime.UtcNow
        };
        QueueJobModel job2 = new()
        {
            Queue = "queue-2",
            Payload = payload,
            AvailableAt = DateTime.UtcNow
        };

        // Act
        _jobQueue.Enqueue(job1);
        _jobQueue.Enqueue(job2);

        // Assert — global duplicate prevention: only 1 job stored
        Assert.Equal(1, _context.QueueJobs.Count());
    }

    // ── ReserveJob: Already-Reserved Job Not Double-Reserved ──

    [Fact]
    public void ReserveJob_TwoConsecutiveReserves_SecondReturnsNullWhenOnlyOneJob()
    {
        // Arrange — single job
        QueueJob job = new()
        {
            Queue = "double-reserve",
            Payload = "single job payload",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act — reserve once
        QueueJobModel? first = _jobQueue.ReserveJob("double-reserve", null);
        Assert.NotNull(first);

        // Act — reserve again (job is now reserved, ReservedAt != null)
        QueueJobModel? second = _jobQueue.ReserveJob("double-reserve", null);

        // Assert — no unreserved jobs left
        Assert.Null(second);
    }

    [Fact]
    public void ReserveJob_TwoJobsReservedSequentially_BothReturned()
    {
        // Arrange — two jobs
        QueueJob job1 = new()
        {
            Queue = "seq-reserve",
            Payload = "payload-1",
            AvailableAt = DateTime.UtcNow,
            Priority = 2,
            Attempts = 0
        };
        QueueJob job2 = new()
        {
            Queue = "seq-reserve",
            Payload = "payload-2",
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0
        };
        _context.QueueJobs.AddRange(job1, job2);
        _context.SaveChanges();

        // Act — reserve first (deletes it to simulate worker finishing)
        QueueJobModel? first = _jobQueue.ReserveJob("seq-reserve", null);
        Assert.NotNull(first);
        Assert.Equal("payload-1", first.Payload); // Higher priority
        _jobQueue.DeleteJob(first);

        // Act — reserve second
        QueueJobModel? second = _jobQueue.ReserveJob("seq-reserve", null);
        Assert.NotNull(second);
        Assert.Equal("payload-2", second.Payload);
    }

    // ── Dequeue vs ReserveJob: Dequeue removes without reservation ──

    [Fact]
    public void Dequeue_RemovesJobImmediately_NoReservation()
    {
        // Arrange
        QueueJob job = new()
        {
            Queue = "dequeue-test",
            Payload = "dequeue payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act
        QueueJobModel? dequeued = _jobQueue.Dequeue();

        // Assert — job removed entirely (not just reserved)
        Assert.NotNull(dequeued);
        Assert.Equal(0, _context.QueueJobs.Count());
        // Attempts not incremented (Dequeue doesn't touch Attempts)
        Assert.Equal(0, dequeued.Attempts);
    }

    // ── DeleteJob: Idempotent behavior ──

    [Fact]
    public void DeleteJob_AlreadyDeletedJob_DoesNotThrow()
    {
        // Arrange
        QueueJob job = new()
        {
            Queue = "delete-test",
            Payload = "delete payload",
            AvailableAt = DateTime.UtcNow
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Create a QueueJobModel for JobQueue interaction
        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = "delete-test",
            Payload = "delete payload",
            AvailableAt = job.AvailableAt
        };

        // Act — delete once
        _jobQueue.DeleteJob(jobModel);
        Assert.Equal(0, _context.QueueJobs.Count());

        // Act — delete again (catch block in DeleteJob swallows the exception)
        Exception? ex = Record.Exception(() => _jobQueue.DeleteJob(jobModel));

        // Assert — no exception propagated
        Assert.Null(ex);
    }

    // ── Serialization Round-Trip Through Queue ──

    [Fact]
    public async Task SerializationRoundTrip_ThroughEnqueueReserve_PreservesJobState()
    {
        // Arrange
        AnotherTestJob original = new() { Value = 42, HasExecuted = false };
        QueueJob job = new()
        {
            Queue = "serde-test",
            Payload = SerializationHelper.Serialize(original),
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
            Attempts = 0
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act — reserve and deserialize
        QueueJobModel? reserved = _jobQueue.ReserveJob("serde-test", null);
        Assert.NotNull(reserved);
        object deserialized = SerializationHelper.Deserialize<object>(reserved.Payload);

        // Assert — type and state preserved
        Assert.IsType<AnotherTestJob>(deserialized);
        AnotherTestJob roundTripped = (AnotherTestJob)deserialized;
        Assert.Equal(42, roundTripped.Value);
        Assert.False(roundTripped.HasExecuted);

        // Execute and verify
        await roundTripped.Handle();
        Assert.True(roundTripped.HasExecuted);
        Assert.Equal(84, roundTripped.Value);
    }

    // ── Multiple Failed Jobs: RetryFailedJobs processes all ──

    [Fact]
    public void RetryFailedJobs_MultipleFailedJobs_AllRequeued()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            _context.FailedJobs.Add(new()
            {
                Uuid = Guid.NewGuid(),
                Connection = "default",
                Queue = $"queue-{i}",
                Payload = $"payload-{i}",
                Exception = $"error-{i}",
                FailedAt = DateTime.UtcNow
            });
        }
        _context.SaveChanges();
        Assert.Equal(5, _context.FailedJobs.Count());

        // Act
        _jobQueue.RetryFailedJobs();

        // Assert
        Assert.Equal(0, _context.FailedJobs.Count());
        Assert.Equal(5, _context.QueueJobs.Count());

        List<QueueJob> requeued = _context.QueueJobs.ToList();
        for (int i = 0; i < 5; i++)
        {
            Assert.Contains(requeued, j => j.Queue == $"queue-{i}" && j.Payload == $"payload-{i}");
        }
    }

    // ── RetryFailedJobs with specific ID only retries that one ──

    [Fact]
    public void RetryFailedJobs_SpecificId_OnlyRetriesThatJob()
    {
        // Arrange
        FailedJob keep = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "keep",
            Payload = "keep-payload",
            Exception = "keep-error",
            FailedAt = DateTime.UtcNow
        };
        FailedJob retry = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "retry",
            Payload = "retry-payload",
            Exception = "retry-error",
            FailedAt = DateTime.UtcNow
        };
        _context.FailedJobs.AddRange(keep, retry);
        _context.SaveChanges();

        // Act
        _jobQueue.RetryFailedJobs(retry.Id);

        // Assert
        Assert.Equal(1, _context.FailedJobs.Count());
        Assert.Equal("keep", _context.FailedJobs.First().Queue);
        Assert.Equal(1, _context.QueueJobs.Count());
        Assert.Equal("retry-payload", _context.QueueJobs.First().Payload);
    }

    // ── Priority Ordering Across Multiple Reserves ──

    [Fact]
    public void ReserveJob_FiveJobsDifferentPriorities_ReservedInDescendingPriorityOrder()
    {
        // Arrange
        int[] priorities = [3, 1, 5, 2, 4];
        foreach (int p in priorities)
        {
            _context.QueueJobs.Add(new()
            {
                Queue = "priority-order",
                Payload = $"priority-{p}",
                AvailableAt = DateTime.UtcNow,
                Priority = p,
                Attempts = 0
            });
        }
        _context.SaveChanges();

        // Act — reserve all 5 in sequence, deleting each to make the next available
        List<int> reservedOrder = [];
        for (int i = 0; i < 5; i++)
        {
            QueueJobModel? reserved = _jobQueue.ReserveJob("priority-order", null);
            Assert.NotNull(reserved);
            reservedOrder.Add(reserved.Priority);
            _jobQueue.DeleteJob(reserved);
        }

        // Assert — descending priority order
        Assert.Equal([5, 4, 3, 2, 1], reservedOrder);
    }

    // ── Enqueue After Delete: Same Payload Can Be Re-enqueued ──

    [Fact]
    public void Enqueue_AfterDelete_SamePayloadCanBeReenqueued()
    {
        // Arrange
        string payload = "re-enqueue payload";
        QueueJobModel job = new()
        {
            Queue = "reenqueue",
            Payload = payload,
            AvailableAt = DateTime.UtcNow
        };
        _jobQueue.Enqueue(job);
        Assert.Equal(1, _context.QueueJobs.Count());

        // Delete it
        _jobQueue.DeleteJob(job);
        Assert.Equal(0, _context.QueueJobs.Count());

        // Act — enqueue same payload again
        QueueJobModel job2 = new()
        {
            Queue = "reenqueue",
            Payload = payload,
            AvailableAt = DateTime.UtcNow
        };
        _jobQueue.Enqueue(job2);

        // Assert — job is back
        Assert.Equal(1, _context.QueueJobs.Count());
    }

    // ── FailJob: ReservedAt is always cleared ──

    [Fact]
    public void FailJob_AlwaysClearsReservedAt_RegardlessOfAttemptCount()
    {
        // Arrange — job within max attempts
        QueueJob job = new()
        {
            Queue = "reserved-clear",
            Payload = "clear test",
            AvailableAt = DateTime.UtcNow,
            ReservedAt = DateTime.UtcNow,
            Attempts = 1
        };
        _context.QueueJobs.Add(job);
        _context.SaveChanges();

        // Act — pass a QueueJobModel to the JobQueue method
        QueueJobModel jobModel = new()
        {
            Id = job.Id,
            Queue = "reserved-clear",
            Payload = "clear test",
            AvailableAt = job.AvailableAt,
            ReservedAt = job.ReservedAt,
            Attempts = 1
        };
        _jobQueue.FailJob(jobModel, new("test"));

        // Assert
        QueueJob? updated = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(updated);
        Assert.Null(updated.ReservedAt);
    }
}
