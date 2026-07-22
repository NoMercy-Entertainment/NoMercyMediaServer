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
using NoMercy.MediaProcessing.Movies;
using NoMercy.MediaProcessing.People;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class MovieExtrasJob : AbstractMediaExraDataJob<TmdbMovieAppends>
{
    public MovieExtrasJob() { }

    public MovieExtrasJob(
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

        PersonRepository personRepository = new(
            context: context,
            logger: LoggerFactory.CreateLogger<PersonRepository>()
        );
        PersonManager personManager = new(
            personRepository: personRepository,
            jobDispatcher: jobDispatcher,
            logger: LoggerFactory.CreateLogger<PersonManager>()
        );

        await personManager.Store(movie: Storage);

        await movieManager.StoreImages(movie: Storage);
        await movieManager.StoreSimilar(movie: Storage);
        await movieManager.StoreRecommendations(movie: Storage);
        await movieManager.StoreAlternativeTitles(movie: Storage);
        await movieManager.StoreWatchProviders(movie: Storage);
        await movieManager.StoreVideos(movie: Storage);
        await movieManager.StoreCompanies(movie: Storage);
        await movieManager.StoreKeywords(movie: Storage);

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent { QueryKey = ["base", "info", Storage.Id.ToString()] }
            );
    }
}
