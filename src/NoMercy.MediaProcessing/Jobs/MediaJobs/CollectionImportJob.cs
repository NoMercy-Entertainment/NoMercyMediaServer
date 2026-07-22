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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
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
public class CollectionImportJob : AbstractMediaJob
{
    public CollectionImportJob() { }

    public CollectionImportJob(
        IStorageFactory storageFactory,
        IStorageDriver storageDriver,
        ILoggerFactory loggerFactory
    )
        : base(storageFactory: storageFactory, storageDriver: storageDriver, loggerFactory: loggerFactory) { }

    public override string QueueName => "import";
    public override int Priority => 4;

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

        Library collectionLibrary = await context
            .Libraries.Where(predicate: f => f.Id == LibraryId)
            .Include(navigationPropertyPath: f => f.FolderLibraries)
                .ThenInclude(navigationPropertyPath: f => f.Folder)
            .FirstAsync();

        TmdbCollectionAppends? collectionAppends = await collectionManager.Add(
            id: Id,
            library: collectionLibrary
        );
        if (collectionAppends == null)
            return;

        await collectionManager.AddCollectionMovies(collectionAppends: collectionAppends, library: collectionLibrary);

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent { QueryKey = ["libraries", LibraryId.ToString()] }
            );

            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent { QueryKey = ["collection"] }
            );

            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent { QueryKey = ["collection", Id.ToString()] }
            );
        }
    }
}
