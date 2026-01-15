using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Client;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

public static class MusicGenresSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        bool hasMusicGenres = await dbContext.MusicGenres.AnyAsync();
        if (hasMusicGenres) return;

        Logger.Setup("Adding Music Genres", LogEventLevel.Verbose);

        MusicBrainzGenreClient musicBrainzGenreClient = new();

        MusicGenre[] genres = (await musicBrainzGenreClient.All()).ToList()
            .ConvertAll<MusicGenre>(genre => new()
            {
                Id = genre.Id,
                Name = genre.Name
            }).ToArray();
        
        try
        {
            await dbContext.MusicGenres.UpsertRange(genres)
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
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
