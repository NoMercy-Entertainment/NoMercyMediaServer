using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMercy.Api.Hubs;
using NoMercy.Api.Services.LiveTranscode;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Service.Extensions;

public static class LiveTranscodeHubServiceExtensions
{
    public static IServiceCollection AddLiveTranscodeHubServices(this IServiceCollection services)
    {
        services.AddScoped<LiveTranscodeHub>();

        // Replace the NoOp transport registered by AddNoMercyEncoder with the real
        // SignalR-backed implementation. Uses Replace (not TryAdd) so the Api-layer
        // transport wins over the encoder's TryAddSingleton default.
        services.Replace(
            ServiceDescriptor.Singleton<ILiveSessionTransport, SignalRLiveSessionTransport>()
        );

        return services;
    }
}
