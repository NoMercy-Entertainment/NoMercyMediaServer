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
using NoMercyQueue.Workers;
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
        _jobQueue = new(context: _adapter);
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
        TestJob testJob = new() { Message = "Worker test", HasExecuted = false };

        QueueJob queueJob = new()
        {
            Queue = "test-worker",
            Payload = SerializationHelper.Serialize(obj: testJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };

        _context.QueueJobs.Add(entity: queueJob);
        await _context.SaveChangesAsync();

        QueueWorker worker = new(queue: _jobQueue, name: "test-worker");
        worker.WorkCompleted += (
            _,
            _
        ) => { /* Work completed */
        };

        // Act
        Task workerTask = Task.Run(action: () =>
        {
            try
            {
                // Let the worker process one job
                QueueJobModel? job = _jobQueue.ReserveJob(name: "test-worker", currentJobId: null);
                if (job != null)
                {
                    object jobWithArguments = SerializationHelper.Deserialize<object>(data: job.Payload);
                    if (jobWithArguments is IShouldQueue classInstance)
                    {
                        classInstance.Handle().Wait();
                        _jobQueue.DeleteJob(queueJob: job);
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
        Assert.Equal(expected: 0, actual: jobCount); // Job should be deleted after successful execution
    }

    [Fact]
    public async Task QueueWorker_JobFails_MovesToFailedJobs()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Failing job",
            HasExecuted = false,
            ShouldFail = true, // This will cause the job to throw an exception
        };

        QueueJob queueJob = new()
        {
            Queue = "test-worker",
            Payload = SerializationHelper.Serialize(obj: testJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 2, // Set to max attempts - 1
        };

        _context.QueueJobs.Add(entity: queueJob);
        await _context.SaveChangesAsync();

        // Act
        Task workerTask = Task.Run(action: () =>
        {
            try
            {
                QueueJobModel? job = _jobQueue.ReserveJob(name: "test-worker", currentJobId: null);
                if (job != null)
                {
                    try
                    {
                        object jobWithArguments = SerializationHelper.Deserialize<object>(
                            data: job.Payload
                        );
                        if (jobWithArguments is IShouldQueue classInstance)
                        {
                            classInstance.Handle().Wait();
                            _jobQueue.DeleteJob(queueJob: job);
                        }
                    }
                    catch (Exception ex)
                    {
                        _jobQueue.FailJob(queueJob: job, exception: ex);
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
        Assert.Equal(expected: 0, actual: queueJobCount); // Should be moved to failed jobs

        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(expected: 1, actual: failedJobCount); // Should have one failed job
    }

    [Fact]
    public void QueueWorker_Stop_StopsProcessing()
    {
        // This test verifies that Stop() doesn't throw exceptions
        // QueueWorker now accepts an optional QueueRunner reference,
        // so calling Stop() without a QueueRunner is safe (returns -1 for index)

        // Arrange
        QueueWorker worker = new(queue: _jobQueue, name: "test-worker");

        // Act & Assert - Should not throw
        Exception? exception = Record.Exception(testCode: () => worker.Stop());

        Assert.Null(@object: exception);
    }

    [Fact]
    public async Task ProcessJob_ValidIShouldQueueJob_ExecutesSuccessfully()
    {
        // Arrange
        TestJob testJob = new() { Message = "Direct execution test", HasExecuted = false };

        // Act
        await testJob.Handle();

        // Assert
        Assert.True(condition: testJob.HasExecuted);
    }

    [Fact]
    public async Task ProcessJob_JobWithDelay_CompletesAfterDelay()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "Delayed job",
            HasExecuted = false,
            ExecutionDelay = 100, // 100ms delay
        };

        DateTime startTime = DateTime.UtcNow;

        // Act
        await testJob.Handle();

        // Assert
        DateTime endTime = DateTime.UtcNow;
        TimeSpan duration = endTime - startTime;

        Assert.True(condition: testJob.HasExecuted);
        Assert.True(condition: duration.TotalMilliseconds >= 80); // Allow for some timing variation
    }

    [Fact]
    public async Task ProcessJob_AnotherTestJob_ModifiesValue()
    {
        // Arrange
        AnotherTestJob testJob = new() { Value = 10, HasExecuted = false };

        // Act
        await testJob.Handle();

        // Assert
        Assert.True(condition: testJob.HasExecuted);
        Assert.Equal(expected: 20, actual: testJob.Value); // Value should be doubled
    }

    [Fact]
    public async Task ProcessJob_FailingJob_ThrowsException()
    {
        // Arrange
        TestJob testJob = new()
        {
            Message = "This will fail",
            HasExecuted = false,
            ShouldFail = true,
        };

        // Act & Assert
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            testCode: () =>
                testJob.Handle()
        );
        Assert.Contains(expectedSubstring: "TestJob failed with message: This will fail", actualString: exception.Message);
        Assert.False(condition: testJob.HasExecuted); // Should not be marked as executed when it fails
    }

    [Fact]
    public async Task QueueWorker_NonIShouldQueuePayload_IsRejectedAndFailed()
    {
        // Arrange — inject a payload that deserializes to a type NOT implementing IShouldQueue
        NotAJob notAJob = new() { Data = "malicious or invalid payload" };
        QueueJob queueJob = new()
        {
            Queue = "test-worker",
            Payload = SerializationHelper.Serialize(obj: notAJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 2, // Set to maxAttempts - 1 so FailJob moves it to FailedJobs
        };

        _context.QueueJobs.Add(entity: queueJob);
        await _context.SaveChangesAsync();

        // Act — simulate the QueueWorker's processing loop (same logic as QueueWorker.Start)
        await Task.Run(action: () =>
        {
            QueueJobModel? job = _jobQueue.ReserveJob(name: "test-worker", currentJobId: null);
            if (job != null)
            {
                object jobWithArguments = SerializationHelper.Deserialize<object>(data: job.Payload);

                if (jobWithArguments is IShouldQueue classInstance)
                {
                    classInstance.Handle().Wait();
                    _jobQueue.DeleteJob(queueJob: job);
                }
                else
                {
                    // This is the new rejection path
                    string typeName = jobWithArguments.GetType().FullName ?? "null";
                    _jobQueue.FailJob(
                        queueJob: job,
                        exception: new InvalidOperationException(
                            message: $"Job payload deserialized to {typeName} which does not implement IShouldQueue"
                        )
                    );
                }
            }
        });

        // Assert — the invalid job should NOT be in the active queue
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: queueJobCount);

        // Assert — it should be in the failed jobs table
        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(expected: 1, actual: failedJobCount);

        FailedJob failedJob = _context.FailedJobs.First();
        Assert.Contains(expectedSubstring: "IShouldQueue", actualString: failedJob.Exception);
    }

    [Fact]
    public async Task QueueWorker_ValidIShouldQueuePayload_ExecutesAndDeletesJob()
    {
        // Arrange — a valid IShouldQueue job goes through the full worker path
        TestJob testJob = new() { Message = "Valid job for full path test", HasExecuted = false };
        QueueJob queueJob = new()
        {
            Queue = "test-worker",
            Payload = SerializationHelper.Serialize(obj: testJob),
            AvailableAt = DateTime.UtcNow,
            Attempts = 0,
        };

        _context.QueueJobs.Add(entity: queueJob);
        await _context.SaveChangesAsync();

        // Act — simulate QueueWorker processing
        bool jobExecuted = false;
        await Task.Run(action: () =>
        {
            QueueJobModel? job = _jobQueue.ReserveJob(name: "test-worker", currentJobId: null);
            if (job != null)
            {
                object jobWithArguments = SerializationHelper.Deserialize<object>(data: job.Payload);

                if (jobWithArguments is IShouldQueue classInstance)
                {
                    classInstance.Handle().Wait();
                    _jobQueue.DeleteJob(queueJob: job);
                    jobExecuted = true;
                }
                else
                {
                    string typeName = jobWithArguments.GetType().FullName ?? "null";
                    _jobQueue.FailJob(
                        queueJob: job,
                        exception: new InvalidOperationException(
                            message: $"Job payload deserialized to {typeName} which does not implement IShouldQueue"
                        )
                    );
                }
            }
        });

        // Assert — valid job was executed
        Assert.True(condition: jobExecuted);

        // Assert — job removed from queue, nothing in failed jobs
        int queueJobCount = _context.QueueJobs.Count();
        Assert.Equal(expected: 0, actual: queueJobCount);

        int failedJobCount = _context.FailedJobs.Count();
        Assert.Equal(expected: 0, actual: failedJobCount);
    }
}
