using NoMercy.Api.Controllers.Socket;
using NoMercy.Api.Controllers.Socket.music;

namespace NoMercy.Server.services;

public static class MusicHubServiceExtensions
{
    public static IServiceCollection AddMusicHubServices(this IServiceCollection services)
    {
        // Singletons - shared state across requests
        services.AddSingleton<PlayerStateManager>();
        services.AddSingleton<PlaybackService>();
        services.AddSingleton<PlaybackCommandHandler>();

        // Scoped - one instance per request
        services.AddScoped<PlaylistManager>();
        services.AddScoped<DeviceManager>();
        services.AddScoped<MusicHub>();

        return services;
    }
}