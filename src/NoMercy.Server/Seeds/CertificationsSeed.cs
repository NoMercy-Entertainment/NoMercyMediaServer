using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Certifications;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

public static class CertificationsSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        bool hasCertifications = await dbContext.Certifications.AnyAsync();
        if (hasCertifications) return;

        Logger.Setup("Adding Certifications", LogEventLevel.Verbose);
        
        TmdbMovieClient tmdbMovieClient = new();
        TmdbTvClient tmdbTvClient = new();

        List<Certification> certifications = [];

        foreach ((string key, TmdbMovieCertification[] value) in (await tmdbMovieClient.Certifications())
                 ?.Certifications ?? [])
        foreach (TmdbMovieCertification certification in value)
            certifications.Add(new()
            {
                Iso31661 = key,
                Rating = certification.Rating,
                Meaning = certification.Meaning,
                Order = certification.Order
            });

        foreach ((string key, TmdbTvShowCertification[] value) in (await tmdbTvClient.Certifications())?.Certifications ?? [])
        foreach (TmdbTvShowCertification certification in value)
            certifications.Add(new()
            {
                Iso31661 = key,
                Rating = certification.Rating,
                Meaning = certification.Meaning,
                Order = certification.Order
            });
        
        try
        {

            await dbContext.Certifications.UpsertRange(certifications)
                .On(v => new { v.Iso31661, v.Rating })
                .WhenMatched(v => new()
                {
                    Iso31661 = v.Iso31661,
                    Rating = v.Rating,
                    Meaning = v.Meaning,
                    Order = v.Order
                })
            .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
