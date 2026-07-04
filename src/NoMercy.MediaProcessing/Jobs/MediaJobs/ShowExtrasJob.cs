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
using NoMercy.MediaProcessing.People;
using NoMercy.MediaProcessing.Shows;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class ShowExtrasJob : AbstractMediaExraDataJob<TmdbTvShowAppends>
{
    public ShowExtrasJob() { }

    public ShowExtrasJob(
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

        ShowRepository showRepository = new(context);
        ShowManager showManager = new(
            showRepository,
            jobDispatcher,
            StorageFactory,
            new MediaTypeClassifier(),
            LoggerFactory.CreateLogger<ShowManager>()
        );

        PersonRepository personRepository = new(
            context,
            LoggerFactory.CreateLogger<PersonRepository>()
        );
        PersonManager personManager = new(
            personRepository,
            jobDispatcher,
            LoggerFactory.CreateLogger<PersonManager>()
        );

        await personManager.Store(Storage);

        await showManager.StoreImages(Storage);
        await showManager.StoreSimilar(Storage);
        await showManager.StoreRecommendations(Storage);
        await showManager.StoreAlternativeTitles(Storage);
        await showManager.StoreWatchProviders(Storage);
        await showManager.StoreVideos(Storage);
        await showManager.StoreNetworks(Storage);
        await showManager.StoreCompanies(Storage);
        await showManager.StoreKeywords(Storage);

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshedEvent { QueryKey = ["base", "info", Storage.Id.ToString()] }
            );
    }
}
