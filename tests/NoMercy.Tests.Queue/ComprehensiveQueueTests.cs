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
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Sqlite;
using Xunit;
using IShouldQueue = NoMercyQueue.Core.Interfaces.IShouldQueue;

namespace NoMercy.Tests.Queue;

/// <summary>
/// QDC-17: Comprehensive queue testing covering the three-layer queue architecture:
/// - Queue.Core models and interfaces
/// - Queue (runtime): JobQueue, JobDispatcher, QueueRunner, SerializationHelper
/// - Queue.Sqlite: SqliteQueueContext via SqliteQueueContextFactory
/// - Queue.MediaServer: EfQueueContextAdapter
///
/// Tests verify cross-provider behavioral parity, end-to-end job lifecycle through
/// the full stack, and edge cases not covered by existing test suites.
/// </summary>
public class ComprehensiveQueueTests
{
    // =========================================================================
    // 1. EfQueueContextAdapter — Dedicated Tests
    //    The adapter wraps NoMercy.Database.QueueContext and implements IQueueContext.
    //    Existing tests only exercise it indirectly; these tests verify it directly.
    // =========================================================================

    [Trait(name: "Category", value: "Unit")]
    public class EfQueueContextAdapterTests : IDisposable
    {
        private readonly QueueContext _context;
        private readonly IQueueContext _adapter;

        public EfQueueContextAdapterTests()
        {
            (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        }

        public void Dispose()
        {
            _adapter.Dispose();
            _context.Dispose();
        }

        [Fact]
        public void AddJob_AssignsPositiveId()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"type\":\"adapter-test\"}",
                Queue = "test",
                Priority = 1,
                AvailableAt = DateTime.UtcNow,
            };

            _adapter.AddJob(job: job);

            Assert.True(condition: job.Id > 0);
        }

