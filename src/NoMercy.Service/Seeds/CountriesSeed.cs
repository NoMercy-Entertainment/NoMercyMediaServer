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
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class CountriesSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        bool hasCountries = await dbContext.Countries.AnyAsync();
        if (hasCountries)
            return;

        Logger.Setup("Adding Countries", LogEventLevel.Verbose);

        TmdbConfigClient tmdbConfigClient = new();

        Country[] countries =
            (await tmdbConfigClient.Countries())
                ?.ToList()
                .ConvertAll<Country>(country =>
                    new()
                    {
                        Iso31661 = country.Iso31661,
                        EnglishName = country.EnglishName,
                        NativeName = country.NativeName,
                    }
                )
                .ToArray()
            ?? [];

        try
        {
            await dbContext
                .Countries.UpsertRange(countries)
                .On(v => new { v.Iso31661 })
                .WhenMatched(v =>
                    new()
                    {
                        Iso31661 = v.Iso31661,
                        NativeName = v.NativeName,
                        EnglishName = v.EnglishName,
                    }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup($"Countries seed failed: {e.Message}", LogEventLevel.Warning);
        }
    }
}
