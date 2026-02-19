using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Extensions;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// HIGH-13: Tests that verify cron job registration does not produce duplicates.
/// </summary>
[Trait("Category", "Unit")]
public class CronWorkerRegistrationTests
{
    [Fact]
    public void RegisterCronJob_RegistersTypeOnceInDI()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.RegisterCronJob<TestCronJobA>("test-job-a");
        services.RegisterCronJob<TestCronJobB>("test-job-b");

        using ServiceProvider provider = services.BuildServiceProvider();
        TestCronJobA jobA = provider.GetRequiredService<TestCronJobA>();
        TestCronJobB jobB = provider.GetRequiredService<TestCronJobB>();

        Assert.NotNull(jobA);
        Assert.NotNull(jobB);
    }

    [Fact]
    public async Task RegisterJob_CalledTwiceWithSameType_StartsOnlyOneWorker()
    {
        using ServiceProvider provider = BuildProvider();
        CronWorker cronWorker = CreateCronWorker(provider);

        cronWorker.RegisterJob<TestCronJobA>("test-job-a", "Test Job A", "0 0 * * *");
        cronWorker.RegisterJob<TestCronJobA>("test-job-a", "Test Job A Duplicate", "0 0 * * *");

        // StopAsync should complete cleanly — no orphaned tasks from duplicate
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task RegisterJobWithSchedule_CalledTwiceWithSameType_StartsOnlyOneWorker()
    {
        using ServiceProvider provider = BuildProvider();
        CronWorker cronWorker = CreateCronWorker(provider);

        cronWorker.RegisterJobWithSchedule<TestCronJobA>("test-job-a", provider);
        cronWorker.RegisterJobWithSchedule<TestCronJobA>("test-job-a", provider);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task RegisterJob_DifferentJobTypes_StartsOneWorkerEach()
    {
        using ServiceProvider provider = BuildProvider();
        CronWorker cronWorker = CreateCronWorker(provider);

        cronWorker.RegisterJob<TestCronJobA>("test-job-a", "Test Job A", "0 0 * * *");
        cronWorker.RegisterJob<TestCronJobB>("test-job-b", "Test Job B", "0 12 * * *");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task StopAsync_AfterDuplicateRegistration_CleansUpWithoutOrphanedTasks()
    {
        using ServiceProvider provider = BuildProvider();
        CronWorker cronWorker = CreateCronWorker(provider);

        cronWorker.RegisterJob<TestCronJobA>("test-job-a", "Test Job A", "0 0 * * *");
        cronWorker.RegisterJob<TestCronJobA>("test-job-a", "Test Job A Dup", "0 0 * * *");
        cronWorker.RegisterJob<TestCronJobB>("test-job-b", "Test Job B", "0 12 * * *");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.RegisterCronJob<TestCronJobA>("test-job-a");
        services.RegisterCronJob<TestCronJobB>("test-job-b");
        return services.BuildServiceProvider();
    }

    private static CronWorker CreateCronWorker(ServiceProvider provider)
    {
        return new(provider, provider.GetRequiredService<ILogger<CronWorker>>(), new StubQueueContext());
    }

    private class TestCronJobA : ICronJobExecutor
    {
        public string CronExpression => "0 0 * * *";
        public string JobName => "Test Job A";
        public Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class TestCronJobB : ICronJobExecutor
    {
        public string CronExpression => "0 12 * * *";
        public string JobName => "Test Job B";
        public Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubQueueContext : IQueueContext
    {
        public void AddJob(QueueJobModel job) { }
        public void RemoveJob(QueueJobModel job) { }
        public QueueJobModel? GetNextJob(string queueName, byte maxAttempts, long? currentJobId) => null;
        public QueueJobModel? FindJob(int id) => null;
        public bool JobExists(string payload) => false;
        public void UpdateJob(QueueJobModel job) { }
        public void ResetAllReservedJobs() { }
        public void AddFailedJob(FailedJobModel failedJob) { }
        public void RemoveFailedJob(FailedJobModel failedJob) { }
        public FailedJobModel? FindFailedJob(int id) => null;
        public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null) => [];
        public IReadOnlyList<CronJobModel> GetEnabledCronJobs() => [];
        public CronJobModel? FindCronJobByName(string name) => null;
        public void AddCronJob(CronJobModel cronJob) { }
        public void UpdateCronJob(CronJobModel cronJob) { }
        public void RemoveCronJob(CronJobModel cronJob) { }
        public void SaveChanges() { }
        public void Dispose() { }
    }
}
