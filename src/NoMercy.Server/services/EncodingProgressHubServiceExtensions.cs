using NoMercy.Api.Controllers.Socket;
using NoMercy.EncoderV2.DependencyInjection;
using NoMercy.EncoderV2.Workers;

namespace NoMercy.Server.services;

/// <summary>
/// Extension methods for registering encoding progress hub services.
/// </summary>
public static class EncodingProgressHubServiceExtensions
{
    /// <summary>
    /// Adds encoding progress hub services to the DI container.
    /// </summary>
    public static IServiceCollection AddEncodingProgressHubServices(this IServiceCollection services)
    {
        services.AddScoped<EncodingProgressHub>();
        services.AddSingleton<IEncodingProgressHubService, EncodingProgressHubService>();

        // Add the broadcaster adapter for EncoderV2 worker integration
        services.AddSingleton<IEncodingProgressBroadcaster, EncodingProgressBroadcasterAdapter>();

        // Add EncoderV2 core services
        services.AddEncoderV2();

        // Add the EncoderV2 task worker to process encoding tasks
        services.AddEncoderV2TaskWorker(options =>
        {
            options.MaxConcurrentTasks = 1; // Default to single task processing
            options.PollingIntervalMs = 1000;
            options.LocalTasksOnly = true;
        });

        return services;
    }
}
