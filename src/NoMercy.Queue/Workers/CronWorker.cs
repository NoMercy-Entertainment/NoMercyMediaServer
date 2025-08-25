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
    private readonly List<CronJob> _codeDefinedJobs = new();

    public CronWorker(IServiceProvider serviceProvider, ILogger<CronWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void RegisterJob<T>(string jobType, string name, string cronExpression, object? parameters = null) 
        where T : class, ICronJobExecutor
    {
        _registeredJobs[jobType] = typeof(T);
        _codeDefinedJobs.Add(new()
        {
            Name = name,
            CronExpression = cronExpression,
            JobType = jobType,
            Parameters = parameters != null ? JsonConvert.SerializeObject(parameters) : null,
            IsEnabled = true
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cron Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessJobs(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing cron jobs");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessJobs(CancellationToken cancellationToken)
    {
        DateTime currentTime = DateTime.UtcNow;

        // Process code-defined jobs
        foreach (CronJob job in _codeDefinedJobs.Where(j => j.IsEnabled))
        {
            if (!await ProcessJob(job, currentTime, cancellationToken)) continue;
            
            job.LastRun = currentTime;
            job.NextRun = CronService.GetNextOccurrence(job.CronExpression, currentTime);
            job.UpdatedAt = currentTime;
        }

        // Process database jobs - Create scope here for DbContext
        using IServiceScope scope = _serviceProvider.CreateScope();
        await using QueueContext dbContext = scope.ServiceProvider.GetRequiredService<QueueContext>();

        List<CronJob> dbJobs = await dbContext.CronJobs
            .Where(j => j.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (CronJob job in dbJobs)
        {
            if (!await ProcessJob(job, currentTime, cancellationToken)) continue;
            
            job.LastRun = currentTime;
            job.NextRun = CronService.GetNextOccurrence(job.CronExpression, currentTime);
            job.UpdatedAt = currentTime;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ProcessJob(CronJob job, DateTime currentTime, CancellationToken cancellationToken)
    {
        try
        { 
            if (job.NextRun.HasValue && currentTime < job.NextRun.Value)
                return false;

            if (!job.NextRun.HasValue)
            {
                DateTime lastRun = job.LastRun ?? currentTime;
                if (!CronService.ShouldRun(job.CronExpression, lastRun, currentTime))
                    return false;
            }
            
            if (!_registeredJobs.TryGetValue(job.JobType, out Type? jobExecutorType))
            {
                _logger.LogWarning("Job type {JobType} not registered for job {JobName}", job.JobType, job.Name);
                return false;
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            ICronJobExecutor executor = (ICronJobExecutor)scope.ServiceProvider.GetRequiredService(jobExecutorType);

            _logger.LogInformation("Executing cron job: {JobName}", job.Name);

            await executor.ExecuteAsync(job.Parameters ?? string.Empty, cancellationToken);

            _logger.LogInformation("Successfully executed cron job: {JobName}", job.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute cron job: {JobName}", job.Name);
            return false;
        }
    }
}