using NoMercy.Api.Controllers.Socket;
using NoMercy.Api.Controllers.Socket.video;

namespace NoMercy.Server.services;

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