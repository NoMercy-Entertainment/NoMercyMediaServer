using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

public static class LanguagesSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        bool hasLanguages = await dbContext.Languages.AnyAsync();
        if (hasLanguages) return;

        Logger.Setup("Adding Languages", LogEventLevel.Verbose);

        TmdbConfigClient configClient = new();
        
        Language[] languages = (await configClient.Languages())?.ToList()
            .ConvertAll<Language>(language => new()
            {
                Iso6391 = language.Iso6391,
                EnglishName = language.EnglishName,
                Name = language.Name
            }).ToArray() ?? [];
        
        try
        {
            await dbContext.Languages.UpsertRange(languages)
                .On(v => new { v.Iso6391 })
                .WhenMatched(v => new()
                {
                    Iso6391 = v.Iso6391,
                    Name = v.Name,
                    EnglishName = v.EnglishName
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
