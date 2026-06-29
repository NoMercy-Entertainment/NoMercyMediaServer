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
using Microsoft.Extensions.Logging;
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
        services.AddSingleton<SignalRPlaybackEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(
                sp.GetRequiredService<ILogger<SignalRPlaybackEventHandler>>(),
                eventBus,
                clientMessenger
            );
        });

        services.AddSingleton<SignalREncodingEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(
                sp.GetRequiredService<ILogger<SignalREncodingEventHandler>>(),
                eventBus,
                clientMessenger
            );
        });

        services.AddSingleton<SignalRLibraryScanEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new(
                sp.GetRequiredService<ILogger<SignalRLibraryScanEventHandler>>(),
                eventBus,
                clientMessenger
            );
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
            IStorageDriver storageDriver = sp.GetRequiredService<IStorageDriver>();
            IStorageFactory storageFactory = sp.GetRequiredService<IStorageFactory>();
            return new(eventBus, storageDriver, storageFactory);
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
            MusicPlaybackService playbackService = sp.GetRequiredService<MusicPlaybackService>();
            return new(eventBus, playbackService);
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
            return new(
                sp.GetRequiredService<ILogger<UserPermissionsEventHandler>>(),
                eventBus,
                clientMessenger
            );
        });

        services.AddSingleton<IInboxMetadataProbe, TmdbMusicBrainzMetadataProbe>();
        services.AddSingleton<IInboxAudioTagReader>(sp =>
        {
            IStorageFactory storageFactory = sp.GetRequiredService<IStorageFactory>();
            return new StorageAudioTagReader(storageFactory);
        });
        services.AddSingleton<InboxClassifier>(sp =>
        {
            IInboxMetadataProbe probe = sp.GetRequiredService<IInboxMetadataProbe>();
            IInboxAudioTagReader tagReader = sp.GetRequiredService<IInboxAudioTagReader>();
            return new InboxClassifier(probe, tagReader);
        });
        services.AddSingleton<InboxRoutingService>(sp =>
        {
            IStorageFactory storageFactory = sp.GetRequiredService<IStorageFactory>();
            return new InboxRoutingService(storageFactory, new JobDispatcher());
        });
        services.AddSingleton<InboxClassifierEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            InboxClassifier classifier = sp.GetRequiredService<InboxClassifier>();
            InboxRoutingService routing = sp.GetRequiredService<InboxRoutingService>();
            IStorageFactory storageFactory = sp.GetRequiredService<IStorageFactory>();
            return new InboxClassifierEventHandler(
                eventBus,
                classifier,
                routing,
                () => sp.GetRequiredService<IDbContextFactory<MediaContext>>().CreateDbContext(),
                storageFactory
            );
        });
        services.AddSingleton<SignalRInboxEventHandler>(sp =>
        {
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            IClientMessenger clientMessenger = sp.GetRequiredService<IClientMessenger>();
            return new SignalRInboxEventHandler(
                sp.GetRequiredService<ILogger<SignalRInboxEventHandler>>(),
                eventBus,
                clientMessenger
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
