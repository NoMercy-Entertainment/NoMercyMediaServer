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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Extensions;
using NoMercyQueue.Workers;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// These DI-wiring decisions are load-bearing, not incidental: if
/// <c>AddCronWorker</c> ever resolved a NEW <see cref="CronWorker"/> for the
/// hosted-service slot instead of the app's own singleton, every
/// <c>RegisterJob</c>/<c>RegisterExecutor</c> call made against the
/// application's resolved instance would register cron jobs on an object the
/// host never starts — jobs silently never fire. If <c>RegisterCronJob</c>
/// ever became a singleton registration, a cron executor holding scoped
/// dependencies (a per-run DbContext) would leak across every run instead of
/// getting a fresh one.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ServiceCollectionExtensionsTests
{
    private sealed class TestCronJobExecutor : ICronJobExecutor
    {
        public string CronExpression => "0 3 * * *";
        public string JobName => "extension-test-job";

        public Task ExecuteAsync(
            string parameters,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    [Fact]
    public void AddCronWorker_HostedServiceResolvesTheSameSingletonInstance()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IQueueContext>(implementationInstance: new StubQueueContext());
        services.AddCronWorker();

        using ServiceProvider provider = services.BuildServiceProvider();

        CronWorker direct = provider.GetRequiredService<CronWorker>();
        IEnumerable<IHostedService> hostedServices = provider.GetServices<IHostedService>();
        IHostedService hosted = hostedServices.OfType<CronWorker>().Single();

        hosted.Should().BeSameAs(expected: direct);
    }

    [Fact]
    public void AddCronJob_RegistersCronJobRegistration_WithGivenJobTypeAndCronExpression()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddCronJob<TestCronJobExecutor>(jobType: "extension-test-job", cronExpression: "0 4 * * *");

        using ServiceProvider provider = services.BuildServiceProvider();

        CronJobRegistration registration = provider.GetServices<CronJobRegistration>().Single();

        registration.JobType.Should().Be(expected: "extension-test-job");
        registration.CronExpression.Should().Be(expected: "0 4 * * *");
        registration.ExecutorType.Should().Be(expected: typeof(TestCronJobExecutor));
    }

    [Fact]
    public void AddCronJob_WithoutExplicitCronExpression_LeavesItNullSoExecutorsOwnIsUsed()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddCronJob<TestCronJobExecutor>(jobType: "extension-test-job");

        using ServiceProvider provider = services.BuildServiceProvider();

        CronJobRegistration registration = provider.GetServices<CronJobRegistration>().Single();

        registration.CronExpression.Should().BeNull();
    }

    [Fact]
    public void RegisterCronJob_RegistersExecutorAsScoped_NotSingleton()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.RegisterCronJob<TestCronJobExecutor>(jobType: "extension-test-job");

        using ServiceProvider provider = services.BuildServiceProvider();

        using IServiceScope scopeA = provider.CreateScope();
        using IServiceScope scopeB = provider.CreateScope();
        TestCronJobExecutor instanceA =
            scopeA.ServiceProvider.GetRequiredService<TestCronJobExecutor>();
        TestCronJobExecutor instanceB =
            scopeB.ServiceProvider.GetRequiredService<TestCronJobExecutor>();

        instanceA.Should().NotBeSameAs(unexpected: instanceB);
    }

    [Fact]
    public async Task RegisterJobWithSchedule_RegistersUsingResolvedExecutorsNameAndExpression()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TestCronJobExecutor>();
        services.AddSingleton<IQueueContext>(implementationInstance: new StubQueueContext());
        using ServiceProvider provider = services.BuildServiceProvider();

        CronWorker cronWorker = new(
            serviceProvider: provider,
            logger: provider.GetRequiredService<ILogger<CronWorker>>(),
            queueContext: provider.GetRequiredService<IQueueContext>()
        );

        cronWorker.RegisterJobWithSchedule<TestCronJobExecutor>(jobType: "extension-test-job", serviceProvider: provider);

        Dictionary<string, Type> registeredJobs =
            (Dictionary<string, Type>)
                typeof(CronWorker)
                    .GetField(name: "_registeredJobs", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(obj: cronWorker)!;
        List<CronJobModel> codeDefinedJobs =
            (List<CronJobModel>)
                typeof(CronWorker)
                    .GetField(name: "_codeDefinedJobs", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(obj: cronWorker)!;

        registeredJobs.Should().ContainKey(expected: "extension-test-job");
        registeredJobs[key: "extension-test-job"].Should().Be(expected: typeof(TestCronJobExecutor));
        CronJobModel scheduled = codeDefinedJobs.Single(predicate: j => j.JobType == "extension-test-job");
        scheduled.Name.Should().Be(expected: "extension-test-job");
        scheduled.CronExpression.Should().Be(expected: "0 3 * * *");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        await cronWorker.StopAsync(cancellationToken: cts.Token);
    }

    /// <summary>
    /// <see cref="CronWorker"/> declares its OWN instance method with the
    /// identical name/signature/generic shape as this static extension
    /// method — C# always prefers an applicable instance method over an
    /// extension method, so <c>cronWorker.RegisterJobWithSchedule&lt;T&gt;(...)</c>
    /// can never actually dispatch here through ordinary extension-method
    /// call syntax; it silently resolves to <c>CronWorker</c>'s own method
    /// instead. This extension is only reachable via the explicit static
    /// call below — worth knowing before trusting it to be "the" registration
    /// path for a future caller.
    /// </summary>
    [Fact]
    public async Task RegisterJobWithSchedule_ExtensionMethod_OnlyReachableViaExplicitStaticCall()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TestCronJobExecutor>();
        services.AddSingleton<IQueueContext>(implementationInstance: new StubQueueContext());
        using ServiceProvider provider = services.BuildServiceProvider();
        CronWorker cronWorker = new(
            serviceProvider: provider,
            logger: provider.GetRequiredService<ILogger<CronWorker>>(),
            queueContext: provider.GetRequiredService<IQueueContext>()
        );

        NoMercyQueue.Extensions.ServiceCollectionExtensions.RegisterJobWithSchedule<TestCronJobExecutor>(
            cronWorker: cronWorker,
            jobType: "extension-static-call",
            serviceProvider: provider
        );

        Dictionary<string, Type> registeredJobs =
            (Dictionary<string, Type>)
                typeof(CronWorker)
                    .GetField(name: "_registeredJobs", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(obj: cronWorker)!;
        registeredJobs.Should().ContainKey(expected: "extension-static-call");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 5));
        await cronWorker.StopAsync(cancellationToken: cts.Token);
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
