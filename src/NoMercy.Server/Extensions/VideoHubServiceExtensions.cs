using NoMercy.Api.Hubs;
using NoMercy.Api.Services.Video;

namespace NoMercy.Server.Extensions;

public static class VideoHubServiceExtensions
{
    public static IServiceCollection AddVideoHubServices(this IServiceCollection services)
    {
        // Singletons - shared state across requests
        services.AddSingleton<VideoPlayerStateManager>();
        services.AddSingleton<VideoPlaybackService>();
        services.AddSingleton<VideoPlaybackCommandHandler>();

        // Scoped - one instance per request
        services.AddScoped<VideoPlaylistManager>();
        services.AddScoped<VideoDeviceManager>();
        services.AddScoped<VideoHub>();

        return services;
    }
}