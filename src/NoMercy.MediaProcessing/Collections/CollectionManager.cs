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

using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Movies;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Providers.TMDB.Models.Movies;
using Microsoft.Extensions.Logging;
namespace NoMercy.MediaProcessing.Collections;

public class CollectionManager(
    ICollectionRepository collectionRepository,
    MovieManager movieManager,
    JobDispatcher jobDispatcher,
    ILogger<CollectionManager> logger
) : BaseManager, ICollectionManager
{
    public async Task<TmdbCollectionAppends?> Add(int id, Library library)
    {
        TmdbCollectionClient collectionClient = new(id: id);
        TmdbCollectionAppends? collectionAppends = await collectionClient.WithAllAppends();

        if (collectionAppends is null)
            return null;

        Collection collection = new()
        {
            Id = collectionAppends.Id,
            Title = collectionAppends.Name,
            TitleSort = collectionAppends.Name.TitleSort(
                date: collectionAppends.Parts.MinBy(keySelector: movie => movie.ReleaseDate)?.ReleaseDate
            ),
            Backdrop = collectionAppends.BackdropPath,
            Poster = collectionAppends.PosterPath,
            Overview = collectionAppends.Overview,
            Parts = collectionAppends.Parts.Length,

            LibraryId = library.Id,
        };

        await collectionRepository.Store(collection: collection);

        logger.LogDebug(message: "Collection: {Title}: Added to Database", args: collection.Title);

        await StoreTranslations(collection: collectionAppends);

        jobDispatcher.DispatchColorPaletteJob(entityType: "collection", entityId: collection.Id.ToString());
        jobDispatcher.DispatchJob<CollectionExtrasJob, TmdbCollectionAppends>(data: collectionAppends);

        logger.LogDebug(message: "Collection: {Name}: Added to Library {Title}", args: [collectionAppends.Name, library.Title]);

        return collectionAppends;
    }

    public Task UpdateCollectionAsync(int id, Library library)
    {
        // Re-importing refreshes all metadata via idempotent upserts.
        return Add(id: id, library: library);
    }

    public async Task RemoveCollectionAsync(int id, Library library)
    {
        await collectionRepository.Remove(id: id);
        logger.LogDebug(message: "Collection: {Id}: Removed from Database", args: id);
    }

    private async Task StoreTranslations(TmdbCollectionAppends collection)
    {
        IEnumerable<Translation> translations = collection.Translations.Translations.Select(
            selector: translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                Title = translation.Data.Title == "" ? null : translation.Data.Title,
                Overview = translation.Data.Overview == "" ? null : translation.Data.Overview,
                EnglishName = translation.EnglishName,
                Homepage = translation.Data.Homepage?.ToString(),
                CollectionId = collection.Id,
            }
        );

        await collectionRepository.StoreTranslations(translations: translations);

        logger.LogDebug(message: "Collection: {Name}: Translations stored", args: collection.Name);
    }

    internal async Task StoreImages(TmdbCollectionAppends collection)
    {
        IEnumerable<Image> posters = collection
            .Images.Posters.Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                CollectionId = collection.Id,
                Type = "poster",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToArray();

        await collectionRepository.StoreImages(images: posters);
        logger.LogDebug(message: "Movie: {Name}: Posters stored", args: collection.Name);

        IEnumerable<Image> backdrops = collection
            .Images.Backdrops.Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                CollectionId = collection.Id,
                Type = "backdrop",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToArray();

        await collectionRepository.StoreImages(images: backdrops);
        logger.LogDebug(message: "Collection: {Name}: backdrops stored", args: collection.Name);

        IEnumerable<Image> logos = collection
            .Images.Logos.Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                CollectionId = collection.Id,
                Type = "logo",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToArray();

        await collectionRepository.StoreImages(images: logos);
        logger.LogDebug(message: "Collection: {Name}: Logos stored", args: collection.Name);
    }

    public async Task AddCollectionMovies(TmdbCollectionAppends collectionAppends, Library library)
    {
        List<TmdbMovieAppends> movies = [];

        await Parallel.ForEachAsync(
            source: collectionAppends.Parts,
            parallelOptions: SystemParallelism.Options,
            body: async (movie, _) =>
            {
                TmdbMovieClient movieClient = new(id: movie.Id);
                TmdbMovieAppends? movieAppends = await movieClient.WithAllAppends();
                if (movieAppends is null)
                    return;

                movies.Add(item: movieAppends);
            }
        );

        foreach (TmdbMovieAppends movie in movies)
            await movieManager.Add(id: movie.Id, library: library);

        await collectionRepository.LinkToMovies(collection: collectionAppends);

        logger.LogDebug(message: "Collection: {Name}: Movies added", args: collectionAppends.Name);
    }
}
