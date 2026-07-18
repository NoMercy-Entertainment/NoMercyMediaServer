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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Proves <c>CronWorker.RegisterExecutor</c> schedules a runtime executor
/// INSTANCE (as opposed to <c>RegisterDescriptor</c>/<c>RegisterJob</c>, which
/// resolve an executor by DI type) and that the job runner genuinely invokes
/// that instance at execution time rather than only recording it.
/// </summary>
[Trait("Category", "Unit")]
public class CronWorkerRegisterExecutorTests
{
    [Fact]
    public async Task RegisterExecutor_SchedulesCodeDefinedJob_WithExecutorNameAndExpression()
    {
        await using ServiceProvider provider = BuildProvider();
        TestQueueContextAdapter queueContext = new();
        CronWorker cronWorker = new(
            provider,
            provider.GetRequiredService<ILogger<CronWorker>>(),
            queueContext
        );

        RecordingCronJobExecutor executor = new(
            "plugin:11111111-1111-1111-1111-111111111111",
            "*/5 * * * *"
        );

        cronWorker.RegisterExecutor(executor);

        CronJobModel scheduled = GetCodeDefinedJobs(cronWorker)
            .Single(job => job.JobType == executor.JobName);

        Assert.Equal(executor.JobName, scheduled.Name);
        Assert.Equal(executor.CronExpression, scheduled.CronExpression);
        Assert.True(scheduled.IsEnabled);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    [Fact]
    public async Task RegisterExecutor_RunnerInvokesTheRegisteredInstance_WithoutDiTypeResolution()
    {
        // The executor's concrete type is deliberately NOT registered in this
        // container. RegisterDescriptor/RegisterJob<T> resolve their executor
        // via `scope.ServiceProvider.GetRequiredService(jobExecutorType)`, which
        // a bare runtime plugin instance can never satisfy. If ExecuteJob still
        // falls back to that DI path for this job, it hits the "job type not
        // registered" branch and returns false without ever touching the
        // instance — this test fails on that old behavior.
        await using ServiceProvider provider = BuildProvider();
        TestQueueContextAdapter queueContext = new();
        CronWorker cronWorker = new(
            provider,
            provider.GetRequiredService<ILogger<CronWorker>>(),
            queueContext
        );

        RecordingCronJobExecutor executor = new(
            "plugin:22222222-2222-2222-2222-222222222222",
            "*/5 * * * *"
        );
        cronWorker.RegisterExecutor(executor);

        CronJobModel scheduled = GetCodeDefinedJobs(cronWorker)
            .Single(job => job.JobType == executor.JobName);

        bool success = await InvokeExecuteJob(cronWorker, scheduled);

        Assert.True(
            success,
            "ExecuteJob must resolve and run the directly-registered instance executor."
        );
        Assert.True(
            executor.WasExecuted,
            "The instance executor itself must run — proves the runner is genuinely live, not merely registered."
        );

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await cronWorker.StopAsync(cts.Token);
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static List<CronJobModel> GetCodeDefinedJobs(CronWorker cronWorker)
    {
        FieldInfo field = typeof(CronWorker).GetField(
            "_codeDefinedJobs",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;

        return (List<CronJobModel>)field.GetValue(cronWorker)!;
    }

    private static async Task<bool> InvokeExecuteJob(CronWorker cronWorker, CronJobModel job)
    {
        MethodInfo executeJob = typeof(CronWorker).GetMethod(
            "ExecuteJob",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;

        Task<bool> resultTask =
            (Task<bool>)
                executeJob.Invoke(cronWorker, [job, DateTime.UtcNow, CancellationToken.None])!;

        return await resultTask;
    }

    private sealed class RecordingCronJobExecutor(string jobName, string cronExpression)
        : ICronJobExecutor
    {
        public bool WasExecuted;

        public string JobName { get; } = jobName;

        public string CronExpression { get; } = cronExpression;

        public Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            return Task.CompletedTask;
        }
    }
}
