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
using NoMercy.Providers.AniList;
using NoMercy.Providers.Jikan;
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
        MediaTypeClassifier mediaTypeClassifier = new(
            new AniListMetadataProvider(),
            new JikanMetadataProvider()
        );
        AnimeEnrichmentService animeEnrichmentService = new(
            mediaTypeClassifier,
            new AniListMetadataProvider(),
            new JikanMetadataProvider(),
            showRepository,
            new NoMercy.MediaProcessing.Movies.MovieRepository(context)
        );
        ShowManager showManager = new(
            showRepository,
            jobDispatcher,
            StorageFactory,
            mediaTypeClassifier,
            animeEnrichmentService,
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

        // Each Store* call fetches from TMDB and/or writes to storage (NFS in
        // production) — bounded so a stall fails the job instead of hanging it
        // and holding an `extras` worker slot forever. See
        // JobOperationTimeoutExtensions for why per-call rather than
        // per-token cancellation.
        await personManager.Store(Storage).WithTimeout(nameof(PersonManager.Store));

        await showManager.StoreImages(Storage).WithTimeout(nameof(ShowManager.StoreImages));
        await showManager.StoreSimilar(Storage).WithTimeout(nameof(ShowManager.StoreSimilar));
        await showManager
            .StoreRecommendations(Storage)
            .WithTimeout(nameof(ShowManager.StoreRecommendations));
        await showManager
            .StoreAlternativeTitles(Storage)
            .WithTimeout(nameof(ShowManager.StoreAlternativeTitles));
        await showManager
            .StoreWatchProviders(Storage)
            .WithTimeout(nameof(ShowManager.StoreWatchProviders));
        await showManager.StoreVideos(Storage).WithTimeout(nameof(ShowManager.StoreVideos));
        await showManager.StoreNetworks(Storage).WithTimeout(nameof(ShowManager.StoreNetworks));
        await showManager.StoreCompanies(Storage).WithTimeout(nameof(ShowManager.StoreCompanies));
        await showManager.StoreKeywords(Storage).WithTimeout(nameof(ShowManager.StoreKeywords));

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshedEvent { QueryKey = ["base", "info", Storage.Id.ToString()] }
            );
    }
}
