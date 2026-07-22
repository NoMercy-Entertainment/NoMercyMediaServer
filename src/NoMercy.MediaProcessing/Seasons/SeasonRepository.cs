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
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.MediaProcessing.Seasons;

public class SeasonRepository(MediaContext context) : ISeasonRepository
{
    public Task StoreAsync(IEnumerable<Season> seasons)
    {
        return context
            .Seasons.UpsertRange(entities: seasons.ToArray())
            .On(match: s => new { s.Id })
            .WhenMatched(
                updater: (ss, si) =>
                    new()
                    {
                        Id = si.Id,
                        Title = si.Title,
                        AirDate = si.AirDate,
                        EpisodeCount = si.EpisodeCount,
                        Overview = si.Overview,
                        Poster = si.Poster,
                        SeasonNumber = si.SeasonNumber,
                        TvId = si.TvId,
                    }
            )
            .RunAsync();
    }

    public Task UpdateAsync(Season season)
    {
        // Refresh an existing season's metadata in place. TvId is intentionally
        // excluded so the foreign-key link to its show is never altered.
        return context
            .Seasons.Where(predicate: s => s.Id == season.Id)
            .ExecuteUpdateAsync(setPropertyCalls: setters =>
                setters
                    .SetProperty(propertyExpression: s => s.Title, valueExpression: season.Title)
                    .SetProperty(propertyExpression: s => s.AirDate, valueExpression: season.AirDate)
                    .SetProperty(propertyExpression: s => s.EpisodeCount, valueExpression: season.EpisodeCount)
                    .SetProperty(propertyExpression: s => s.Overview, valueExpression: season.Overview)
                    .SetProperty(propertyExpression: s => s.Poster, valueExpression: season.Poster)
                    .SetProperty(propertyExpression: s => s.SeasonNumber, valueExpression: season.SeasonNumber)
            );
    }

    public Task StoreTranslationsAsync(IEnumerable<Translation> translations)
    {
        return context
            .Translations.UpsertRange(entities: translations.ToArray())
            .On(match: t => new
            {
                t.Iso31661,
                t.Iso6391,
                t.SeasonId,
            })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Iso31661 = ti.Iso31661,
                        Iso6391 = ti.Iso6391,
                        Name = ti.Name,
                        EnglishName = ti.EnglishName,
                        Title = ti.Title,
                        Overview = ti.Overview,
                        Homepage = ti.Homepage,
                        Biography = ti.Biography,
                        TvId = ti.TvId,
                        SeasonId = ti.SeasonId,
                        EpisodeId = ti.EpisodeId,
                        MovieId = ti.MovieId,
                        CollectionId = ti.CollectionId,
                        PersonId = ti.PersonId,
                    }
            )
            .RunAsync();
    }

    public Task StoreImagesAsync(IEnumerable<Image> images)
    {
        return context
            .Images.UpsertRange(entities: images.ToArray())
            .On(match: v => new { v.FilePath, v.SeasonId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        AspectRatio = ti.AspectRatio,
                        FilePath = ti.FilePath,
                        Height = ti.Height,
                        Iso6391 = ti.Iso6391,
                        Site = ti.Site,
                        VoteAverage = ti.VoteAverage,
                        VoteCount = ti.VoteCount,
                        Width = ti.Width,
                        Type = ti.Type,
                        SeasonId = ti.SeasonId,
                    }
            )
            .RunAsync();
    }

    public async Task<bool> RemoveSeasonAsync(int seasonId)
    {
        Season? season = await context.Seasons.FirstOrDefaultAsync(predicate: s => s.Id == seasonId);

        if (season is null)
            return false;

        context.Seasons.Remove(entity: season);
        await context.SaveChangesAsync();

        return true;
    }
}
