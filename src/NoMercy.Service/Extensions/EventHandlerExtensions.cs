// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.Api.EventHandlers;
using NoMercy.Api.Services.Music;
using NoMercy.Database;
using NoMercy.Events;
using NoMercy.MediaProcessing.EventHandlers;
using NoMercy.MediaProcessing.Inbox;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.Networking.Messaging;
using NoMercy.Storage;

namespace NoMercy.Service.Extensions;

public static class EventHandlerExtensions
{
    public static IServiceCollection AddSignalREventHandlers(this IServiceCollection services)
    {
        services.AddSingleton<SignalRPlaybackEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(
                logger: sp.GetRequiredService<ILogger<SignalRPlaybackEventHandler>>(),
                eventBus: eventBus,
                clientMessenger: clientMessenger
            );
        });

        services.AddSingleton<SignalREncodingEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(
                logger: sp.GetRequiredService<ILogger<SignalREncodingEventHandler>>(),
                eventBus: eventBus,
                clientMessenger: clientMessenger
            );
        });

        services.AddSingleton<SignalRLibraryScanEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(
                logger: sp.GetRequiredService<ILogger<SignalRLibraryScanEventHandler>>(),
                eventBus: eventBus,
                clientMessenger: clientMessenger
            );
        });

        services.AddSingleton<SignalRLibraryRefreshEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus: eventBus, clientMessenger: clientMessenger);
        });

        services.AddSingleton<FileWatcherEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IStorageDriver storageDriver = sp.GetRequiredService<IStorageDriver>();
            IStorageFactory storageFactory = sp.GetRequiredService<IStorageFactory>();
            return new(
                logger: sp.GetRequiredService<ILogger<FileWatcherEventHandler>>(),
                eventBus: eventBus,
                storageDriver: storageDriver,
                storageFactory: storageFactory
            );
        });

        services.AddSingleton<FolderPathEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new(eventBus: eventBus, scopeFactory: scopeFactory);
        });

        services.AddSingleton<MusicLikeEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            MusicPlaybackService playbackService = sp.GetRequiredService<MusicPlaybackService>();
            return new(eventBus: eventBus, musicPlaybackService: playbackService);
        });

        services.AddSingleton<SignalRNotificationEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus: eventBus, clientMessenger: clientMessenger);
        });

        services.AddSingleton<DriveMonitorEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus: eventBus, clientMessenger: clientMessenger);
        });

        services.AddSingleton<CastEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(eventBus: eventBus, clientMessenger: clientMessenger);
        });

        services.AddSingleton<UserPermissionsEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(
                logger: sp.GetRequiredService<ILogger<UserPermissionsEventHandler>>(),
                eventBus: eventBus,
                clientMessenger: clientMessenger
            );
        });

        services.AddSingleton<IInboxMetadataProbe, TmdbMusicBrainzMetadataProbe>();
        services.AddSingleton<IInboxAudioTagReader>(implementationFactory: sp =>
        {
            IStorageFactory storageFactory = sp.GetRequiredService<IStorageFactory>();
            return new StorageAudioTagReader(storageFactory: storageFactory);
        });
        services.AddSingleton<InboxClassifier>(implementationFactory: sp =>
        {
            IInboxMetadataProbe probe = sp.GetRequiredService<IInboxMetadataProbe>();
            IInboxAudioTagReader tagReader = sp.GetRequiredService<IInboxAudioTagReader>();
            return new(probe: probe, tagReader: tagReader);
        });
        services.AddSingleton<InboxRoutingService>(implementationFactory: sp =>
        {
            IStorageFactory storageFactory = sp.GetRequiredService<IStorageFactory>();
            return new(storageFactory: storageFactory, jobDispatcher: new());
        });
        services.AddSingleton<InboxClassifierEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            InboxClassifier classifier = sp.GetRequiredService<InboxClassifier>();
            InboxRoutingService routing = sp.GetRequiredService<InboxRoutingService>();
            IStorageFactory storageFactory = sp.GetRequiredService<IStorageFactory>();
            return new(
                logger: sp.GetRequiredService<ILogger<InboxClassifierEventHandler>>(),
                eventBus: eventBus,
                classifier: classifier,
                routing: routing,
                contextFactory: () => sp.GetRequiredService<IDbContextFactory<MediaContext>>().CreateDbContext(),
                storageFactory: storageFactory
            );
        });
        services.AddSingleton<SignalRInboxEventHandler>(implementationFactory: sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(
                logger: sp.GetRequiredService<ILogger<SignalRInboxEventHandler>>(),
                eventBus: eventBus,
                clientMessenger: clientMessenger
            );
        });

        return services;
    }

    public static IServiceProvider InitializeSignalREventHandlers(
        this IServiceProvider serviceProvider
    )
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
        serviceProvider.GetRequiredService<InboxClassifierEventHandler>();
        serviceProvider.GetRequiredService<SignalRInboxEventHandler>();

        return serviceProvider;
    }
}
