using Microsoft.Extensions.DependencyInjection;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Workers;

namespace NoMercyQueue.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCronWorker(this IServiceCollection services)
    {
        services.AddSingleton<CronWorker>();
        services.AddHostedService<CronWorker>(provider => provider.GetRequiredService<CronWorker>());
            
        return services;
    }

    public static IServiceCollection RegisterCronJob<T>(this IServiceCollection services, string jobType)
        where T : class, ICronJobExecutor
    {
        services.AddScoped<T>();
        return services;
    }
    
    public static void RegisterJobWithSchedule<T>(this CronWorker cronWorker, string jobType, IServiceProvider serviceProvider)
        where T : class, ICronJobExecutor
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        T tempInstance = scope.ServiceProvider.GetRequiredService<T>();

        cronWorker.RegisterJob<T>(jobType, tempInstance.JobName, tempInstance.CronExpression);
    }
}