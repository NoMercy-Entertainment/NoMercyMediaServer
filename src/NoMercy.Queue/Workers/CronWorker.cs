using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Queue.Interfaces;
using NoMercy.Queue.Services;

namespace NoMercy.Queue.Workers;

public class CronWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CronWorker> _logger;
    private readonly Dictionary<string, Type> _registeredJobs = new();
    private readonly List<CronJob> _codeDefinedJobs = [];
    private readonly Dictionary<string, CancellationTokenSource> _jobCancellationTokens = new();
    private readonly Dictionary<string, Task> _jobTasks = new();

    private static readonly TaskCompletionSource<bool> QueueWorkersReadyTcs = new();

    /// <summary>
    /// Signal that queue workers have started and cron jobs can begin execution.
    /// Call this from QueueRunner.Initialize() after workers are spawned.
    /// </summary>
    public static void SignalQueueWorkersReady()
    {
        QueueWorkersReadyTcs.TrySetResult(true);
    }

    public CronWorker(IServiceProvider serviceProvider, ILogger<CronWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void RegisterJob<T>(string jobType, string name, string cronExpression, object? parameters = null)
        where T : class, ICronJobExecutor
    {
        _registeredJobs[jobType] = typeof(T);
        
        CronJob job = new()
        {
            Name = name,
            CronExpression = cronExpression,
            JobType = jobType,
            Parameters = parameters != null ? JsonConvert.SerializeObject(parameters) : null,
            IsEnabled = true,
            NextRun = CronService.GetNextOccurrence(cronExpression, DateTime.Now)
        };
        
        _codeDefinedJobs.Add(job);
        
        // Start individual worker for this job
        StartJobWorker(job);
    }

    public void RegisterJobWithSchedule<T>(string jobType, IServiceProvider serviceProvider)
        where T : class, ICronJobExecutor
    {
        _registeredJobs[jobType] = typeof(T);

        using IServiceScope scope = serviceProvider.CreateScope();
        T executor = scope.ServiceProvider.GetRequiredService<T>();

        DateTime currentTime = DateTime.Now;
        DateTime nextRun = CronService.GetNextOccurrence(executor.CronExpression, currentTime);

        _logger.LogDebug("Registered job {JobType}: {JobName}, Cron: {Cron}, Next run: {NextRun}",
            jobType, executor.JobName, executor.CronExpression, nextRun);

        CronJob job = new()
        {
            Name = executor.JobName,
            CronExpression = executor.CronExpression,
            JobType = jobType,
            Parameters = null,
            IsEnabled = true,
            NextRun = nextRun,
            CreatedAt = currentTime,
        };

        _codeDefinedJobs.Add(job);
        
        // Start individual worker for this job
        StartJobWorker(job);
    }

    private void StartJobWorker(CronJob job)
    {
        CancellationTokenSource cts = new();
        _jobCancellationTokens[job.JobType] = cts;

        Task task = Task.Run(async () => await JobWorkerLoop(job, cts.Token), cts.Token);
        _jobTasks[job.JobType] = task;

        _logger.LogDebug("Started worker thread for job: {JobName}", job.Name);
    }

    private async Task JobWorkerLoop(CronJob job, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Job worker started for: {JobName}, waiting for queue workers...", job.Name);

        // Wait for queue workers to be ready before starting cron job execution
        try
        {
            await QueueWorkersReadyTcs.Task.WaitAsync(cancellationToken);
            _logger.LogDebug("Queue workers ready, cron job {JobName} can now execute", job.Name);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job worker cancelled while waiting for queue workers: {JobName}", job.Name);
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                DateTime currentTime = DateTime.Now;

                // Check if it's time to run
                if (job.NextRun.HasValue && currentTime >= job.NextRun.Value)
                {
                    _logger.LogDebug("Executing cron job: {JobName} (Scheduled: {NextRun}, Current: {CurrentTime})",
                        job.Name, job.NextRun, currentTime);

                    bool success = await ExecuteJob(job, currentTime, cancellationToken);

                    if (success)
                    {
                        job.LastRun = currentTime;
                        job.NextRun = CronService.GetNextOccurrence(job.CronExpression, currentTime);

                        _logger.LogDebug("Successfully executed cron job: {JobName}. Next run: {NextRun}",
                            job.Name, job.NextRun);

                        // Update database if this is a database job
                        await UpdateDatabaseJob(job, cancellationToken);
                    }
                }

                // Check every 30 seconds instead of 1 minute for better precision
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Job worker cancelled for: {JobName}", job.Name);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job worker for: {JobName}", job.Name);
                
                // Continue running even if there's an error
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }
    }

    private async Task<bool> ExecuteJob(CronJob job, DateTime currentTime, CancellationToken cancellationToken)
    {
        try
        {
            if (!_registeredJobs.TryGetValue(job.JobType, out Type? jobExecutorType))
            {
                _logger.LogWarning("Job type {JobType} not registered for job {JobName}", job.JobType, job.Name);
                return false;
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            ICronJobExecutor executor = (ICronJobExecutor)scope.ServiceProvider.GetRequiredService(jobExecutorType);
            
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(30));
            using CancellationTokenSource combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await executor.ExecuteAsync(job.Parameters ?? string.Empty, combinedCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Job execution cancelled for: {JobName}", job.Name);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Job execution timed out for: {JobName}", job.Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to execute cron job: {JobName}", job.Name);
            return false;
        }
    }

    private async Task UpdateDatabaseJob(CronJob job, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            await using QueueContext dbContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            CronJob? dbJob = await dbContext.CronJobs
                .FirstOrDefaultAsync(j => j.JobType == job.JobType, cancellationToken);

            if (dbJob != null)
            {
                dbJob.LastRun = job.LastRun;
                dbJob.NextRun = job.NextRun;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update database for job: {JobName}", job.Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cron Worker started with individual job workers");

        // Load and start workers for database jobs
        await StartDatabaseJobWorkers(stoppingToken);

        // Keep the main service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cron Worker stopping...");
        }
    }

    private async Task StartDatabaseJobWorkers(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            await using QueueContext dbContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

            List<CronJob> dbJobs = await dbContext.CronJobs
                .Where(j => j.IsEnabled)
                .ToListAsync(cancellationToken);

            foreach (CronJob job in dbJobs)
            {
                if (_registeredJobs.ContainsKey(job.JobType))
                {
                    StartJobWorker(job);
                }
                else
                {
                    _logger.LogWarning("Database job {JobName} has unregistered job type: {JobType}", 
                        job.Name, job.JobType);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start database job workers");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping all job workers...");

        // Cancel all job workers
        foreach (CancellationTokenSource cts in _jobCancellationTokens.Values)
        {
            await cts.CancelAsync();
        }

        // Wait for all workers to complete
        if (_jobTasks.Values.Count != 0)
        {
            try
            {
                await Task.WhenAll(_jobTasks.Values).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Some job workers did not stop within the timeout period");
            }
        }

        // Dispose resources
        foreach (CancellationTokenSource cts in _jobCancellationTokens.Values)
        {
            cts.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}