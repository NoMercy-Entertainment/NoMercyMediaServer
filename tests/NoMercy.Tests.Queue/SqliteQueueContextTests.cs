using NoMercy.Queue.Core.Interfaces;
using NoMercy.Queue.Core.Models;
using NoMercy.Queue.Sqlite;
using Xunit;

namespace NoMercy.Tests.Queue;

[Trait("Category", "Unit")]
public class SqliteQueueContextTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IQueueContext _context;

    public SqliteQueueContextTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"queue_test_{Guid.NewGuid()}.db");
        _context = SqliteQueueContextFactory.Create(_dbPath);
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // =========================================================================
    // Job operations
    // =========================================================================

    [Fact]
    public void AddJob_AssignsId()
    {
        QueueJobModel job = new()
        {
            Payload = "{\"type\":\"test\"}",
            Queue = "default",
            Priority = 1,
            AvailableAt = DateTime.UtcNow
        };

        _context.AddJob(job);

        Assert.True(job.Id > 0);
    }

    [Fact]
    public void FindJob_ReturnsAddedJob()
    {
        QueueJobModel job = new()
        {
            Payload = "{\"type\":\"find-test\"}",
            Queue = "queue",
            Priority = 5,
            AvailableAt = DateTime.UtcNow
        };
        _context.AddJob(job);

        QueueJobModel? found = _context.FindJob(job.Id);

        Assert.NotNull(found);
        Assert.Equal(job.Id, found.Id);
        Assert.Equal("queue", found.Queue);
        Assert.Equal(5, found.Priority);
        Assert.Equal("{\"type\":\"find-test\"}", found.Payload);
    }

    [Fact]
    public void FindJob_ReturnsNullForMissingId()
    {
        QueueJobModel? found = _context.FindJob(999);
        Assert.Null(found);
    }

    [Fact]
    public void RemoveJob_DeletesFromDatabase()
    {
        QueueJobModel job = new()
        {
            Payload = "{\"type\":\"remove-test\"}",
            Queue = "default",
            Priority = 1,
            AvailableAt = DateTime.UtcNow
        };
        _context.AddJob(job);
        int id = job.Id;

        _context.RemoveJob(job);

        Assert.Null(_context.FindJob(id));
    }

    [Fact]
    public void JobExists_ReturnsTrueForExistingPayload()
    {
        string payload = "{\"type\":\"exists-test\"}";
        _context.AddJob(new()
        {
            Payload = payload,
            Queue = "default",
            AvailableAt = DateTime.UtcNow
        });

        Assert.True(_context.JobExists(payload));
    }

    [Fact]
    public void JobExists_ReturnsFalseForMissingPayload()
    {
        Assert.False(_context.JobExists("{\"type\":\"nonexistent\"}"));
    }

    [Fact]
    public void UpdateJob_ModifiesJobProperties()
    {
        QueueJobModel job = new()
        {
            Payload = "{\"type\":\"update-test\"}",
            Queue = "default",
            Priority = 1,
            AvailableAt = DateTime.UtcNow
        };
        _context.AddJob(job);

        job.Priority = 10;
        job.Attempts = 2;
        job.ReservedAt = DateTime.UtcNow;
        _context.UpdateJob(job);

        QueueJobModel? updated = _context.FindJob(job.Id);
        Assert.NotNull(updated);
        Assert.Equal(10, updated.Priority);
        Assert.Equal(2, updated.Attempts);
        Assert.NotNull(updated.ReservedAt);
    }

    [Fact]
    public void GetNextJob_ReturnsHighestPriorityUnreservedJob()
    {
        _context.AddJob(new()
        {
            Payload = "{\"type\":\"low\"}",
            Queue = "worker",
            Priority = 1,
            AvailableAt = DateTime.UtcNow
        });
        _context.AddJob(new()
        {
            Payload = "{\"type\":\"high\"}",
            Queue = "worker",
            Priority = 10,
            AvailableAt = DateTime.UtcNow
        });

        QueueJobModel? next = _context.GetNextJob("worker", 3, null);

        Assert.NotNull(next);
        Assert.Equal("{\"type\":\"high\"}", next.Payload);
    }

    [Fact]
    public void GetNextJob_ReturnsNullWhenNoJobsAvailable()
    {
        QueueJobModel? next = _context.GetNextJob("empty-queue", 3, null);
        Assert.Null(next);
    }

    [Fact]
    public void GetNextJob_SkipsReservedJobs()
    {
        QueueJobModel reserved = new()
        {
            Payload = "{\"type\":\"reserved\"}",
            Queue = "worker",
            Priority = 10,
            ReservedAt = DateTime.UtcNow,
            AvailableAt = DateTime.UtcNow
        };
        _context.AddJob(reserved);

        QueueJobModel unreserved = new()
        {
            Payload = "{\"type\":\"unreserved\"}",
            Queue = "worker",
            Priority = 1,
            AvailableAt = DateTime.UtcNow
        };
        _context.AddJob(unreserved);

        QueueJobModel? next = _context.GetNextJob("worker", 3, null);

        Assert.NotNull(next);
        Assert.Equal("{\"type\":\"unreserved\"}", next.Payload);
    }

    [Fact]
    public void GetNextJob_EmptyQueueName_ReturnsAnyJob()
    {
        _context.AddJob(new()
        {
            Payload = "{\"type\":\"any\"}",
            Queue = "some-queue",
            Priority = 1,
            AvailableAt = DateTime.UtcNow
        });

        QueueJobModel? next = _context.GetNextJob("", 3, null);
        Assert.NotNull(next);
    }

    [Fact]
    public void ResetAllReservedJobs_ClearsReservedAt()
    {
        QueueJobModel job = new()
        {
            Payload = "{\"type\":\"reset-test\"}",
            Queue = "default",
            Priority = 1,
            ReservedAt = DateTime.UtcNow,
            AvailableAt = DateTime.UtcNow
        };
        _context.AddJob(job);

        _context.ResetAllReservedJobs();

        QueueJobModel? found = _context.FindJob(job.Id);
        Assert.NotNull(found);
        Assert.Null(found.ReservedAt);
    }

    // =========================================================================
    // Failed job operations
    // =========================================================================

    [Fact]
    public void AddFailedJob_AndFind_RoundTrips()
    {
        FailedJobModel failedJob = new()
        {
            Uuid = Guid.NewGuid(),
            Queue = "default",
            Payload = "{\"type\":\"failed\"}",
            Exception = "Test exception",
            FailedAt = DateTime.UtcNow
        };

        _context.AddFailedJob(failedJob);
        _context.SaveChanges();

        IReadOnlyList<FailedJobModel> failedJobs = _context.GetFailedJobs();
        Assert.Single(failedJobs);
        Assert.Equal("Test exception", failedJobs[0].Exception);
        Assert.Equal("{\"type\":\"failed\"}", failedJobs[0].Payload);
    }

    [Fact]
    public void FindFailedJob_ReturnsNullForMissingId()
    {
        FailedJobModel? found = _context.FindFailedJob(999);
        Assert.Null(found);
    }

    [Fact]
    public void RemoveFailedJob_DeletesFromDatabase()
    {
        FailedJobModel failedJob = new()
        {
            Uuid = Guid.NewGuid(),
            Queue = "default",
            Payload = "{\"type\":\"remove-failed\"}",
            Exception = "err"
        };
        _context.AddFailedJob(failedJob);
        _context.SaveChanges();

        IReadOnlyList<FailedJobModel> jobs = _context.GetFailedJobs();
        Assert.Single(jobs);

        _context.RemoveFailedJob(jobs[0]);
        _context.SaveChanges();

        Assert.Empty(_context.GetFailedJobs());
    }

    [Fact]
    public void GetFailedJobs_FilterById()
    {
        _context.AddFailedJob(new()
        {
            Uuid = Guid.NewGuid(),
            Queue = "q1",
            Payload = "{\"a\":1}",
            Exception = "err1"
        });
        _context.AddFailedJob(new()
        {
            Uuid = Guid.NewGuid(),
            Queue = "q2",
            Payload = "{\"a\":2}",
            Exception = "err2"
        });
        _context.SaveChanges();

        IReadOnlyList<FailedJobModel> all = _context.GetFailedJobs();
        Assert.Equal(2, all.Count);

        IReadOnlyList<FailedJobModel> filtered = _context.GetFailedJobs(all[0].Id);
        Assert.Single(filtered);
        Assert.Equal(all[0].Id, filtered[0].Id);
    }

    // =========================================================================
    // Cron job operations
    // =========================================================================

    [Fact]
    public void AddCronJob_AndFindByName_RoundTrips()
    {
        CronJobModel cronJob = new()
        {
            Name = "test-cron",
            CronExpression = "0 * * * *",
            JobType = "TestJob",
            IsEnabled = true
        };

        _context.AddCronJob(cronJob);

        CronJobModel? found = _context.FindCronJobByName("test-cron");
        Assert.NotNull(found);
        Assert.Equal("0 * * * *", found.CronExpression);
        Assert.Equal("TestJob", found.JobType);
    }

    [Fact]
    public void FindCronJobByName_ReturnsNullForMissing()
    {
        CronJobModel? found = _context.FindCronJobByName("nonexistent");
        Assert.Null(found);
    }

    [Fact]
    public void GetEnabledCronJobs_FiltersDisabled()
    {
        _context.AddCronJob(new()
        {
            Name = "enabled",
            CronExpression = "0 * * * *",
            JobType = "A",
            IsEnabled = true
        });
        _context.AddCronJob(new()
        {
            Name = "disabled",
            CronExpression = "0 * * * *",
            JobType = "B",
            IsEnabled = false
        });

        IReadOnlyList<CronJobModel> enabled = _context.GetEnabledCronJobs();
        Assert.Single(enabled);
        Assert.Equal("enabled", enabled[0].Name);
    }

    [Fact]
    public void UpdateCronJob_ModifiesProperties()
    {
        CronJobModel cronJob = new()
        {
            Name = "update-cron",
            CronExpression = "0 * * * *",
            JobType = "TestJob",
            IsEnabled = true
        };
        _context.AddCronJob(cronJob);

        CronJobModel? found = _context.FindCronJobByName("update-cron");
        Assert.NotNull(found);

        found.CronExpression = "*/5 * * * *";
        found.IsEnabled = false;
        found.LastRun = DateTime.UtcNow;
        _context.UpdateCronJob(found);

        CronJobModel? updated = _context.FindCronJobByName("update-cron");
        Assert.NotNull(updated);
        Assert.Equal("*/5 * * * *", updated.CronExpression);
        Assert.False(updated.IsEnabled);
        Assert.NotNull(updated.LastRun);
    }

    [Fact]
    public void RemoveCronJob_DeletesFromDatabase()
    {
        _context.AddCronJob(new()
        {
            Name = "remove-cron",
            CronExpression = "0 * * * *",
            JobType = "TestJob"
        });

        CronJobModel? found = _context.FindCronJobByName("remove-cron");
        Assert.NotNull(found);

        _context.RemoveCronJob(found);

        Assert.Null(_context.FindCronJobByName("remove-cron"));
    }

    // =========================================================================
    // Factory tests
    // =========================================================================

    [Fact]
    public void Factory_CreatesWorkingContext()
    {
        string path = Path.Combine(Path.GetTempPath(), $"factory_test_{Guid.NewGuid()}.db");
        try
        {
            using IQueueContext ctx = SqliteQueueContextFactory.Create(path);

            ctx.AddJob(new()
            {
                Payload = "{\"test\":true}",
                Queue = "default",
                AvailableAt = DateTime.UtcNow
            });

            Assert.True(ctx.JobExists("{\"test\":true}"));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Factory_CreatesDatabaseFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"factory_file_test_{Guid.NewGuid()}.db");
        try
        {
            using IQueueContext ctx = SqliteQueueContextFactory.Create(path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
