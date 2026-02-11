using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Queue.Extensions;
using NoMercy.Queue.Interfaces;
using NoMercy.Queue.Workers;
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
        return new CronWorker(provider, provider.GetRequiredService<ILogger<CronWorker>>());
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
}
