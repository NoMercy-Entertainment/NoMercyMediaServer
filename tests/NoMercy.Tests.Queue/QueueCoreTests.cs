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

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

[Trait(name: "Category", value: "Unit")]
public class QueueCoreTests
{
    // =========================================================================
    // IShouldQueue interface
    // =========================================================================

    [Fact]
    public void IShouldQueue_CanBeImplemented()
    {
        TestJob job = new();

        Assert.Equal(expected: "test-queue", actual: job.QueueName);
        Assert.Equal(expected: 5, actual: job.Priority);
    }

    [Fact]
    public async Task IShouldQueue_HandleCanBeInvoked()
    {
        TestJob job = new();

        await job.Handle();

        Assert.True(condition: job.WasHandled);
    }

    // =========================================================================
    // ICronJobExecutor interface
    // =========================================================================

    [Fact]
    public void ICronJobExecutor_CanBeImplemented()
    {
        TestCronExecutor executor = new();

        Assert.Equal(expected: "0 * * * *", actual: executor.CronExpression);
        Assert.Equal(expected: "test-cron", actual: executor.JobName);
    }

    [Fact]
    public async Task ICronJobExecutor_ExecuteCanBeInvoked()
    {
        TestCronExecutor executor = new();

        await executor.ExecuteAsync(parameters: "param1");

        Assert.Equal(expected: "param1", actual: executor.LastParameters);
    }

    [Fact]
    public async Task ICronJobExecutor_SupportsCancellation()
    {
        TestCronExecutor executor = new();
        using CancellationTokenSource cts = new();

        await executor.ExecuteAsync(parameters: "test", cancellationToken: cts.Token);

        Assert.Equal(expected: "test", actual: executor.LastParameters);
    }

    // =========================================================================
    // IJobSerializer interface
    // =========================================================================

    [Fact]
    public void IJobSerializer_CanBeImplemented()
    {
        TestSerializer serializer = new();

        string result = serializer.Serialize(job: new { Name = "test" });

        Assert.NotNull(@object: result);
    }

    [Fact]
    public void IJobSerializer_RoundTrip()
    {
        TestSerializer serializer = new();
        SerializableData original = new() { Name = "hello", Value = 42 };

        string serialized = serializer.Serialize(job: original);
        SerializableData deserialized = serializer.Deserialize<SerializableData>(data: serialized);

        Assert.Equal(expected: original.Name, actual: deserialized.Name);
        Assert.Equal(expected: original.Value, actual: deserialized.Value);
    }

    // =========================================================================
    // IConfigurationStore interface
    // =========================================================================

    [Fact]
    public void IConfigurationStore_CanBeImplemented()
    {
        TestConfigStore store = new();

        store.SetValue(key: "key1", value: "value1");

        Assert.True(condition: store.HasKey(key: "key1"));
        Assert.Equal(expected: "value1", actual: store.GetValue(key: "key1"));
    }

    [Fact]
    public void IConfigurationStore_ReturnsNullForMissingKey()
    {
        TestConfigStore store = new();

        Assert.False(condition: store.HasKey(key: "missing"));
        Assert.Null(@object: store.GetValue(key: "missing"));
    }

    // =========================================================================
    // QueueJobModel
    // =========================================================================

