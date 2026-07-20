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

using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// <see cref="CronWorker.ExecuteAsync"/> loads DB-persisted cron jobs once the
/// database is ready and decides, per row, whether its <c>JobType</c> was
/// registered by this boot (code-defined job or plugin instance executor). A
/// DB row for a job type that's no longer registered (a plugin removed, a
/// stale row from a renamed job) must be skipped with a warning rather than
/// crash the whole boot pass. Invoked directly via reflection on the private
/// <c>StartDatabaseJobWorkers</c> — this both isolates the decision from
/// <c>ExecuteAsync</c>'s DB-readiness gate (avoiding a race on the
/// process-wide static <c>DatabaseReadyTcs</c>/<c>QueueWorkersReadyTcs</c>
/// other tests in this suite also touch) and seeds <c>_registeredJobs</c>
/// directly rather than via <c>RegisterJob</c>, so the dedup guard in
/// <c>StartJobWorker</c> can't mask whether THIS method's own decision
/// actually started the worker.
/// </summary>
[Trait("Category", "Unit")]
public class CronWorkerDatabaseJobTests
{
    private sealed class TestCronJobExecutor : ICronJobExecutor
    {
        public string CronExpression => "0 0 * * *";
        public string JobName => "db-job-test";

        public Task ExecuteAsync(
            string parameters,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    private static Dictionary<string, CancellationTokenSource> GetJobCancellationTokens(
        CronWorker worker
    ) =>
        (Dictionary<string, CancellationTokenSource>)
            typeof(CronWorker)
                .GetField("_jobCancellationTokens", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(worker)!;

    private static void SetRegisteredJobType(CronWorker worker, string jobType, Type executorType)
    {
        Dictionary<string, Type> registeredJobs =
            (Dictionary<string, Type>)
                typeof(CronWorker)
                    .GetField("_registeredJobs", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(worker)!;
        registeredJobs[jobType] = executorType;
    }

    private static void InvokeStartDatabaseJobWorkers(CronWorker worker) =>
        typeof(CronWorker)
            .GetMethod("StartDatabaseJobWorkers", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(worker, null);

    [Fact]
    public async Task StartDatabaseJobWorkers_RegisteredJobType_StartsAWorker()
    {
        TestQueueContextAdapter context = new();
        context.AddCronJob(
            new CronJobModel
            {
                Name = "registered-db-job",
                CronExpression = "0 0 * * *",
                JobType = "registered-type",
                IsEnabled = true,
            }
        );
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped<TestCronJobExecutor>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CronWorker worker = new(
            provider,
            provider.GetRequiredService<ILogger<CronWorker>>(),
            context
        );
        SetRegisteredJobType(worker, "registered-type", typeof(TestCronJobExecutor));

        GetJobCancellationTokens(worker).Should().BeEmpty("nothing has started a worker yet");

        InvokeStartDatabaseJobWorkers(worker);

        GetJobCancellationTokens(worker).Should().ContainKey("registered-type");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await worker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task StartDatabaseJobWorkers_UnregisteredJobType_DoesNotStartAWorker()
    {
        TestQueueContextAdapter context = new();
        context.AddCronJob(
            new CronJobModel
            {
                Name = "orphaned-db-job",
                CronExpression = "0 0 * * *",
                JobType = "never-registered-type",
                IsEnabled = true,
            }
        );
        ServiceCollection services = new();
        services.AddLogging();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CronWorker worker = new(
            provider,
            provider.GetRequiredService<ILogger<CronWorker>>(),
            context
        );

        InvokeStartDatabaseJobWorkers(worker);

        GetJobCancellationTokens(worker)
            .Should()
            .BeEmpty(
                "a DB row whose JobType was never registered this boot must not start a worker"
            );

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await worker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task StartDatabaseJobWorkers_DisabledJobRow_IsNeverLoaded()
    {
        // GetEnabledCronJobs() filters disabled rows before StartDatabaseJobWorkers
        // ever sees them — a disabled row must not start a worker even when its
        // JobType IS registered.
        TestQueueContextAdapter context = new();
        context.AddCronJob(
            new CronJobModel
            {
                Name = "disabled-db-job",
                CronExpression = "0 0 * * *",
                JobType = "registered-type",
                IsEnabled = false,
            }
        );
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped<TestCronJobExecutor>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CronWorker worker = new(
            provider,
            provider.GetRequiredService<ILogger<CronWorker>>(),
            context
        );
        SetRegisteredJobType(worker, "registered-type", typeof(TestCronJobExecutor));

        InvokeStartDatabaseJobWorkers(worker);

        GetJobCancellationTokens(worker)
            .Should()
            .BeEmpty("a disabled cron job row must never start a worker");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await worker.StopAsync(cts.Token);
    }
}
