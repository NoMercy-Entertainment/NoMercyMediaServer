using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Information;
using NoMercy.Queue.MediaServer.Configuration;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Models;

namespace NoMercy.Queue.MediaServer;

public static class ServiceRegistration
{
    public static IServiceCollection AddMediaServerQueue(this IServiceCollection services)
    {
        services.AddSingleton<IQueueContext>(_ => new EfQueueContextAdapter());
        services.AddSingleton<IConfigurationStore, MediaConfigurationStore>();
        services.AddSingleton<QueueRunner>(sp =>
        {
            IQueueContext queueContext = sp.GetRequiredService<IQueueContext>();
            IConfigurationStore configStore = sp.GetRequiredService<IConfigurationStore>();
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            NoMercy.NmSystem.Lifecycle.IServerPhaseTracker? phaseTracker =
                sp.GetService<NoMercy.NmSystem.Lifecycle.IServerPhaseTracker>();
            QueueConfiguration configuration = new()
            {
                WorkerCounts = new()
                {
                    [Config.LibraryWorkers.Key] = Config.LibraryWorkers.Value,
                    [Config.ImportWorkers.Key] = Config.ImportWorkers.Value,
                    [Config.ExtrasWorkers.Key] = Config.ExtrasWorkers.Value,
                    [Config.EncoderWorkers.Key] = Config.EncoderWorkers.Value,
                    [Config.EncoderTaskWorkers.Key] = Config.EncoderTaskWorkers.Value,
                    [Config.CronWorkers.Key] = Config.CronWorkers.Value,
                    [Config.ImageWorkers.Key] = Config.ImageWorkers.Value,
                    [Config.FileWorkers.Key] = Config.FileWorkers.Value,
                    [Config.MusicWorkers.Key] = Config.MusicWorkers.Value,
                },
            };
            return new(
                queueContext,
                configuration,
                loggerFactory,
                configStore,
                scopeFactory,
                phaseTracker
            );
        });
        services.AddSingleton<JobDispatcher>(sp => sp.GetRequiredService<QueueRunner>().Dispatcher);

        // Phase 4.14 — orphan job recovery on boot. Runs before QueueRunner
        // resets reserved jobs, so we can distinguish first-time orphans
        // (which deserve one retry) from repeat offenders (which get
        // moved to FailedJobs).
        services.AddHostedService<OrphanJobRecoveryHostedService>();

        return services;
    }
}
