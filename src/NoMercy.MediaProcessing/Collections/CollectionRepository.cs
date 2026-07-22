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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.MediaProcessing.Collections;

public class CollectionRepository(MediaContext context) : ICollectionRepository
{
    public Task Store(Collection collection)
    {
        return context
            .Collections.Upsert(entity: collection)
            .On(match: v => new { v.Id })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Id = ti.Id,
                        Backdrop = ti.Backdrop,
                        Poster = ti.Poster,
                        Title = ti.Title,
                        Overview = ti.Overview,
                        Parts = ti.Parts,
                        LibraryId = ti.LibraryId,
                        TitleSort = ti.TitleSort,
                    }
            )
            .RunAsync();
    }

    public async Task Remove(int id)
    {
        // SQLite schema uses DeleteBehavior.Restrict globally. Disable FK
        // enforcement on this pinned connection so the collection and all its
        // dependents are removed atomically, mirroring
        // Data.Repositories.CollectionRepository.DeleteAsync.
        bool ownsConnection =
            context.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (ownsConnection)
            await context.Database.OpenConnectionAsync();

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = OFF");
            try
            {
                await context.Collections.Where(predicate: c => c.Id == id).ExecuteDeleteAsync();
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = ON");
            }
        }
        finally
        {
            if (ownsConnection)
                await context.Database.CloseConnectionAsync();
        }
    }

    public Task LinkToMovies(TmdbCollectionAppends collection)
    {
        return context
            .CollectionMovie.UpsertRange(
                entities: collection.Parts.Select(selector: movie => new CollectionMovie
                {
                    MovieId = movie.Id,
                    CollectionId = collection.Id,
                })
            )
            .On(match: v => new { v.MovieId, v.CollectionId })
            .WhenMatched(updater: (ts, ti) => new() { MovieId = ti.MovieId, CollectionId = ti.CollectionId })
            .RunAsync();
    }

    public Task LinkToLibrary(Library library, Collection collection)
    {
        return context
            .CollectionLibrary.Upsert(
                entity: new() { LibraryId = library.Id, CollectionId = collection.Id }
            )
            .On(match: v => new { v.LibraryId, v.CollectionId })
            .WhenMatched(
                updater: (ts, ti) => new() { LibraryId = ti.LibraryId, CollectionId = ti.CollectionId }
            )
            .RunAsync();
    }

    public Task StoreAlternativeTitles(IEnumerable<AlternativeTitle> alternativeTitles)
    {
        // return context.AlternativeTitles.UpsertRange(alternativeTitles)
        //     .On(v => new { v.Iso31661, v.CollectionId })
        //     .WhenMatched((ts, ti) => new AlternativeTitle
        //     {
        //         Iso31661 = ti.Iso31661,
        //         Title = ti.Title,
        //         CollectionId = ti.CollectionId
        //     })
        //     .RunAsync();

        return Task.CompletedTask;
    }

    public Task StoreTranslations(IEnumerable<Translation> translations)
    {
        return context
            .Translations.UpsertRange(
                entities: translations.Where(predicate: translation =>
                    translation.Title != null || translation.Overview != ""
                )
            )
            .On(match: t => new
            {
                t.Iso31661,
                t.Iso6391,
                t.CollectionId,
            })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Iso31661 = ti.Iso31661,
                        Iso6391 = ti.Iso6391,
                        Title = ti.Title,
                        EnglishName = ti.EnglishName,
                        Name = ti.Name,
                        Overview = ti.Overview,
                        Homepage = ti.Homepage,
                        Biography = ti.Biography,
                        SeasonId = ti.SeasonId,
                        EpisodeId = ti.EpisodeId,
                        CollectionId = ti.CollectionId,
                        PersonId = ti.PersonId,
                    }
            )
            .RunAsync();
    }

    public Task StoreImages(IEnumerable<Image> images)
    {
        return context
            .Images.UpsertRange(entities: images)
            .On(match: v => new { v.FilePath, v.CollectionId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        AspectRatio = ti.AspectRatio,
                        FilePath = ti.FilePath,
                        Height = ti.Height,
                        Iso6391 = ti.Iso6391,
                        Site = ti.Site,
                        VoteAverage = ti.VoteAverage,
                        VoteCount = ti.VoteCount,
                        Width = ti.Width,
                        Type = ti.Type,
                        CollectionId = ti.CollectionId,
                    }
            )
            .RunAsync();
    }
}
