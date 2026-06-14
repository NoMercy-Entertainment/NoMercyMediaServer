using NoMercy.Api.Hubs;
using NoMercy.Api.Services.Music;

namespace NoMercy.Service.Extensions;

public static class MusicHubServiceExtensions
{
    public static IServiceCollection AddMusicHubServices(this IServiceCollection services)
    {
        // Singletons - shared state across requests
        services.AddSingleton<MusicPlayerStateManager>();
        services.AddSingleton<MusicPlaybackService>();
        services.AddSingleton<MusicPlaybackCommandHandler>();
        // Single-flight lyric fetch coalescing across concurrent device requests.
        services.AddSingleton<LyricsResolver>();

        // Scoped - one instance per request
        services.AddScoped<MusicPlaylistManager>();
        services.AddScoped<MusicDeviceManager>();
        services.AddScoped<MusicHub>();

        return services;
    }
}
