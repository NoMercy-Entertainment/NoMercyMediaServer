using NoMercy.Api.EventHandlers;
using NoMercy.Events;
using NoMercy.MediaProcessing.EventHandlers;

namespace NoMercy.Service.Extensions;

public static class EventHandlerExtensions
{
    public static IServiceCollection AddSignalREventHandlers(this IServiceCollection services)
    {
        services.AddSingleton<SignalRPlaybackEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            return new(eventBus);
        });

        services.AddSingleton<SignalREncodingEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            return new(eventBus);
        });

        services.AddSingleton<SignalRLibraryScanEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            return new(eventBus);
        });

        services.AddSingleton<SignalRLibraryRefreshEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            return new(eventBus);
        });

        services.AddSingleton<FileWatcherEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            return new(eventBus);
        });

        return services;
    }

    public static IServiceProvider InitializeSignalREventHandlers(this IServiceProvider serviceProvider)
    {
        // Resolve handlers to trigger their construction and event subscriptions
        serviceProvider.GetRequiredService<SignalRPlaybackEventHandler>();
        serviceProvider.GetRequiredService<SignalREncodingEventHandler>();
        serviceProvider.GetRequiredService<SignalRLibraryScanEventHandler>();
        serviceProvider.GetRequiredService<SignalRLibraryRefreshEventHandler>();
        serviceProvider.GetRequiredService<FileWatcherEventHandler>();

        return serviceProvider;
    }
}
