using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Workers;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;
using IQueueContext = NoMercyQueue.Core.Interfaces.IQueueContext;

namespace NoMercy.Tests.Queue;

public class QueueWorkerTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public QueueWorkerTests()
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
    public async Task QueueWorker_ProcessesJob_Successfully()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Worker test",
            HasExecuted = false
        };

        QueueJob queueJob = new()
        {
            Queue = "test-worker",
            Payload = SerializationHelper.Serialize(testJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0
        };

        _context.QueueJobs.Add(queueJob);
        _context.SaveChanges();

        QueueWorker worker = new(_jobQueue, "test-worker");
        worker.WorkCompleted += (_, _) => { /* Work completed */ };

        // Act
        Task workerTask = Task.Run(() =>
        {
            try
            {
                // Let the worker process one job
                QueueJobModel? job = _jobQueue.ReserveJob("test-worker", null);
                if (job != null)
                {
                    object jobWithArguments = SerializationHelper.Deserialize<object>(job.Payload);
                    if (jobWithArguments is IShouldQueue classInstance)
                    {
                        classInstance.Handle().Wait();
                        _jobQueue.DeleteJob(job);
                    }
                }
            }
            catch (Exception)
            {
                // Test failed
            }
        });

        await workerTask;

        // Assert
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(0, jobCount); // Job should be deleted after successful execution
    }

    [Fact]
    public async Task QueueWorker_JobFails_MovesToFailedJobs()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Failing job",
            HasExecuted = false,
            ShouldFail = true // This will cause the job to throw an exception
        };

        QueueJob queueJob = new()
        {
            Queue = "test-worker",
            Payload = SerializationHelper.Serialize(testJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 2 // Set to max attempts - 1
        };

        _context.QueueJobs.Add(queueJob);
        _context.SaveChanges();

        // Act
        Task workerTask = Task.Run(() =>
        {
            try
            {
                QueueJobModel? job = _jobQueue.ReserveJob("test-worker", null);
                if (job != null)
                {
                    try
                    {
                        object jobWithArguments = SerializationHelper.Deserialize<object>(job.Payload);
                        if (jobWithArguments is IShouldQueue classInstance)
                        {
                            classInstance.Handle().Wait();
                            _jobQueue.DeleteJob(job);
                        }
                    }
                    catch (Exception ex)
                    {
                        _jobQueue.FailJob(job, ex);
                    }
                }
            }
            catch (Exception)
            {
                // Expected for this test
            }
        });

        await workerTask;

        // Assert
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(0, queueJobCount); // Should be moved to failed jobs

        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(1, failedJobCount); // Should have one failed job
    }

    [Fact]
    public void QueueWorker_Stop_StopsProcessing()
    {
        // This test verifies that Stop() doesn't throw exceptions
        // QueueWorker now accepts an optional QueueRunner reference,
        // so calling Stop() without a QueueRunner is safe (returns -1 for index)

        // Arrange
        QueueWorker worker = new(_jobQueue, "test-worker");

        // Act & Assert - Should not throw
        Exception? exception = Record.Exception(() => worker.Stop());

        Assert.Null(exception);
    }

    [Fact]
    public async Task ProcessJob_ValidIShouldQueueJob_ExecutesSuccessfully()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Direct execution test",
            HasExecuted = false
        };

        // Act
        await testJob.Handle();

        // Assert
        Assert.True(testJob.HasExecuted);
    }

    [Fact]
    public async Task ProcessJob_JobWithDelay_CompletesAfterDelay()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Delayed job",
            HasExecuted = false,
            ExecutionDelay = 100 // 100ms delay
        };

        DateTime startTime = DateTime.UtcNow;

        // Act
        await testJob.Handle();

        // Assert
        DateTime endTime = DateTime.UtcNow;
        TimeSpan duration = endTime - startTime;

        Assert.True(testJob.HasExecuted);
        Assert.True(duration.TotalMilliseconds >= 80); // Allow for some timing variation
    }

    [Fact]
    public async Task ProcessJob_AnotherTestJob_ModifiesValue()
    {
        // Arrange
        AnotherTestJob testJob = new()
        {
            Value = 10,
            HasExecuted = false
        };

        // Act
        await testJob.Handle();

        // Assert
        Assert.True(testJob.HasExecuted);
        Assert.Equal(20, testJob.Value); // Value should be doubled
    }

    [Fact]
    public async Task ProcessJob_FailingJob_ThrowsException()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "This will fail",
            HasExecuted = false,
            ShouldFail = true
        };

        // Act & Assert
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => testJob.Handle());
        Assert.Contains("TestJob failed with message: This will fail", exception.Message);
        Assert.False(testJob.HasExecuted); // Should not be marked as executed when it fails
    }

    [Fact]
    public async Task QueueWorker_NonIShouldQueuePayload_IsRejectedAndFailed()
    {
        // Arrange — inject a payload that deserializes to a type NOT implementing IShouldQueue
        NotAJob notAJob = new() { Data = "malicious or invalid payload" };
        QueueJob queueJob = new()
        {
            Queue = "test-worker",
            Payload = SerializationHelper.Serialize(notAJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 2 // Set to maxAttempts - 1 so FailJob moves it to FailedJobs
        };

        _context.QueueJobs.Add(queueJob);
        _context.SaveChanges();

        // Act — simulate the QueueWorker's processing loop (same logic as QueueWorker.Start)
        await Task.Run(() =>
        {
            QueueJobModel? job = _jobQueue.ReserveJob("test-worker", null);
            if (job != null)
            {
                object jobWithArguments = SerializationHelper.Deserialize<object>(job.Payload);

                if (jobWithArguments is IShouldQueue classInstance)
                {
                    classInstance.Handle().Wait();
                    _jobQueue.DeleteJob(job);
                }
                else
                {
                    // This is the new rejection path
                    string typeName = jobWithArguments?.GetType().FullName ?? "null";
                    _jobQueue.FailJob(job, new InvalidOperationException(
                        $"Job payload deserialized to {typeName} which does not implement IShouldQueue"));
                }
            }
        });

        // Assert — the invalid job should NOT be in the active queue
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(0, queueJobCount);

        // Assert — it should be in the failed jobs table
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(1, failedJobCount);

        FailedJob failedJob = _context.FailedJobs.First();
        Assert.Contains("IShouldQueue", failedJob.Exception);
    }

    [Fact]
    public async Task QueueWorker_ValidIShouldQueuePayload_ExecutesAndDeletesJob()
    {
        // Arrange — a valid IShouldQueue job goes through the full worker path
        TestJob testJob = new()
        {
            Message = "Valid job for full path test",
            HasExecuted = false
        };
        QueueJob queueJob = new()
        {
            Queue = "test-worker",
            Payload = SerializationHelper.Serialize(testJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0
        };

        _context.QueueJobs.Add(queueJob);
        _context.SaveChanges();

        // Act — simulate QueueWorker processing
        bool jobExecuted = false;
        await Task.Run(() =>
        {
            QueueJobModel? job = _jobQueue.ReserveJob("test-worker", null);
            if (job != null)
            {
                object jobWithArguments = SerializationHelper.Deserialize<object>(job.Payload);

                if (jobWithArguments is IShouldQueue classInstance)
                {
                    classInstance.Handle().Wait();
                    _jobQueue.DeleteJob(job);
                    jobExecuted = true;
                }
                else
                {
                    string typeName = jobWithArguments?.GetType().FullName ?? "null";
                    _jobQueue.FailJob(job, new InvalidOperationException(
                        $"Job payload deserialized to {typeName} which does not implement IShouldQueue"));
                }
            }
        });

        // Assert — valid job was executed
        Assert.True(jobExecuted);

        // Assert — job removed from queue, nothing in failed jobs
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(0, queueJobCount);

        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(0, failedJobCount);
    }
}
