using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.NmSystem.Information;
using NoMercy.Queue.Core.Interfaces;
using NoMercy.Queue.Core.Models;
using NoMercy.Queue.MediaServer.Configuration;

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
            QueueConfiguration configuration = new()
            {
                WorkerCounts = new()
                {
                    [Config.LibraryWorkers.Key] = Config.LibraryWorkers.Value,
                    [Config.ImportWorkers.Key] = Config.ImportWorkers.Value,
                    [Config.ExtrasWorkers.Key] = Config.ExtrasWorkers.Value,
                    [Config.EncoderWorkers.Key] = Config.EncoderWorkers.Value,
                    [Config.CronWorkers.Key] = Config.CronWorkers.Value,
                    [Config.ImageWorkers.Key] = Config.ImageWorkers.Value,
                    [Config.FileWorkers.Key] = Config.FileWorkers.Value,
                    [Config.MusicWorkers.Key] = Config.MusicWorkers.Value
                }
            };
            return new(queueContext, configuration, configStore);
        });
        services.AddSingleton<JobDispatcher>(sp => sp.GetRequiredService<QueueRunner>().Dispatcher);

        return services;
    }
}
