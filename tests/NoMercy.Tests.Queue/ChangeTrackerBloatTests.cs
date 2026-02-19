using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

public class ChangeTrackerBloatTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly IQueueContext _adapter;
    private readonly JobQueue _jobQueue;

    public ChangeTrackerBloatTests()
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
    public void Enqueue_ManyJobs_ChangeTrackerDoesNotAccumulate()
    {
        for (int i = 0; i < 100; i++)
        {
            QueueJobModel job = new()
            {
                Queue = "test",
                Payload = $"payload-{i}",
                AvailableAt = DateTime.UtcNow
            };
            _jobQueue.Enqueue(job);

            int trackedEntities = _context.ChangeTracker.Entries().Count();
            Assert.Equal(0, trackedEntities);
        }

        Assert.Equal(100, _context.QueueJobs.Count());
    }

    [Fact]
    public void Dequeue_ManyJobs_ChangeTrackerDoesNotAccumulate()
    {
        for (int i = 0; i < 50; i++)
        {
            _jobQueue.Enqueue(new()
            {
                Queue = "test",
                Payload = $"payload-{i}",
                AvailableAt = DateTime.UtcNow
            });
        }

        for (int i = 0; i < 50; i++)
        {
            QueueJobModel? job = _jobQueue.Dequeue();
            Assert.NotNull(job);

            int trackedEntities = _context.ChangeTracker.Entries().Count();
            Assert.Equal(0, trackedEntities);
        }

        Assert.Equal(0, _context.QueueJobs.Count());
    }

    [Fact]
    public void DeleteJob_ChangeTrackerClearedAfterSave()
    {
        for (int i = 0; i < 20; i++)
        {
            _jobQueue.Enqueue(new()
            {
                Queue = "test",
                Payload = $"payload-{i}",
                AvailableAt = DateTime.UtcNow
            });
        }

        List<QueueJob> jobs = _context.QueueJobs.ToList();
        foreach (QueueJob job in jobs)
        {
            QueueJobModel model = new()
            {
                Id = job.Id,
                Priority = job.Priority,
                Queue = job.Queue,
                Payload = job.Payload,
                Attempts = job.Attempts,
                ReservedAt = job.ReservedAt,
                AvailableAt = job.AvailableAt,
                CreatedAt = job.CreatedAt
            };
            _jobQueue.DeleteJob(model);

            int trackedEntities = _context.ChangeTracker.Entries().Count();
            Assert.Equal(0, trackedEntities);
        }

        Assert.Equal(0, _context.QueueJobs.Count());
    }

    [Fact]
    public void RetryFailedJobs_ChangeTrackerClearedAfterSave()
    {
        for (int i = 0; i < 20; i++)
        {
            _context.FailedJobs.Add(new()
            {
                Uuid = Guid.NewGuid(),
                Connection = "default",
                Queue = "test",
                Payload = $"payload-{i}",
                Exception = "error",
                FailedAt = DateTime.UtcNow
            });
        }
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        _jobQueue.RetryFailedJobs();

        int trackedEntities = _context.ChangeTracker.Entries().Count();
        Assert.Equal(0, trackedEntities);

        Assert.Equal(0, _context.FailedJobs.Count());
        Assert.Equal(20, _context.QueueJobs.Count());
    }

    [Fact]
    public void FailJob_ChangeTrackerClearedAfterSave()
    {
        JobQueue jobQueue = new(_adapter, maxAttempts: 1);

        for (int i = 0; i < 20; i++)
        {
            _context.QueueJobs.Add(new()
            {
                Queue = "test",
                Payload = $"payload-{i}",
                AvailableAt = DateTime.UtcNow,
                ReservedAt = DateTime.UtcNow,
                Attempts = 1
            });
        }
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        List<QueueJob> jobs = _context.QueueJobs.ToList();
        foreach (QueueJob job in jobs)
        {
            QueueJobModel model = new()
            {
                Id = job.Id,
                Priority = job.Priority,
                Queue = job.Queue,
                Payload = job.Payload,
                Attempts = job.Attempts,
                ReservedAt = job.ReservedAt,
                AvailableAt = job.AvailableAt,
                CreatedAt = job.CreatedAt
            };
            jobQueue.FailJob(model, new("test error"));

            int trackedEntities = _context.ChangeTracker.Entries().Count();
            Assert.Equal(0, trackedEntities);
        }

        Assert.Equal(0, _context.QueueJobs.Count());
        Assert.Equal(20, _context.FailedJobs.Count());
    }

    [Fact]
    public void EnqueueAndDequeue_HighVolume_ContextRemainsHealthy()
    {
        for (int cycle = 0; cycle < 10; cycle++)
        {
            for (int i = 0; i < 100; i++)
            {
                _jobQueue.Enqueue(new()
                {
                    Queue = "test",
                    Payload = $"cycle-{cycle}-payload-{i}",
                    AvailableAt = DateTime.UtcNow
                });
            }

            for (int i = 0; i < 100; i++)
            {
                QueueJobModel? job = _jobQueue.Dequeue();
                Assert.NotNull(job);
            }

            int trackedEntities = _context.ChangeTracker.Entries().Count();
            Assert.Equal(0, trackedEntities);
        }

        Assert.Equal(0, _context.QueueJobs.Count());
    }
}
