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

using FluentAssertions;
using Microsoft.Data.Sqlite;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Sqlite;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// A stale reference to an already-gone row (removed by a different worker,
/// or never persisted at all) must be a silent no-op across every mutating
/// method — not an exception that crashes the caller. This matters
/// concretely for <see cref="JobQueue"/>: orphan recovery and a normal
/// worker completion can race on the same row, and both must be safe to call
/// against a row the other side already removed.
///
/// <para><c>RemoveJob</c> and <c>AddFailedJobAndRemoveJob</c> are deliberately
/// NOT covered for the "job ID never existed at all" case here: against this
/// class's real backing store (a relational SQLite file, not EF's InMemory
/// provider), their "not found in the change tracker → Attach then Remove"
/// fallback issues a DELETE that affects 0 rows, which EF's SaveChanges
/// reports as <c>DbUpdateConcurrencyException</c> rather than swallowing —
/// so that fallback throws instead of being the safe no-op its own class doc
/// implies. This is a real latent defect in <c>NoMercyQueue.Sqlite</c>, not
/// something to encode as "expected" here. Confirmed zero current blast
/// radius: the media server's DI wiring
/// (<c>ServiceRegistration.AddMediaServerQueue</c>) registers
/// <c>EfQueueContextAdapter</c>, not this class, as <c>IQueueContext</c> —
/// <c>SqliteQueueContext</c> is not reachable from the running server today.
/// Flagged for a follow-up fix rather than silently asserted as correct.</para>
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class SqliteQueueContextNotFoundBranchTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IQueueContext _context;

    public SqliteQueueContextNotFoundBranchTests()
    {
        _dbPath = Path.Combine(path1: Path.GetTempPath(), path2: $"queue_notfound_{Guid.NewGuid()}.db");
        _context = SqliteQueueContextFactory.Create(databasePath: _dbPath);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        SqliteConnection.ClearAllPools();
        if (File.Exists(path: _dbPath))
        {
            for (int attempt = 1; attempt <= 30; attempt++)
            {
                try
                {
                    File.Delete(path: _dbPath);
                    break;
                }
                catch (IOException) when (attempt < 30)
                {
                    Thread.Sleep(millisecondsTimeout: 200);
                }
            }
        }
    }

    [Fact]
    public void UpdateJob_UnknownId_IsNoOp_DoesNotThrow()
    {
        QueueJobModel ghost = new()
        {
            Id = 424242,
            Queue = "default",
            Payload = "{}",
            Priority = 9,
        };

        Action act = () => _context.UpdateJob(job: ghost);

        act.Should().NotThrow();
        _context.FindJob(id: 424242).Should().BeNull();
    }

    [Fact]
    public void UpdateJobPayload_UnknownId_IsNoOp_DoesNotThrow()
    {
        Action act = () => _context.UpdateJobPayload(jobId: 424242, newPayload: "{\"new\":true}", availableAt: DateTime.UtcNow);

        act.Should().NotThrow();
    }

    [Fact]
    public void RemoveFailedJob_UnknownId_IsNoOp_DoesNotThrow()
    {
        FailedJobModel ghost = new()
        {
            Id = 555,
            Queue = "default",
            Payload = "{}",
            Exception = "n/a",
        };

        Action act = () => _context.RemoveFailedJob(failedJob: ghost);

        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateCronJob_UnknownId_IsNoOp_DoesNotThrow()
    {
        CronJobModel ghost = new()
        {
            Id = 777,
            Name = "ghost-cron",
            CronExpression = "0 0 * * *",
            JobType = "Ghost",
        };

        Action act = () => _context.UpdateCronJob(cronJob: ghost);

        act.Should().NotThrow();
        _context.FindCronJobByName(name: "ghost-cron").Should().BeNull();
    }

    [Fact]
    public void RemoveCronJob_UnknownId_IsNoOp_DoesNotThrow()
    {
        CronJobModel ghost = new()
        {
            Id = 888,
            Name = "ghost-cron-2",
            CronExpression = "0 0 * * *",
            JobType = "Ghost",
        };

        Action act = () => _context.RemoveCronJob(cronJob: ghost);

        act.Should().NotThrow();
    }
}
