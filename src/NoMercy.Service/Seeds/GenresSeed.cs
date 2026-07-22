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

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class GenresSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        bool hasGenres = await dbContext.Genres.AnyAsync();
        if (hasGenres)
            return;

        Logger.Setup(message: "Adding Genres", level: LogEventLevel.Verbose);

        TmdbMovieClient tmdbMovieClient = new();
        TmdbTvClient tmdbTvClient = new();

        try
        {
            List<Genre> genres = [];
            List<Genre>? movieGenres = (await tmdbMovieClient.Genres())
                ?.Genres.Select(selector: genre => new Genre { Id = genre.Id, Name = genre.Name.OrEmpty() })
                .ToList();
            genres.AddRange(collection: movieGenres ?? []);

            List<Genre>? tvGenres = (await tmdbTvClient.Genres())
                ?.Genres.Select(selector: genre => new Genre { Id = genre.Id, Name = genre.Name.OrEmpty() })
                .ToList();
            genres.AddRange(collection: tvGenres ?? []);

            await dbContext
                .Genres.UpsertRange(entities: genres)
                .On(match: v => new { v.Id })
                .WhenMatched(updater: v => new() { Id = v.Id, Name = v.Name })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(message: $"Genres seed failed: {e.Message}", level: LogEventLevel.Warning);
        }

        try
        {
            ConcurrentBag<Translation> translations = [];

            List<Language> languages = await dbContext
                .Languages.Where(predicate: l => l.Iso6391 != "en")
                .ToListAsync();

            await Parallel.ForEachAsync(
                source: languages,
                parallelOptions: SystemParallelism.Options,
                body: async (language, _) =>
                {
                    Logger.Setup(
                        message: $"Adding Genres for {language.EnglishName}",
                        level: LogEventLevel.Verbose
                    );

                    IEnumerable<Translation>? mg = (await tmdbMovieClient.Genres(language: language.Iso6391))
                        ?.Genres.Where(predicate: g => g.Name != null)
                        .Select(selector: genre => new Translation
                        {
                            GenreId = genre.Id,
                            Name = genre.Name.OrEmpty(),
                            Iso6391 = language.Iso6391,
                        });

                    if (mg != null)
                    {
                        foreach (Translation translation in mg)
                            translations.Add(item: translation);
                    }

                    IEnumerable<Translation>? tg = (await tmdbTvClient.Genres(language: language.Iso6391))
                        ?.Genres.Where(predicate: g => g.Name != null)
                        .Select(selector: genre => new Translation
                        {
                            GenreId = genre.Id,
                            Name = genre.Name.OrEmpty(),
                            Iso6391 = language.Iso6391,
                        });

                    if (tg != null)
                    {
                        foreach (Translation translation in tg)
                            translations.Add(item: translation);
                    }
                }
            );

            Logger.Setup(message: $"Adding {translations.Count} genre translations", level: LogEventLevel.Verbose);

            await dbContext
                .Translations.UpsertRange(entities: translations.Where(predicate: genre => genre.Name != null))
                .On(match: v => new { v.GenreId, v.Iso6391 })
                .WhenMatched(updater: v =>
                    new()
                    {
                        GenreId = v.GenreId,
                        Name = v.Name,
                        Iso6391 = v.Iso6391,
                    }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(message: $"Genres seed failed: {e.Message}", level: LogEventLevel.Warning);
        }
    }
}
