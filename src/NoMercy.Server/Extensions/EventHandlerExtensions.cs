using NoMercy.Api.EventHandlers;
using NoMercy.Events;

namespace NoMercy.Server.Extensions;

public static class EventHandlerExtensions
{
    public static IServiceCollection AddSignalREventHandlers(this IServiceCollection services)
    {
        services.AddSingleton<SignalRPlaybackEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            return new SignalRPlaybackEventHandler(eventBus);
        });

        services.AddSingleton<SignalREncodingEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            return new SignalREncodingEventHandler(eventBus);
        });

        services.AddSingleton<SignalRLibraryScanEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            return new SignalRLibraryScanEventHandler(eventBus);
        });

        return services;
    }

    public static IServiceProvider InitializeSignalREventHandlers(this IServiceProvider serviceProvider)
    {
        // Resolve handlers to trigger their construction and event subscriptions
        serviceProvider.GetRequiredService<SignalRPlaybackEventHandler>();
        serviceProvider.GetRequiredService<SignalREncodingEventHandler>();
        serviceProvider.GetRequiredService<SignalRLibraryScanEventHandler>();

        return serviceProvider;
    }
}
