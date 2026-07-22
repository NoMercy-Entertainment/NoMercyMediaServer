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

using System.Reflection;
using NoMercy.Database;
using NoMercy.Queue.MediaServer;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

public class WriteLockTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public WriteLockTests()
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
    public void WriteLock_IsNotDbContextInstance()
    {
        // Use reflection to verify the lock object is a dedicated object, not the Context
        FieldInfo? writeLockField = typeof(JobQueue).GetField(
            name: "_writeLock",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: writeLockField);

        object? writeLockValue = writeLockField.GetValue(obj: _jobQueue);
        Assert.NotNull(@object: writeLockValue);

        // The lock object must be a dedicated object (not a DbContext or IQueueContext)
        Assert.IsNotType<QueueContext>(@object: writeLockValue);
    }

    [Fact]
    public void WriteLock_IsPerInstance_NotSharedAcrossInstances()
    {
        // Each JobQueue owns its own write lock; independent queues do not serialise against each other
        using QueueContext context2 = TestQueueContextFactory.CreateInMemoryContext();
        IQueueContext adapter2 = new EfQueueContextAdapter(context: context2);
        JobQueue jobQueue2 = new(context: adapter2);

        FieldInfo? writeLockField = typeof(JobQueue).GetField(
            name: "_writeLock",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: writeLockField);

        // Static field — same value regardless of instance
        object? lockFromInstance1 = writeLockField.GetValue(obj: _jobQueue);
        object? lockFromInstance2 = writeLockField.GetValue(obj: jobQueue2);
        Assert.NotSame(expected: lockFromInstance1, actual: lockFromInstance2);

        adapter2.Dispose();
    }

    [Fact]
    public void ConcurrentEnqueue_AllJobsSucceed()
    {
        // Arrange — use a shared context for all threads (simulates the real
        // static JobQueue pattern where one context is shared)
        int jobCount = 20;
        CountdownEvent countdown = new(initialCount: jobCount);
        List<Exception> errors = [];

        // Act — enqueue jobs from multiple threads concurrently
        for (int i = 0; i < jobCount; i++)
        {
            int index = i;
            ThreadPool.QueueUserWorkItem(callBack: _ =>
            {
                try
                {
                    _jobQueue.Enqueue(
                        queueJob: new()
                        {
                            Queue = "concurrent-test",
                            Payload = $"payload-{index}",
                            AvailableAt = DateTime.UtcNow,
                            Priority = 1,
                        }
                    );
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(item: ex);
                    }
                }
                finally
                {
                    countdown.Signal();
                }
            });
        }

        bool completed = countdown.Wait(timeout: TimeSpan.FromSeconds(seconds: 10));

        // Assert
        Assert.True(condition: completed, userMessage: "Not all enqueue operations completed within timeout");
        Assert.Empty(collection: errors);

        int totalJobs = _context.QueueJobs.Count();
        Assert.Equal(expected: jobCount, actual: totalJobs);
    }

    [Fact]
    public void ConcurrentEnqueueAndDequeue_MaintainsDataIntegrity()
    {
        // Seed some jobs first
        for (int i = 0; i < 10; i++)
        {
            _context.QueueJobs.Add(
                entity: new()
                {
                    Queue = "integrity-test",
                    Payload = $"seed-{i}",
                    AvailableAt = DateTime.UtcNow,
                    Priority = 1,
                }
            );
        }
        _context.SaveChanges();

        int enqueueCount = 10;
        int dequeueCount = 5;
        CountdownEvent countdown = new(initialCount: enqueueCount + dequeueCount);
        List<Exception> errors = [];

        // Enqueue new jobs concurrently
        for (int i = 0; i < enqueueCount; i++)
        {
            int index = i;
            ThreadPool.QueueUserWorkItem(callBack: _ =>
            {
                try
                {
                    _jobQueue.Enqueue(
                        queueJob: new()
                        {
                            Queue = "integrity-test",
                            Payload = $"new-{index}",
                            AvailableAt = DateTime.UtcNow,
                            Priority = 1,
                        }
                    );
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(item: ex);
                    }
                }
                finally
                {
                    countdown.Signal();
                }
            });
        }

        // Dequeue jobs concurrently
        List<QueueJobModel?> dequeued = [];
        for (int i = 0; i < dequeueCount; i++)
        {
            ThreadPool.QueueUserWorkItem(callBack: _ =>
            {
                try
                {
                    QueueJobModel? job = _jobQueue.Dequeue();
                    lock (dequeued)
                    {
                        dequeued.Add(item: job);
                    }
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(item: ex);
                    }
                }
                finally
                {
                    countdown.Signal();
                }
            });
        }

        bool completed = countdown.Wait(timeout: TimeSpan.FromSeconds(seconds: 10));

        // Assert
        Assert.True(condition: completed, userMessage: "Not all operations completed within timeout");
        Assert.Empty(collection: errors);

        // Total should be: 10 seeded + 10 new - dequeued (non-null)
        int dequeuedCount = dequeued.Count(predicate: j => j != null);
        int remaining = _context.QueueJobs.Count();
        Assert.Equal(expected: 20 - dequeuedCount, actual: remaining);
    }
}
