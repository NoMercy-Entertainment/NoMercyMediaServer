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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;
using NoMercyQueue.Services;

namespace NoMercyQueue.Workers;

public class CronWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CronWorker> _logger;
    private readonly IQueueContext _queueContext;
    private readonly Dictionary<string, Type> _registeredJobs = new();

    // Runtime executor INSTANCES (e.g. one per loaded plugin) can't go through
    // _registeredJobs — that dictionary resolves an executor by DI type at fire
    // time, and a plugin instance was never registered in the container. Keyed
    // by JobName (== CronJobModel.JobType for these jobs); ExecuteJob checks
    // this map before falling back to DI resolution.
    private readonly Dictionary<string, ICronJobExecutor> _instanceExecutors = new();
    private readonly List<CronJobModel> _codeDefinedJobs = [];
    private readonly Dictionary<string, CancellationTokenSource> _jobCancellationTokens = new();
    private readonly Dictionary<string, Task> _jobTasks = new();
    private readonly IEnumerable<CronJobRegistration> _registrations;

    private static readonly TaskCompletionSource<bool> QueueWorkersReadyTcs = new();
    private static readonly TaskCompletionSource<bool> DatabaseReadyTcs = new();

    /// <summary>
    /// Signal that queue workers have started and cron jobs can begin execution.
    /// Call this from QueueRunner.Initialize() after workers are spawned.
    /// </summary>
    public static void SignalQueueWorkersReady()
    {
        QueueWorkersReadyTcs.TrySetResult(result: true);
    }

    /// <summary>
    /// Returns a task that completes when the database is ready for queries.
    /// </summary>
    public static Task<bool> GetDatabaseReadyTask() => DatabaseReadyTcs.Task;

    /// <summary>
    /// Signal that the database has been migrated and is ready for queries.
    /// Call this from DatabaseSeeder after migrations complete.
    /// </summary>
    public static void SignalDatabaseReady(bool success = true)
    {
        if (success)
            DatabaseReadyTcs.TrySetResult(result: true);
        else
            DatabaseReadyTcs.TrySetResult(result: false);
    }

    public CronWorker(
        IServiceProvider serviceProvider,
        ILogger<CronWorker> logger,
        IQueueContext queueContext,
        IEnumerable<CronJobRegistration>? registrations = null
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _queueContext = queueContext;
        _registrations = registrations ?? [];
    }

    public void RegisterJob<T>(
        string jobType,
        string name,
        string cronExpression,
        object? parameters = null
    )
        where T : class, ICronJobExecutor
    {
        _registeredJobs[key: jobType] = typeof(T);

        CronJobModel job = new()
        {
            Name = name,
            CronExpression = cronExpression,
            JobType = jobType,
            Parameters = parameters != null ? JsonConvert.SerializeObject(value: parameters) : null,
            IsEnabled = true,
            NextRun = CronService.GetNextOccurrence(cronExpression: cronExpression, baseTime: DateTime.UtcNow),
        };

        _codeDefinedJobs.Add(item: job);

        // Start individual worker for this job
        StartJobWorker(job: job);
    }

    public void RegisterJobWithSchedule<T>(string jobType, IServiceProvider serviceProvider)
        where T : class, ICronJobExecutor
    {
        _registeredJobs[key: jobType] = typeof(T);

        using IServiceScope scope = serviceProvider.CreateScope();
        T executor = scope.ServiceProvider.GetRequiredService<T>();

        DateTime currentTime = DateTime.UtcNow;
        DateTime nextRun = CronService.GetNextOccurrence(cronExpression: executor.CronExpression, baseTime: currentTime);

        // Individual registration logged at trace — summary logged in ExecuteAsync

        CronJobModel job = new()
        {
            Name = executor.JobName,
            CronExpression = executor.CronExpression,
            JobType = jobType,
            Parameters = null,
            IsEnabled = true,
            NextRun = nextRun,
            CreatedAt = currentTime,
        };

        _codeDefinedJobs.Add(item: job);

        // Start individual worker for this job
        StartJobWorker(job: job);
    }

    private void StartJobWorker(CronJobModel job)
    {
        if (
            _jobCancellationTokens.TryGetValue(
                key: job.JobType,
                value: out CancellationTokenSource? existingCts
            )
        )
        {
            _logger.LogDebug(
                message: "Worker already running for job: {JobName}, skipping duplicate registration",
                args: job.Name
            );
            return;
        }

        CancellationTokenSource cts = new();
        _jobCancellationTokens[key: job.JobType] = cts;

        Task task = Task.Run(function: async () => await JobWorkerLoop(job: job, cancellationToken: cts.Token), cancellationToken: cts.Token);
        _jobTasks[key: job.JobType] = task;

        // Per-job start logged at trace level only
    }

    private async Task JobWorkerLoop(CronJobModel job, CancellationToken cancellationToken)
    {
        // Wait for queue workers to be ready before starting cron job execution
        try
        {
            await QueueWorkersReadyTcs.Task.WaitAsync(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                message: "Job worker cancelled while waiting for queue workers: {JobName}",
                args: job.Name
            );
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Schedules are evaluated in UTC so Daily(n) means n:00 UTC on every host.
                DateTime currentTime = DateTime.UtcNow;

                // Check if it's time to run
                if (job.NextRun.HasValue && currentTime >= job.NextRun.Value)
                {
                    _logger.LogDebug(
                        message: "Executing cron job: {JobName} (Scheduled: {NextRun}, Current: {CurrentTime})", args: [job.Name, job.NextRun, currentTime]
                    );

                    bool success = await ExecuteJob(job: job, currentTime: currentTime, cancellationToken: cancellationToken);

                    job.LastRun = currentTime;
                    // Advance NextRun on BOTH success and failure — without
                    // this, a failing cron job re-fires every 30 s (the loop's
                    // poll interval) until it succeeds, hammering the failing
                    // code path and flooding logs. Failures still surface via
                    // ExecuteJob's LogError catch; the schedule shouldn't
                    // accelerate as a side effect.
                    job.NextRun = CronService.GetNextOccurrence(cronExpression: job.CronExpression, baseTime: currentTime);

                    if (success)
                    {
                        _logger.LogDebug(
                            message: "Successfully executed cron job: {JobName}. Next run: {NextRun}", args: [job.Name, job.NextRun]
                        );
                    }
                    else
                    {
                        _logger.LogWarning(
                            message: "Cron job {JobName} failed; rescheduling for {NextRun}", args: [job.Name, job.NextRun]
                        );
                    }

                    // Update database whether the job succeeded or not — keeps
                    // the persisted NextRun aligned with the in-memory state.
                    UpdateDatabaseJob(job: job);
                }

                // Check every 30 seconds instead of 1 minute for better precision
                await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 30), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(message: "Job worker cancelled for: {JobName}", args: job.Name);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(exception: ex, message: "Error in job worker for: {JobName}", args: job.Name);

                // Continue running even if there's an error
                await Task.Delay(delay: TimeSpan.FromMinutes(minutes: 1), cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<bool> ExecuteJob(
        CronJobModel job,
        DateTime currentTime,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (_instanceExecutors.TryGetValue(key: job.JobType, value: out ICronJobExecutor? instanceExecutor))
            {
                return await RunExecutor(executor: instanceExecutor, job: job, cancellationToken: cancellationToken);
            }

            if (!_registeredJobs.TryGetValue(key: job.JobType, value: out Type? jobExecutorType))
            {
                _logger.LogWarning(
                    message: "Job type {JobType} not registered for job {JobName}", args: [job.JobType, job.Name]
                );
                return false;
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            ICronJobExecutor executor = (ICronJobExecutor)
                scope.ServiceProvider.GetRequiredService(serviceType: jobExecutorType);

            return await RunExecutor(executor: executor, job: job, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(message: "Job execution cancelled for: {JobName}", args: job.Name);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(message: "Job execution timed out for: {JobName}", args: job.Name);
            return false;
        }
        catch (Exception ex)
        {
            // Was LogDebug — invisible at default Information level, so cron
            // failures showed up as the bare "Failed to execute …" line with no
            // exception detail, leaving the operator no way to diagnose.
            _logger.LogError(
                exception: ex,
                message: "Failed to execute cron job: {JobName} — {ErrorType}: {ErrorMessage}", args: [job.Name, ex.GetType().Name, ex.Message]
            );
            return false;
        }
    }

    private static async Task<bool> RunExecutor(
        ICronJobExecutor executor,
        CronJobModel job,
        CancellationToken cancellationToken
    )
    {
        using CancellationTokenSource timeoutCts = new(delay: TimeSpan.FromMinutes(minutes: 30));
        using CancellationTokenSource combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            token1: cancellationToken,
            token2: timeoutCts.Token
        );

        await executor.ExecuteAsync(parameters: job.Parameters.OrEmpty(), cancellationToken: combinedCts.Token);
        return true;
    }

    private void UpdateDatabaseJob(CronJobModel job)
    {
        try
        {
            CronJobModel? dbJob = _queueContext.FindCronJobByName(name: job.Name);

            if (dbJob != null)
            {
                dbJob.LastRun = job.LastRun;
                dbJob.NextRun = job.NextRun;
                _queueContext.UpdateCronJob(cronJob: dbJob);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Failed to update database for job: {JobName}", args: job.Name);
        }
    }

    private void RegisterDescriptor(CronJobRegistration registration)
    {
        _registeredJobs[key: registration.JobType] = registration.ExecutorType;

        using IServiceScope scope = _serviceProvider.CreateScope();
        ICronJobExecutor executor = (ICronJobExecutor)
            scope.ServiceProvider.GetRequiredService(serviceType: registration.ExecutorType);

        string cronExpression = registration.CronExpression ?? executor.CronExpression;
        DateTime now = DateTime.UtcNow;

        CronJobModel job = new()
        {
            Name = executor.JobName,
            CronExpression = cronExpression,
            JobType = registration.JobType,
            Parameters = null,
            IsEnabled = true,
            NextRun = CronService.GetNextOccurrence(cronExpression: cronExpression, baseTime: now),
            CreatedAt = now,
        };

        _codeDefinedJobs.Add(item: job);
        StartJobWorker(job: job);
    }

    // Schedules an executor INSTANCE directly — for runtime-discovered executors
    // (plugins) that have no DI registration to resolve by type. JobType is set
    // to the executor's own JobName so the instance registry lookup in
    // ExecuteJob and the dedup check in StartJobWorker key off the same value.
    public void RegisterExecutor(ICronJobExecutor executor)
    {
        _instanceExecutors[key: executor.JobName] = executor;

        DateTime now = DateTime.UtcNow;

        CronJobModel job = new()
        {
            Name = executor.JobName,
            CronExpression = executor.CronExpression,
            JobType = executor.JobName,
            Parameters = null,
            IsEnabled = true,
            NextRun = CronService.GetNextOccurrence(cronExpression: executor.CronExpression, baseTime: now),
            CreatedAt = now,
        };

        _codeDefinedJobs.Add(item: job);
        StartJobWorker(job: job);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (CronJobRegistration registration in _registrations)
        {
            try
            {
                RegisterDescriptor(registration: registration);
            }
            catch (Exception ex)
            {
                // A single unresolvable/misconfigured cron executor (e.g. a plugin
                // whose DI registration is missing) must NOT fault this
                // BackgroundService — that would trip the StopHost default and
                // refuse to boot the entire server. Skip it, keep the rest.
                _logger.LogError(
                    exception: ex,
                    message: "Failed to register cron job {JobType}; skipping it so remaining jobs and the server still start",
                    args: registration.JobType
                );
            }
        }

        _logger.LogDebug(
            message: "Cron Worker started with {JobCount} registered jobs",
            args: _codeDefinedJobs.Count
        );

        // Wait for database migrations to complete before querying the database
        try
        {
            using CancellationTokenSource timeoutCts = new(delay: TimeSpan.FromSeconds(seconds: 30));
            using CancellationTokenSource combinedCts =
                CancellationTokenSource.CreateLinkedTokenSource(token1: stoppingToken, token2: timeoutCts.Token);

            bool dbReady = await DatabaseReadyTcs.Task.WaitAsync(cancellationToken: combinedCts.Token);
            if (dbReady)
            {
                _logger.LogDebug(message: "Database ready — loading database job workers");
                StartDatabaseJobWorkers();
            }
            else
            {
                _logger.LogWarning(message: "Database seeding failed — skipping database job workers");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(message: "Cron Worker stopping before database was ready");
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                message: "Timed out waiting for database readiness — skipping database job workers"
            );
        }

        // Keep the main service running
        try
        {
            await Task.Delay(millisecondsDelay: Timeout.Infinite, cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(message: "Cron Worker stopping...");
        }
    }

    private void StartDatabaseJobWorkers()
    {
        try
        {
            IReadOnlyList<CronJobModel> dbJobs = _queueContext.GetEnabledCronJobs();

            foreach (CronJobModel job in dbJobs)
            {
                if (
                    _registeredJobs.ContainsKey(key: job.JobType)
                    || _instanceExecutors.ContainsKey(key: job.JobType)
                )
                {
                    StartJobWorker(job: job);
                }
                else
                {
                    _logger.LogWarning(
                        message: "Database job {JobName} has unregistered job type: {JobType}", args: [job.Name, job.JobType]
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "Failed to start database job workers");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(message: "Stopping all job workers...");

        // Cancel all job workers
        foreach (CancellationTokenSource cts in _jobCancellationTokens.Values)
        {
            await cts.CancelAsync();
        }

        // Wait for all workers to complete with a reduced timeout
        if (_jobTasks.Values.Count != 0)
        {
            try
            {
                await Task.WhenAll(tasks: _jobTasks.Values)
                    .WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5), cancellationToken: cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(message: "Some job workers did not stop within the timeout period");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(message: "Shutdown cancelled, forcing job worker termination");
            }
        }

        // Dispose resources
        foreach (CancellationTokenSource cts in _jobCancellationTokens.Values)
        {
            cts.Dispose();
        }

        await base.StopAsync(cancellationToken: cancellationToken);
    }
}
