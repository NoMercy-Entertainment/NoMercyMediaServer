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
[Trait(name: "Category", value: "Unit")]
public class CronWorkerRegistrationTests
{
    [Fact]
    public void RegisterCronJob_RegistersTypeOnceInDI()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.RegisterCronJob<TestCronJobA>(jobType: "test-job-a");
        services.RegisterCronJob<TestCronJobB>(jobType: "test-job-b");

        using ServiceProvider provider = services.BuildServiceProvider();
        TestCronJobA jobA = provider.GetRequiredService<TestCronJobA>();
        TestCronJobB jobB = provider.GetRequiredService<TestCronJobB>();

        Assert.NotNull(@object: jobA);
        Assert.NotNull(@object: jobB);
    }

    [Fact]
    public async Task RegisterJob_CalledTwiceWithSameType_StartsOnlyOneWorker()
    {
        await using ServiceProvider provider = BuildProvider();
        CronWorker cronWorker = CreateCronWorker(provider: provider);

        cronWorker.RegisterJob<TestCronJobA>(jobType: "test-job-a", name: "Test Job A", cronExpression: "0 0 * * *");
        cronWorker.RegisterJob<TestCronJobA>(jobType: "test-job-a", name: "Test Job A Duplicate", cronExpression: "0 0 * * *");

        // StopAsync should complete cleanly — no orphaned tasks from duplicate
        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        await cronWorker.StopAsync(cancellationToken: cts.Token);
    }

    [Fact]
    public async Task RegisterJobWithSchedule_CalledTwiceWithSameType_StartsOnlyOneWorker()
    {
        await using ServiceProvider provider = BuildProvider();
        CronWorker cronWorker = CreateCronWorker(provider: provider);

        cronWorker.RegisterJobWithSchedule<TestCronJobA>(jobType: "test-job-a", serviceProvider: provider);
        cronWorker.RegisterJobWithSchedule<TestCronJobA>(jobType: "test-job-a", serviceProvider: provider);

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        await cronWorker.StopAsync(cancellationToken: cts.Token);
    }

    [Fact]
    public async Task RegisterJob_DifferentJobTypes_StartsOneWorkerEach()
    {
        await using ServiceProvider provider = BuildProvider();
        CronWorker cronWorker = CreateCronWorker(provider: provider);

        cronWorker.RegisterJob<TestCronJobA>(jobType: "test-job-a", name: "Test Job A", cronExpression: "0 0 * * *");
        cronWorker.RegisterJob<TestCronJobB>(jobType: "test-job-b", name: "Test Job B", cronExpression: "0 12 * * *");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        await cronWorker.StopAsync(cancellationToken: cts.Token);
    }

    [Fact]
    public async Task StopAsync_AfterDuplicateRegistration_CleansUpWithoutOrphanedTasks()
    {
        await using ServiceProvider provider = BuildProvider();
        CronWorker cronWorker = CreateCronWorker(provider: provider);

        cronWorker.RegisterJob<TestCronJobA>(jobType: "test-job-a", name: "Test Job A", cronExpression: "0 0 * * *");
        cronWorker.RegisterJob<TestCronJobA>(jobType: "test-job-a", name: "Test Job A Dup", cronExpression: "0 0 * * *");
        cronWorker.RegisterJob<TestCronJobB>(jobType: "test-job-b", name: "Test Job B", cronExpression: "0 12 * * *");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        await cronWorker.StopAsync(cancellationToken: cts.Token);
    }

    [Fact]
    public async Task StartAsync_WhenACronExecutorCannotBeResolved_StillRegistersTheRemainingJobs()
    {
        // A registration whose executor type is NOT in DI makes RegisterDescriptor's
        // GetRequiredService throw. Unguarded, that throw aborts the registration
        // foreach in ExecuteAsync, so EVERY later job is silently skipped and the
        // faulted BackgroundService trips the .NET StopHost default (server won't
        // boot). Contained, the bad registration is logged+skipped and the rest
        // register. The bad registration is listed FIRST so a regression is only
        // caught by observing that the GOOD executor is still resolved after it.
        ObservableCronJob.WasResolved = false;
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<ObservableCronJob>();
        await using ServiceProvider provider = services.BuildServiceProvider();

        List<CronJobRegistration> registrations =
        [
            new(ExecutorType: typeof(UnresolvableCronJob), JobType: "unresolvable-job", CronExpression: "0 0 * * *"),
            new(ExecutorType: typeof(ObservableCronJob), JobType: "good-job", CronExpression: "0 0 * * *"),
        ];

        CronWorker cronWorker = new(
            serviceProvider: provider,
            logger: provider.GetRequiredService<ILogger<CronWorker>>(),
            queueContext: new StubQueueContext(),
            registrations: registrations
        );

        await cronWorker.StartAsync(cancellationToken: CancellationToken.None);

        // ExecuteAsync (and its registration loop) is scheduled by StartAsync rather
        // than run inline, so wait — bounded — for the loop to process both
        // registrations. Old (unguarded) code aborts at the unresolvable
        // registration and never reaches the good one, so WasResolved stays false
        // until this times out; the fix skips the bad one and resolves the good one.
        using CancellationTokenSource waitCts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        while (!ObservableCronJob.WasResolved && !waitCts.IsCancellationRequested)
            await Task.Delay(millisecondsDelay: 25);

        Assert.True(
            condition: ObservableCronJob.WasResolved,
            userMessage: "The good cron job after an unresolvable one must still be registered."
        );

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        await cronWorker.StopAsync(cancellationToken: cts.Token);
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.RegisterCronJob<TestCronJobA>(jobType: "test-job-a");
        services.RegisterCronJob<TestCronJobB>(jobType: "test-job-b");
        return services.BuildServiceProvider();
    }

    private static CronWorker CreateCronWorker(ServiceProvider provider)
    {
        return new(
            serviceProvider: provider,
            logger: provider.GetRequiredService<ILogger<CronWorker>>(),
            queueContext: new StubQueueContext()
        );
    }

    private class TestCronJobA : ICronJobExecutor
    {
        public string CronExpression => "0 0 * * *";
        public string JobName => "Test Job A";

        public Task ExecuteAsync(
            string parameters,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    private class TestCronJobB : ICronJobExecutor
    {
        public string CronExpression => "0 12 * * *";
        public string JobName => "Test Job B";

        public Task ExecuteAsync(
            string parameters,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    // Deliberately NOT registered in the test ServiceProvider so that resolving
    // it via GetRequiredService throws, simulating a misconfigured/plugin cron
    // executor at boot.
    private sealed class UnresolvableCronJob : ICronJobExecutor
    {
        public string CronExpression => "0 0 * * *";
        public string JobName => "Unresolvable";

        public Task ExecuteAsync(
            string parameters,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    // Registered in DI; RegisterDescriptor reads CronExpression while wiring the
    // job, so WasResolved flips only once the registration loop actually reaches
    // this executor (i.e. it was not aborted by an earlier bad registration).
    private sealed class ObservableCronJob : ICronJobExecutor
    {
        // Static so the assertion is independent of which instance DI hands the
        // worker: the flag flips when ANY ObservableCronJob is wired.
        public static bool WasResolved;

        public string CronExpression => "0 0 * * *";

        // RegisterDescriptor unconditionally reads JobName (line ~331) when wiring
        // the CronJobModel, so this flips only once the registration loop actually
        // reaches this executor — i.e. it was not aborted by an earlier bad one.
        public string JobName
        {
            get
            {
                WasResolved = true;
                return "Observable";
            }
        }

        public Task ExecuteAsync(
            string parameters,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    private sealed class StubQueueContext : IQueueContext
    {
        public void AddJob(QueueJobModel job) { }

        public void RemoveJob(QueueJobModel job) { }

        public QueueJobModel? GetNextJob(
            string queueName,
            byte maxAttempts,
            long? currentJobId,
            DateTime now
        ) => null;

        public QueueJobModel? FindJob(int id) => null;

        public bool JobExists(string payload) => false;

        public void UpdateJob(QueueJobModel job) { }

        public void UpdateJobPayload(int jobId, string newPayload, DateTime availableAt) { }

        public void ResetAllReservedJobs() { }

        public IReadOnlyList<QueueJobModel> GetReservedJobsOlderThan(DateTime cutoffUtc) => [];

        public void AddFailedJob(FailedJobModel failedJob) { }

        public void RemoveFailedJob(FailedJobModel failedJob) { }

        public void AddFailedJobAndRemoveJob(FailedJobModel failedJob, QueueJobModel job) { }

        public FailedJobModel? FindFailedJob(int id) => null;

        public IReadOnlyList<FailedJobModel> GetFailedJobs(long? failedJobId = null) => [];

        public IReadOnlyList<CronJobModel> GetEnabledCronJobs() => [];

        public CronJobModel? FindCronJobByName(string name) => null;

        public void AddCronJob(CronJobModel cronJob) { }

        public void UpdateCronJob(CronJobModel cronJob) { }

        public void RemoveCronJob(CronJobModel cronJob) { }

        public bool IsParentFailed(int parentJobId) => false;

        public void SaveChanges() { }

        public void Dispose() { }
    }
}
