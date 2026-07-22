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

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.TvShows;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
using NoMercy.NmSystem;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class ShowImportJob : AbstractMediaJob
{
    public ShowImportJob() { }

    public ShowImportJob(
        IStorageFactory storageFactory,
        IStorageDriver storageDriver,
        ILoggerFactory loggerFactory
    )
        : base(storageFactory: storageFactory, storageDriver: storageDriver, loggerFactory: loggerFactory) { }

    public override string QueueName => "import";
    public override int Priority => 5;

    public bool HighPriority { get; set; }

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

        SeasonRepository seasonRepository = new(context: context);
        SeasonManager seasonManager = new(
            seasonRepository: seasonRepository,
            jobDispatcher: jobDispatcher,
            logger: LoggerFactory.CreateLogger<SeasonManager>()
        );

        EpisodeRepository episodeRepository = new(context: context);
        EpisodeManager episodeManager = new(
            episodeRepository: episodeRepository,
            jobDispatcher: jobDispatcher,
            logger: LoggerFactory.CreateLogger<EpisodeManager>()
        );

        Library tvLibrary = await context
            .Libraries.Where(predicate: f => f.Id == LibraryId)
            .Include(navigationPropertyPath: f => f.FolderLibraries)
                .ThenInclude(navigationPropertyPath: f => f.Folder)
            .FirstAsync();

        bool wasEmpty = !await context.LibraryTv.AnyAsync(predicate: lt => lt.LibraryId == LibraryId);

        TmdbTvShowAppends? show = await showManager.AddShowAsync(id: Id, library: tvLibrary, priority: HighPriority);
        if (show == null)
        {
            await ImportFailureRecorder.RecordAsync(
                context: context,
                jobType: "ShowImportJob",
                filePath: Id.ToString(),
                libraryId: LibraryId,
                errorMessage: "TMDB show metadata fetch returned no result after retries."
            );
            return;
        }

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new MediaAddedEvent
                {
                    MediaId = Id,
                    MediaType = "tvshow",
                    Title = show.Name,
                    LibraryId = LibraryId,
                }
            );
        }

        IEnumerable<TmdbSeasonAppends> seasons = await seasonManager.StoreSeasonsAsync(
            show: show,
            priority: HighPriority
        );

        ConcurrentBag<Episode> episodes = [];
        await Parallel.ForEachAsync(
            source: seasons,
            parallelOptions: SystemParallelism.Options,
            body: async (season, _) =>
            {
                IEnumerable<Episode> eps = await episodeManager.Add(show: show, season: season, priority: HighPriority);
                foreach (Episode episode in eps)
                {
                    episodes.Add(item: episode);
                }
            }
        );

        await episodeRepository.StoreEpisodes(episodes: episodes);

        jobDispatcher.DispatchJob<FileRescanJob>(id: Id, library: tvLibrary);

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new LibraryRefreshedEvent { QueryKey = ["base", "info", Id.ToString()] }
            );

            if (wasEmpty)
                await EventBusProvider.Current.PublishAsync(
                    @event: new LibraryRefreshedEvent { QueryKey = ["libraries"] }
                );
        }
    }
}
