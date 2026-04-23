using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class MusicGenresSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        Logger.Setup("Checking Music Genres seed", LogEventLevel.Verbose);

        try
        {
            MusicBrainzGenreClient musicBrainzGenreClient = new();

            MusicBrainzAllGenres? firstPage = await musicBrainzGenreClient.FirstPage();
            if (firstPage is null)
            {
                Logger.Setup(
                    "Music genres seed skipped: MusicBrainz first-page fetch returned null",
                    LogEventLevel.Warning
                );
                return;
            }

            long expected = firstPage.GenreCount;
            long actual = await dbContext.MusicGenres.LongCountAsync();

            if (actual >= expected)
                return;

            Logger.Setup(
                $"Adding Music Genres ({actual}/{expected} present)",
                LogEventLevel.Verbose
            );

            List<MusicBrainzGenre> fetched = [.. firstPage.Genres];
            fetched.AddRange(await musicBrainzGenreClient.RemainingPages(firstPage));

            MusicGenre[] genres = fetched
                .ConvertAll<MusicGenre>(genre => new() { Id = genre.Id, Name = genre.Name })
                .ToArray();

            await dbContext
                .MusicGenres.UpsertRange(genres)
                .On(v => new { v.Id })
                .WhenMatched(v => new() { Id = v.Id, Name = v.Name })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup($"Music genres seed failed: {e.Message}", LogEventLevel.Warning);
        }
    }
}
