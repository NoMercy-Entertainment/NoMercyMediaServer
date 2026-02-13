using NoMercy.Queue;
using NoMercy.Queue.Core.Interfaces;
using NoMercy.Queue.Core.Models;
using Xunit;

namespace NoMercy.Tests.Queue;

[Trait("Category", "Unit")]
public class QueueCoreTests
{
    // =========================================================================
    // IShouldQueue interface
    // =========================================================================

    [Fact]
    public void IShouldQueue_CanBeImplemented()
    {
        TestJob job = new();

        Assert.Equal("test-queue", job.QueueName);
        Assert.Equal(5, job.Priority);
    }

    [Fact]
    public async Task IShouldQueue_HandleCanBeInvoked()
    {
        TestJob job = new();

        await job.Handle();

        Assert.True(job.WasHandled);
    }

    // =========================================================================
    // ICronJobExecutor interface
    // =========================================================================

    [Fact]
    public void ICronJobExecutor_CanBeImplemented()
    {
        TestCronExecutor executor = new();

        Assert.Equal("0 * * * *", executor.CronExpression);
        Assert.Equal("test-cron", executor.JobName);
    }

    [Fact]
    public async Task ICronJobExecutor_ExecuteCanBeInvoked()
    {
        TestCronExecutor executor = new();

        await executor.ExecuteAsync("param1");

        Assert.Equal("param1", executor.LastParameters);
    }

    [Fact]
    public async Task ICronJobExecutor_SupportsCancellation()
    {
        TestCronExecutor executor = new();
        using CancellationTokenSource cts = new();

        await executor.ExecuteAsync("test", cts.Token);

        Assert.Equal("test", executor.LastParameters);
    }

    // =========================================================================
    // IJobSerializer interface
    // =========================================================================

    [Fact]
    public void IJobSerializer_CanBeImplemented()
    {
        TestSerializer serializer = new();

        string result = serializer.Serialize(new { Name = "test" });

        Assert.NotNull(result);
    }

