using NoMercy.Api.Controllers.Socket;
using NoMercy.Api.Controllers.Socket.music;

namespace NoMercy.Server.Services;

public static class MusicHubServiceExtensions
{
    public static IServiceCollection AddMusicHubServices(this IServiceCollection services)
    {
        // Singletons - shared state across requests
        services.AddSingleton<MusicPlayerStateManager>();
        services.AddSingleton<MusicPlaybackService>();
        services.AddSingleton<MusicPlaybackCommandHandler>();

        // Scoped - one instance per request
        services.AddScoped<MusicPlaylistManager>();
        services.AddScoped<MusicDeviceManager>();
        services.AddScoped<MusicHub>();

        return services;
    }
}