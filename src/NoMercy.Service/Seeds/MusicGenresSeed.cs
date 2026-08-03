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

        // Mirrors the sibling TMDB seeds beside this one (Languages/Countries/Genres/
        // Certifications): check the DB before touching the network. The previous shape
        // fetched MusicBrainz's first page BEFORE this check to learn `expected` for a
        // count comparison — paying a rate-limited round-trip on the pre-auth blocking
        // boot path every single boot, even once fully seeded, just to learn there was
        // nothing to do.
        bool hasGenres = await dbContext.MusicGenres.AnyAsync();
        if (hasGenres)
            return;

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

            Logger.Setup(
                $"Adding Music Genres (0/{firstPage.GenreCount} present)",
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