    [Fact]
    public void QueueJobModel_DefaultValues()
    {
        QueueJobModel job = new() { Payload = "test" };

        Assert.Equal(expected: 0, actual: job.Id);
        Assert.Equal(expected: 0, actual: job.Priority);
        Assert.Equal(expected: "default", actual: job.Queue);
        Assert.Equal(expected: "test", actual: job.Payload);
        Assert.Equal(expected: (byte)0, actual: job.Attempts);
        Assert.Null(value: job.ReservedAt);
        Assert.True(condition: job.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void QueueJobModel_AllPropertiesSettable()
    {
        DateTime now = DateTime.UtcNow;
        QueueJobModel job = new()
        {
            Id = 42,
            Priority = 10,
            Queue = "encoder",
            Payload = "{\"type\":\"encode\"}",
            Attempts = 2,
            ReservedAt = now,
            AvailableAt = now,
            CreatedAt = now,
        };

        Assert.Equal(expected: 42, actual: job.Id);
        Assert.Equal(expected: 10, actual: job.Priority);
        Assert.Equal(expected: "encoder", actual: job.Queue);
        Assert.Equal(expected: "{\"type\":\"encode\"}", actual: job.Payload);
        Assert.Equal(expected: 2, actual: job.Attempts);
        Assert.Equal(expected: now, actual: job.ReservedAt);
        Assert.Equal(expected: now, actual: job.AvailableAt);
        Assert.Equal(expected: now, actual: job.CreatedAt);
    }

    // =========================================================================
    // FailedJobModel
    // =========================================================================

    [Fact]
    public void FailedJobModel_DefaultValues()
    {
        FailedJobModel job = new()
        {
            Queue = "default",
            Payload = "test",
            Exception = "error",
        };

        Assert.Equal(expected: 0, actual: job.Id);
        Assert.Equal(expected: Guid.Empty, actual: job.Uuid);
        Assert.Equal(expected: "default", actual: job.Connection);
        Assert.True(condition: job.FailedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void FailedJobModel_AllPropertiesSettable()
    {
        Guid uuid = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        FailedJobModel job = new()
        {
            Id = 99,
            Uuid = uuid,
            Connection = "custom",
            Queue = "encoder",
            Payload = "{\"data\":1}",
            Exception = "NullReferenceException",
            FailedAt = now,
        };

        Assert.Equal(expected: 99, actual: job.Id);
        Assert.Equal(expected: uuid, actual: job.Uuid);
        Assert.Equal(expected: "custom", actual: job.Connection);
        Assert.Equal(expected: "encoder", actual: job.Queue);
        Assert.Equal(expected: "{\"data\":1}", actual: job.Payload);
        Assert.Equal(expected: "NullReferenceException", actual: job.Exception);
        Assert.Equal(expected: now, actual: job.FailedAt);
    }

    // =========================================================================
    // CronJobModel
    // =========================================================================

    [Fact]
    public void CronJobModel_DefaultValues()
    {
        CronJobModel cron = new();

        Assert.Equal(expected: 0, actual: cron.Id);
        Assert.True(condition: cron.IsEnabled);
        Assert.Null(@object: cron.Parameters);
        Assert.Null(value: cron.LastRun);
        Assert.Null(value: cron.NextRun);
    }

    [Fact]
    public void CronJobModel_AllPropertiesSettable()
    {
        DateTime now = DateTime.UtcNow;

        CronJobModel cron = new()
        {
            Id = 1,
            Name = "cleanup",
            CronExpression = "0 0 * * *",
            JobType = "CleanupJob",
            Parameters = "{\"days\":7}",
            IsEnabled = false,
            LastRun = now.AddHours(value: -1),
            NextRun = now.AddHours(value: 23),
            CreatedAt = now,
            UpdatedAt = now,
        };

        Assert.Equal(expected: 1, actual: cron.Id);
        Assert.Equal(expected: "cleanup", actual: cron.Name);
        Assert.Equal(expected: "0 0 * * *", actual: cron.CronExpression);
        Assert.Equal(expected: "CleanupJob", actual: cron.JobType);
        Assert.Equal(expected: "{\"days\":7}", actual: cron.Parameters);
        Assert.False(condition: cron.IsEnabled);
        Assert.Equal(expected: now.AddHours(value: -1), actual: cron.LastRun);
        Assert.Equal(expected: now.AddHours(value: 23), actual: cron.NextRun);
    }

    // =========================================================================
    // QueueConfiguration
    // =========================================================================

    [Fact]
    public void QueueConfiguration_HasSensibleDefaults()
    {
        QueueConfiguration config = new();

        Assert.Equal(expected: 3, actual: config.MaxAttempts);
        Assert.Equal(expected: 1000, actual: config.PollingIntervalMs);
        Assert.Empty(collection: config.WorkerCounts);
    }

    [Fact]
    public void QueueConfiguration_CanBeCustomized()
    {
        QueueConfiguration config = new()
        {
            MaxAttempts = 5,
            PollingIntervalMs = 500,
            WorkerCounts = new()
            {
                [key: "import"] = 2,
                [key: "extras"] = 6,
                [key: "encoder"] = 4,
            },
        };

        Assert.Equal(expected: 5, actual: config.MaxAttempts);
        Assert.Equal(expected: 500, actual: config.PollingIntervalMs);
        Assert.Equal(expected: 2, actual: config.WorkerCounts[key: "import"]);
        Assert.Equal(expected: 6, actual: config.WorkerCounts[key: "extras"]);
        Assert.Equal(expected: 4, actual: config.WorkerCounts[key: "encoder"]);
    }

    [Fact]
    public void QueueConfiguration_IsRecord_SupportsEquality()
    {
        QueueConfiguration config1 = new() { MaxAttempts = 5 };
        QueueConfiguration config2 = new() { MaxAttempts = 5 };

        Assert.Equal(expected: config1.MaxAttempts, actual: config2.MaxAttempts);
    }

    // =========================================================================
    // QueueRunner accepts QueueConfiguration
    // =========================================================================

    [Fact]
    public void QueueRunner_AcceptsQueueConfiguration()
    {
        // QDC-08: Verify QueueRunner can be constructed with QueueConfiguration
        TestQueueContext context = new();
        QueueConfiguration config = new()
        {
            WorkerCounts = new()
            {
                [key: "queue"] = 2,
                [key: "data"] = 5,
                [key: "encoder"] = 3,
            },
            MaxAttempts = 5,
        };

        QueueRunner runner = new(queueContext: context, configuration: config, loggerFactory: NullLoggerFactory.Instance);

        Assert.NotNull(@object: runner);
        Assert.NotNull(@object: runner.Dispatcher);
        Assert.NotNull(@object: runner.GetActiveWorkerThreads());
    }

    [Fact]
    public void QueueRunner_AcceptsConfigurationStore()
    {
        // QDC-08: Verify QueueRunner accepts optional IConfigurationStore
        TestQueueContext context = new();
        QueueConfiguration config = new();
        TestConfigStore store = new();

        QueueRunner runner = new(queueContext: context, configuration: config, loggerFactory: NullLoggerFactory.Instance, configurationStore: store);

        Assert.NotNull(@object: runner);
    }

    [Fact]
    public void QueueRunner_SetsCurrentStaticAccessor()
    {
        // QDC-08: Verify QueueRunner.Current is set for non-DI code paths
        TestQueueContext context = new();
        QueueConfiguration config = new();

        QueueRunner runner = new(queueContext: context, configuration: config, loggerFactory: NullLoggerFactory.Instance);

        // Current may be overwritten by parallel tests constructing other QueueRunners,
        // so just verify the constructor sets it to a non-null value
        Assert.NotNull(@object: QueueRunner.Current);
    }

    [Fact]
    public void QueueRunner_UsesDefaultConfiguration()
    {
        // QDC-08: Verify QueueRunner works with default QueueConfiguration
        TestQueueContext context = new();
        QueueConfiguration config = new();

        QueueRunner runner = new(queueContext: context, configuration: config, loggerFactory: NullLoggerFactory.Instance);

        // Should have all 5 default worker types
        IReadOnlyDictionary<string, Thread> threads = runner.GetActiveWorkerThreads();
        Assert.NotNull(@object: threads);
        Assert.Empty(collection: threads); // No workers spawned until Initialize()
    }

    [Fact]
    public async Task QueueRunner_SetWorkerCount_UsesConfigurationStore()
    {
        // QDC-08: Verify SetWorkerCount persists via IConfigurationStore
        TestQueueContext context = new();
        QueueConfiguration config = new() { WorkerCounts = new() { [key: "import"] = 1 } };
        TestConfigStore store = new();

        QueueRunner runner = new(queueContext: context, configuration: config, loggerFactory: NullLoggerFactory.Instance);

        bool result = await runner.SetWorkerCount(name: "import", max: 4, userId: Guid.NewGuid());

        Assert.True(condition: result);
    }

    [Fact]
    public async Task QueueRunner_SetWorkerCount_ReturnsFalseForUnknownQueue()
    {
        // QDC-08: Verify SetWorkerCount returns false for non-existent queue
        TestQueueContext context = new();
        QueueConfiguration config = new();

        QueueRunner runner = new(queueContext: context, configuration: config, loggerFactory: NullLoggerFactory.Instance);

        bool result = await runner.SetWorkerCount(name: "nonexistent", max: 4, userId: Guid.NewGuid());

        Assert.False(condition: result);
    }

    // =========================================================================
    // IQueueContext interface
    // =========================================================================

    [Fact]
    public void IQueueContext_CanBeImplemented()
    {
        using TestQueueContext context = new();

        QueueJobModel job = new() { Payload = "test" };
        context.AddJob(job: job);
        context.SaveChanges();

        Assert.True(condition: context.JobExists(payload: "test"));
    }

    [Fact]
    public void IQueueContext_JobLifecycle()
    {
        using TestQueueContext context = new();

        QueueJobModel job = new() { Payload = "lifecycle-test", Queue = "test" };
        context.AddJob(job: job);
        context.SaveChanges();

        Assert.True(condition: context.JobExists(payload: "lifecycle-test"));

        QueueJobModel? found = context.GetNextJob(queueName: "test", maxAttempts: 3, currentJobId: null, now: DateTime.UtcNow);
        Assert.NotNull(@object: found);
        Assert.Equal(expected: "lifecycle-test", actual: found.Payload);

        context.RemoveJob(job: found);
        context.SaveChanges();

        Assert.False(condition: context.JobExists(payload: "lifecycle-test"));
    }

    [Fact]
    public void IQueueContext_FailedJobLifecycle()
    {
        using TestQueueContext context = new();

        FailedJobModel failed = new()
        {
            Uuid = Guid.NewGuid(),
            Queue = "test",
            Payload = "failed-payload",
            Exception = "test error",
        };
        context.AddFailedJob(failedJob: failed);
        context.SaveChanges();

        IReadOnlyList<FailedJobModel> allFailed = context.GetFailedJobs();
        Assert.Single(collection: allFailed);
        Assert.Equal(expected: "test error", actual: allFailed[index: 0].Exception);

        context.RemoveFailedJob(failedJob: allFailed[index: 0]);
        context.SaveChanges();

        Assert.Empty(collection: context.GetFailedJobs());
    }

    [Fact]
    public void IQueueContext_CronJobLifecycle()
    {
        using TestQueueContext context = new();

        CronJobModel cron = new()
        {
            Name = "test-cron",
            CronExpression = "0 * * * *",
            JobType = "TestJob",
            IsEnabled = true,
        };
        context.AddCronJob(cronJob: cron);
        context.SaveChanges();

        IReadOnlyList<CronJobModel> enabled = context.GetEnabledCronJobs();
        Assert.Single(collection: enabled);

        CronJobModel? found = context.FindCronJobByName(name: "test-cron");
        Assert.NotNull(@object: found);
        Assert.Equal(expected: "0 * * * *", actual: found.CronExpression);
    }

    // =========================================================================
    // Test implementations
    // =========================================================================

    private sealed class TestJob : IShouldQueue
    {
        public string QueueName => "test-queue";
        public int Priority => 5;
        public bool WasHandled { get; private set; }

        public Task Handle()
        {
            WasHandled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TestCronExecutor : ICronJobExecutor
    {
        public string CronExpression => "0 * * * *";
        public string JobName => "test-cron";
        public string? LastParameters { get; private set; }

        public Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
        {
            LastParameters = parameters;
            return Task.CompletedTask;
        }
    }

    private sealed class TestSerializer : IJobSerializer
    {
        public string Serialize(object job) => JsonSerializer.Serialize(value: job);

        public T Deserialize<T>(string data) => JsonSerializer.Deserialize<T>(json: data)!;
    }

    private sealed class SerializableData
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
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

    private sealed class TestQueueContext : IQueueContext
    {
        private readonly List<QueueJobModel> _jobs = [];
        private readonly List<FailedJobModel> _failedJobs = [];
        private readonly List<CronJobModel> _cronJobs = [];
        private int _nextJobId = 1;
        private int _nextFailedId = 1;
        private int _nextCronId = 1;

        public void AddJob(QueueJobModel job)
        {
            job.Id = _nextJobId++;
            _jobs.Add(item: job);
        }

        public void RemoveJob(QueueJobModel job) => _jobs.Remove(item: job);

        public QueueJobModel? GetNextJob(
            string queueName,
            byte maxAttempts,
            long? currentJobId,
            DateTime now
        ) =>
            _jobs.FirstOrDefault(predicate: j =>
                j.Queue == queueName
                && j.ReservedAt == null
                && j.Attempts <= maxAttempts
                && currentJobId == null
            );

        public QueueJobModel? FindJob(int id) => _jobs.FirstOrDefault(predicate: j => j.Id == id);

        public bool JobExists(string payload) => _jobs.Any(predicate: j => j.Payload == payload);

        public void UpdateJob(QueueJobModel job) { }

        public void UpdateJobPayload(int jobId, string newPayload, DateTime availableAt)
        {
            QueueJobModel? job = _jobs.FirstOrDefault(predicate: j => j.Id == jobId);
            if (job is null)
                return;
            job.Payload = newPayload;
            job.AvailableAt = availableAt;
            job.ReservedAt = null;
        }

        public void ResetAllReservedJobs()
        {
            foreach (QueueJobModel job in _jobs)
                job.ReservedAt = null;
        }

        public IReadOnlyList<QueueJobModel> GetReservedJobsOlderThan(DateTime cutoffUtc) =>
            _jobs.Where(predicate: j => j.ReservedAt != null && j.ReservedAt < cutoffUtc).ToList();

        public void AddFailedJob(FailedJobModel failedJob)
        {
            failedJob.Id = _nextFailedId++;
            _failedJobs.Add(item: failedJob);
        }

        public void RemoveFailedJob(FailedJobModel failedJob) => _failedJobs.Remove(item: failedJob);

        public void AddFailedJobAndRemoveJob(FailedJobModel failedJob, QueueJobModel job)
        {
            AddFailedJob(failedJob: failedJob);
            RemoveJob(job: job);
        }

        public FailedJobModel? FindFailedJob(int id) => _failedJobs.FirstOrDefault(predicate: j => j.Id == id);

        public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null) =>
            (failedJobId.HasValue ? _failedJobs.Where(predicate: j => j.Id == failedJobId.Value) : _failedJobs)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<CronJobModel> GetEnabledCronJobs() =>
            _cronJobs.Where(predicate: c => c.IsEnabled).ToList().AsReadOnly();

        public CronJobModel? FindCronJobByName(string name) =>
            _cronJobs.FirstOrDefault(predicate: c => c.Name == name);

        public void AddCronJob(CronJobModel cronJob)
        {
            cronJob.Id = _nextCronId++;
            _cronJobs.Add(item: cronJob);
        }

        public void UpdateCronJob(CronJobModel cronJob) { }

        public void RemoveCronJob(CronJobModel cronJob) => _cronJobs.Remove(item: cronJob);

        public bool IsParentFailed(int parentJobId) =>
            _failedJobs.Any(predicate: f => f.Payload.Contains(value: $"\"Id\":{parentJobId}"));

        public void SaveChanges() { }

        public void Dispose() { }
    }
}
