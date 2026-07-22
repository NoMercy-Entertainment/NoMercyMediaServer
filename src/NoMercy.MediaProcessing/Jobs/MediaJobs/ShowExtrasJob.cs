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
        : base(storageFactory: storageFactory, storageDriver: storageDriver, loggerFactory: loggerFactory) { }

    public override string QueueName => "extras";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        ShowRepository showRepository = new(context: context);
        ShowManager showManager = new(
            showRepository: showRepository,
            jobDispatcher: jobDispatcher,
            storageFactory: StorageFactory,
            mediaTypeClassifier: new MediaTypeClassifier(),
            logger: LoggerFactory.CreateLogger<ShowManager>()
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

        // Each Store* call fetches from TMDB and/or writes to storage (NFS in
        // production) — bounded so a stall fails the job instead of hanging it
        // and holding an `extras` worker slot forever. See
        // JobOperationTimeoutExtensions for why per-call rather than
        // per-token cancellation.
        await personManager.Store(show: Storage).WithTimeout(operationName: nameof(PersonManager.Store));

        await showManager.StoreImages(show: Storage).WithTimeout(operationName: nameof(ShowManager.StoreImages));
        await showManager.StoreSimilar(show: Storage).WithTimeout(operationName: nameof(ShowManager.StoreSimilar));
        await showManager
            .StoreRecommendations(show: Storage)
            .WithTimeout(operationName: nameof(ShowManager.StoreRecommendations));
        await showManager
            .StoreAlternativeTitles(show: Storage)
            .WithTimeout(operationName: nameof(ShowManager.StoreAlternativeTitles));
        await showManager
            .StoreWatchProviders(show: Storage)
            .WithTimeout(operationName: nameof(ShowManager.StoreWatchProviders));
        await showManager.StoreVideos(show: Storage).WithTimeout(operationName: nameof(ShowManager.StoreVideos));
        await showManager.StoreNetworks(show: Storage).WithTimeout(operationName: nameof(ShowManager.StoreNetworks));
        await showManager.StoreCompanies(show: Storage).WithTimeout(operationName: nameof(ShowManager.StoreCompanies));
        await showManager.StoreKeywords(show: Storage).WithTimeout(operationName: nameof(ShowManager.StoreKeywords));

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent { QueryKey = ["base", "info", Storage.Id.ToString()] }
            );
    }
}
