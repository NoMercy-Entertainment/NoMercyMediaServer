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
        : base(storageFactory, storageDriver, loggerFactory) { }

    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        MovieRepository movieRepository = new(context);
        MovieManager movieManager = new(
            movieRepository,
            jobDispatcher,
            StorageFactory,
            StorageDriver,
            LoggerFactory.CreateLogger<MovieManager>()
        );

        PersonRepository personRepository = new(context, LoggerFactory.CreateLogger<PersonRepository>());
        PersonManager personManager = new(
            personRepository,
            jobDispatcher,
            LoggerFactory.CreateLogger<PersonManager>()
        );

        await personManager.Store(Storage);

        await movieManager.StoreImages(Storage);
        await movieManager.StoreSimilar(Storage);
        await movieManager.StoreRecommendations(Storage);
        await movieManager.StoreAlternativeTitles(Storage);
        await movieManager.StoreWatchProviders(Storage);
        await movieManager.StoreVideos(Storage);
        await movieManager.StoreCompanies(Storage);
        await movieManager.StoreKeywords(Storage);

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshedEvent { QueryKey = ["base", "info", Storage.Id.ToString()] }
            );
    }
}
