using NoMercy.Api.EventHandlers;
using NoMercy.Api.Services.Music;
using NoMercy.Events;
using NoMercy.MediaProcessing.EventHandlers;
using NoMercy.Networking.Messaging;

namespace NoMercy.Service.Extensions;

public static class EventHandlerExtensions
{
    public static IServiceCollection AddSignalREventHandlers(this IServiceCollection services)
    {
        services.AddSingleton<SignalRPlaybackEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus, clientMessenger);
        });

        services.AddSingleton<SignalREncodingEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus, clientMessenger);
        });

        services.AddSingleton<SignalRLibraryScanEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus, clientMessenger);
        });

        services.AddSingleton<SignalRLibraryRefreshEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus, clientMessenger);
        });

        services.AddSingleton<FileWatcherEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            return new(eventBus);
        });

        services.AddSingleton<FolderPathEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new(eventBus, scopeFactory);
        });

        services.AddSingleton<MusicLikeEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            MusicPlayerStateManager stateManager = sp.GetRequiredService<MusicPlayerStateManager>();
            MusicPlaybackService playbackService = sp.GetRequiredService<MusicPlaybackService>();
            IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new(eventBus, stateManager, playbackService, scopeFactory);
        });

        services.AddSingleton<SignalRNotificationEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus, clientMessenger);
        });

        services.AddSingleton<DriveMonitorEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus, clientMessenger);
        });

        services.AddSingleton<CastEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus, clientMessenger);
        });

        services.AddSingleton<UserPermissionsEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus, clientMessenger);
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
        serviceProvider.GetRequiredService<FolderPathEventHandler>();
        serviceProvider.GetRequiredService<MusicLikeEventHandler>();
        serviceProvider.GetRequiredService<SignalRNotificationEventHandler>();
        serviceProvider.GetRequiredService<DriveMonitorEventHandler>();
        serviceProvider.GetRequiredService<CastEventHandler>();
        serviceProvider.GetRequiredService<UserPermissionsEventHandler>();

        return serviceProvider;
    }
}
