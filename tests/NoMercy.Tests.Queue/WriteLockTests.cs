using System.Reflection;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Queue;
using NoMercy.Queue.Core.Interfaces;
using NoMercy.Queue.Core.Models;
using NoMercy.Tests.Queue.TestHelpers;
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
        _jobQueue = new(_adapter);
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
        FieldInfo? writeLockField = typeof(JobQueue)
            .GetField("_writeLock", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(writeLockField);

        object? writeLockValue = writeLockField.GetValue(null);
        Assert.NotNull(writeLockValue);

        // The lock object must be a dedicated object (not a DbContext or IQueueContext)
        Assert.IsNotType<QueueContext>(writeLockValue);
    }

    [Fact]
    public void WriteLock_IsStaticAndSharedAcrossInstances()
    {
        // Two JobQueue instances should share the same static lock object
        using QueueContext context2 = TestQueueContextFactory.CreateInMemoryContext();
        IQueueContext adapter2 = new EfQueueContextAdapter(context2);
        JobQueue jobQueue2 = new(adapter2);

        FieldInfo? writeLockField = typeof(JobQueue)
            .GetField("_writeLock", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(writeLockField);

        // Static field — same value regardless of instance
        object? lockFromInstance1 = writeLockField.GetValue(null);
        object? lockFromInstance2 = writeLockField.GetValue(null);
        Assert.Same(lockFromInstance1, lockFromInstance2);

        adapter2.Dispose();
    }

    [Fact]
    public void ConcurrentEnqueue_AllJobsSucceed()
    {
        // Arrange — use a shared context for all threads (simulates the real
        // static JobQueue pattern where one context is shared)
        int jobCount = 20;
        CountdownEvent countdown = new(jobCount);
        List<Exception> errors = [];

        // Act — enqueue jobs from multiple threads concurrently
        for (int i = 0; i < jobCount; i++)
        {
            int index = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    _jobQueue.Enqueue(new QueueJobModel
                    {
                        Queue = "concurrent-test",
                        Payload = $"payload-{index}",
                        AvailableAt = DateTime.UtcNow,
                        Priority = 1
                    });
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add(ex);
                    }
                }
                finally
                {
                    countdown.Signal();
                }
            });
        }

        bool completed = countdown.Wait(TimeSpan.FromSeconds(10));

        // Assert
        Assert.True(completed, "Not all enqueue operations completed within timeout");
        Assert.Empty(errors);

        int totalJobs = _context.QueueJobs.Count();
        Assert.Equal(jobCount, totalJobs);
    }

    [Fact]
    public void ConcurrentEnqueueAndDequeue_MaintainsDataIntegrity()
    {
        // Seed some jobs first
        for (int i = 0; i < 10; i++)
        {
            _context.QueueJobs.Add(new QueueJob
            {
                Queue = "integrity-test",
                Payload = $"seed-{i}",
                AvailableAt = DateTime.UtcNow,
                Priority = 1
            });
        }
        _context.SaveChanges();

        int enqueueCount = 10;
        int dequeueCount = 5;
        CountdownEvent countdown = new(enqueueCount + dequeueCount);
        List<Exception> errors = [];

        // Enqueue new jobs concurrently
        for (int i = 0; i < enqueueCount; i++)
        {
            int index = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    _jobQueue.Enqueue(new QueueJobModel
                    {
                        Queue = "integrity-test",
                        Payload = $"new-{index}",
                        AvailableAt = DateTime.UtcNow,
                        Priority = 1
                    });
                }
                catch (Exception ex)
                {
                    lock (errors) { errors.Add(ex); }
                }
                finally { countdown.Signal(); }
            });
        }

        // Dequeue jobs concurrently
        List<QueueJobModel?> dequeued = [];
        for (int i = 0; i < dequeueCount; i++)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    QueueJobModel? job = _jobQueue.Dequeue();
                    lock (dequeued) { dequeued.Add(job); }
                }
                catch (Exception ex)
                {
                    lock (errors) { errors.Add(ex); }
                }
                finally { countdown.Signal(); }
            });
        }

        bool completed = countdown.Wait(TimeSpan.FromSeconds(10));

        // Assert
        Assert.True(completed, "Not all operations completed within timeout");
        Assert.Empty(errors);

        // Total should be: 10 seeded + 10 new - dequeued (non-null)
        int dequeuedCount = dequeued.Count(j => j != null);
        int remaining = _context.QueueJobs.Count();
        Assert.Equal(20 - dequeuedCount, remaining);
    }
}
