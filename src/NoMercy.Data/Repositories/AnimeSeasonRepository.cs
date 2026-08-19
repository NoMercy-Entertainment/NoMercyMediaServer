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

public class AnimeSeasonRepository(MediaContext context) : IAnimeSeasonRepository
{
    public async Task<List<AnimeSeasonWithCountsDto>> GetSeasonsWithCountsAsync(
        Guid userId,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        // Two steps on purpose: mirrors GenreRepository.GetGenresWithCountsAsync —
        // page the seasons first, then aggregate their counts with grouped
        // queries. Do not fold this back into a single projection (SQLite APPLY
        // restriction). AnimeSeason has no Translations, so there is no
        // TranslatedName step here.
        var seasons = await context
            .AnimeSeasons.AsNoTracking()
            .Where(season =>
                season.AnimeSeasonMovies.Any(s =>
                    s.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || season.AnimeSeasonTvShows.Any(s =>
                    s.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .OrderByDescending(season => season.Year)
            .ThenBy(season => season.Quarter)
            .ThenBy(season => season.Id)
            .Skip(page * take)
            .Take(take)
            .Select(season => new
            {
                season.Id,
                season.Year,
                season.Quarter,
            })
            .ToListAsync(ct);

        if (seasons.Count == 0)
            return [];

        List<int> ids = [.. seasons.Select(season => season.Id)];

        Dictionary<int, int> movieTotals = await context
            .AnimeSeasonMovie.AsNoTracking()
            .Where(asm =>
                ids.Contains(asm.AnimeSeasonId)
                && asm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
            )
            .GroupBy(asm => asm.AnimeSeasonId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, int> movieWithVideo = await context
            .AnimeSeasonMovie.AsNoTracking()
            .Where(asm =>
                ids.Contains(asm.AnimeSeasonId)
                && asm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                && asm.Movie.VideoFiles.Any(v => v.Folder != null)
            )
            .GroupBy(asm => asm.AnimeSeasonId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, int> tvTotals = await context
            .AnimeSeasonTv.AsNoTracking()
            .Where(ast =>
                ids.Contains(ast.AnimeSeasonId)
                && ast.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
            )
            .GroupBy(ast => ast.AnimeSeasonId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, int> tvWithVideo = await context
            .AnimeSeasonTv.AsNoTracking()
            .Where(ast =>
                ids.Contains(ast.AnimeSeasonId)
                && ast.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                && ast.Tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null))
            )
            .GroupBy(ast => ast.AnimeSeasonId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return
        [
            .. seasons.Select(season => new AnimeSeasonWithCountsDto
            {
                Id = season.Id,
                Year = season.Year,
                Quarter = season.Quarter,
                TotalMovies = movieTotals.GetValueOrDefault(season.Id),
                TotalTvShows = tvTotals.GetValueOrDefault(season.Id),
                MoviesWithVideo = movieWithVideo.GetValueOrDefault(season.Id),
                TvShowsWithVideo = tvWithVideo.GetValueOrDefault(season.Id),
            }),
        ];
    }
}
