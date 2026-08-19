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

public class AnimeThemeRepository(MediaContext context) : IAnimeThemeRepository
{
    public async Task<List<AnimeThemeWithCountsDto>> GetThemesWithCountsAsync(
        Guid userId,
        string language,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        // Two steps on purpose: mirrors GenreRepository.GetGenresWithCountsAsync —
        // page the themes first, then aggregate their counts with grouped queries.
        // Do not fold this back into a single projection (SQLite APPLY restriction).
        var themes = await context
            .AnimeThemes.AsNoTracking()
            .Where(theme =>
                theme.AnimeThemeMovies.Any(t =>
                    t.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || theme.AnimeThemeTvShows.Any(t =>
                    t.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .OrderBy(theme => theme.Name)
            .ThenBy(theme => theme.Id)
            .Skip(page * take)
            .Take(take)
            .Select(theme => new
            {
                theme.Id,
                theme.Name,
                TranslatedName = theme
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Name)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        if (themes.Count == 0)
            return [];

        List<int> ids = [.. themes.Select(theme => theme.Id)];

        Dictionary<int, int> movieTotals = await context
            .AnimeThemeMovie.AsNoTracking()
            .Where(atm =>
                ids.Contains(atm.AnimeThemeId)
                && atm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
            )
            .GroupBy(atm => atm.AnimeThemeId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, int> movieWithVideo = await context
            .AnimeThemeMovie.AsNoTracking()
            .Where(atm =>
                ids.Contains(atm.AnimeThemeId)
                && atm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                && atm.Movie.VideoFiles.Any(v => v.Folder != null)
            )
            .GroupBy(atm => atm.AnimeThemeId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, int> tvTotals = await context
            .AnimeThemeTv.AsNoTracking()
            .Where(att =>
                ids.Contains(att.AnimeThemeId)
                && att.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
            )
            .GroupBy(att => att.AnimeThemeId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, int> tvWithVideo = await context
            .AnimeThemeTv.AsNoTracking()
            .Where(att =>
                ids.Contains(att.AnimeThemeId)
                && att.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                && att.Tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null))
            )
            .GroupBy(att => att.AnimeThemeId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return
        [
            .. themes.Select(theme => new AnimeThemeWithCountsDto
            {
                Id = theme.Id,
                Name = theme.TranslatedName ?? theme.Name,
                TotalMovies = movieTotals.GetValueOrDefault(theme.Id),
                TotalTvShows = tvTotals.GetValueOrDefault(theme.Id),
                MoviesWithVideo = movieWithVideo.GetValueOrDefault(theme.Id),
                TvShowsWithVideo = tvWithVideo.GetValueOrDefault(theme.Id),
            }),
        ];
    }
}