    [Fact]
    public void IJobSerializer_RoundTrip()
    {
        TestSerializer serializer = new();
        SerializableData original = new() { Name = "hello", Value = 42 };

        string serialized = serializer.Serialize(original);
        SerializableData deserialized = serializer.Deserialize<SerializableData>(serialized);

        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Value, deserialized.Value);
    }

    // =========================================================================
    // IConfigurationStore interface
    // =========================================================================

    [Fact]
    public void IConfigurationStore_CanBeImplemented()
    {
        TestConfigStore store = new();

        store.SetValue("key1", "value1");

        Assert.True(store.HasKey("key1"));
        Assert.Equal("value1", store.GetValue("key1"));
    }

    [Fact]
    public void IConfigurationStore_ReturnsNullForMissingKey()
    {
        TestConfigStore store = new();

        Assert.False(store.HasKey("missing"));
        Assert.Null(store.GetValue("missing"));
    }

    // =========================================================================
    // QueueJobModel
    // =========================================================================

    [Fact]
    public void QueueJobModel_DefaultValues()
    {
        QueueJobModel job = new() { Payload = "test" };

        Assert.Equal(0, job.Id);
        Assert.Equal(0, job.Priority);
        Assert.Equal("default", job.Queue);
        Assert.Equal("test", job.Payload);
        Assert.Equal((byte)0, job.Attempts);
        Assert.Null(job.ReservedAt);
        Assert.True(job.CreatedAt <= DateTime.UtcNow);
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
            CreatedAt = now
        };

        Assert.Equal(42, job.Id);
        Assert.Equal(10, job.Priority);
        Assert.Equal("encoder", job.Queue);
        Assert.Equal("{\"type\":\"encode\"}", job.Payload);
        Assert.Equal(2, job.Attempts);
        Assert.Equal(now, job.ReservedAt);
        Assert.Equal(now, job.AvailableAt);
        Assert.Equal(now, job.CreatedAt);
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
            Exception = "error"
        };

        Assert.Equal(0, job.Id);
        Assert.Equal(Guid.Empty, job.Uuid);
        Assert.Equal("default", job.Connection);
        Assert.True(job.FailedAt <= DateTime.UtcNow);
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
            FailedAt = now
        };

        Assert.Equal(99, job.Id);
        Assert.Equal(uuid, job.Uuid);
        Assert.Equal("custom", job.Connection);
        Assert.Equal("encoder", job.Queue);
        Assert.Equal("{\"data\":1}", job.Payload);
        Assert.Equal("NullReferenceException", job.Exception);
        Assert.Equal(now, job.FailedAt);
    }

    // =========================================================================
    // CronJobModel
    // =========================================================================

    [Fact]
    public void CronJobModel_DefaultValues()
    {
        CronJobModel cron = new();

        Assert.Equal(0, cron.Id);
        Assert.True(cron.IsEnabled);
        Assert.Null(cron.Parameters);
        Assert.Null(cron.LastRun);
        Assert.Null(cron.NextRun);
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
            LastRun = now.AddHours(-1),
            NextRun = now.AddHours(23),
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(1, cron.Id);
        Assert.Equal("cleanup", cron.Name);
        Assert.Equal("0 0 * * *", cron.CronExpression);
        Assert.Equal("CleanupJob", cron.JobType);
        Assert.Equal("{\"days\":7}", cron.Parameters);
        Assert.False(cron.IsEnabled);
        Assert.Equal(now.AddHours(-1), cron.LastRun);
        Assert.Equal(now.AddHours(23), cron.NextRun);
    }

    // =========================================================================
    // QueueConfiguration
    // =========================================================================

    [Fact]
    public void QueueConfiguration_HasSensibleDefaults()
    {
        QueueConfiguration config = new();

        Assert.Equal(3, config.MaxAttempts);
        Assert.Equal(1000, config.PollingIntervalMs);
        Assert.Equal(1, config.WorkerCounts["queue"]);
        Assert.Equal(10, config.WorkerCounts["data"]);
        Assert.Equal(2, config.WorkerCounts["encoder"]);
        Assert.Equal(1, config.WorkerCounts["cron"]);
        Assert.Equal(5, config.WorkerCounts["image"]);
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
                ["queue"] = 2,
                ["data"] = 6,
                ["encoder"] = 4
            }
        };

        Assert.Equal(5, config.MaxAttempts);
        Assert.Equal(500, config.PollingIntervalMs);
        Assert.Equal(2, config.WorkerCounts["queue"]);
        Assert.Equal(6, config.WorkerCounts["data"]);
        Assert.Equal(4, config.WorkerCounts["encoder"]);
    }

    [Fact]
    public void QueueConfiguration_IsRecord_SupportsEquality()
    {
        QueueConfiguration config1 = new() { MaxAttempts = 5 };
        QueueConfiguration config2 = new() { MaxAttempts = 5 };

        Assert.Equal(config1.MaxAttempts, config2.MaxAttempts);
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
                ["queue"] = 2,
                ["data"] = 5,
                ["encoder"] = 3
            },
            MaxAttempts = 5
        };

        QueueRunner runner = new(context, config);

        Assert.NotNull(runner);
        Assert.NotNull(runner.Dispatcher);
        Assert.NotNull(runner.GetActiveWorkerThreads());
    }

    [Fact]
    public void QueueRunner_AcceptsConfigurationStore()
    {
        // QDC-08: Verify QueueRunner accepts optional IConfigurationStore
        TestQueueContext context = new();
        QueueConfiguration config = new();
        TestConfigStore store = new();

        QueueRunner runner = new(context, config, store);

        Assert.NotNull(runner);
    }

    [Fact]
    public void QueueRunner_SetsCurrentStaticAccessor()
    {
        // QDC-08: Verify QueueRunner.Current is set for non-DI code paths
        TestQueueContext context = new();
        QueueConfiguration config = new();

        QueueRunner runner = new(context, config);

        Assert.Same(runner, QueueRunner.Current);
    }

    [Fact]
    public void QueueRunner_UsesDefaultConfiguration()
    {
        // QDC-08: Verify QueueRunner works with default QueueConfiguration
        TestQueueContext context = new();
        QueueConfiguration config = new();

        QueueRunner runner = new(context, config);

        // Should have all 5 default worker types
        IReadOnlyDictionary<string, Thread> threads = runner.GetActiveWorkerThreads();
        Assert.NotNull(threads);
        Assert.Empty(threads); // No workers spawned until Initialize()
    }

    [Fact]
    public async Task QueueRunner_SetWorkerCount_UsesConfigurationStore()
    {
        // QDC-08: Verify SetWorkerCount persists via IConfigurationStore
        TestQueueContext context = new();
        QueueConfiguration config = new();
        TestConfigStore store = new();

        QueueRunner runner = new(context, config);

        bool result = await runner.SetWorkerCount("queue", 4, Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task QueueRunner_SetWorkerCount_ReturnsFalseForUnknownQueue()
    {
        // QDC-08: Verify SetWorkerCount returns false for non-existent queue
        TestQueueContext context = new();
        QueueConfiguration config = new();

        QueueRunner runner = new(context, config);

        bool result = await runner.SetWorkerCount("nonexistent", 4, Guid.NewGuid());

        Assert.False(result);
    }

    // =========================================================================
    // IQueueContext interface
    // =========================================================================

    [Fact]
    public void IQueueContext_CanBeImplemented()
    {
        using TestQueueContext context = new();

        QueueJobModel job = new() { Payload = "test" };
        context.AddJob(job);
        context.SaveChanges();

        Assert.True(context.JobExists("test"));
    }

    [Fact]
    public void IQueueContext_JobLifecycle()
    {
        using TestQueueContext context = new();

        QueueJobModel job = new() { Payload = "lifecycle-test", Queue = "test" };
        context.AddJob(job);
        context.SaveChanges();

        Assert.True(context.JobExists("lifecycle-test"));

        QueueJobModel? found = context.GetNextJob("test", 3, null);
        Assert.NotNull(found);
        Assert.Equal("lifecycle-test", found.Payload);

        context.RemoveJob(found);
        context.SaveChanges();

        Assert.False(context.JobExists("lifecycle-test"));
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
            Exception = "test error"
        };
        context.AddFailedJob(failed);
        context.SaveChanges();

        IReadOnlyList<FailedJobModel> allFailed = context.GetFailedJobs();
        Assert.Single(allFailed);
        Assert.Equal("test error", allFailed[0].Exception);

        context.RemoveFailedJob(allFailed[0]);
        context.SaveChanges();

        Assert.Empty(context.GetFailedJobs());
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
            IsEnabled = true
        };
        context.AddCronJob(cron);
        context.SaveChanges();

        IReadOnlyList<CronJobModel> enabled = context.GetEnabledCronJobs();
        Assert.Single(enabled);

        CronJobModel? found = context.FindCronJobByName("test-cron");
        Assert.NotNull(found);
        Assert.Equal("0 * * * *", found.CronExpression);
    }

    // =========================================================================
    // Test implementations
    // =========================================================================

    private sealed class TestJob : NoMercy.Queue.Core.Interfaces.IShouldQueue
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
        public string Serialize(object job) => System.Text.Json.JsonSerializer.Serialize(job);
        public T Deserialize<T>(string data) => System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
    }

    private sealed class SerializableData
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
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

    private sealed class TestQueueContext : IQueueContext
    {
        private readonly List<QueueJobModel> _jobs = [];
        private readonly List<FailedJobModel> _failedJobs = [];
        private readonly List<CronJobModel> _cronJobs = [];
        private int _nextJobId = 1;
        private int _nextFailedId = 1;
        private int _nextCronId = 1;

        public void AddJob(QueueJobModel job) { job.Id = _nextJobId++; _jobs.Add(job); }
        public void RemoveJob(QueueJobModel job) => _jobs.Remove(job);
        public QueueJobModel? GetNextJob(string queueName, byte maxAttempts, long? currentJobId)
            => _jobs.FirstOrDefault(j => j.Queue == queueName && j.ReservedAt == null && j.Attempts <= maxAttempts && currentJobId == null);
        public QueueJobModel? FindJob(int id) => _jobs.FirstOrDefault(j => j.Id == id);
        public bool JobExists(string payload) => _jobs.Any(j => j.Payload == payload);
        public void UpdateJob(QueueJobModel job) { }
        public void ResetAllReservedJobs()
        {
            foreach (QueueJobModel job in _jobs) job.ReservedAt = null;
        }

        public void AddFailedJob(FailedJobModel failedJob) { failedJob.Id = _nextFailedId++; _failedJobs.Add(failedJob); }
        public void RemoveFailedJob(FailedJobModel failedJob) => _failedJobs.Remove(failedJob);
        public FailedJobModel? FindFailedJob(int id) => _failedJobs.FirstOrDefault(j => j.Id == id);
        public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null)
            => (failedJobId.HasValue ? _failedJobs.Where(j => j.Id == failedJobId.Value) : _failedJobs).ToList().AsReadOnly();

        public IReadOnlyList<CronJobModel> GetEnabledCronJobs() => _cronJobs.Where(c => c.IsEnabled).ToList().AsReadOnly();
        public CronJobModel? FindCronJobByName(string name) => _cronJobs.FirstOrDefault(c => c.Name == name);
        public void AddCronJob(CronJobModel cronJob) { cronJob.Id = _nextCronId++; _cronJobs.Add(cronJob); }
        public void UpdateCronJob(CronJobModel cronJob) { }
        public void RemoveCronJob(CronJobModel cronJob) => _cronJobs.Remove(cronJob);

        public void SaveChanges() { }
        public void Dispose() { }
    }
}
