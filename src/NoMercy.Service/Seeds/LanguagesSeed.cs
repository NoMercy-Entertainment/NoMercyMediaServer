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
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class LanguagesSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        bool hasLanguages = await dbContext.Languages.AnyAsync();
        if (hasLanguages)
            return;

        Logger.Setup(message: "Adding Languages", level: LogEventLevel.Verbose);

        TmdbConfigClient configClient = new();

        Language[] languages =
            (await configClient.Languages())
                ?.ToList()
                .ConvertAll<Language>(converter: language =>
                    new()
                    {
                        Iso6391 = language.Iso6391,
                        EnglishName = language.EnglishName,
                        Name = language.Name,
                    }
                )
                .ToArray()
            ?? [];

        try
        {
            await dbContext
                .Languages.UpsertRange(entities: languages)
                .On(match: v => new { v.Iso6391 })
                .WhenMatched(updater: v =>
                    new()
                    {
                        Iso6391 = v.Iso6391,
                        Name = v.Name,
                        EnglishName = v.EnglishName,
                    }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(message: $"Languages seed failed: {e.Message}", level: LogEventLevel.Warning);
        }
    }
}
