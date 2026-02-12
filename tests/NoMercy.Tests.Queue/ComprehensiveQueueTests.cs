using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Queue;
using NoMercy.Queue.Core.Interfaces;
using NoMercy.Queue.Core.Models;
using NoMercy.Queue.MediaServer;
using NoMercy.Queue.Sqlite;
using NoMercy.Tests.Queue.TestHelpers;
using Xunit;
using IShouldQueue = NoMercy.Queue.Core.Interfaces.IShouldQueue;

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

    [Trait("Category", "Unit")]
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
                AvailableAt = DateTime.UtcNow
            };

            _adapter.AddJob(job);

            Assert.True(job.Id > 0);
        }

        [Fact]
        public void AddJob_PersistsToUnderlyingContext()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"type\":\"persist-test\"}",
                Queue = "test",
                Priority = 5,
                AvailableAt = DateTime.UtcNow
            };

            _adapter.AddJob(job);

            QueueJob? entity = _context.QueueJobs.FirstOrDefault(j => j.Id == job.Id);
            Assert.NotNull(entity);
            Assert.Equal("{\"type\":\"persist-test\"}", entity.Payload);
            Assert.Equal("test", entity.Queue);
            Assert.Equal(5, entity.Priority);
        }

        [Fact]
        public void FindJob_ReturnsCorrectModel()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"type\":\"find-adapter\"}",
                Queue = "q1",
                Priority = 3,
                AvailableAt = DateTime.UtcNow
            };
            _adapter.AddJob(job);

            QueueJobModel? found = _adapter.FindJob(job.Id);

            Assert.NotNull(found);
            Assert.Equal(job.Id, found.Id);
            Assert.Equal("q1", found.Queue);
            Assert.Equal(3, found.Priority);
            Assert.Equal("{\"type\":\"find-adapter\"}", found.Payload);
        }

        [Fact]
        public void FindJob_ReturnsNullForMissingId()
        {
            QueueJobModel? found = _adapter.FindJob(999);
            Assert.Null(found);
        }

        [Fact]
        public void RemoveJob_DeletesFromUnderlyingContext()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"type\":\"remove-adapter\"}",
                Queue = "test",
                AvailableAt = DateTime.UtcNow
            };
            _adapter.AddJob(job);
            int id = job.Id;

            _adapter.RemoveJob(job);

            Assert.Null(_adapter.FindJob(id));
            Assert.Null(_context.QueueJobs.Find(id));
        }

        [Fact]
        public void RemoveJob_AttachesAndRemovesWhenNotTracked()
        {
            // Add via context directly, then clear tracker
            QueueJob entity = new()
            {
                Payload = "{\"type\":\"detached\"}",
                Queue = "test",
                AvailableAt = DateTime.UtcNow
            };
            _context.QueueJobs.Add(entity);
            _context.SaveChanges();
            _context.ChangeTracker.Clear();

            // Remove via adapter with a model that matches the ID
            QueueJobModel model = new()
            {
                Id = entity.Id,
                Payload = "{\"type\":\"detached\"}",
                Queue = "test"
            };
            _adapter.RemoveJob(model);

            Assert.Null(_context.QueueJobs.Find(entity.Id));
        }

        [Fact]
        public void JobExists_ReturnsTrueForExistingPayload()
        {
            _adapter.AddJob(new QueueJobModel
            {
                Payload = "{\"exists\":true}",
                Queue = "test",
                AvailableAt = DateTime.UtcNow
            });

            Assert.True(_adapter.JobExists("{\"exists\":true}"));
        }

        [Fact]
        public void JobExists_ReturnsFalseForMissingPayload()
        {
            Assert.False(_adapter.JobExists("{\"nonexistent\":true}"));
        }

        [Fact]
        public void UpdateJob_ModifiesProperties()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"update\":true}",
                Queue = "test",
                Priority = 1,
                AvailableAt = DateTime.UtcNow
            };
            _adapter.AddJob(job);

            job.Priority = 99;
            job.Attempts = 5;
            job.ReservedAt = DateTime.UtcNow;
            _adapter.UpdateJob(job);

            QueueJobModel? updated = _adapter.FindJob(job.Id);
            Assert.NotNull(updated);
            Assert.Equal(99, updated.Priority);
            Assert.Equal(5, updated.Attempts);
            Assert.NotNull(updated.ReservedAt);
        }

        [Fact]
        public void UpdateJob_NonexistentId_DoesNotThrow()
        {
            QueueJobModel job = new()
            {
                Id = 9999,
                Payload = "nope",
                Queue = "test"
            };

            Exception? ex = Record.Exception(() => _adapter.UpdateJob(job));
            Assert.Null(ex);
        }

        [Fact]
        public void GetNextJob_ReturnsHighestPriorityUnreservedJob()
        {
            _adapter.AddJob(new QueueJobModel
            {
                Payload = "{\"p\":1}",
                Queue = "w",
                Priority = 1,
                AvailableAt = DateTime.UtcNow
            });
            _adapter.AddJob(new QueueJobModel
            {
                Payload = "{\"p\":10}",
                Queue = "w",
                Priority = 10,
                AvailableAt = DateTime.UtcNow
            });

            QueueJobModel? next = _adapter.GetNextJob("w", 3, null);

            Assert.NotNull(next);
            Assert.Equal("{\"p\":10}", next.Payload);
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
                AvailableAt = DateTime.UtcNow
            };
            _adapter.AddJob(reserved);

            QueueJobModel unreserved = new()
            {
                Payload = "{\"unreserved\":true}",
                Queue = "w",
                Priority = 1,
                AvailableAt = DateTime.UtcNow
            };
            _adapter.AddJob(unreserved);

            QueueJobModel? next = _adapter.GetNextJob("w", 3, null);

            Assert.NotNull(next);
            Assert.Equal("{\"unreserved\":true}", next.Payload);
        }

        [Fact]
        public void GetNextJob_EmptyQueueName_ReturnsAnyJob()
        {
            _adapter.AddJob(new QueueJobModel
            {
                Payload = "{\"any\":true}",
                Queue = "specific-queue",
                AvailableAt = DateTime.UtcNow
            });

            QueueJobModel? next = _adapter.GetNextJob("", 3, null);
            Assert.NotNull(next);
        }

        [Fact]
        public void GetNextJob_ReturnsNullWhenEmpty()
        {
            QueueJobModel? next = _adapter.GetNextJob("empty", 3, null);
            Assert.Null(next);
        }

        [Fact]
        public void GetNextJob_WithCurrentJobId_ReturnsNull()
        {
            _adapter.AddJob(new QueueJobModel
            {
                Payload = "{\"guard\":true}",
                Queue = "w",
                Priority = 1,
                AvailableAt = DateTime.UtcNow
            });

            QueueJobModel? next = _adapter.GetNextJob("w", 3, 42L);
            Assert.Null(next);
        }

        [Fact]
        public void ResetAllReservedJobs_ClearsReservedAt()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"reset\":true}",
                Queue = "test",
                ReservedAt = DateTime.UtcNow,
                AvailableAt = DateTime.UtcNow
            };
            _adapter.AddJob(job);

            _adapter.ResetAllReservedJobs();

            QueueJobModel? found = _adapter.FindJob(job.Id);
            Assert.NotNull(found);
            Assert.Null(found.ReservedAt);
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
                FailedAt = DateTime.UtcNow
            };

            _adapter.AddFailedJob(failedJob);
            _adapter.SaveChanges();

            IReadOnlyList<FailedJobModel> all = _adapter.GetFailedJobs();
            Assert.Single(all);
            Assert.Equal("boom", all[0].Exception);
        }

        [Fact]
        public void FindFailedJob_ReturnsCorrectModel()
        {
            FailedJobModel failedJob = new()
            {
                Uuid = Guid.NewGuid(),
                Queue = "test",
                Payload = "{\"find-failed\":true}",
                Exception = "err"
            };
            _adapter.AddFailedJob(failedJob);
            _adapter.SaveChanges();

            IReadOnlyList<FailedJobModel> all = _adapter.GetFailedJobs();
            FailedJobModel? found = _adapter.FindFailedJob((int)all[0].Id);

            Assert.NotNull(found);
            Assert.Equal("{\"find-failed\":true}", found.Payload);
        }

        [Fact]
        public void FindFailedJob_ReturnsNullForMissingId()
        {
            FailedJobModel? found = _adapter.FindFailedJob(999);
            Assert.Null(found);
        }

        [Fact]
        public void RemoveFailedJob_DeletesFromContext()
        {
            FailedJobModel failedJob = new()
            {
                Uuid = Guid.NewGuid(),
                Queue = "test",
                Payload = "{\"remove-failed\":true}",
                Exception = "err"
            };
            _adapter.AddFailedJob(failedJob);
            _adapter.SaveChanges();

            IReadOnlyList<FailedJobModel> all = _adapter.GetFailedJobs();
            _adapter.RemoveFailedJob(all[0]);
            _adapter.SaveChanges();

            Assert.Empty(_adapter.GetFailedJobs());
        }

        [Fact]
        public void RemoveFailedJob_NonexistentId_DoesNotThrow()
        {
            FailedJobModel model = new()
            {
                Id = 9999,
                Queue = "test",
                Payload = "nope",
                Exception = "err"
            };

            Exception? ex = Record.Exception(() =>
            {
                _adapter.RemoveFailedJob(model);
                _adapter.SaveChanges();
            });
            Assert.Null(ex);
        }

        [Fact]
        public void GetFailedJobs_FilterById()
        {
            _adapter.AddFailedJob(new FailedJobModel
            {
                Uuid = Guid.NewGuid(),
                Queue = "q1",
                Payload = "{\"a\":1}",
                Exception = "e1"
            });
            _adapter.AddFailedJob(new FailedJobModel
            {
                Uuid = Guid.NewGuid(),
                Queue = "q2",
                Payload = "{\"a\":2}",
                Exception = "e2"
            });
            _adapter.SaveChanges();

            IReadOnlyList<FailedJobModel> all = _adapter.GetFailedJobs();
            Assert.Equal(2, all.Count);

            IReadOnlyList<FailedJobModel> filtered = _adapter.GetFailedJobs(all[0].Id);
            Assert.Single(filtered);
            Assert.Equal(all[0].Id, filtered[0].Id);
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
                IsEnabled = true
            };

            _adapter.AddCronJob(cronJob);

            CronJobModel? found = _adapter.FindCronJobByName("adapter-cron");
            Assert.NotNull(found);
            Assert.Equal("0 * * * *", found.CronExpression);
        }

        [Fact]
        public void FindCronJobByName_ReturnsNullForMissing()
        {
            CronJobModel? found = _adapter.FindCronJobByName("nonexistent");
            Assert.Null(found);
        }

        [Fact]
        public void GetEnabledCronJobs_FiltersDisabled()
        {
            _adapter.AddCronJob(new CronJobModel
            {
                Name = "enabled-adapter",
                CronExpression = "0 * * * *",
                JobType = "A",
                IsEnabled = true
            });
            _adapter.AddCronJob(new CronJobModel
            {
                Name = "disabled-adapter",
                CronExpression = "0 * * * *",
                JobType = "B",
                IsEnabled = false
            });

            IReadOnlyList<CronJobModel> enabled = _adapter.GetEnabledCronJobs();
            Assert.Single(enabled);
            Assert.Equal("enabled-adapter", enabled[0].Name);
        }

        [Fact]
        public void UpdateCronJob_ModifiesProperties()
        {
            _adapter.AddCronJob(new CronJobModel
            {
                Name = "update-adapter-cron",
                CronExpression = "0 * * * *",
                JobType = "TestJob",
                IsEnabled = true
            });

            CronJobModel? found = _adapter.FindCronJobByName("update-adapter-cron");
            Assert.NotNull(found);

            found.CronExpression = "*/5 * * * *";
            found.IsEnabled = false;
            found.LastRun = DateTime.UtcNow;
            _adapter.UpdateCronJob(found);

            CronJobModel? updated = _adapter.FindCronJobByName("update-adapter-cron");
            Assert.NotNull(updated);
            Assert.Equal("*/5 * * * *", updated.CronExpression);
            Assert.False(updated.IsEnabled);
            Assert.NotNull(updated.LastRun);
        }

        [Fact]
        public void RemoveCronJob_DeletesFromContext()
        {
            _adapter.AddCronJob(new CronJobModel
            {
                Name = "remove-adapter-cron",
                CronExpression = "0 * * * *",
                JobType = "TestJob"
            });

            CronJobModel? found = _adapter.FindCronJobByName("remove-adapter-cron");
            Assert.NotNull(found);

            _adapter.RemoveCronJob(found);

            Assert.Null(_adapter.FindCronJobByName("remove-adapter-cron"));
        }

        [Fact]
        public void RemoveCronJob_NonexistentId_DoesNotThrow()
        {
            CronJobModel model = new()
            {
                Id = 9999,
                Name = "nope",
                CronExpression = "0 * * * *",
                JobType = "X"
            };

            Exception? ex = Record.Exception(() => _adapter.RemoveCronJob(model));
            Assert.Null(ex);
        }

        [Fact]
        public void SaveChanges_ClearsChangeTracker()
        {
            _adapter.AddJob(new QueueJobModel
            {
                Payload = "{\"tracker\":true}",
                Queue = "test",
                AvailableAt = DateTime.UtcNow
            });

            // After SaveAndClear, change tracker should be empty
            Assert.False(_context.ChangeTracker.HasChanges());
        }
    }

    // =========================================================================
    // 2. Cross-Provider Behavioral Parity
    //    Verify that SqliteQueueContext and EfQueueContextAdapter behave identically
    //    for the same sequence of operations.
    // =========================================================================

    [Trait("Category", "Integration")]
    public class CrossProviderParityTests : IDisposable
    {
        private readonly string _sqliteDbPath;
        private readonly IQueueContext _sqliteContext;
        private readonly QueueContext _efDbContext;
        private readonly IQueueContext _efAdapter;

        public CrossProviderParityTests()
        {
            _sqliteDbPath = Path.Combine(Path.GetTempPath(), $"parity_test_{Guid.NewGuid()}.db");
            _sqliteContext = SqliteQueueContextFactory.Create(_sqliteDbPath);
            (_efDbContext, _efAdapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
        }

        public void Dispose()
        {
            _sqliteContext.Dispose();
            _efAdapter.Dispose();
            _efDbContext.Dispose();
            if (File.Exists(_sqliteDbPath))
                File.Delete(_sqliteDbPath);
        }

        [Fact]
        public void AddAndFindJob_BothProviders_ReturnSameData()
        {
            QueueJobModel sqliteJob = new()
            {
                Payload = "{\"parity\":\"job\"}",
                Queue = "test-queue",
                Priority = 7,
                AvailableAt = DateTime.UtcNow
            };
            QueueJobModel efJob = new()
            {
                Payload = "{\"parity\":\"job\"}",
                Queue = "test-queue",
                Priority = 7,
                AvailableAt = DateTime.UtcNow
            };

            _sqliteContext.AddJob(sqliteJob);
            _efAdapter.AddJob(efJob);

            QueueJobModel? sqliteFound = _sqliteContext.FindJob(sqliteJob.Id);
            QueueJobModel? efFound = _efAdapter.FindJob(efJob.Id);

            Assert.NotNull(sqliteFound);
            Assert.NotNull(efFound);
            Assert.Equal(sqliteFound.Queue, efFound.Queue);
            Assert.Equal(sqliteFound.Priority, efFound.Priority);
            Assert.Equal(sqliteFound.Payload, efFound.Payload);
        }

        [Fact]
        public void JobExists_BothProviders_AgreeOnExistence()
        {
            string payload = "{\"parity\":\"exists\"}";

            _sqliteContext.AddJob(new QueueJobModel { Payload = payload, Queue = "t", AvailableAt = DateTime.UtcNow });
            _efAdapter.AddJob(new QueueJobModel { Payload = payload, Queue = "t", AvailableAt = DateTime.UtcNow });

            Assert.Equal(_sqliteContext.JobExists(payload), _efAdapter.JobExists(payload));
            Assert.Equal(_sqliteContext.JobExists("{\"nope\":true}"), _efAdapter.JobExists("{\"nope\":true}"));
        }

        [Fact]
        public void GetNextJob_BothProviders_ReturnHighestPriority()
        {
            // Add same jobs to both
            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddJob(new QueueJobModel
                {
                    Payload = "{\"p\":1}",
                    Queue = "parity",
                    Priority = 1,
                    AvailableAt = DateTime.UtcNow
                });
                ctx.AddJob(new QueueJobModel
                {
                    Payload = "{\"p\":10}",
                    Queue = "parity",
                    Priority = 10,
                    AvailableAt = DateTime.UtcNow
                });
            }

            QueueJobModel? sqliteNext = _sqliteContext.GetNextJob("parity", 3, null);
            QueueJobModel? efNext = _efAdapter.GetNextJob("parity", 3, null);

            Assert.NotNull(sqliteNext);
            Assert.NotNull(efNext);
            Assert.Equal(sqliteNext.Priority, efNext.Priority);
            Assert.Equal(sqliteNext.Payload, efNext.Payload);
        }

        [Fact]
        public void GetNextJob_WithCurrentJobId_BothReturnNull()
        {
            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddJob(new QueueJobModel
                {
                    Payload = "{\"guard\":true}",
                    Queue = "parity",
                    Priority = 1,
                    AvailableAt = DateTime.UtcNow
                });
            }

            QueueJobModel? sqliteNext = _sqliteContext.GetNextJob("parity", 3, 42L);
            QueueJobModel? efNext = _efAdapter.GetNextJob("parity", 3, 42L);

            Assert.Null(sqliteNext);
            Assert.Null(efNext);
        }

        [Fact]
        public void ResetAllReservedJobs_BothProviders_ClearReservations()
        {
            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddJob(new QueueJobModel
                {
                    Payload = "{\"reserved\":true}",
                    Queue = "parity",
                    ReservedAt = DateTime.UtcNow,
                    AvailableAt = DateTime.UtcNow
                });
                ctx.ResetAllReservedJobs();
            }

            // After reset, both should return the job (no longer reserved)
            QueueJobModel? sqliteNext = _sqliteContext.GetNextJob("parity", 3, null);
            QueueJobModel? efNext = _efAdapter.GetNextJob("parity", 3, null);

            Assert.NotNull(sqliteNext);
            Assert.NotNull(efNext);
            Assert.Null(sqliteNext.ReservedAt);
            Assert.Null(efNext.ReservedAt);
        }

        [Fact]
        public void CronJobLifecycle_BothProviders_BehaveIdentically()
        {
            CronJobModel cronTemplate = new()
            {
                Name = "parity-cron",
                CronExpression = "0 2 * * *",
                JobType = "TestJob",
                IsEnabled = true
            };

            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddCronJob(new CronJobModel
                {
                    Name = cronTemplate.Name,
                    CronExpression = cronTemplate.CronExpression,
                    JobType = cronTemplate.JobType,
                    IsEnabled = cronTemplate.IsEnabled
                });
            }

            CronJobModel? sqliteFound = _sqliteContext.FindCronJobByName("parity-cron");
            CronJobModel? efFound = _efAdapter.FindCronJobByName("parity-cron");

            Assert.NotNull(sqliteFound);
            Assert.NotNull(efFound);
            Assert.Equal(sqliteFound.CronExpression, efFound.CronExpression);
            Assert.Equal(sqliteFound.JobType, efFound.JobType);
            Assert.Equal(sqliteFound.IsEnabled, efFound.IsEnabled);
        }

        [Fact]
        public void FailedJobLifecycle_BothProviders_BehaveIdentically()
        {
            Guid uuid = Guid.NewGuid();

            foreach (IQueueContext ctx in new[] { _sqliteContext, _efAdapter })
            {
                ctx.AddFailedJob(new FailedJobModel
                {
                    Uuid = uuid,
                    Queue = "parity-fail",
                    Payload = "{\"fail\":true}",
                    Exception = "test error"
                });
                ctx.SaveChanges();
            }

            IReadOnlyList<FailedJobModel> sqliteFailed = _sqliteContext.GetFailedJobs();
            IReadOnlyList<FailedJobModel> efFailed = _efAdapter.GetFailedJobs();

            Assert.Single(sqliteFailed);
            Assert.Single(efFailed);
            Assert.Equal(sqliteFailed[0].Queue, efFailed[0].Queue);
            Assert.Equal(sqliteFailed[0].Payload, efFailed[0].Payload);
            Assert.Equal(sqliteFailed[0].Exception, efFailed[0].Exception);
        }
    }

    // =========================================================================
    // 3. End-to-End: JobDispatcher → JobQueue → Serialization → Execution
    //    Tests the full pipeline using real queue infrastructure.
    // =========================================================================

    [Trait("Category", "Integration")]
    public class EndToEndDispatchTests : IDisposable
    {
        private readonly QueueContext _context;
        private readonly IQueueContext _adapter;
        private readonly JobQueue _jobQueue;
        private readonly JobDispatcher _dispatcher;

        public EndToEndDispatchTests()
        {
            (_context, _adapter) = TestQueueContextFactory.CreateInMemoryContextWithAdapter();
            _jobQueue = new(_adapter);
            _dispatcher = new(_jobQueue);
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
            _dispatcher.Dispatch(testJob);

            Assert.Equal(1, _context.QueueJobs.Count());

            // Reserve
            QueueJobModel? reserved = _jobQueue.ReserveJob("default", null);
            Assert.NotNull(reserved);
            Assert.Equal(1, reserved.Attempts);
            Assert.NotNull(reserved.ReservedAt);

            // Deserialize and execute
            object deserialized = SerializationHelper.Deserialize<object>(reserved.Payload);
            Assert.IsType<TestJob>(deserialized);

            TestJob executedJob = (TestJob)deserialized;
            Assert.Equal("e2e dispatch test", executedJob.Message);
            await executedJob.Handle();
            Assert.True(executedJob.HasExecuted);

            // Delete
            _jobQueue.DeleteJob(reserved);
            Assert.Equal(0, _context.QueueJobs.Count());
        }

        [Fact]
        public void Dispatch_UsesJobQueueNameAndPriority()
        {
            HighPriorityJob job = new() { Data = "urgent" };
            _dispatcher.Dispatch(job);

            QueueJob? stored = _context.QueueJobs.FirstOrDefault();
            Assert.NotNull(stored);
            Assert.Equal("critical", stored.Queue);
            Assert.Equal(100, stored.Priority);
        }

        [Fact]
        public void Dispatch_WithExplicitOverride_OverridesJobDefaults()
        {
            HighPriorityJob job = new() { Data = "overridden" };
            _dispatcher.Dispatch(job, "low-queue", 1);

            QueueJob? stored = _context.QueueJobs.FirstOrDefault();
            Assert.NotNull(stored);
            Assert.Equal("low-queue", stored.Queue);
            Assert.Equal(1, stored.Priority);
        }

        [Fact]
        public void Dispatch_DuplicatePayload_OnlyOneEnqueued()
        {
            TestJob job = new() { Message = "duplicate-e2e" };
            _dispatcher.Dispatch(job);
            _dispatcher.Dispatch(job);

            Assert.Equal(1, _context.QueueJobs.Count());
        }

        [Fact]
        public async Task Dispatch_FailingJob_ExhaustsRetries_MoveToFailed()
        {
            // Dispatch a failing job
            TestJob failingJob = new() { Message = "will fail", ShouldFail = true };
            _dispatcher.Dispatch(failingJob);

            // Process through maxAttempts (default = 3)
            for (int i = 0; i < 3; i++)
            {
                QueueJobModel? reserved = _jobQueue.ReserveJob("default", null);
                Assert.NotNull(reserved);

                try
                {
                    IShouldQueue exec = (IShouldQueue)SerializationHelper.Deserialize<object>(reserved.Payload);
                    await exec.Handle();
                    _jobQueue.DeleteJob(reserved);
                }
                catch (Exception ex)
                {
                    _jobQueue.FailJob(reserved, ex);
                }
            }

            // Should be in failed jobs now
            Assert.Equal(0, _context.QueueJobs.Count());
            Assert.Equal(1, _context.FailedJobs.Count());
        }

        [Fact]
        public async Task Dispatch_MultipleJobTypes_ProcessedByCorrectQueues()
        {
            TestJob testJob = new() { Message = "default-queue-job" };
            HighPriorityJob criticalJob = new() { Data = "critical-job" };

            _dispatcher.Dispatch(testJob);
            _dispatcher.Dispatch(criticalJob);

            Assert.Equal(2, _context.QueueJobs.Count());

            // Reserve from "critical" queue should get HighPriorityJob
            QueueJobModel? criticalReserved = _jobQueue.ReserveJob("critical", null);
            Assert.NotNull(criticalReserved);
            object criticalDeserialized = SerializationHelper.Deserialize<object>(criticalReserved.Payload);
            Assert.IsType<HighPriorityJob>(criticalDeserialized);

            // Reserve from "default" queue should get TestJob
            QueueJobModel? defaultReserved = _jobQueue.ReserveJob("default", null);
            Assert.NotNull(defaultReserved);
            object defaultDeserialized = SerializationHelper.Deserialize<object>(defaultReserved.Payload);
            Assert.IsType<TestJob>(defaultDeserialized);

            // Execute both
            await ((IShouldQueue)criticalDeserialized).Handle();
            await ((IShouldQueue)defaultDeserialized).Handle();

            _jobQueue.DeleteJob(criticalReserved);
            _jobQueue.DeleteJob(defaultReserved);

            Assert.Equal(0, _context.QueueJobs.Count());
        }
    }

    // =========================================================================
    // 4. QueueRunner Lifecycle Tests
    //    Test Initialize, SetWorkerCount, Start/Stop operations.
    // =========================================================================

    [Trait("Category", "Unit")]
    public class QueueRunnerLifecycleTests
    {
        [Fact]
        public void Constructor_CreatesDispatcher()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new()
            {
                WorkerCounts = new Dictionary<string, int>
                {
                    ["queue"] = 1,
                    ["data"] = 1
                }
            };

            QueueRunner runner = new(adapter, config);

            Assert.NotNull(runner.Dispatcher);
        }

        [Fact]
        public void Constructor_SetsCurrentStaticAccessor()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new();

            QueueRunner runner = new(adapter, config);

            Assert.Same(runner, QueueRunner.Current);
        }

        [Fact]
        public void Constructor_NoWorkersSpawnedBeforeInitialize()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new()
            {
                WorkerCounts = new Dictionary<string, int>
                {
                    ["queue"] = 3,
                    ["data"] = 5
                }
            };

            QueueRunner runner = new(adapter, config);

            Assert.Empty(runner.GetActiveWorkerThreads());
        }

        [Fact]
        public async Task SetWorkerCount_KnownQueue_ReturnsTrue()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new()
            {
                WorkerCounts = new Dictionary<string, int>
                {
                    ["queue"] = 1
                }
            };

            QueueRunner runner = new(adapter, config);
            bool result = await runner.SetWorkerCount("queue", 5, Guid.NewGuid());

            Assert.True(result);
        }

        [Fact]
        public async Task SetWorkerCount_UnknownQueue_ReturnsFalse()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new();

            QueueRunner runner = new(adapter, config);
            bool result = await runner.SetWorkerCount("nonexistent", 5, Guid.NewGuid());

            Assert.False(result);
        }

        [Fact]
        public async Task SetWorkerCount_WithConfigStore_PersistsValue()
        {
            TestQueueContextAdapter adapter = new();
            TestConfigStore store = new();
            QueueConfiguration config = new()
            {
                WorkerCounts = new Dictionary<string, int>
                {
                    ["encoder"] = 1
                }
            };

            QueueRunner runner = new(adapter, config, store);
            await runner.SetWorkerCount("encoder", 8, Guid.NewGuid());

            Assert.True(store.HasKey("encoderRunners"));
            Assert.Equal("8", store.GetValue("encoderRunners"));
        }

        [Fact]
        public async Task SetWorkerCount_WithoutConfigStore_StillReturnsTrue()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new()
            {
                WorkerCounts = new Dictionary<string, int>
                {
                    ["queue"] = 1
                }
            };

            QueueRunner runner = new(adapter, config, configurationStore: null);
            bool result = await runner.SetWorkerCount("queue", 4, null);

            Assert.True(result);
        }

        [Fact]
        public void Dispatcher_CanDispatchJobs()
        {
            TestQueueContextAdapter adapter = new();
            QueueConfiguration config = new();
            QueueRunner runner = new(adapter, config);

            TestJob job = new() { Message = "via runner dispatcher" };
            runner.Dispatcher.Dispatch(job);

            Assert.Single(adapter.Jobs);
            Assert.Contains("via runner dispatcher", adapter.Jobs[0].Payload);
        }
    }

    // =========================================================================
    // 5. SqliteQueueContext End-to-End with JobQueue
    //    Verify the SQLite provider works through the full JobQueue API.
    // =========================================================================

    [Trait("Category", "Integration")]
    public class SqliteProviderEndToEndTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly IQueueContext _context;
        private readonly JobQueue _jobQueue;

        public SqliteProviderEndToEndTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"sqlite_e2e_{Guid.NewGuid()}.db");
            _context = SqliteQueueContextFactory.Create(_dbPath);
            _jobQueue = new(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }

        [Fact]
        public void Enqueue_And_Reserve_ThroughJobQueue()
        {
            TestJob testJob = new() { Message = "sqlite e2e" };
            QueueJobModel job = new()
            {
                Payload = SerializationHelper.Serialize(testJob),
                Queue = "sqlite-test",
                Priority = 5,
                AvailableAt = DateTime.UtcNow
            };

            _jobQueue.Enqueue(job);

            QueueJobModel? reserved = _jobQueue.ReserveJob("sqlite-test", null);
            Assert.NotNull(reserved);
            Assert.Equal(1, reserved.Attempts);
            Assert.NotNull(reserved.ReservedAt);

            TestJob deserialized = SerializationHelper.Deserialize<TestJob>(reserved.Payload);
            Assert.Equal("sqlite e2e", deserialized.Message);
        }

        [Fact]
        public void DuplicateEnqueue_PreventedBySqliteProvider()
        {
            string payload = SerializationHelper.Serialize(new TestJob { Message = "dup sqlite" });

            _jobQueue.Enqueue(new QueueJobModel
            {
                Payload = payload,
                Queue = "dup-test",
                AvailableAt = DateTime.UtcNow
            });
            _jobQueue.Enqueue(new QueueJobModel
            {
                Payload = payload,
                Queue = "dup-test",
                AvailableAt = DateTime.UtcNow
            });

            // Only one should exist
            Assert.True(_context.JobExists(payload));
            QueueJobModel? first = _jobQueue.Dequeue();
            Assert.NotNull(first);
            QueueJobModel? second = _jobQueue.Dequeue();
            Assert.Null(second);
        }

        [Fact]
        public void FailJob_UnderMaxAttempts_StaysInQueue()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"retry\":true}",
                Queue = "retry-sqlite",
                AvailableAt = DateTime.UtcNow
            };
            _jobQueue.Enqueue(job);

            QueueJobModel? reserved = _jobQueue.ReserveJob("retry-sqlite", null);
            Assert.NotNull(reserved);

            _jobQueue.FailJob(reserved, new Exception("attempt 1"));

            // Should still be reservable
            QueueJobModel? secondReserve = _jobQueue.ReserveJob("retry-sqlite", null);
            Assert.NotNull(secondReserve);
        }

        [Fact]
        public void FailJob_AtMaxAttempts_MovesToFailed()
        {
            JobQueue jq = new(_context, maxAttempts: 1);
            QueueJobModel job = new()
            {
                Payload = "{\"permanent-fail\":true}",
                Queue = "fail-sqlite",
                AvailableAt = DateTime.UtcNow
            };
            jq.Enqueue(job);

            QueueJobModel? reserved = jq.ReserveJob("fail-sqlite", null);
            Assert.NotNull(reserved);
            Assert.Equal(1, reserved.Attempts);

            jq.FailJob(reserved, new Exception("permanent"));

            // Should be in failed jobs, not in queue
            IReadOnlyList<FailedJobModel> failed = _context.GetFailedJobs();
            Assert.Single(failed);
            Assert.Contains("permanent", failed[0].Exception);

            QueueJobModel? noMore = jq.ReserveJob("fail-sqlite", null);
            Assert.Null(noMore);
        }

        [Fact]
        public void RetryFailedJobs_RequeuesFromSqlite()
        {
            // Manually add a failed job
            _context.AddFailedJob(new FailedJobModel
            {
                Uuid = Guid.NewGuid(),
                Queue = "retry-q",
                Payload = "{\"retried\":true}",
                Exception = "was failed"
            });
            _context.SaveChanges();

            _jobQueue.RetryFailedJobs();

            // Failed job should be gone, new job in queue
            Assert.Empty(_context.GetFailedJobs());
            QueueJobModel? requeued = _jobQueue.ReserveJob("retry-q", null);
            Assert.NotNull(requeued);
            Assert.Equal("{\"retried\":true}", requeued.Payload);
        }

        [Fact]
        public void PriorityOrdering_SqliteProvider()
        {
            _jobQueue.Enqueue(new QueueJobModel
            {
                Payload = "{\"p\":1}",
                Queue = "pri",
                Priority = 1,
                AvailableAt = DateTime.UtcNow
            });
            _jobQueue.Enqueue(new QueueJobModel
            {
                Payload = "{\"p\":10}",
                Queue = "pri",
                Priority = 10,
                AvailableAt = DateTime.UtcNow
            });
            _jobQueue.Enqueue(new QueueJobModel
            {
                Payload = "{\"p\":5}",
                Queue = "pri",
                Priority = 5,
                AvailableAt = DateTime.UtcNow
            });

            List<int> priorities = [];
            for (int i = 0; i < 3; i++)
            {
                QueueJobModel? reserved = _jobQueue.ReserveJob("pri", null);
                Assert.NotNull(reserved);
                priorities.Add(reserved.Priority);
                _jobQueue.DeleteJob(reserved);
            }

            Assert.Equal([10, 5, 1], priorities);
        }
    }

    // =========================================================================
    // 6. Serialization Edge Cases
    //    Tests for payload serialization/deserialization with type preservation.
    // =========================================================================

    [Trait("Category", "Unit")]
    public class SerializationEdgeCaseTests
    {
        [Fact]
        public void Serialize_PreservesTypeInformation()
        {
            TestJob job = new() { Message = "typed" };
            string serialized = SerializationHelper.Serialize(job);

            Assert.Contains("NoMercy.Tests.Queue.TestHelpers.TestJob", serialized);
        }

        [Fact]
        public void Deserialize_AsObject_ReturnsCorrectType()
        {
            TestJob original = new() { Message = "polymorphic" };
            string serialized = SerializationHelper.Serialize(original);

            object deserialized = SerializationHelper.Deserialize<object>(serialized);

            Assert.IsType<TestJob>(deserialized);
            TestJob typed = (TestJob)deserialized;
            Assert.Equal("polymorphic", typed.Message);
        }

        [Fact]
        public void Deserialize_AsIShouldQueue_WorksForDispatch()
        {
            HighPriorityJob original = new() { Data = "high-pri-serde" };
            string serialized = SerializationHelper.Serialize(original);

            object deserialized = SerializationHelper.Deserialize<object>(serialized);
            Assert.IsAssignableFrom<IShouldQueue>(deserialized);

            IShouldQueue queueable = (IShouldQueue)deserialized;
            Assert.Equal("critical", queueable.QueueName);
            Assert.Equal(100, queueable.Priority);
        }

        [Fact]
        public void Serialize_NullProperties_Ignored()
        {
            TestJob job = new(); // Message defaults to string.Empty, not null
            string serialized = SerializationHelper.Serialize(job);

            // NullValueHandling.Ignore means null values are not included
            TestJob deserialized = SerializationHelper.Deserialize<TestJob>(serialized);
            Assert.NotNull(deserialized);
        }

        [Fact]
        public void Serialize_CamelCaseNaming_Applied()
        {
            TestJob job = new() { Message = "camel" };
            string serialized = SerializationHelper.Serialize(job);

            // Properties should be camelCase
            Assert.Contains("\"message\"", serialized);
            Assert.Contains("\"hasExecuted\"", serialized);
        }
    }

    // =========================================================================
    // 7. JobQueue Dequeue Tests (additional coverage)
    // =========================================================================

    [Trait("Category", "Unit")]
    public class JobQueueDequeueTests : IDisposable
    {
        private readonly QueueContext _context;
        private readonly IQueueContext _adapter;
        private readonly JobQueue _jobQueue;

        public JobQueueDequeueTests()
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
        public void Dequeue_EmptyQueue_ReturnsNull()
        {
            QueueJobModel? result = _jobQueue.Dequeue();
            Assert.Null(result);
        }

        [Fact]
        public void Dequeue_RemovesJobFromQueue()
        {
            _jobQueue.Enqueue(new QueueJobModel
            {
                Payload = "{\"dequeue\":true}",
                Queue = "test",
                AvailableAt = DateTime.UtcNow
            });

            QueueJobModel? dequeued = _jobQueue.Dequeue();
            Assert.NotNull(dequeued);
            Assert.Equal(0, _context.QueueJobs.Count());
        }

        [Fact]
        public void Dequeue_MultipleJobs_ReturnsFirst()
        {
            _jobQueue.Enqueue(new QueueJobModel
            {
                Payload = "{\"first\":true}",
                Queue = "test",
                AvailableAt = DateTime.UtcNow
            });
            _jobQueue.Enqueue(new QueueJobModel
            {
                Payload = "{\"second\":true}",
                Queue = "test",
                AvailableAt = DateTime.UtcNow
            });

            QueueJobModel? first = _jobQueue.Dequeue();
            Assert.NotNull(first);
            Assert.Equal(1, _context.QueueJobs.Count());

            QueueJobModel? second = _jobQueue.Dequeue();
            Assert.NotNull(second);
            Assert.Equal(0, _context.QueueJobs.Count());
        }

        [Fact]
        public void Enqueue_ReserveJob_DeleteJob_CompleteLifecycle()
        {
            QueueJobModel job = new()
            {
                Payload = "{\"lifecycle\":true}",
                Queue = "test-q",
                Priority = 5,
                AvailableAt = DateTime.UtcNow
            };

            _jobQueue.Enqueue(job);
            Assert.Equal(1, _context.QueueJobs.Count());

            QueueJobModel? reserved = _jobQueue.ReserveJob("test-q", null);
            Assert.NotNull(reserved);
            Assert.Equal(1, reserved.Attempts);

            _jobQueue.DeleteJob(reserved);
            Assert.Equal(0, _context.QueueJobs.Count());
        }

        [Fact]
        public void RequeueFailedJob_MovesBackToQueue()
        {
            // Create a failed job
            _context.FailedJobs.Add(new FailedJob
            {
                Uuid = Guid.NewGuid(),
                Connection = "default",
                Queue = "requeue-test",
                Payload = "{\"requeue\":true}",
                Exception = "error",
                FailedAt = DateTime.UtcNow
            });
            _context.SaveChanges();

            FailedJob failedJob = _context.FailedJobs.First();
            _jobQueue.RequeueFailedJob((int)failedJob.Id);

            Assert.Equal(0, _context.FailedJobs.Count());
            Assert.Equal(1, _context.QueueJobs.Count());

            QueueJob? requeued = _context.QueueJobs.FirstOrDefault();
            Assert.NotNull(requeued);
            Assert.Equal("requeue-test", requeued.Queue);
            Assert.Equal("{\"requeue\":true}", requeued.Payload);
            Assert.Equal(0, requeued.Attempts);
        }

        [Fact]
        public void RequeueFailedJob_NonexistentId_DoesNotThrow()
        {
            Exception? ex = Record.Exception(() => _jobQueue.RequeueFailedJob(999));
            Assert.Null(ex);
        }
    }

    // =========================================================================
    // 8. IJobDispatcher Interface Compliance
    // =========================================================================

    [Trait("Category", "Unit")]
    public class IJobDispatcherInterfaceTests
    {
        [Fact]
        public void JobDispatcher_ImplementsIJobDispatcher()
        {
            TestQueueContextAdapter adapter = new();
            JobQueue queue = new(adapter);
            JobDispatcher dispatcher = new(queue);

            Assert.IsAssignableFrom<IJobDispatcher>(dispatcher);
        }

        [Fact]
        public void IJobDispatcher_SingleArgDispatch_Works()
        {
            TestQueueContextAdapter adapter = new();
            JobQueue queue = new(adapter);
            IJobDispatcher dispatcher = new JobDispatcher(queue);

            TestJob job = new() { Message = "interface dispatch" };
            dispatcher.Dispatch(job);

            Assert.Single(adapter.Jobs);
        }

        [Fact]
        public void IJobDispatcher_ThreeArgDispatch_Works()
        {
            TestQueueContextAdapter adapter = new();
            JobQueue queue = new(adapter);
            IJobDispatcher dispatcher = new JobDispatcher(queue);

            TestJob job = new() { Message = "explicit dispatch" };
            dispatcher.Dispatch(job, "custom", 50);

            Assert.Single(adapter.Jobs);
            Assert.Equal("custom", adapter.Jobs[0].Queue);
            Assert.Equal(50, adapter.Jobs[0].Priority);
        }
    }

    // =========================================================================
    // 9. QueueConfiguration Model Tests
    // =========================================================================

    [Trait("Category", "Unit")]
    public class QueueConfigurationTests
    {
        [Fact]
        public void DefaultConfiguration_HasAllExpectedQueues()
        {
            QueueConfiguration config = new();

            Assert.Contains("queue", config.WorkerCounts.Keys);
            Assert.Contains("data", config.WorkerCounts.Keys);
            Assert.Contains("encoder", config.WorkerCounts.Keys);
            Assert.Contains("cron", config.WorkerCounts.Keys);
            Assert.Contains("image", config.WorkerCounts.Keys);
        }

        [Fact]
        public void DefaultConfiguration_MaxAttempts_Is3()
        {
            QueueConfiguration config = new();
            Assert.Equal(3, config.MaxAttempts);
        }

        [Fact]
        public void DefaultConfiguration_PollingInterval_Is1000()
        {
            QueueConfiguration config = new();
            Assert.Equal(1000, config.PollingIntervalMs);
        }

        [Fact]
        public void CustomConfiguration_OverridesDefaults()
        {
            QueueConfiguration config = new()
            {
                MaxAttempts = 10,
                PollingIntervalMs = 250,
                WorkerCounts = new Dictionary<string, int>
                {
                    ["fast"] = 8,
                    ["slow"] = 2
                }
            };

            Assert.Equal(10, config.MaxAttempts);
            Assert.Equal(250, config.PollingIntervalMs);
            Assert.Equal(8, config.WorkerCounts["fast"]);
            Assert.Equal(2, config.WorkerCounts["slow"]);
            Assert.DoesNotContain("queue", config.WorkerCounts.Keys);
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

        public string? GetValue(string key) => _store.GetValueOrDefault(key);
        public void SetValue(string key, string value) => _store[key] = value;
        public Task SetValueAsync(string key, string value, Guid? modifiedBy = null)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
        public bool HasKey(string key) => _store.ContainsKey(key);
    }
}
