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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Queue.MediaServer;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// CRIT — EfQueueContextAdapter opened a fresh DbContext (and called
/// SaveChanges) inside every mutating method, so FailJob's dead-letter path
/// ("add the FailedJob row" then "remove the QueueJob row") ran as two
/// independently-committed transactions. A crash between them could leave a
/// job counted twice (in both QueueJobs and FailedJobs) or dropped from both.
/// <see cref="EfQueueContextAdapter.AddFailedJobAndRemoveJob"/> now performs
/// both writes inside one <c>Execute</c> unit of work — one DbContext, one
/// SaveChanges call. These tests inject a real SaveChanges failure via an EF
/// Core interceptor (not a reimplementation of the adapter's logic) and
/// assert the persisted OUTCOME.
/// </summary>
[Trait("Category", "Unit")]
public class FailJobAtomicityTests
{
    private sealed class ThrowOnNthSaveInterceptor(int throwOnCallNumber) : SaveChangesInterceptor
    {
        private int _callCount;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result
        )
        {
            _callCount++;
            if (_callCount == throwOnCallNumber)
                throw new InvalidOperationException(
                    $"Injected failure on SaveChanges call #{throwOnCallNumber}"
                );
            return base.SavingChanges(eventData, result);
        }
    }

    private static int SeedJob(string dbName, byte attempts)
    {
        DbContextOptions<QueueContext> seedOptions = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using TestQueueContext seedContext = new(seedOptions);
        seedContext.Database.EnsureCreated();
        QueueJob seedJob = new()
        {
            Queue = "atomic",
            Payload = "atomic payload",
            AvailableAt = DateTime.UtcNow,
            Attempts = attempts,
        };
        seedContext.QueueJobs.Add(seedJob);
        seedContext.SaveChanges();
        return seedJob.Id;
    }

    [Fact]
    public void AddFailedJobAndRemoveJob_SaveChangesFails_NeitherChangeIsPersisted()
    {
        string dbName = $"AtomicityTest_{Guid.NewGuid()}";
        int jobId = SeedJob(dbName, attempts: 3);

        DbContextOptions<QueueContext> throwingOptions = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(new ThrowOnNthSaveInterceptor(1))
            .Options;
        using TestQueueContext throwingContext = new(throwingOptions);
        EfQueueContextAdapter adapter = new(throwingContext);

        FailedJobModel failedJob = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "atomic",
            Payload = "atomic payload",
            Exception = "boom",
            FailedAt = DateTime.UtcNow,
        };
        QueueJobModel queueJob = new()
        {
            Id = jobId,
            Queue = "atomic",
            Payload = "atomic payload",
            Attempts = 3,
        };

        Assert.Throws<InvalidOperationException>(() =>
            adapter.AddFailedJobAndRemoveJob(failedJob, queueJob)
        );

        DbContextOptions<QueueContext> verifyOptions = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using TestQueueContext verifyContext = new(verifyOptions);
        Assert.Single(verifyContext.QueueJobs);
        Assert.Empty(verifyContext.FailedJobs);
    }

    [Fact]
    public void SeparateAddThenRemoveCalls_SecondCallFails_LeavesJobCountedInBothTables()
    {
        string dbName = $"AtomicityContrast_{Guid.NewGuid()}";
        int jobId = SeedJob(dbName, attempts: 3);

        DbContextOptions<QueueContext> throwingOptions = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(new ThrowOnNthSaveInterceptor(2))
            .Options;
        using TestQueueContext throwingContext = new(throwingOptions);
        EfQueueContextAdapter adapter = new(throwingContext);

        FailedJobModel failedJob = new()
        {
            Uuid = Guid.NewGuid(),
            Connection = "default",
            Queue = "atomic",
            Payload = "atomic payload",
            Exception = "boom",
            FailedAt = DateTime.UtcNow,
        };
        QueueJobModel queueJob = new()
        {
            Id = jobId,
            Queue = "atomic",
            Payload = "atomic payload",
            Attempts = 3,
        };

        adapter.AddFailedJob(failedJob);
        Assert.Throws<InvalidOperationException>(() => adapter.RemoveJob(queueJob));

        DbContextOptions<QueueContext> verifyOptions = new DbContextOptionsBuilder<QueueContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using TestQueueContext verifyContext = new(verifyOptions);
        Assert.Single(verifyContext.QueueJobs);
        Assert.Single(verifyContext.FailedJobs);
    }
}
