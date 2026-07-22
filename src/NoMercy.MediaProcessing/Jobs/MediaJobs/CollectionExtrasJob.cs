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

// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.Collections;
using NoMercy.MediaProcessing.Movies;
using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class CollectionExtrasJob : AbstractMediaExraDataJob<TmdbCollectionAppends>
{
    public CollectionExtrasJob() { }

    public CollectionExtrasJob(
        IStorageFactory storageFactory,
        IStorageDriver storageDriver,
        ILoggerFactory loggerFactory
    )
        : base(storageFactory: storageFactory, storageDriver: storageDriver, loggerFactory: loggerFactory) { }

    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        MovieRepository movieRepository = new(context: context);
        MovieManager movieManager = new(
            movieRepository: movieRepository,
            jobDispatcher: jobDispatcher,
            storageFactory: StorageFactory,
            logger: LoggerFactory.CreateLogger<MovieManager>()
        );

        CollectionRepository collectionRepository = new(context: context);
        CollectionManager collectionManager = new(
            collectionRepository: collectionRepository,
            movieManager: movieManager,
            jobDispatcher: jobDispatcher,
            logger: LoggerFactory.CreateLogger<CollectionManager>()
        );

        await collectionManager.StoreImages(collection: Storage);

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent { QueryKey = ["collection", Storage.Id.ToString()] }
            );
    }
}
