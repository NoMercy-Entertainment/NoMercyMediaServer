using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class GenresSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        bool hasGenres = await dbContext.Genres.AnyAsync();
        if (hasGenres) return;

        Logger.Setup("Adding Genres", LogEventLevel.Verbose);
        
        TmdbMovieClient tmdbMovieClient = new();
        TmdbTvClient tmdbTvClient = new();

        try
        {
            List<Genre> genres = [];
            List<Genre>? movieGenres = (await tmdbMovieClient.Genres())?
                .Genres.Select(genre => new Genre
                {
                    Id = genre.Id,
                    Name = genre.Name ?? string.Empty
                })
                .ToList();
            genres.AddRange(movieGenres ?? []);

            List<Genre>? tvGenres = (await tmdbTvClient.Genres())?
                .Genres.Select(genre => new Genre
                {
                    Id = genre.Id,
                    Name = genre.Name ?? string.Empty
                })
                .ToList();
            genres.AddRange(tvGenres ?? []);
        
            await dbContext.Genres.UpsertRange(genres)
                .On(v => new { v.Id })
                .WhenMatched(v => new()
                {
                    Id = v.Id,
                    Name = v.Name
                })
                .RunAsync();

        }
        catch (Exception e)
        {
            Logger.Setup($"Genres seed failed: {e.Message}", LogEventLevel.Warning);
        }

        try
        {
            ConcurrentBag<Translation> translations = [];
            
            List<Language> languages = await dbContext.Languages
                .Where(l => l.Iso6391 != "en")
                .ToListAsync();

            await Parallel.ForEachAsync(languages, Config.ParallelOptions, async (language, _) =>
            {
                Logger.Setup($"Adding Genres for {language.EnglishName}", LogEventLevel.Verbose);

                IEnumerable<Translation>? mg = (await tmdbMovieClient.Genres(language.Iso6391))?.Genres
                    .Where(g => g.Name != null)
                    .Select(genre => new Translation
                    {
                        GenreId = genre.Id,
                        Name = genre.Name ?? string.Empty,
                        Iso6391 = language.Iso6391
                    });

                if (mg != null)
                {
                    foreach (Translation translation in mg)
                        translations.Add(translation);
                }

                IEnumerable<Translation>? tg = (await tmdbTvClient.Genres(language.Iso6391))?.Genres
                    .Where(g => g.Name != null)
                    .Select(genre => new Translation
                    {
                        GenreId = genre.Id,
                        Name = genre.Name ?? string.Empty,
                        Iso6391 = language.Iso6391
                    });

                if (tg != null)
                {
                    foreach (Translation translation in tg)
                        translations.Add(translation);
                }
            });

            Logger.Setup($"Adding {translations.Count} genre translations", LogEventLevel.Verbose);

            await dbContext.Translations.UpsertRange(translations.Where(genre => genre.Name != null))
                .On(v => new { v.GenreId, v.Iso6391 })
                .WhenMatched(v => new()
                {
                    GenreId = v.GenreId,
                    Name = v.Name,
                    Iso6391 = v.Iso6391
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup($"Genres seed failed: {e.Message}", LogEventLevel.Warning);
        }
    }
}
