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
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.MediaProcessing.Movies;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class MovieImportJob : AbstractMediaJob
{
    public override string QueueName => "import";
    public override int Priority => 5;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        MovieRepository movieRepository = new(context);
        MovieManager movieManager = new(
            movieRepository,
            jobDispatcher,
            StorageFactory,
            StorageDriver
        );

        Library? movieLibrary = await context
            .Libraries.Where(f => f.Id == LibraryId)
            .Include(f => f.FolderLibraries)
                .ThenInclude(f => f.Folder)
            .FirstOrDefaultAsync();

        if (movieLibrary is null)
        {
            Logger.App($"MovieImportJob: library {LibraryId} not found, skipping movie {Id}");
            return;
        }

        bool wasEmpty = !await context.LibraryMovie.AnyAsync(lm => lm.LibraryId == LibraryId);

        TmdbMovieAppends? movieAppends = await movieManager.Add(Id, movieLibrary);
        if (movieAppends == null)
            return;

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new MediaAddedEvent
                {
                    MediaId = Id,
                    MediaType = "movie",
                    Title = movieAppends.Title ?? $"Movie {Id}",
                    LibraryId = LibraryId,
                }
            );
        }

        if (movieAppends.BelongsToCollection != null)
            jobDispatcher.DispatchJob<CollectionImportJob>(
                movieAppends.BelongsToCollection.Id,
                LibraryId
            );

        jobDispatcher.DispatchJob<FileRescanJob>(Id, movieLibrary);

        Logger.App($"Movie {Id} added to library, extra data will be added in the background");

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshedEvent { QueryKey = ["base", "info", Id.ToString()] }
            );

            if (wasEmpty)
                await EventBusProvider.Current.PublishAsync(
                    new LibraryRefreshedEvent { QueryKey = ["libraries"] }
                );
        }
    }
}