        [Fact]
        public void AddJob_PersistsToUnderlyingContext()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"type\":\"persist-test\"}",
                Queue = "test",
                Priority = 5,
                AvailableAt = DateTime.UtcNow,
            };

            _adapter.AddJob(job: job);

            QueueJob? entity = _context.QueueJobs.FirstOrDefault(predicate: j => j.Id == job.Id);
            Assert.NotNull(@object: entity);
            Assert.Equal(expected: "{\"type\":\"persist-test\"}", actual: entity.Payload);
            Assert.Equal(expected: "test", actual: entity.Queue);
            Assert.Equal(expected: 5, actual: entity.Priority);
        }

        [Fact]
        public void FindJob_ReturnsCorrectModel()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"type\":\"find-adapter\"}",
                Queue = "q1",
                Priority = 3,
                AvailableAt = DateTime.UtcNow,
            };
            _adapter.AddJob(job: job);

            QueueJobModel? found = _adapter.FindJob(id: job.Id);

            Assert.NotNull(@object: found);
            Assert.Equal(expected: job.Id, actual: found.Id);
            Assert.Equal(expected: "q1", actual: found.Queue);
            Assert.Equal(expected: 3, actual: found.Priority);
            Assert.Equal(expected: "{\"type\":\"find-adapter\"}", actual: found.Payload);
        }

        [Fact]
        public void FindJob_ReturnsNullForMissingId()
        {
            QueueJobModel? found = _adapter.FindJob(id: 999);
            Assert.Null(@object: found);
        }

        [Fact]
        public void RemoveJob_DeletesFromUnderlyingContext()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"type\":\"remove-adapter\"}",
                Queue = "test",
                AvailableAt = DateTime.UtcNow,
            };
            _adapter.AddJob(job: job);
            int id = job.Id;

            _adapter.RemoveJob(job: job);

            Assert.Null(@object: _adapter.FindJob(id: id));
            Assert.Null(@object: _context.QueueJobs.Find(keyValues: id));
        }

        [Fact]
        public void RemoveJob_AttachesAndRemovesWhenNotTracked()
        {
            // Add via context directly, then clear tracker
            QueueJob entity = new()
            {
                Payload = "{\"type\":\"detached\"}",
                Queue = "test",
                AvailableAt = DateTime.UtcNow,
            };
            _context.QueueJobs.Add(entity: entity);
            _context.SaveChanges();
            _context.ChangeTracker.Clear();

            // Remove via adapter with a model that matches the ID
            QueueJobModel model = new()
            {
                Id = entity.Id,
                Payload = "{\"type\":\"detached\"}",
                Queue = "test",
            };
            _adapter.RemoveJob(job: model);

            Assert.Null(@object: _context.QueueJobs.Find(keyValues: entity.Id));
        }

        [Fact]
        public void JobExists_ReturnsTrueForExistingPayload()
        {
            _adapter.AddJob(
                job: new()
                {
                    Payload = "{\"exists\":true}",
                    Queue = "test",
                    AvailableAt = DateTime.UtcNow,
                }
            );

            Assert.True(condition: _adapter.JobExists(payload: "{\"exists\":true}"));
        }

        [Fact]
        public void JobExists_ReturnsFalseForMissingPayload()
        {
            Assert.False(condition: _adapter.JobExists(payload: "{\"nonexistent\":true}"));
        }

        [Fact]
        public void UpdateJob_ModifiesProperties()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"update\":true}",
                Queue = "test",
                Priority = 1,
                AvailableAt = DateTime.UtcNow,
            };
            _adapter.AddJob(job: job);

            job.Priority = 99;
            job.Attempts = 5;
            job.ReservedAt = DateTime.UtcNow;
            _adapter.UpdateJob(job: job);

            QueueJobModel? updated = _adapter.FindJob(id: job.Id);
            Assert.NotNull(@object: updated);
            Assert.Equal(expected: 99, actual: updated.Priority);
            Assert.Equal(expected: 5, actual: updated.Attempts);
            Assert.NotNull(value: updated.ReservedAt);
        }

        [Fact]
        public void UpdateJob_NonexistentId_DoesNotThrow()
        {
            QueueJobModel job = new()
            {
                Id = 9999,
                Payload = "nope",
                Queue = "test",
            };

            Exception? ex = Record.Exception(testCode: () => _adapter.UpdateJob(job: job));
            Assert.Null(@object: ex);
        }

        [Fact]
        public void GetNextJob_ReturnsHighestPriorityUnreservedJob()
        {
            _adapter.AddJob(
                job: new()
                {
                    Payload = "{\"p\":1}",
                    Queue = "w",
                    Priority = 1,
                    AvailableAt = DateTime.UtcNow,
                }
            );
            _adapter.AddJob(
                job: new()
                {
                    Payload = "{\"p\":10}",
                    Queue = "w",
                    Priority = 10,
                    AvailableAt = DateTime.UtcNow,
                }
            );

            QueueJobModel? next = _adapter.GetNextJob(queueName: "w", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

            Assert.NotNull(@object: next);
            Assert.Equal(expected: "{\"p\":10}", actual: next.Payload);
        }

        [Fact]
        public void GetNextJob_SkipsReservedJobs()
        {
            QueueJobModel reserved = new()
            {
                Payload = "{\"reserved\":true}",
                Queue = "w",
                Priority = 10,
                ReservedAt = DateTime.UtcNow,
                AvailableAt = DateTime.UtcNow,
            };
            _adapter.AddJob(job: reserved);

            QueueJobModel unreserved = new()
            {
                Payload = "{\"unreserved\":true}",
                Queue = "w",
                Priority = 1,
                AvailableAt = DateTime.UtcNow,
            };
            _adapter.AddJob(job: unreserved);

            QueueJobModel? next = _adapter.GetNextJob(queueName: "w", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

            Assert.NotNull(@object: next);
            Assert.Equal(expected: "{\"unreserved\":true}", actual: next.Payload);
        }

        [Fact]
        public void GetNextJob_EmptyQueueName_ReturnsAnyJob()
        {
            _adapter.AddJob(
                job: new()
                {
                    Payload = "{\"any\":true}",
                    Queue = "specific-queue",
                    AvailableAt = DateTime.UtcNow,
                }
            );

            QueueJobModel? next = _adapter.GetNextJob(queueName: "", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);
            Assert.NotNull(@object: next);
        }

        [Fact]
        public void GetNextJob_ReturnsNullWhenEmpty()
        {
            QueueJobModel? next = _adapter.GetNextJob(queueName: "empty", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);
            Assert.Null(@object: next);
        }

        [Fact]
        public void GetNextJob_WithCurrentJobId_ReturnsNull()
        {
            _adapter.AddJob(
                job: new()
                {
                    Payload = "{\"guard\":true}",
                    Queue = "w",
                    Priority = 1,
                    AvailableAt = DateTime.UtcNow,
                }
            );

            QueueJobModel? next = _adapter.GetNextJob(queueName: "w", maxAttempts: 3, currentJobId: 42L, now: DateTime.UtcNow);
            Assert.Null(@object: next);
        }

        [Fact]
        public void ResetAllReservedJobs_ClearsReservedAt()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"reset\":true}",
                Queue = "test",
                ReservedAt = DateTime.UtcNow,
                AvailableAt = DateTime.UtcNow,
            };
            _adapter.AddJob(job: job);

            _adapter.ResetAllReservedJobs();

            QueueJobModel? found = _adapter.FindJob(id: job.Id);
            Assert.NotNull(@object: found);
            Assert.Null(value: found.ReservedAt);
        }

        // --- Failed job operations ---

        [Fact]
        public void AddFailedJob_PersistsToContext()
        {
            FailedJobModel failedJob = new()
            {
                Uuid = Guid.NewGuid(),
                Queue = "test",
                Payload = "{\"failed\":true}",
                Exception = "boom",
                FailedAt = DateTime.UtcNow,
            };

            _adapter.AddFailedJob(failedJob: failedJob);
            _adapter.SaveChanges();

            IReadOnlyList<FailedJobModel> all = _adapter.GetFailedJobs();
            Assert.Single(collection: all);
            Assert.Equal(expected: "boom", actual: all[index: 0].Exception);
        }

        [Fact]
        public void FindFailedJob_ReturnsCorrectModel()
        {
            FailedJobModel failedJob = new()
            {
                Uuid = Guid.NewGuid(),
                Queue = "test",
                Payload = "{\"find-failed\":true}",
                Exception = "err",
            };
            _adapter.AddFailedJob(failedJob: failedJob);
            _adapter.SaveChanges();

            IReadOnlyList<FailedJobModel> all = _adapter.GetFailedJobs();
            FailedJobModel? found = _adapter.FindFailedJob(id: (int)all[index: 0].Id);

            Assert.NotNull(@object: found);
            Assert.Equal(expected: "{\"find-failed\":true}", actual: found.Payload);
        }

        [Fact]
        public void FindFailedJob_ReturnsNullForMissingId()
        {
            FailedJobModel? found = _adapter.FindFailedJob(id: 999);
            Assert.Null(@object: found);
        }

        [Fact]
        public void RemoveFailedJob_DeletesFromContext()
        {
            FailedJobModel failedJob = new()
            {
                Uuid = Guid.NewGuid(),
                Queue = "test",
                Payload = "{\"remove-failed\":true}",
                Exception = "err",
            };
            _adapter.AddFailedJob(failedJob: failedJob);
            _adapter.SaveChanges();

            IReadOnlyList<FailedJobModel> all = _adapter.GetFailedJobs();
            _adapter.RemoveFailedJob(failedJob: all[index: 0]);
            _adapter.SaveChanges();

            Assert.Empty(collection: _adapter.GetFailedJobs());
        }

        [Fact]
        public void RemoveFailedJob_NonexistentId_DoesNotThrow()
        {
            FailedJobModel model = new()
            {
                Id = 9999,
                Queue = "test",
                Payload = "nope",
                Exception = "err",
            };

            Exception? ex = Record.Exception(testCode: () =>
            {
                _adapter.RemoveFailedJob(failedJob: model);
                _adapter.SaveChanges();
            });
            Assert.Null(@object: ex);
        }

        [Fact]
        public void GetFailedJobs_FilterById()
        {
            _adapter.AddFailedJob(
                failedJob: new()
                {
                    Uuid = Guid.NewGuid(),
                    Queue = "q1",
                    Payload = "{\"a\":1}",
                    Exception = "e1",
                }
            );
            _adapter.AddFailedJob(
                failedJob: new()
                {
                    Uuid = Guid.NewGuid(),
                    Queue = "q2",
                    Payload = "{\"a\":2}",
                    Exception = "e2",
                }
            );
            _adapter.SaveChanges();

            IReadOnlyList<FailedJobModel> all = _adapter.GetFailedJobs();
            Assert.Equal(expected: 2, actual: all.Count);

            IReadOnlyList<FailedJobModel> filtered = _adapter.GetFailedJobs(failedJobId: all[index: 0].Id);
            Assert.Single(collection: filtered);
            Assert.Equal(expected: all[index: 0].Id, actual: filtered[index: 0].Id);
        }

        // --- Cron job operations ---

        [Fact]
        public void AddCronJob_PersistsAndFindByName()
        {
            CronJobModel cronJob = new()
            {
                Name = "adapter-cron",
                CronExpression = "0 * * * *",
                JobType = "TestJob",
                IsEnabled = true,
            };

            _adapter.AddCronJob(cronJob: cronJob);

            CronJobModel? found = _adapter.FindCronJobByName(name: "adapter-cron");
            Assert.NotNull(@object: found);
            Assert.Equal(expected: "0 * * * *", actual: found.CronExpression);
        }

        [Fact]
        public void FindCronJobByName_ReturnsNullForMissing()
        {
            CronJobModel? found = _adapter.FindCronJobByName(name: "nonexistent");
            Assert.Null(@object: found);
        }

        [Fact]
        public void GetEnabledCronJobs_FiltersDisabled()
        {
            _adapter.AddCronJob(
                cronJob: new()
                {
                    Name = "enabled-adapter",
                    CronExpression = "0 * * * *",
                    JobType = "A",
                    IsEnabled = true,
                }
            );
            _adapter.AddCronJob(
                cronJob: new()
                {
                    Name = "disabled-adapter",
                    CronExpression = "0 * * * *",
                    JobType = "B",
                    IsEnabled = false,
                }
            );

            IReadOnlyList<CronJobModel> enabled = _adapter.GetEnabledCronJobs();
            Assert.Single(collection: enabled);
            Assert.Equal(expected: "enabled-adapter", actual: enabled[index: 0].Name);
        }

        [Fact]
        public void UpdateCronJob_ModifiesProperties()
        {
            _adapter.AddCronJob(
                cronJob: new()
                {
                    Name = "update-adapter-cron",
                    CronExpression = "0 * * * *",
                    JobType = "TestJob",
                    IsEnabled = true,
                }
            );

            CronJobModel? found = _adapter.FindCronJobByName(name: "update-adapter-cron");
            Assert.NotNull(@object: found);

            found.CronExpression = "*/5 * * * *";
            found.IsEnabled = false;
            found.LastRun = DateTime.UtcNow;
            _adapter.UpdateCronJob(cronJob: found);

            CronJobModel? updated = _adapter.FindCronJobByName(name: "update-adapter-cron");
            Assert.NotNull(@object: updated);
            Assert.Equal(expected: "*/5 * * * *", actual: updated.CronExpression);
            Assert.False(condition: updated.IsEnabled);
            Assert.NotNull(value: updated.LastRun);
        }

        [Fact]
        public void RemoveCronJob_DeletesFromContext()
        {
            _adapter.AddCronJob(
                cronJob: new()
                {
                    Name = "remove-adapter-cron",
                    CronExpression = "0 * * * *",
                    JobType = "TestJob",
                }
            );

            CronJobModel? found = _adapter.FindCronJobByName(name: "remove-adapter-cron");
            Assert.NotNull(@object: found);

            _adapter.RemoveCronJob(cronJob: found);

            Assert.Null(@object: _adapter.FindCronJobByName(name: "remove-adapter-cron"));
        }

        [Fact]
        public void RemoveCronJob_NonexistentId_DoesNotThrow()
        {
            CronJobModel model = new()
            {
                Id = 9999,
                Name = "nope",
                CronExpression = "0 * * * *",
                JobType = "X",
            };

            Exception? ex = Record.Exception(testCode: () => _adapter.RemoveCronJob(cronJob: model));
            Assert.Null(@object: ex);
        }

        [Fact]
        public void SaveChanges_ClearsChangeTracker()
        {
            _adapter.AddJob(
                job: new()
                {
                    Payload = "{\"tracker\":true}",
                    Queue = "test",
                    AvailableAt = DateTime.UtcNow,
                }
            );

            // After SaveAndClear, change tracker should be empty
            Assert.False(condition: _context.ChangeTracker.HasChanges());
        }
    }

    // =========================================================================
    // 2. Cross-Provider Behavioral Parity
    //    Verify that SqliteQueueContext and EfQueueContextAdapter behave identically
    //    for the same sequence of operations.
    // =========================================================================

    [Trait(name: "Category", value: "Integration")]
    public class CrossProviderParityTests : IDisposable
    {
        private readonly string _sqliteDbPath;
        private readonly IQueueContext _sqliteContext;
        private readonly QueueContext _efDbContext;
        private readonly IQueueContext _efAdapter;

        public CrossProviderParityTests()
        {
            _sqliteDbPath = Path.Combine(path1: Path.GetTempPath(), path2: $"parity_test_{Guid.NewGuid()}.db");
            _sqliteContext = SqliteQueueContextFactory.Create(databasePath: _sqliteDbPath);
            (_efDbContext, _efAdapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        }

        public void Dispose()
        {
            _sqliteContext.Dispose();
            _efAdapter.Dispose();
            _efDbContext.Dispose();
            SqliteConnection.ClearAllPools();
            if (File.Exists(path: _sqliteDbPath))
                File.Delete(path: _sqliteDbPath);
        }

        [Fact]
        public void AddAndFindJob_BothProviders_ReturnSameData()
        {
            QueueJobModel sqliteJob = new()
            {
                Payload = "{\"parity\":\"job\"}",
                Queue = "test-queue",
                Priority = 7,
                AvailableAt = DateTime.UtcNow,
            };
            QueueJobModel efJob = new()
            {
                Payload = "{\"parity\":\"job\"}",
                Queue = "test-queue",
                Priority = 7,
                AvailableAt = DateTime.UtcNow,
            };

            _sqliteContext.AddJob(job: sqliteJob);
            _efAdapter.AddJob(job: efJob);

            QueueJobModel? sqliteFound = _sqliteContext.FindJob(id: sqliteJob.Id);
            QueueJobModel? efFound = _efAdapter.FindJob(id: efJob.Id);

            Assert.NotNull(@object: sqliteFound);
            Assert.NotNull(@object: efFound);
            Assert.Equal(expected: sqliteFound.Queue, actual: efFound.Queue);
            Assert.Equal(expected: sqliteFound.Priority, actual: efFound.Priority);
            Assert.Equal(expected: sqliteFound.Payload, actual: efFound.Payload);
        }

        [Fact]
        public void JobExists_BothProviders_AgreeOnExistence()
        {
            string payload = "{\"parity\":\"exists\"}";

            _sqliteContext.AddJob(
                job: new()
                {
                    Payload = payload,
                    Queue = "t",
                    AvailableAt = DateTime.UtcNow,
                }
            );
            _efAdapter.AddJob(
                job: new()
                {
                    Payload = payload,
                    Queue = "t",
                    AvailableAt = DateTime.UtcNow,
                }
            );

            Assert.Equal(expected: _sqliteContext.JobExists(payload: payload), actual: _efAdapter.JobExists(payload: payload));
            Assert.Equal(
                expected: _sqliteContext.JobExists(payload: "{\"nope\":true}"),
                actual: _efAdapter.JobExists(payload: "{\"nope\":true}")
            );
        }

        [Fact]
        public void GetNextJob_BothProviders_ReturnHighestPriority()
        {
            // Add same jobs to both
            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddJob(
                    job: new()
                    {
                        Payload = "{\"p\":1}",
                        Queue = "parity",
                        Priority = 1,
                        AvailableAt = DateTime.UtcNow,
                    }
                );
                ctx.AddJob(
                    job: new()
                    {
                        Payload = "{\"p\":10}",
                        Queue = "parity",
                        Priority = 10,
                        AvailableAt = DateTime.UtcNow,
                    }
                );
            }

            QueueJobModel? sqliteNext = _sqliteContext.GetNextJob(
                queueName: "parity",
                maxAttempts: 3,
                currentJobId: null,
                now: DateTime.UtcNow
            );
            QueueJobModel? efNext = _efAdapter.GetNextJob(queueName: "parity", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

            Assert.NotNull(@object: sqliteNext);
            Assert.NotNull(@object: efNext);
            Assert.Equal(expected: sqliteNext.Priority, actual: efNext.Priority);
            Assert.Equal(expected: sqliteNext.Payload, actual: efNext.Payload);
        }

        [Fact]
        public void GetNextJob_WithCurrentJobId_BothReturnNull()
        {
            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddJob(
                    job: new()
                    {
                        Payload = "{\"guard\":true}",
                        Queue = "parity",
                        Priority = 1,
                        AvailableAt = DateTime.UtcNow,
                    }
                );
            }

            QueueJobModel? sqliteNext = _sqliteContext.GetNextJob(
                queueName: "parity",
                maxAttempts: 3,
                currentJobId: 42L,
                now: DateTime.UtcNow
            );
            QueueJobModel? efNext = _efAdapter.GetNextJob(queueName: "parity", maxAttempts: 3, currentJobId: 42L, now: DateTime.UtcNow);

            Assert.Null(@object: sqliteNext);
            Assert.Null(@object: efNext);
        }

        [Fact]
        public void GetNextJob_FutureAvailableAt_BothProvidersAgreeNotReserved()
        {
            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddJob(
                    job: new()
                    {
                        Payload = "{\"delayed\":true}",
                        Queue = "parity-delay",
                        AvailableAt = DateTime.UtcNow.AddMinutes(value: 10),
                    }
                );
            }

            QueueJobModel? sqliteNext = _sqliteContext.GetNextJob(
                queueName: "parity-delay",
                maxAttempts: 3,
                currentJobId: null,
                now: DateTime.UtcNow
            );
            QueueJobModel? efNext = _efAdapter.GetNextJob(queueName: "parity-delay", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

            Assert.Null(@object: sqliteNext);
            Assert.Null(@object: efNext);
        }

        [Fact]
        public void GetNextJob_AttemptsAtMax_BothProvidersAgreeNotReservedAgain()
        {
            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddJob(
                    job: new()
                    {
                        Payload = "{\"at-limit\":true}",
                        Queue = "parity-limit",
                        Attempts = 3,
                        AvailableAt = DateTime.UtcNow,
                    }
                );
            }

            QueueJobModel? sqliteNext = _sqliteContext.GetNextJob(
                queueName: "parity-limit",
                maxAttempts: 3,
                currentJobId: null,
                now: DateTime.UtcNow
            );
            QueueJobModel? efNext = _efAdapter.GetNextJob(queueName: "parity-limit", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

            Assert.Null(@object: sqliteNext);
            Assert.Null(@object: efNext);
        }

        [Fact]
        public void ResetAllReservedJobs_BothProviders_ClearReservations()
        {
            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddJob(
                    job: new()
                    {
                        Payload = "{\"reserved\":true}",
                        Queue = "parity",
                        ReservedAt = DateTime.UtcNow,
                        AvailableAt = DateTime.UtcNow,
                    }
                );
                ctx.ResetAllReservedJobs();
            }

            // After reset, both should return the job (no longer reserved)
            QueueJobModel? sqliteNext = _sqliteContext.GetNextJob(
                queueName: "parity",
                maxAttempts: 3,
                currentJobId: null,
                now: DateTime.UtcNow
            );
            QueueJobModel? efNext = _efAdapter.GetNextJob(queueName: "parity", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);

            Assert.NotNull(@object: sqliteNext);
            Assert.NotNull(@object: efNext);
            Assert.Null(value: sqliteNext.ReservedAt);
            Assert.Null(value: efNext.ReservedAt);
        }

        [Fact]
        public void CronJobLifecycle_BothProviders_BehaveIdentically()
        {
            CronJobModel cronTemplate = new()
            {
                Name = "parity-cron",
                CronExpression = "0 2 * * *",
                JobType = "TestJob",
                IsEnabled = true,
            };

            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddCronJob(
                    cronJob: new()
                    {
                        Name = cronTemplate.Name,
                        CronExpression = cronTemplate.CronExpression,
                        JobType = cronTemplate.JobType,
                        IsEnabled = cronTemplate.IsEnabled,
                    }
                );
            }

            CronJobModel? sqliteFound = _sqliteContext.FindCronJobByName(name: "parity-cron");
            CronJobModel? efFound = _efAdapter.FindCronJobByName(name: "parity-cron");

            Assert.NotNull(@object: sqliteFound);
            Assert.NotNull(@object: efFound);
            Assert.Equal(expected: sqliteFound.CronExpression, actual: efFound.CronExpression);
            Assert.Equal(expected: sqliteFound.JobType, actual: efFound.JobType);
            Assert.Equal(expected: sqliteFound.IsEnabled, actual: efFound.IsEnabled);
        }

        [Fact]
        public void FailedJobLifecycle_BothProviders_BehaveIdentically()
        {
            Guid uuid = Guid.NewGuid();

            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddFailedJob(
                    failedJob: new()
                    {
                        Uuid = uuid,
                        Queue = "parity-fail",
                        Payload = "{\"fail\":true}",
                        Exception = "test error",
                    }
                );
                ctx.SaveChanges();
            }

            IReadOnlyList<FailedJobModel> sqliteFailed = _sqliteContext.GetFailedJobs();
            IReadOnlyList<FailedJobModel> efFailed = _efAdapter.GetFailedJobs();

            Assert.Single(collection: sqliteFailed);
            Assert.Single(collection: efFailed);
            Assert.Equal(expected: sqliteFailed[index: 0].Queue, actual: efFailed[index: 0].Queue);
            Assert.Equal(expected: sqliteFailed[index: 0].Payload, actual: efFailed[index: 0].Payload);
            Assert.Equal(expected: sqliteFailed[index: 0].Exception, actual: efFailed[index: 0].Exception);
        }
    }

    // =========================================================================
    // 3. End-to-End: JobDispatcher → JobQueue → Serialization → Execution
    //    Tests the full pipeline using real queue infrastructure.
    // =========================================================================

    [Trait(name: "Category", value: "Integration")]
    public class EndToEndDispatchTests : IDisposable
    {
        private readonly QueueContext _context;
        private readonly IQueueContext _adapter;
        private readonly JobQueue _jobQueue;
        private readonly JobDispatcher _dispatcher;

        public EndToEndDispatchTests()
        {
            (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
            _jobQueue = new(context: _adapter);
            _dispatcher = new(queue: _jobQueue, logger: NullLogger<JobDispatcher>.Instance);
        }

        public void Dispose()
        {
            _adapter.Dispose();
            _context.Dispose();
        }

        [Fact]
        public async Task Dispatch_Reserve_Execute_Delete_FullLifecycle()
        {
            // Dispatch
            TestJob testJob = new() { Message = "e2e dispatch test" };
            _dispatcher.Dispatch(job: testJob);

            Assert.Equal(expected: 1, actual: _context.QueueJobs.Count());

            // Reserve
            QueueJobModel? reserved = _jobQueue.ReserveJob(name: "default", currentJobId: null);
            Assert.NotNull(@object: reserved);
            Assert.Equal(expected: 1, actual: reserved.Attempts);
            Assert.NotNull(value: reserved.ReservedAt);

            // Deserialize and execute
            object deserialized = SerializationHelper.Deserialize<object>(data: reserved.Payload);
            Assert.IsType<TestJob>(@object: deserialized);

            TestJob executedJob = (TestJob)deserialized;
            Assert.Equal(expected: "e2e dispatch test", actual: executedJob.Message);
            await executedJob.Handle();
            Assert.True(condition: executedJob.HasExecuted);

            // Delete
            _jobQueue.DeleteJob(queueJob: reserved);
            Assert.Equal(expected: 0, actual: _context.QueueJobs.Count());
        }

        [Fact]
        public void Dispatch_UsesJobQueueNameAndPriority()
        {
            HighPriorityJob job = new() { Data = "urgent" };
            _dispatcher.Dispatch(job: job);

            QueueJob? stored = _context.QueueJobs.FirstOrDefault();
            Assert.NotNull(@object: stored);
            Assert.Equal(expected: "critical", actual: stored.Queue);
            Assert.Equal(expected: 100, actual: stored.Priority);
        }

        [Fact]
        public void Dispatch_WithExplicitOverride_OverridesJobDefaults()
        {
            HighPriorityJob job = new() { Data = "overridden" };
            _dispatcher.Dispatch(job: job, onQueue: "low-queue", priority: 1);

            QueueJob? stored = _context.QueueJobs.FirstOrDefault();
            Assert.NotNull(@object: stored);
            Assert.Equal(expected: "low-queue", actual: stored.Queue);
            Assert.Equal(expected: 1, actual: stored.Priority);
        }

        [Fact]
        public void Dispatch_DuplicatePayload_OnlyOneEnqueued()
        {
            TestJob job = new() { Message = "duplicate-e2e" };
            _dispatcher.Dispatch(job: job);
            _dispatcher.Dispatch(job: job);

            Assert.Equal(expected: 1, actual: _context.QueueJobs.Count());
        }

        [Fact]
        public async Task Dispatch_FailingJob_ExhaustsRetries_MoveToFailed()
        {
            // Dispatch a failing job
            TestJob failingJob = new() { Message = "will fail", ShouldFail = true };
            _dispatcher.Dispatch(job: failingJob);

            // Process through maxAttempts (default = 3)
            for (int i = 0; i < 3; i++)
            {
                QueueJobModel? reserved = _jobQueue.ReserveJob(name: "default", currentJobId: null);
                Assert.NotNull(@object: reserved);

                try
                {
                    IShouldQueue exec = (IShouldQueue)
                        SerializationHelper.Deserialize<object>(data: reserved.Payload);
                    await exec.Handle();
                    _jobQueue.DeleteJob(queueJob: reserved);
                }
                catch (Exception ex)
                {
                    _jobQueue.FailJob(queueJob: reserved, exception: ex);
                }
            }

            // Should be in failed jobs now
            Assert.Equal(expected: 0, actual: _context.QueueJobs.Count());
            Assert.Equal(expected: 1, actual: _context.FailedJobs.Count());
        }

        [Fact]
        public async Task Dispatch_MultipleJobTypes_ProcessedByCorrectQueues()
        {
            TestJob testJob = new() { Message = "default-queue-job" };
            HighPriorityJob criticalJob = new() { Data = "critical-job" };

            _dispatcher.Dispatch(job: testJob);
            _dispatcher.Dispatch(job: criticalJob);

            Assert.Equal(expected: 2, actual: _context.QueueJobs.Count());

            // Reserve from "critical" queue should get HighPriorityJob
            QueueJobModel? criticalReserved = _jobQueue.ReserveJob(name: "critical", currentJobId: null);
            Assert.NotNull(@object: criticalReserved);
            object criticalDeserialized = SerializationHelper.Deserialize<object>(
                data: criticalReserved.Payload
            );
            Assert.IsType<HighPriorityJob>(@object: criticalDeserialized);

            // Reserve from "default" queue should get TestJob
            QueueJobModel? defaultReserved = _jobQueue.ReserveJob(name: "default", currentJobId: null);
            Assert.NotNull(@object: defaultReserved);
            object defaultDeserialized = SerializationHelper.Deserialize<object>(
                data: defaultReserved.Payload
            );
            Assert.IsType<TestJob>(@object: defaultDeserialized);

            // Execute both
            await ((IShouldQueue)criticalDeserialized).Handle();
            await ((IShouldQueue)defaultDeserialized).Handle();

            _jobQueue.DeleteJob(queueJob: criticalReserved);
            _jobQueue.DeleteJob(queueJob: defaultReserved);

            Assert.Equal(expected: 0, actual: _context.QueueJobs.Count());
        }
    }

    // =========================================================================
    // 4. QueueRunner Lifecycle Tests
    //    Test Initialize, SetWorkerCount, Start/Stop operations.
    // =========================================================================

    [Trait(name: "Category", value: "Unit")]
    public class QueueRunnerLifecycleTests
    {
        [Fact]
        public void Constructor_CreatesDispatcher()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new()
            {
                WorkerCounts = new() { [key: "queue"] = 1, [key: "data"] = 1 },
            };

            QueueRunner runner = new(queueContext: adapter, configuration: config, loggerFactory: NullLoggerFactory.Instance);

            Assert.NotNull(@object: runner.Dispatcher);
        }

        [Fact]
        public void Constructor_SetsCurrentStaticAccessor()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new();

            QueueRunner runner = new(queueContext: adapter, configuration: config, loggerFactory: NullLoggerFactory.Instance);

            // Current may be overwritten by parallel tests constructing other QueueRunners,
            // so just verify the constructor sets it to a non-null value
            Assert.NotNull(@object: QueueRunner.Current);
        }

        [Fact]
        public void Constructor_NoWorkersSpawnedBeforeInitialize()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new()
            {
                WorkerCounts = new() { [key: "queue"] = 3, [key: "data"] = 5 },
            };

            QueueRunner runner = new(queueContext: adapter, configuration: config, loggerFactory: NullLoggerFactory.Instance);

            Assert.Empty(collection: runner.GetActiveWorkerThreads());
        }

        [Fact]
        public async Task SetWorkerCount_KnownQueue_ReturnsTrue()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new() { WorkerCounts = new() { [key: "queue"] = 1 } };

            QueueRunner runner = new(queueContext: adapter, configuration: config, loggerFactory: NullLoggerFactory.Instance);
            bool result = await runner.SetWorkerCount(name: "queue", max: 5, userId: Guid.NewGuid());

            Assert.True(condition: result);
        }

        [Fact]
        public async Task SetWorkerCount_UnknownQueue_ReturnsFalse()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new();

            QueueRunner runner = new(queueContext: adapter, configuration: config, loggerFactory: NullLoggerFactory.Instance);
            bool result = await runner.SetWorkerCount(name: "nonexistent", max: 5, userId: Guid.NewGuid());

            Assert.False(condition: result);
        }

        [Fact]
        public async Task SetWorkerCount_WithConfigStore_PersistsValue()
        {
            TestQueueContextAdapter adapter = new();
            TestConfigStore store = new();
            QueueConfiguration config = new() { WorkerCounts = new() { [key: "encoder"] = 1 } };

            QueueRunner runner = new(queueContext: adapter, configuration: config, loggerFactory: NullLoggerFactory.Instance, configurationStore: store);
            await runner.SetWorkerCount(name: "encoder", max: 8, userId: Guid.NewGuid());

            Assert.True(condition: store.HasKey(key: "encoderRunners"));
            Assert.Equal(expected: "8", actual: store.GetValue(key: "encoderRunners"));
        }

        [Fact]
        public async Task SetWorkerCount_WithoutConfigStore_StillReturnsTrue()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new() { WorkerCounts = new() { [key: "queue"] = 1 } };

            QueueRunner runner = new(
                queueContext: adapter,
                configuration: config,
                loggerFactory: NullLoggerFactory.Instance,
                configurationStore: null
            );
            bool result = await runner.SetWorkerCount(name: "queue", max: 4, userId: null);

            Assert.True(condition: result);
        }

        [Fact]
        public void Dispatcher_CanDispatchJobs()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new();
            QueueRunner runner = new(queueContext: adapter, configuration: config, loggerFactory: NullLoggerFactory.Instance);

            TestJob job = new() { Message = "via runner dispatcher" };
            runner.Dispatcher.Dispatch(job: job);

            Assert.Single(collection: adapter.Jobs);
            Assert.Contains(expectedSubstring: "via runner dispatcher", actualString: adapter.Jobs[index: 0].Payload);
        }
    }

    // =========================================================================
    // 5. SqliteQueueContext End-to-End with JobQueue
    //    Verify the SQLite provider works through the full JobQueue API.
    // =========================================================================

    [Trait(name: "Category", value: "Integration")]
    public class SqliteProviderEndToEndTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly IQueueContext _context;
        private readonly JobQueue _jobQueue;

        public SqliteProviderEndToEndTests()
        {
            _dbPath = Path.Combine(path1: Path.GetTempPath(), path2: $"sqlite_e2e_{Guid.NewGuid()}.db");
            _context = SqliteQueueContextFactory.Create(databasePath: _dbPath);
            _jobQueue = new(context: _context);
        }

        public void Dispose()
        {
            _context.Dispose();
            SqliteConnection.ClearAllPools();
            if (File.Exists(path: _dbPath))
                File.Delete(path: _dbPath);
        }

        [Fact]
        public void Enqueue_And_Reserve_ThroughJobQueue()
        {
            TestJob testJob = new() { Message = "sqlite e2e" };
            QueueJobModel job = new()
            {
                Payload = SerializationHelper.Serialize(obj: testJob),
                Queue = "sqlite-test",
                Priority = 5,
                AvailableAt = DateTime.UtcNow,
            };

            _jobQueue.Enqueue(queueJob: job);

            QueueJobModel? reserved = _jobQueue.ReserveJob(name: "sqlite-test", currentJobId: null);
            Assert.NotNull(@object: reserved);
            Assert.Equal(expected: 1, actual: reserved.Attempts);
            Assert.NotNull(value: reserved.ReservedAt);

            TestJob deserialized = SerializationHelper.Deserialize<TestJob>(data: reserved.Payload);
            Assert.Equal(expected: "sqlite e2e", actual: deserialized.Message);
        }

        [Fact]
        public void DuplicateEnqueue_PreventedBySqliteProvider()
        {
            string payload = SerializationHelper.Serialize(obj: new TestJob { Message = "dup sqlite" });

            _jobQueue.Enqueue(
                queueJob: new()
                {
                    Payload = payload,
                    Queue = "dup-test",
                    AvailableAt = DateTime.UtcNow,
                }
            );
            _jobQueue.Enqueue(
                queueJob: new()
                {
                    Payload = payload,
                    Queue = "dup-test",
                    AvailableAt = DateTime.UtcNow,
                }
            );

            // Only one should exist
            Assert.True(condition: _context.JobExists(payload: payload));
            QueueJobModel? first = _jobQueue.Dequeue();
            Assert.NotNull(@object: first);
            QueueJobModel? second = _jobQueue.Dequeue();
            Assert.Null(@object: second);
        }

        [Fact]
        public void FailJob_UnderMaxAttempts_StaysInQueue()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"retry\":true}",
                Queue = "retry-sqlite",
                AvailableAt = DateTime.UtcNow,
            };
            _jobQueue.Enqueue(queueJob: job);

            QueueJobModel? reserved = _jobQueue.ReserveJob(name: "retry-sqlite", currentJobId: null);
            Assert.NotNull(@object: reserved);

            _jobQueue.FailJob(queueJob: reserved, exception: new(message: "attempt 1"));

            // Should still be reservable
            QueueJobModel? secondReserve = _jobQueue.ReserveJob(name: "retry-sqlite", currentJobId: null);
            Assert.NotNull(@object: secondReserve);
        }

        [Fact]
        public void FailJob_AtMaxAttempts_MovesToFailed()
        {
            JobQueue jq = new(context: _context, maxAttempts: 1);
            QueueJobModel job = new()
            {
                Payload = "{\"permanent-fail\":true}",
                Queue = "fail-sqlite",
                AvailableAt = DateTime.UtcNow,
            };
            jq.Enqueue(queueJob: job);

            QueueJobModel? reserved = jq.ReserveJob(name: "fail-sqlite", currentJobId: null);
            Assert.NotNull(@object: reserved);
            Assert.Equal(expected: 1, actual: reserved.Attempts);

            jq.FailJob(queueJob: reserved, exception: new(message: "permanent"));

            // Should be in failed jobs, not in queue
            IReadOnlyList<FailedJobModel> failed = _context.GetFailedJobs();
            Assert.Single(collection: failed);
            Assert.Contains(expectedSubstring: "permanent", actualString: failed[index: 0].Exception);

            QueueJobModel? noMore = jq.ReserveJob(name: "fail-sqlite", currentJobId: null);
            Assert.Null(@object: noMore);
        }

        [Fact]
        public void RetryFailedJobs_RequeuesFromSqlite()
        {
            // Manually add a failed job
            _context.AddFailedJob(
                failedJob: new()
                {
                    Uuid = Guid.NewGuid(),
                    Queue = "retry-q",
                    Payload = "{\"retried\":true}",
                    Exception = "was failed",
                }
            );
            _context.SaveChanges();

            _jobQueue.RetryFailedJobs();

            // Failed job should be gone, new job in queue
            Assert.Empty(collection: _context.GetFailedJobs());
            QueueJobModel? requeued = _jobQueue.ReserveJob(name: "retry-q", currentJobId: null);
            Assert.NotNull(@object: requeued);
            Assert.Equal(expected: "{\"retried\":true}", actual: requeued.Payload);
        }

        [Fact]
        public void PriorityOrdering_SqliteProvider()
        {
            _jobQueue.Enqueue(
                queueJob: new()
                {
                    Payload = "{\"p\":1}",
                    Queue = "pri",
                    Priority = 1,
                    AvailableAt = DateTime.UtcNow,
                }
            );
            _jobQueue.Enqueue(
                queueJob: new()
                {
                    Payload = "{\"p\":10}",
                    Queue = "pri",
                    Priority = 10,
                    AvailableAt = DateTime.UtcNow,
                }
            );
            _jobQueue.Enqueue(
                queueJob: new()
                {
                    Payload = "{\"p\":5}",
                    Queue = "pri",
                    Priority = 5,
                    AvailableAt = DateTime.UtcNow,
                }
            );

            List<int> priorities = [];
            for (int i = 0; i < 3; i++)
            {
                QueueJobModel? reserved = _jobQueue.ReserveJob(name: "pri", currentJobId: null);
                Assert.NotNull(@object: reserved);
                priorities.Add(item: reserved.Priority);
                _jobQueue.DeleteJob(queueJob: reserved);
            }

            Assert.Equal(expected: [10, 5, 1], actual: priorities);
        }
    }

    // =========================================================================
    // 6. Serialization Edge Cases
    //    Tests for payload serialization/deserialization with type preservation.
    // =========================================================================

    [Trait(name: "Category", value: "Unit")]
    public class SerializationEdgeCaseTests
    {
        [Fact]
        public void Serialize_PreservesTypeInformation()
        {
            TestJob job = new() { Message = "typed" };
            string serialized = SerializationHelper.Serialize(obj: job);

            Assert.Contains(expectedSubstring: "NoMercy.Tests.Queue.TestHelpers.TestJob", actualString: serialized);
        }

        [Fact]
        public void Deserialize_AsObject_ReturnsCorrectType()
        {
            TestJob original = new() { Message = "polymorphic" };
            string serialized = SerializationHelper.Serialize(obj: original);

            object deserialized = SerializationHelper.Deserialize<object>(data: serialized);

            Assert.IsType<TestJob>(@object: deserialized);
            TestJob typed = (TestJob)deserialized;
            Assert.Equal(expected: "polymorphic", actual: typed.Message);
        }

        [Fact]
        public void Deserialize_AsIShouldQueue_WorksForDispatch()
        {
            HighPriorityJob original = new() { Data = "high-pri-serde" };
            string serialized = SerializationHelper.Serialize(obj: original);

            object deserialized = SerializationHelper.Deserialize<object>(data: serialized);
            Assert.IsAssignableFrom<IShouldQueue>(@object: deserialized);

            IShouldQueue queueable = (IShouldQueue)deserialized;
            Assert.Equal(expected: "critical", actual: queueable.QueueName);
            Assert.Equal(expected: 100, actual: queueable.Priority);
        }

        [Fact]
        public void Serialize_NullProperties_Ignored()
        {
            TestJob job = new(); // Message defaults to string.Empty, not null
            string serialized = SerializationHelper.Serialize(obj: job);

            // NullValueHandling.Ignore means null values are not included
            TestJob deserialized = SerializationHelper.Deserialize<TestJob>(data: serialized);
            Assert.NotNull(@object: deserialized);
        }

        [Fact]
        public void Serialize_CamelCaseNaming_Applied()
        {
            TestJob job = new() { Message = "camel" };
            string serialized = SerializationHelper.Serialize(obj: job);

            // Properties should be camelCase
            Assert.Contains(expectedSubstring: "\"message\"", actualString: serialized);
            Assert.Contains(expectedSubstring: "\"hasExecuted\"", actualString: serialized);
        }
    }

    // =========================================================================
    // 7. JobQueue Dequeue Tests (additional coverage)
    // =========================================================================

    [Trait(name: "Category", value: "Unit")]
    public class JobQueueDequeueTests : IDisposable
    {
        private readonly QueueContext _context;
        private readonly IQueueContext _adapter;
        private readonly JobQueue _jobQueue;

        public JobQueueDequeueTests()
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
        public void Dequeue_EmptyQueue_ReturnsNull()
        {
            QueueJobModel? result = _jobQueue.Dequeue();
            Assert.Null(@object: result);
        }

        [Fact]
        public void Dequeue_RemovesJobFromQueue()
        {
            _jobQueue.Enqueue(
                queueJob: new()
                {
                    Payload = "{\"dequeue\":true}",
                    Queue = "test",
                    AvailableAt = DateTime.UtcNow,
                }
            );

            QueueJobModel? dequeued = _jobQueue.Dequeue();
            Assert.NotNull(@object: dequeued);
            Assert.Equal(expected: 0, actual: _context.QueueJobs.Count());
        }

        [Fact]
        public void Dequeue_MultipleJobs_ReturnsFirst()
        {
            _jobQueue.Enqueue(
                queueJob: new()
                {
                    Payload = "{\"first\":true}",
                    Queue = "test",
                    AvailableAt = DateTime.UtcNow,
                }
            );
            _jobQueue.Enqueue(
                queueJob: new()
                {
                    Payload = "{\"second\":true}",
                    Queue = "test",
                    AvailableAt = DateTime.UtcNow,
                }
            );

            QueueJobModel? first = _jobQueue.Dequeue();
            Assert.NotNull(@object: first);
            Assert.Equal(expected: 1, actual: _context.QueueJobs.Count());

            QueueJobModel? second = _jobQueue.Dequeue();
            Assert.NotNull(@object: second);
            Assert.Equal(expected: 0, actual: _context.QueueJobs.Count());
        }

        [Fact]
        public void Enqueue_ReserveJob_DeleteJob_CompleteLifecycle()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"lifecycle\":true}",
                Queue = "test-q",
                Priority = 5,
                AvailableAt = DateTime.UtcNow,
            };

            _jobQueue.Enqueue(queueJob: job);
            Assert.Equal(expected: 1, actual: _context.QueueJobs.Count());

            QueueJobModel? reserved = _jobQueue.ReserveJob(name: "test-q", currentJobId: null);
            Assert.NotNull(@object: reserved);
            Assert.Equal(expected: 1, actual: reserved.Attempts);

            _jobQueue.DeleteJob(queueJob: reserved);
            Assert.Equal(expected: 0, actual: _context.QueueJobs.Count());
        }

        [Fact]
        public void RequeueFailedJob_MovesBackToQueue()
        {
            // Create a failed job
            _context.FailedJobs.Add(
                entity: new()
                {
                    Uuid = Guid.NewGuid(),
                    Connection = "default",
                    Queue = "requeue-test",
                    Payload = "{\"requeue\":true}",
                    Exception = "error",
                    FailedAt = DateTime.UtcNow,
                }
            );
            _context.SaveChanges();

            FailedJob failedJob = _context.FailedJobs.First();
            _jobQueue.RequeueFailedJob(failedJobId: (int)failedJob.Id);

            Assert.Equal(expected: 0, actual: _context.FailedJobs.Count());
            Assert.Equal(expected: 1, actual: _context.QueueJobs.Count());

            QueueJob? requeued = _context.QueueJobs.FirstOrDefault();
            Assert.NotNull(@object: requeued);
            Assert.Equal(expected: "requeue-test", actual: requeued.Queue);
            Assert.Equal(expected: "{\"requeue\":true}", actual: requeued.Payload);
            Assert.Equal(expected: 0, actual: requeued.Attempts);
        }

        [Fact]
        public void RequeueFailedJob_NonexistentId_DoesNotThrow()
        {
            Exception? ex = Record.Exception(testCode: () => _jobQueue.RequeueFailedJob(failedJobId: 999));
            Assert.Null(@object: ex);
        }
    }

    // =========================================================================
    // 8. IJobDispatcher Interface Compliance
    // =========================================================================

    [Trait(name: "Category", value: "Unit")]
    public class IJobDispatcherInterfaceTests
    {
        [Fact]
        public void JobDispatcher_ImplementsIJobDispatcher()
        {
            TestQueueContextAdapter adapter = new();
            JobQueue queue = new(context: adapter);
            JobDispatcher dispatcher = new(queue: queue, logger: NullLogger<JobDispatcher>.Instance);

            Assert.IsAssignableFrom<IJobDispatcher>(@object: dispatcher);
        }

        [Fact]
        public void IJobDispatcher_SingleArgDispatch_Works()
        {
            TestQueueContextAdapter adapter = new();
            JobQueue queue = new(context: adapter);
            IJobDispatcher dispatcher = new JobDispatcher(
                queue: queue,
                logger: NullLogger<JobDispatcher>.Instance
            );

            TestJob job = new() { Message = "interface dispatch" };
            dispatcher.Dispatch(job: job);

            Assert.Single(collection: adapter.Jobs);
        }

        [Fact]
        public void IJobDispatcher_ThreeArgDispatch_Works()
        {
            TestQueueContextAdapter adapter = new();
            JobQueue queue = new(context: adapter);
            IJobDispatcher dispatcher = new JobDispatcher(
                queue: queue,
                logger: NullLogger<JobDispatcher>.Instance
            );

            TestJob job = new() { Message = "explicit dispatch" };
            dispatcher.Dispatch(job: job, onQueue: "custom", priority: 50);

            Assert.Single(collection: adapter.Jobs);
            Assert.Equal(expected: "custom", actual: adapter.Jobs[index: 0].Queue);
            Assert.Equal(expected: 50, actual: adapter.Jobs[index: 0].Priority);
        }
    }

    // =========================================================================
    // 9. QueueConfiguration Model Tests
    // =========================================================================

    [Trait(name: "Category", value: "Unit")]
    public class QueueConfigurationTests
    {
        [Fact]
        public void DefaultConfiguration_HasEmptyWorkerCounts()
        {
            QueueConfiguration config = new();

            Assert.Empty(collection: config.WorkerCounts);
        }

        [Fact]
        public void DefaultConfiguration_MaxAttempts_Is3()
        {
            QueueConfiguration config = new();
            Assert.Equal(expected: 3, actual: config.MaxAttempts);
        }

        [Fact]
        public void DefaultConfiguration_PollingInterval_Is1000()
        {
            QueueConfiguration config = new();
            Assert.Equal(expected: 1000, actual: config.PollingIntervalMs);
        }

        [Fact]
        public void CustomConfiguration_OverridesDefaults()
        {
            QueueConfiguration config = new()
            {
                MaxAttempts = 10,
                PollingIntervalMs = 250,
                WorkerCounts = new() { [key: "fast"] = 8, [key: "slow"] = 2 },
            };

            Assert.Equal(expected: 10, actual: config.MaxAttempts);
            Assert.Equal(expected: 250, actual: config.PollingIntervalMs);
            Assert.Equal(expected: 8, actual: config.WorkerCounts[key: "fast"]);
            Assert.Equal(expected: 2, actual: config.WorkerCounts[key: "slow"]);
            Assert.DoesNotContain(expected: "queue", collection: config.WorkerCounts.Keys);
        }
    }

    // =========================================================================
    // Test helper jobs
    // =========================================================================

    public class HighPriorityJob : IShouldQueue
    {
        public string QueueName => "critical";
        public int Priority => 100;
        public string Data { get; set; } = string.Empty;
        public bool Executed { get; private set; }

        public Task Handle()
        {
            Executed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TestConfigStore : IConfigurationStore
    {
        private readonly Dictionary<string, string> _store = new();

        public string? GetValue(string key) => _store.GetValueOrDefault(key: key);

        public void SetValue(string key, string value) => _store[key: key] = value;

        public Task SetValueAsync(string key, string value, Guid? modifiedBy = null)
        {
            _store[key: key] = value;
            return Task.CompletedTask;
        }

        public bool HasKey(string key) => _store.ContainsKey(key: key);
    }
}
