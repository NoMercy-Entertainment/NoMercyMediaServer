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
        Logger.Setup(message: "Checking Music Genres seed", level: LogEventLevel.Verbose);

        try
        {
            MusicBrainzGenreClient musicBrainzGenreClient = new();

            MusicBrainzAllGenres? firstPage = await musicBrainzGenreClient.FirstPage();
            if (firstPage is null)
            {
                Logger.Setup(
                    message: "Music genres seed skipped: MusicBrainz first-page fetch returned null",
                    level: LogEventLevel.Warning
                );
                return;
            }

            long expected = firstPage.GenreCount;
            long actual = await dbContext.MusicGenres.LongCountAsync();

            if (actual >= expected)
                return;

            Logger.Setup(
                message: $"Adding Music Genres ({actual}/{expected} present)",
                level: LogEventLevel.Verbose
            );

            List<MusicBrainzGenre> fetched = [.. firstPage.Genres];
            fetched.AddRange(collection: await musicBrainzGenreClient.RemainingPages(firstPage: firstPage));

            MusicGenre[] genres = fetched
                .ConvertAll<MusicGenre>(converter: genre => new() { Id = genre.Id, Name = genre.Name })
                .ToArray();

            await dbContext
                .MusicGenres.UpsertRange(entities: genres)
                .On(match: v => new { v.Id })
                .WhenMatched(updater: v => new() { Id = v.Id, Name = v.Name })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(message: $"Music genres seed failed: {e.Message}", level: LogEventLevel.Warning);
        }
    }
}
