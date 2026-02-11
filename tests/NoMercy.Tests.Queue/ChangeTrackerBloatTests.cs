using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;

namespace NoMercy.Tests.Queue;

public class ChangeTrackerBloatTests : IDisposable
{
    private readonly QueueContext _context;
    private readonly JobQueue _jobQueue;

    public ChangeTrackerBloatTests()
    {
        _context = TestQueueContextFactory.CreateInMemoryContext();
        _jobQueue = new(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void Enqueue_ManyJobs_ChangeTrackerDoesNotAccumulate()
    {
        for (int i = 0; i < 100; i++)
        {
            QueueJob job = new()
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
            _jobQueue.Enqueue(new QueueJob
            {
                Queue = "test",
                Payload = $"payload-{i}",
                AvailableAt = DateTime.UtcNow
            });
        }

        for (int i = 0; i < 50; i++)
        {
            QueueJob? job = _jobQueue.Dequeue();
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
            _jobQueue.Enqueue(new QueueJob
            {
                Queue = "test",
                Payload = $"payload-{i}",
                AvailableAt = DateTime.UtcNow
            });
        }

        List<QueueJob> jobs = _context.QueueJobs.ToList();
        foreach (QueueJob job in jobs)
        {
            _jobQueue.DeleteJob(job);

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
            _context.FailedJobs.Add(new FailedJob
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
        JobQueue jobQueue = new(_context, maxAttempts: 1);

        for (int i = 0; i < 20; i++)
        {
            _context.QueueJobs.Add(new QueueJob
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
            jobQueue.FailJob(job, new Exception("test error"));

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
                _jobQueue.Enqueue(new QueueJob
                {
                    Queue = "test",
                    Payload = $"cycle-{cycle}-payload-{i}",
                    AvailableAt = DateTime.UtcNow
                });
            }

            for (int i = 0; i < 100; i++)
            {
                QueueJob? job = _jobQueue.Dequeue();
                Assert.NotNull(job);
            }

            int trackedEntities = _context.ChangeTracker.Entries().Count();
            Assert.Equal(0, trackedEntities);
        }

        Assert.Equal(0, _context.QueueJobs.Count());
    }
}
