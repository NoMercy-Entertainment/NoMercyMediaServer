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
using IQueueContext = NoMercyQueue.Core.Interfaces.IQueueContext;

namespace NoMercy.Tests.Queue;

public class QueueIntegrationTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public QueueIntegrationTests()
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
    public async Task FullWorkflow_EnqueueDeserializeExecute_CompletesSuccessfully()
    {
        // Arrange - Create a job
        TestJob originalJob = new() { Message = "Integration test job", HasExecuted = false };

        // Act 1 - Serialize and enqueue the job
        string serializedJob = SerializationHelper.Serialize(obj: originalJob);
        QueueJobModel queueJob = new()
        {
            Queue = "integration-test",
            Payload = serializedJob,
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
        };

        _jobQueue.Enqueue(queueJob: queueJob);

        // Verify job is stored
        QueueJob? storedJob = _context.QueueJobs.FirstOrDefault();
        Assert.NotNull(@object: storedJob);
        Assert.Equal(expected: "integration-test", actual: storedJob.Queue);

        // Act 2 - Reserve and deserialize the job
        QueueJobModel? reservedJob = _jobQueue.ReserveJob(name: "integration-test", currentJobId: null);
        Assert.NotNull(@object: reservedJob);

        object deserializedJobObject = SerializationHelper.Deserialize<object>(data: reservedJob.Payload);
        Assert.NotNull(@object: deserializedJobObject);
        Assert.IsType<TestJob>(@object: deserializedJobObject);

        TestJob deserializedJob = (TestJob)deserializedJobObject;
        Assert.Equal(expected: originalJob.Message, actual: deserializedJob.Message);
        Assert.False(condition: deserializedJob.HasExecuted);

        // Act 3 - Execute the job
        await deserializedJob.Handle();

        // Act 4 - Delete the completed job
        _jobQueue.DeleteJob(queueJob: reservedJob);

        // Assert - Verify complete workflow
        Assert.True(condition: deserializedJob.HasExecuted);
        int remainingJobs = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: remainingJobs);
    }

    [Fact]
    public async Task FullWorkflow_MultipleJobTypes_ProcessedCorrectly()
    {
        // Arrange - Create different types of jobs
        TestJob testJob1 = new() { Message = "First job", HasExecuted = false };

        AnotherTestJob testJob2 = new() { Value = 5, HasExecuted = false };

        // Act - Enqueue both jobs
        QueueJobModel queueJob1 = new()
        {
            Queue = "multi-test",
            Payload = SerializationHelper.Serialize(obj: testJob1),
            AvailableAt = DateTime.UtcNow,
            Priority = 1,
        };

        QueueJobModel queueJob2 = new()
        {
            Queue = "multi-test",
            Payload = SerializationHelper.Serialize(obj: testJob2),
            AvailableAt = DateTime.UtcNow,
            Priority = 2, // Higher priority
        };

        _jobQueue.Enqueue(queueJob: queueJob1);
        _jobQueue.Enqueue(queueJob: queueJob2);

        // Process first job (should be higher priority)
        QueueJobModel? firstReservedJob = _jobQueue.ReserveJob(name: "multi-test", currentJobId: null);
        Assert.NotNull(@object: firstReservedJob);

        object firstDeserializedJob = SerializationHelper.Deserialize<object>(
            data: firstReservedJob.Payload
        );
        Assert.IsType<AnotherTestJob>(@object: firstDeserializedJob); // Should be the higher priority job

        await ((IShouldQueue)firstDeserializedJob).Handle();
        _jobQueue.DeleteJob(queueJob: firstReservedJob);

        // Process second job
        QueueJobModel? secondReservedJob = _jobQueue.ReserveJob(name: "multi-test", currentJobId: null);
        Assert.NotNull(@object: secondReservedJob);

        object secondDeserializedJob = SerializationHelper.Deserialize<object>(
            data: secondReservedJob.Payload
        );
        Assert.IsType<TestJob>(@object: secondDeserializedJob);

        await ((IShouldQueue)secondDeserializedJob).Handle();
        _jobQueue.DeleteJob(queueJob: secondReservedJob);

        // Assert
        AnotherTestJob anotherJob = (AnotherTestJob)firstDeserializedJob;
        TestJob testJob = (TestJob)secondDeserializedJob;

        Assert.True(condition: anotherJob.HasExecuted);
        Assert.Equal(expected: 10, actual: anotherJob.Value); // Should be doubled
        Assert.True(condition: testJob.HasExecuted);
        Assert.Equal(expected: "First job", actual: testJob.Message);

        int remainingJobs = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: remainingJobs);
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
            ShouldFail = true,
        };

        QueueJobModel queueJob = new()
        {
            Queue = "failure-test",
            Payload = SerializationHelper.Serialize(obj: failingJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 2, // Set to max attempts - 1
        };

        _jobQueue.Enqueue(queueJob: queueJob);

        // Act 1 - Try to process the job (it will fail)
        QueueJobModel? reservedJob = _jobQueue.ReserveJob(name: "failure-test", currentJobId: null);
        Assert.NotNull(@object: reservedJob);

        object deserializedJob = SerializationHelper.Deserialize<object>(data: reservedJob.Payload);
        IShouldQueue executableJob = (IShouldQueue)deserializedJob;

        Exception? caughtException = null;
        try
        {
            await executableJob.Handle();
        }
        catch (Exception ex)
        {
            caughtException = ex;
            _jobQueue.FailJob(queueJob: reservedJob, exception: ex);
        }

        // Assert - Job should be moved to failed jobs
        Assert.NotNull(@object: caughtException);
        Assert.IsType<InvalidOperationException>(@object: caughtException);

        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: queueJobCount); // Should be removed from queue

        FailedJob? failedJob = _context.FailedJobs.FirstOrDefault();
        Assert.NotNull(@object: failedJob);
        Assert.Equal(expected: "failure-test", actual: failedJob.Queue);

        // Act 2 - Manual requeue (workaround for the type mismatch bug)
        _context.FailedJobs.Remove(entity: failedJob);
        _context.QueueJobs.Add(
            entity: new()
            {
                Queue = failedJob.Queue,
                Payload = failedJob.Payload,
                AvailableAt = DateTime.UtcNow,
                Attempts = 0,
            }
        );
        await _context.SaveChangesAsync();

        // Act 3 - Fix the job and process it successfully
        QueueJobModel? retriedJob = _jobQueue.ReserveJob(name: "failure-test", currentJobId: null);
        Assert.NotNull(@object: retriedJob);

        TestJob retriedDeserializedJob = SerializationHelper.Deserialize<TestJob>(
            data: retriedJob.Payload
        );
        retriedDeserializedJob.ShouldFail = false; // Fix the job

        await retriedDeserializedJob.Handle();
        _jobQueue.DeleteJob(queueJob: retriedJob);

        // Assert - Job should complete successfully
        Assert.True(condition: retriedDeserializedJob.HasExecuted);

        int finalQueueJobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: finalQueueJobCount);

        int finalFailedJobCount = _context.FailedJobs.Count();
        Assert.Equal(expected: 0, actual: finalFailedJobCount);
    }

    [Fact]
    public void DuplicateJobPrevention_SamePayload_OnlyOneJobEnqueued()
    {
        // Arrange - Create identical jobs
        TestJob job = new() { Message = "Duplicate test", HasExecuted = false };

        string serializedPayload = SerializationHelper.Serialize(obj: job);

        QueueJobModel queueJob1 = new()
        {
            Queue = "duplicate-test",
            Payload = serializedPayload,
            AvailableAt = DateTime.UtcNow,
        };

        QueueJobModel queueJob2 = new()
        {
            Queue = "duplicate-test",
            Payload = serializedPayload, // Same payload
            AvailableAt = DateTime.UtcNow,
        };

        // Act
        _jobQueue.Enqueue(queueJob: queueJob1);
        _jobQueue.Enqueue(queueJob: queueJob2); // Should be prevented

        // Assert
        int jobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 1, actual: jobCount); // Only one job should exist
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
                Payload = SerializationHelper.Serialize(obj: lowPriorityJob),
                AvailableAt = DateTime.UtcNow,
                Priority = 1,
            },
            new()
            {
                Queue = "priority-test",
                Payload = SerializationHelper.Serialize(obj: highPriorityJob),
                AvailableAt = DateTime.UtcNow,
                Priority = 10,
            },
            new()
            {
                Queue = "priority-test",
                Payload = SerializationHelper.Serialize(obj: mediumPriorityJob),
                AvailableAt = DateTime.UtcNow,
                Priority = 5,
            },
        ];

        // Act - Enqueue in random order
        foreach (QueueJobModel job in jobs)
        {
            _jobQueue.Enqueue(queueJob: job);
        }

        // Process jobs and verify order
        List<string> processedMessages = [];

        for (int i = 0; i < 3; i++)
        {
            QueueJobModel? reservedJob = _jobQueue.ReserveJob(name: "priority-test", currentJobId: null);
            Assert.NotNull(@object: reservedJob);

            TestJob deserializedJob = SerializationHelper.Deserialize<TestJob>(data: reservedJob.Payload);
            processedMessages.Add(item: deserializedJob.Message);

            await deserializedJob.Handle();
            _jobQueue.DeleteJob(queueJob: reservedJob);
        }

        // Assert - Jobs should be processed in priority order (highest first)
        Assert.Equal(expected: "High priority", actual: processedMessages[index: 0]);
        Assert.Equal(expected: "Medium priority", actual: processedMessages[index: 1]);
        Assert.Equal(expected: "Low priority", actual: processedMessages[index: 2]);
    }
}
