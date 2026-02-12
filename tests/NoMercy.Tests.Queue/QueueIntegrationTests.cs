using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Queue;
using NoMercy.Queue.Core.Models;
using IQueueContext = NoMercy.Queue.Core.Interfaces.IQueueContext;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

public class QueueIntegrationTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public QueueIntegrationTests()
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
    public async Task FullWorkflow_EnqueueDeserializeExecute_CompletesSuccessfully()
    {
        // Arrange - Create a job
        TestJob originalJob = new()
        {
            Message = "Integration test job",
            HasExecuted = false
        };

        // Act 1 - Serialize and enqueue the job
        string serializedJob = SerializationHelper.Serialize(originalJob);
        QueueJobModel queueJob = new()
        {
            Queue = "integration-test",
            Payload = serializedJob,
            AvailableAt = DateTime.UtcNow,
            Priority = 1
        };

        _jobQueue.Enqueue(queueJob);

        // Verify job is stored
        QueueJob? storedJob = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(storedJob);
        Assert.Equal("integration-test", storedJob.Queue);

        // Act 2 - Reserve and deserialize the job
        QueueJobModel? reservedJob = _jobQueue.ReserveJob("integration-test", null);
        Assert.NotNull(reservedJob);

        object deserializedJobObject = SerializationHelper.Deserialize<object>(reservedJob.Payload);
        Assert.NotNull(deserializedJobObject);
        Assert.IsType<TestJob>(deserializedJobObject);

        TestJob deserializedJob = (TestJob)deserializedJobObject;
        Assert.Equal(originalJob.Message, deserializedJob.Message);
        Assert.False(deserializedJob.HasExecuted);

        // Act 3 - Execute the job
        await deserializedJob.Handle();

        // Act 4 - Delete the completed job
        _jobQueue.DeleteJob(reservedJob);

        // Assert - Verify complete workflow
        Assert.True(deserializedJob.HasExecuted);
        int remainingJobs = _context.QueueJobs.Count();
        Assert.Equal(0, remainingJobs);
    }

    [Fact]
    public async Task FullWorkflow_MultipleJobTypes_ProcessedCorrectly()
    {
        // Arrange - Create different types of jobs
        TestJob testJob1 = new()
        {
            Message = "First job",
            HasExecuted = false
        };

        AnotherTestJob testJob2 = new()
        {
            Value = 5,
            HasExecuted = false
        };

        // Act - Enqueue both jobs
        QueueJobModel queueJob1 = new()
        {
            Queue = "multi-test",
            Payload = SerializationHelper.Serialize(testJob1),
            AvailableAt = DateTime.UtcNow,
            Priority = 1
        };

        QueueJobModel queueJob2 = new()
        {
            Queue = "multi-test",
            Payload = SerializationHelper.Serialize(testJob2),
            AvailableAt = DateTime.UtcNow,
            Priority = 2 // Higher priority
        };

        _jobQueue.Enqueue(queueJob1);
        _jobQueue.Enqueue(queueJob2);

        // Process first job (should be higher priority)
        QueueJobModel? firstReservedJob = _jobQueue.ReserveJob("multi-test", null);
        Assert.NotNull(firstReservedJob);

        object firstDeserializedJob = SerializationHelper.Deserialize<object>(firstReservedJob.Payload);
        Assert.IsType<AnotherTestJob>(firstDeserializedJob); // Should be the higher priority job

        await ((IShouldQueue)firstDeserializedJob).Handle();
        _jobQueue.DeleteJob(firstReservedJob);

        // Process second job
        QueueJobModel? secondReservedJob = _jobQueue.ReserveJob("multi-test", null);
        Assert.NotNull(secondReservedJob);

        object secondDeserializedJob = SerializationHelper.Deserialize<object>(secondReservedJob.Payload);
        Assert.IsType<TestJob>(secondDeserializedJob);

        await ((IShouldQueue)secondDeserializedJob).Handle();
        _jobQueue.DeleteJob(secondReservedJob);

        // Assert
        AnotherTestJob anotherJob = (AnotherTestJob)firstDeserializedJob;
        TestJob testJob = (TestJob)secondDeserializedJob;

        Assert.True(anotherJob.HasExecuted);
        Assert.Equal(10, anotherJob.Value); // Should be doubled
        Assert.True(testJob.HasExecuted);
        Assert.Equal("First job", testJob.Message);

        int remainingJobs = _context.QueueJobs.Count();
        Assert.Equal(0, remainingJobs);
    }

    [Fact]
    public async Task FailureWorkflow_WithRequeueWorkaround_CompletesSuccessfully()
    {
        // This test demonstrates the complete failure and recovery workflow
        // Note: Uses workaround for the RequeueFailedJob type mismatch bug

        // Arrange - Create a failing job
        TestJob failingJob = new()
        {
            Message = "This job will fail",
            HasExecuted = false,
            ShouldFail = true
        };

        QueueJobModel queueJob = new()
        {
            Queue = "failure-test",
            Payload = SerializationHelper.Serialize(failingJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 2 // Set to max attempts - 1
        };

        _jobQueue.Enqueue(queueJob);

        // Act 1 - Try to process the job (it will fail)
        QueueJobModel? reservedJob = _jobQueue.ReserveJob("failure-test", null);
        Assert.NotNull(reservedJob);

        object deserializedJob = SerializationHelper.Deserialize<object>(reservedJob.Payload);
        IShouldQueue executableJob = (IShouldQueue)deserializedJob;

        Exception? caughtException = null;
        try
        {
            await executableJob.Handle();
        }
        catch (Exception ex)
        {
            caughtException = ex;
            _jobQueue.FailJob(reservedJob, ex);
        }

        // Assert - Job should be moved to failed jobs
        Assert.NotNull(caughtException);
        Assert.IsType<InvalidOperationException>(caughtException);

        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(0, queueJobCount); // Should be removed from queue

        FailedJob? failedJob = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(failedJob);
        Assert.Equal("failure-test", failedJob.Queue);

        // Act 2 - Manual requeue (workaround for the type mismatch bug)
        _context.FailedJobs.Remove(failedJob);
        _context.QueueJobs.Add(new()
        {
            Queue = failedJob.Queue,
            Payload = failedJob.Payload,
            AvailableAt = DateTime.UtcNow,
            Attempts = 0
        });
        _context.SaveChanges();

        // Act 3 - Fix the job and process it successfully
        QueueJobModel? retriedJob = _jobQueue.ReserveJob("failure-test", null);
        Assert.NotNull(retriedJob);

        TestJob retriedDeserializedJob = SerializationHelper.Deserialize<TestJob>(retriedJob.Payload);
        retriedDeserializedJob.ShouldFail = false; // Fix the job

        await retriedDeserializedJob.Handle();
        _jobQueue.DeleteJob(retriedJob);

        // Assert - Job should complete successfully
        Assert.True(retriedDeserializedJob.HasExecuted);

        int finalQueueJobCount = _context.QueueJobs.Count();
        Assert.Equal(0, finalQueueJobCount);

        int finalFailedJobCount = _context.FailedJobs.Count();
        Assert.Equal(0, finalFailedJobCount);
    }

    [Fact]
    public void DuplicateJobPrevention_SamePayload_OnlyOneJobEnqueued()
    {
        // Arrange - Create identical jobs
        TestJob job = new()
        {
            Message = "Duplicate test",
            HasExecuted = false
        };

        string serializedPayload = SerializationHelper.Serialize(job);

        QueueJobModel queueJob1 = new()
        {
            Queue = "duplicate-test",
            Payload = serializedPayload,
            AvailableAt = DateTime.UtcNow
        };

        QueueJobModel queueJob2 = new()
        {
            Queue = "duplicate-test",
            Payload = serializedPayload, // Same payload
            AvailableAt = DateTime.UtcNow
        };

        // Act
        _jobQueue.Enqueue(queueJob1);
        _jobQueue.Enqueue(queueJob2); // Should be prevented

        // Assert
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(1, jobCount); // Only one job should exist
    }

    [Fact]
    public async Task PriorityOrdering_MultipleJobs_ProcessedInCorrectOrder()
    {
        // Arrange - Create jobs with different priorities
        TestJob lowPriorityJob = new() { Message = "Low priority" };
        TestJob mediumPriorityJob = new() { Message = "Medium priority" };
        TestJob highPriorityJob = new() { Message = "High priority" };

        QueueJobModel[] jobs =
        [
            new()
            {
                Queue = "priority-test",
                Payload = SerializationHelper.Serialize(lowPriorityJob),
                AvailableAt = DateTime.UtcNow,
                Priority = 1
            },
            new()
            {
                Queue = "priority-test",
                Payload = SerializationHelper.Serialize(highPriorityJob),
                AvailableAt = DateTime.UtcNow,
                Priority = 10
            },
            new()
            {
                Queue = "priority-test",
                Payload = SerializationHelper.Serialize(mediumPriorityJob),
                AvailableAt = DateTime.UtcNow,
                Priority = 5
            }
        ];

        // Act - Enqueue in random order
        foreach (QueueJobModel job in jobs)
        {
            _jobQueue.Enqueue(job);
        }

        // Process jobs and verify order
        List<string> processedMessages = [];

        for (int i = 0; i < 3; i++)
        {
            QueueJobModel? reservedJob = _jobQueue.ReserveJob("priority-test", null);
            Assert.NotNull(reservedJob);

            TestJob deserializedJob = SerializationHelper.Deserialize<TestJob>(reservedJob.Payload);
            processedMessages.Add(deserializedJob.Message);

            await deserializedJob.Handle();
            _jobQueue.DeleteJob(reservedJob);
        }

        // Assert - Jobs should be processed in priority order (highest first)
        Assert.Equal("High priority", processedMessages[0]);
        Assert.Equal("Medium priority", processedMessages[1]);
        Assert.Equal("Low priority", processedMessages[2]);
    }
}
