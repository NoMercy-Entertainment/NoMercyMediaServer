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

using Microsoft.Data.Sqlite;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Sqlite;
using Xunit;

namespace NoMercy.Tests.Queue;

[Trait(name: "Category", value: "Unit")]
public class SqliteQueueContextTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IQueueContext _context;

    public SqliteQueueContextTests()
    {
        _dbPath = Path.Combine(path1: Path.GetTempPath(), path2: $"queue_test_{Guid.NewGuid()}.db");
        _context = SqliteQueueContextFactory.Create(databasePath: _dbPath);
    }

    public void Dispose()
    {
        _context.Dispose();
        // Force GC and finalization to release any outstanding handles
        GC.Collect();
        GC.WaitForPendingFinalizers();
        SqliteConnection.ClearAllPools();
        if (File.Exists(path: _dbPath))
        {
            const int maxAttempts = 30;
            const int delayMs = 200;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.Delete(path: _dbPath);
                    break;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(millisecondsTimeout: delayMs);
                }
            }
        }
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
            AvailableAt = DateTime.UtcNow,
        };

        _context.AddJob(job: job);

        Assert.True(condition: job.Id > 0);
    }

    [Fact]
    public void FindJob_ReturnsAddedJob()
    {
        QueueJobModel job = new()
        {
            Payload = "{\"type\":\"find-test\"}",
            Queue = "queue",
            Priority = 5,
            AvailableAt = DateTime.UtcNow,
        };
        _context.AddJob(job: job);

        QueueJobModel? found = _context.FindJob(id: job.Id);

        Assert.NotNull(@object: found);
        Assert.Equal(expected: job.Id, actual: found.Id);
        Assert.Equal(expected: "queue", actual: found.Queue);
        Assert.Equal(expected: 5, actual: found.Priority);
        Assert.Equal(expected: "{\"type\":\"find-test\"}", actual: found.Payload);
    }

    [Fact]
    public void FindJob_ReturnsNullForMissingId()
    {
        QueueJobModel? found = _context.FindJob(id: 999);
        Assert.Null(@object: found);
    }

    [Fact]
    public void RemoveJob_DeletesFromDatabase()
    {
        QueueJobModel job = new()
        {
            Payload = "{\"type\":\"remove-test\"}",
            Queue = "default",
            Priority = 1,
            AvailableAt = DateTime.UtcNow,
        };
        _context.AddJob(job: job);
        int id = job.Id;

        _context.RemoveJob(job: job);

        Assert.Null(@object: _context.FindJob(id: id));
    }

    [Fact]
    public void JobExists_ReturnsTrueForExistingPayload()
    {
        string payload = "{\"type\":\"exists-test\"}";
        _context.AddJob(
            job: new()
            {
                Payload = payload,
                Queue = "default",
                AvailableAt = DateTime.UtcNow,
            }
        );

        Assert.True(condition: _context.JobExists(payload: payload));
    }

    [Fact]
    public void JobExists_ReturnsFalseForMissingPayload()
    {
        Assert.False(condition: _context.JobExists(payload: "{\"type\":\"nonexistent\"}"));
    }

    [Fact]
    public void UpdateJob_ModifiesJobProperties()
    {
        QueueJobModel job = new()
        {
            Payload = "{\"type\":\"update-test\"}",
            Queue = "default",
            Priority = 1,
            AvailableAt = DateTime.UtcNow,
        };
        _context.AddJob(job: job);

        job.Priority = 10;
        job.Attempts = 2;
        job.ReservedAt = DateTime.UtcNow;
        _context.UpdateJob(job: job);

        QueueJobModel? updated = _context.FindJob(id: job.Id);
        Assert.NotNull(@object: updated);
        Assert.Equal(expected: 10, actual: updated.Priority);
        Assert.Equal(expected: 2, actual: updated.Attempts);
        Assert.NotNull(value: updated.ReservedAt);
    }

    [Fact]
    public void GetNextJob_ReturnsHighestPriorityUnreservedJob()
    {
        _context.AddJob(
            job: new()
            {
                Payload = "{\"type\":\"low\"}",
                Queue = "worker",
                Priority = 1,
                AvailableAt = DateTime.UtcNow,
            }
        );
        _context.AddJob(
            job: new()
            {
                Payload = "{\"type\":\"high\"}",
                Queue = "worker",
                Priority = 10,
                AvailableAt = DateTime.UtcNow,
            }
        );

        QueueJobModel? next = _context.GetNextJob(queueName: "worker", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

        Assert.NotNull(@object: next);
        Assert.Equal(expected: "{\"type\":\"high\"}", actual: next.Payload);
    }

    [Fact]
    public void GetNextJob_ReturnsNullWhenNoJobsAvailable()
    {
        QueueJobModel? next = _context.GetNextJob(queueName: "empty-queue", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);
        Assert.Null(@object: next);
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
            AvailableAt = DateTime.UtcNow,
        };
        _context.AddJob(job: reserved);

        QueueJobModel unreserved = new()
        {
            Payload = "{\"type\":\"unreserved\"}",
            Queue = "worker",
            Priority = 1,
            AvailableAt = DateTime.UtcNow,
        };
        _context.AddJob(job: unreserved);

        QueueJobModel? next = _context.GetNextJob(queueName: "worker", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

        Assert.NotNull(@object: next);
        Assert.Equal(expected: "{\"type\":\"unreserved\"}", actual: next.Payload);
    }

    [Fact]
    public void GetNextJob_JobWithFutureAvailableAt_IsNotReserved()
    {
        QueueJobModel future = new()
        {
            Payload = "{\"type\":\"future\"}",
            Queue = "availability",
            Priority = 1,
            AvailableAt = DateTime.UtcNow.AddMinutes(value: 5),
        };
        _context.AddJob(job: future);

        QueueJobModel? next = _context.GetNextJob(queueName: "availability", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

        Assert.Null(@object: next);
        QueueJobModel? unchanged = _context.FindJob(id: future.Id);
        Assert.NotNull(@object: unchanged);
        Assert.Null(value: unchanged.ReservedAt);
    }

    [Fact]
    public void GetNextJob_JobWithPastAvailableAt_IsReserved()
    {
        QueueJobModel past = new()
        {
            Payload = "{\"type\":\"past\"}",
            Queue = "availability",
            Priority = 1,
            AvailableAt = DateTime.UtcNow.AddMinutes(value: -5),
        };
        _context.AddJob(job: past);

        QueueJobModel? next = _context.GetNextJob(queueName: "availability", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

        Assert.NotNull(@object: next);
        Assert.Equal(expected: "{\"type\":\"past\"}", actual: next.Payload);
    }

    [Fact]
    public void GetNextJob_JobAtMaxAttempts_IsNotReserved()
    {
        QueueJobModel atLimit = new()
        {
            Payload = "{\"type\":\"at-limit\"}",
            Queue = "attempts-boundary",
            Priority = 1,
            Attempts = 3,
            AvailableAt = DateTime.UtcNow,
        };
        _context.AddJob(job: atLimit);

        QueueJobModel? next = _context.GetNextJob(queueName: "attempts-boundary", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

        Assert.Null(@object: next);
        QueueJobModel? unchanged = _context.FindJob(id: atLimit.Id);
        Assert.NotNull(@object: unchanged);
        Assert.Null(value: unchanged.ReservedAt);
        Assert.Equal(expected: 3, actual: unchanged.Attempts);
    }

    [Fact]
    public void GetNextJob_JobOneUnderMaxAttempts_IsReserved()
    {
        QueueJobModel underLimit = new()
        {
            Payload = "{\"type\":\"under-limit\"}",
            Queue = "attempts-boundary",
            Priority = 1,
            Attempts = 2,
            AvailableAt = DateTime.UtcNow,
        };
        _context.AddJob(job: underLimit);

        QueueJobModel? next = _context.GetNextJob(queueName: "attempts-boundary", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

        Assert.NotNull(@object: next);
        Assert.Equal(expected: "{\"type\":\"under-limit\"}", actual: next.Payload);
    }

    [Fact]
    public void GetNextJob_EmptyQueueName_ReturnsAnyJob()
    {
        _context.AddJob(
            job: new()
            {
                Payload = "{\"type\":\"any\"}",
                Queue = "some-queue",
                Priority = 1,
                AvailableAt = DateTime.UtcNow,
            }
        );

        QueueJobModel? next = _context.GetNextJob(queueName: "", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);
        Assert.NotNull(@object: next);
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
            AvailableAt = DateTime.UtcNow,
        };
        _context.AddJob(job: job);

        _context.ResetAllReservedJobs();

        QueueJobModel? found = _context.FindJob(id: job.Id);
        Assert.NotNull(@object: found);
        Assert.Null(value: found.ReservedAt);
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
            FailedAt = DateTime.UtcNow,
        };

        _context.AddFailedJob(failedJob: failedJob);
        _context.SaveChanges();

        IReadOnlyList<FailedJobModel> failedJobs = _context.GetFailedJobs();
        Assert.Single(collection: failedJobs);
        Assert.Equal(expected: "Test exception", actual: failedJobs[index: 0].Exception);
        Assert.Equal(expected: "{\"type\":\"failed\"}", actual: failedJobs[index: 0].Payload);
    }

    [Fact]
    public void FindFailedJob_ReturnsNullForMissingId()
    {
        FailedJobModel? found = _context.FindFailedJob(id: 999);
        Assert.Null(@object: found);
    }

    [Fact]
    public void RemoveFailedJob_DeletesFromDatabase()
    {
        FailedJobModel failedJob = new()
        {
            Uuid = Guid.NewGuid(),
            Queue = "default",
            Payload = "{\"type\":\"remove-failed\"}",
            Exception = "err",
        };
        _context.AddFailedJob(failedJob: failedJob);
        _context.SaveChanges();

        IReadOnlyList<FailedJobModel> jobs = _context.GetFailedJobs();
        Assert.Single(collection: jobs);

        _context.RemoveFailedJob(failedJob: jobs[index: 0]);
        _context.SaveChanges();

        Assert.Empty(collection: _context.GetFailedJobs());
    }

    [Fact]
    public void GetFailedJobs_FilterById()
    {
        _context.AddFailedJob(
            failedJob: new()
            {
                Uuid = Guid.NewGuid(),
                Queue = "q1",
                Payload = "{\"a\":1}",
                Exception = "err1",
            }
        );
        _context.AddFailedJob(
            failedJob: new()
            {
                Uuid = Guid.NewGuid(),
                Queue = "q2",
                Payload = "{\"a\":2}",
                Exception = "err2",
            }
        );
        _context.SaveChanges();

        IReadOnlyList<FailedJobModel> all = _context.GetFailedJobs();
        Assert.Equal(expected: 2, actual: all.Count);

        IReadOnlyList<FailedJobModel> filtered = _context.GetFailedJobs(failedJobId: all[index: 0].Id);
        Assert.Single(collection: filtered);
        Assert.Equal(expected: all[index: 0].Id, actual: filtered[index: 0].Id);
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
            IsEnabled = true,
        };

        _context.AddCronJob(cronJob: cronJob);

        CronJobModel? found = _context.FindCronJobByName(name: "test-cron");
        Assert.NotNull(@object: found);
        Assert.Equal(expected: "0 * * * *", actual: found.CronExpression);
        Assert.Equal(expected: "TestJob", actual: found.JobType);
    }

    [Fact]
    public void FindCronJobByName_ReturnsNullForMissing()
    {
        CronJobModel? found = _context.FindCronJobByName(name: "nonexistent");
        Assert.Null(@object: found);
    }

    [Fact]
    public void GetEnabledCronJobs_FiltersDisabled()
    {
        _context.AddCronJob(
            cronJob: new()
            {
                Name = "enabled",
                CronExpression = "0 * * * *",
                JobType = "A",
                IsEnabled = true,
            }
        );
        _context.AddCronJob(
            cronJob: new()
            {
                Name = "disabled",
                CronExpression = "0 * * * *",
                JobType = "B",
                IsEnabled = false,
            }
        );

        IReadOnlyList<CronJobModel> enabled = _context.GetEnabledCronJobs();
        Assert.Single(collection: enabled);
        Assert.Equal(expected: "enabled", actual: enabled[index: 0].Name);
    }

    [Fact]
    public void UpdateCronJob_ModifiesProperties()
    {
        CronJobModel cronJob = new()
        {
            Name = "update-cron",
            CronExpression = "0 * * * *",
            JobType = "TestJob",
            IsEnabled = true,
        };
        _context.AddCronJob(cronJob: cronJob);

        CronJobModel? found = _context.FindCronJobByName(name: "update-cron");
        Assert.NotNull(@object: found);

        found.CronExpression = "*/5 * * * *";
        found.IsEnabled = false;
        found.LastRun = DateTime.UtcNow;
        _context.UpdateCronJob(cronJob: found);

        CronJobModel? updated = _context.FindCronJobByName(name: "update-cron");
        Assert.NotNull(@object: updated);
        Assert.Equal(expected: "*/5 * * * *", actual: updated.CronExpression);
        Assert.False(condition: updated.IsEnabled);
        Assert.NotNull(value: updated.LastRun);
    }

    [Fact]
    public void RemoveCronJob_DeletesFromDatabase()
    {
        _context.AddCronJob(
            cronJob: new()
            {
                Name = "remove-cron",
                CronExpression = "0 * * * *",
                JobType = "TestJob",
            }
        );

        CronJobModel? found = _context.FindCronJobByName(name: "remove-cron");
        Assert.NotNull(@object: found);

        _context.RemoveCronJob(cronJob: found);

        Assert.Null(@object: _context.FindCronJobByName(name: "remove-cron"));
    }

    // =========================================================================
    // Factory tests
    // =========================================================================

    [Fact]
    public void Factory_CreatesWorkingContext()
    {
        string path = Path.Combine(path1: Path.GetTempPath(), path2: $"factory_test_{Guid.NewGuid()}.db");
        try
        {
            using IQueueContext ctx = SqliteQueueContextFactory.Create(databasePath: path);

            ctx.AddJob(
                job: new()
                {
                    Payload = "{\"test\":true}",
                    Queue = "default",
                    AvailableAt = DateTime.UtcNow,
                }
            );

            Assert.True(condition: ctx.JobExists(payload: "{\"test\":true}"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path: path))
                File.Delete(path: path);
        }
    }

    [Fact]
    public void Factory_CreatesDatabaseFile()
    {
        string path = Path.Combine(path1: Path.GetTempPath(), path2: $"factory_file_test_{Guid.NewGuid()}.db");
        try
        {
            using IQueueContext ctx = SqliteQueueContextFactory.Create(databasePath: path);
            Assert.True(condition: File.Exists(path: path));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path: path))
                File.Delete(path: path);
        }
    }
}
