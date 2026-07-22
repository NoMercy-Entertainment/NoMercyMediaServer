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
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Certifications;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class CertificationsSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        bool hasCertifications = await dbContext.Certifications.AnyAsync();
        if (hasCertifications)
            return;

        Logger.Setup(message: "Adding Certifications", level: LogEventLevel.Verbose);

        TmdbMovieClient tmdbMovieClient = new();
        TmdbTvClient tmdbTvClient = new();

        List<Certification> certifications = [];

        foreach (
            (string key, TmdbMovieCertification[] value) in (
                await tmdbMovieClient.Certifications()
            )?.Certifications
                ?? []
        )
        foreach (TmdbMovieCertification certification in value)
            certifications.Add(
                item: new()
                {
                    Iso31661 = key,
                    Rating = certification.Rating,
                    Meaning = certification.Meaning,
                    Order = certification.Order,
                }
            );

        foreach (
            (string key, TmdbTvShowCertification[] value) in (
                await tmdbTvClient.Certifications()
            )?.Certifications
                ?? []
        )
        foreach (TmdbTvShowCertification certification in value)
            certifications.Add(
                item: new()
                {
                    Iso31661 = key,
                    Rating = certification.Rating,
                    Meaning = certification.Meaning,
                    Order = certification.Order,
                }
            );

        try
        {
            await dbContext
                .Certifications.UpsertRange(entities: certifications)
                .On(match: v => new { v.Iso31661, v.Rating })
                .WhenMatched(updater: v =>
                    new()
                    {
                        Iso31661 = v.Iso31661,
                        Rating = v.Rating,
                        Meaning = v.Meaning,
                        Order = v.Order,
                    }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(message: $"Certifications seed failed: {e.Message}", level: LogEventLevel.Warning);
        }
    }
}
