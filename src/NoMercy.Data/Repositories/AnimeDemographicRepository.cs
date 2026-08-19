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

namespace NoMercy.Data.Repositories;

public class AnimeDemographicRepository(MediaContext context) : IAnimeDemographicRepository
{
    public async Task<List<AnimeDemographicWithCountsDto>> GetDemographicsWithCountsAsync(
        Guid userId,
        string language,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        // Two steps on purpose: mirrors GenreRepository.GetGenresWithCountsAsync —
        // page the demographics first, then aggregate their counts with grouped
        // queries. Do not fold this back into a single projection (SQLite APPLY
        // restriction).
        var demographics = await context
            .AnimeDemographics.AsNoTracking()
            .Where(demographic =>
                demographic.AnimeDemographicMovies.Any(d =>
                    d.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || demographic.AnimeDemographicTvShows.Any(d =>
                    d.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .OrderBy(demographic => demographic.Name)
            .ThenBy(demographic => demographic.Id)
            .Skip(page * take)
            .Take(take)
            .Select(demographic => new
            {
                demographic.Id,
                demographic.Name,
                TranslatedName = demographic
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Name)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        if (demographics.Count == 0)
            return [];

        List<int> ids = [.. demographics.Select(demographic => demographic.Id)];

        Dictionary<int, int> movieTotals = await context
            .AnimeDemographicMovie.AsNoTracking()
            .Where(adm =>
                ids.Contains(adm.AnimeDemographicId)
                && adm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
            )
            .GroupBy(adm => adm.AnimeDemographicId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, int> movieWithVideo = await context
            .AnimeDemographicMovie.AsNoTracking()
            .Where(adm =>
                ids.Contains(adm.AnimeDemographicId)
                && adm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                && adm.Movie.VideoFiles.Any(v => v.Folder != null)
            )
            .GroupBy(adm => adm.AnimeDemographicId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, int> tvTotals = await context
            .AnimeDemographicTv.AsNoTracking()
            .Where(adt =>
                ids.Contains(adt.AnimeDemographicId)
                && adt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
            )
            .GroupBy(adt => adt.AnimeDemographicId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, int> tvWithVideo = await context
            .AnimeDemographicTv.AsNoTracking()
            .Where(adt =>
                ids.Contains(adt.AnimeDemographicId)
                && adt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                && adt.Tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null))
            )
            .GroupBy(adt => adt.AnimeDemographicId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return
        [
            .. demographics.Select(demographic => new AnimeDemographicWithCountsDto
            {
                Id = demographic.Id,
                Name = demographic.TranslatedName ?? demographic.Name,
                TotalMovies = movieTotals.GetValueOrDefault(demographic.Id),
                TotalTvShows = tvTotals.GetValueOrDefault(demographic.Id),
                MoviesWithVideo = movieWithVideo.GetValueOrDefault(demographic.Id),
                TvShowsWithVideo = tvWithVideo.GetValueOrDefault(demographic.Id),
            }),
        ];
    }
}
