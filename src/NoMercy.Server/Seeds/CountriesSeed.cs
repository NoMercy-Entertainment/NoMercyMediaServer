using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

public static class CountriesSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        bool hasCountries = await dbContext.Countries.AnyAsync();
        if (hasCountries) return;

        Logger.Setup("Adding Countries", LogEventLevel.Verbose);
        
        TmdbConfigClient tmdbConfigClient = new();
        
        Country[] countries = (await tmdbConfigClient.Countries())?.ToList()
            .ConvertAll<Country>(country => new()
            {
                Iso31661 = country.Iso31661,
                EnglishName = country.EnglishName,
                NativeName = country.NativeName
            }).ToArray() ?? [];

        try
        {
            await dbContext.Countries.UpsertRange(countries)
                .On(v => new { v.Iso31661 })
                .WhenMatched(v => new()
                {
                    Iso31661 = v.Iso31661,
                    NativeName = v.NativeName,
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
