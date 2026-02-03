using NoMercy.Api.Controllers.Socket;

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

        return services;
    }
}
