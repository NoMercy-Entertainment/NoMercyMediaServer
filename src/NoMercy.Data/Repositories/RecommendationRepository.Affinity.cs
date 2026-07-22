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
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Data.Repositories;

public partial class RecommendationRepository
{
    public async Task<List<UserAffinitySourceDto>> GetUserMovieAffinityDataAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        // Fetch flat data without nested collection projections to avoid SQL APPLY
        var movies = await context
            .Movies.AsNoTracking()
            .Where(predicate: m => m.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: m => m.VideoFiles.Any())
            .Select(selector: m => new
            {
                m.Id,
                m.Title,
                m.Poster,
                m._colorPalette,
                m.Runtime,
                Rating = m
                    .UserData.Where(ud => ud.UserId == userId && ud.Rating != null)
                    .Select(ud => ud.Rating)
                    .FirstOrDefault(),
                TimeWatched = m
                    .UserData.Where(ud => ud.UserId == userId)
                    .OrderByDescending(ud => ud.Time)
                    .ThenByDescending(ud => ud.Id)
                    .Select(ud => ud.Time)
                    .FirstOrDefault(),
                IsFavorited = m.MovieUser.Any(mu => mu.UserId == userId),
            })
            .ToListAsync(cancellationToken: ct);

        if (movies.Count == 0)
            return [];

        List<int> movieIds = movies.Select(selector: m => m.Id).ToList();

        // Fetch genre and keyword mappings separately
        Dictionary<int, List<int>> genreMap = await context
            .GenreMovie.AsNoTracking()
            .Where(predicate: gm => movieIds.Contains(gm.MovieId))
            .GroupBy(keySelector: gm => gm.MovieId)
            .ToDictionaryAsync(keySelector: g => g.Key, elementSelector: g => g.Select(selector: gm => gm.GenreId).ToList(), cancellationToken: ct);

        Dictionary<int, List<int>> keywordMap = await context
            .KeywordMovie.AsNoTracking()
            .Where(predicate: km => movieIds.Contains(km.MovieId))
            .GroupBy(keySelector: km => km.MovieId)
            .ToDictionaryAsync(keySelector: g => g.Key, elementSelector: g => g.Select(selector: km => km.KeywordId).ToList(), cancellationToken: ct);

        return movies
            .Select(selector: m => new UserAffinitySourceDto
            {
                ItemId = m.Id,
                Title = m.Title,
                Poster = m.Poster,
                ColorPalette = m._colorPalette.OrEmpty(),
                MediaType = MediaTypes.MovieMediaType,
                Rating = m.Rating,
                TimeWatched = m.TimeWatched,
                Duration = m.Runtime != null ? m.Runtime * 60 : null,
                IsFavorited = m.IsFavorited,
                GenreIds = genreMap.GetValueOrDefault(key: m.Id, defaultValue: []),
                KeywordIds = keywordMap.GetValueOrDefault(key: m.Id, defaultValue: []),
            })
            .ToList();
    }

    public async Task<List<UserAffinitySourceDto>> GetUserTvAffinityDataAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        var tvShows = await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => t.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: t => t.MediaType != MediaTypes.AnimeMediaType)
            .Where(predicate: t => t.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Select(selector: t => new
            {
                t.Id,
                t.Title,
                t.Poster,
                t._colorPalette,
                t.Duration,
                Rating = t
                    .UserData.Where(ud => ud.UserId == userId && ud.Rating != null)
                    .Select(ud => ud.Rating)
                    .FirstOrDefault(),
                TimeWatched = t
                    .UserData.Where(ud => ud.UserId == userId)
                    .OrderByDescending(ud => ud.Time)
                    .ThenByDescending(ud => ud.Id)
                    .Select(ud => ud.Time)
                    .FirstOrDefault(),
                IsFavorited = t.TvUser.Any(tu => tu.UserId == userId),
            })
            .ToListAsync(cancellationToken: ct);

        if (tvShows.Count == 0)
            return [];

        List<int> tvIds = tvShows.Select(selector: t => t.Id).ToList();

        Dictionary<int, List<int>> genreMap = await context
            .GenreTv.AsNoTracking()
            .Where(predicate: gt => tvIds.Contains(gt.TvId))
            .GroupBy(keySelector: gt => gt.TvId)
            .ToDictionaryAsync(keySelector: g => g.Key, elementSelector: g => g.Select(selector: gt => gt.GenreId).ToList(), cancellationToken: ct);

        Dictionary<int, List<int>> keywordMap = await context
            .KeywordTv.AsNoTracking()
            .Where(predicate: kt => tvIds.Contains(kt.TvId))
            .GroupBy(keySelector: kt => kt.TvId)
            .ToDictionaryAsync(keySelector: g => g.Key, elementSelector: g => g.Select(selector: kt => kt.KeywordId).ToList(), cancellationToken: ct);

        return tvShows
            .Select(selector: t => new UserAffinitySourceDto
            {
                ItemId = t.Id,
                Title = t.Title,
                Poster = t.Poster,
                ColorPalette = t._colorPalette.OrEmpty(),
                MediaType = MediaTypes.TvMediaType,
                Rating = t.Rating,
                TimeWatched = t.TimeWatched,
                Duration = t.Duration,
                IsFavorited = t.IsFavorited,
                GenreIds = genreMap.GetValueOrDefault(key: t.Id, defaultValue: []),
                KeywordIds = keywordMap.GetValueOrDefault(key: t.Id, defaultValue: []),
            })
            .ToList();
    }

    public async Task<List<UserAffinitySourceDto>> GetUserAnimeAffinityDataAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        var animeShows = await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => t.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: t => t.MediaType == MediaTypes.AnimeMediaType)
            .Where(predicate: t => t.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Select(selector: t => new
            {
                t.Id,
                t.Title,
                t.Poster,
                t._colorPalette,
                t.Duration,
                Rating = t
                    .UserData.Where(ud => ud.UserId == userId && ud.Rating != null)
                    .Select(ud => ud.Rating)
                    .FirstOrDefault(),
                TimeWatched = t
                    .UserData.Where(ud => ud.UserId == userId)
                    .OrderByDescending(ud => ud.Time)
                    .ThenByDescending(ud => ud.Id)
                    .Select(ud => ud.Time)
                    .FirstOrDefault(),
                IsFavorited = t.TvUser.Any(tu => tu.UserId == userId),
            })
            .ToListAsync(cancellationToken: ct);

        if (animeShows.Count == 0)
            return [];

        List<int> animeIds = animeShows.Select(selector: t => t.Id).ToList();

        Dictionary<int, List<int>> genreMap = await context
            .GenreTv.AsNoTracking()
            .Where(predicate: gt => animeIds.Contains(gt.TvId))
            .GroupBy(keySelector: gt => gt.TvId)
            .ToDictionaryAsync(keySelector: g => g.Key, elementSelector: g => g.Select(selector: gt => gt.GenreId).ToList(), cancellationToken: ct);

        Dictionary<int, List<int>> keywordMap = await context
            .KeywordTv.AsNoTracking()
            .Where(predicate: kt => animeIds.Contains(kt.TvId))
            .GroupBy(keySelector: kt => kt.TvId)
            .ToDictionaryAsync(keySelector: g => g.Key, elementSelector: g => g.Select(selector: kt => kt.KeywordId).ToList(), cancellationToken: ct);

        return animeShows
            .Select(selector: t => new UserAffinitySourceDto
            {
                ItemId = t.Id,
                Title = t.Title,
                Poster = t.Poster,
                ColorPalette = t._colorPalette.OrEmpty(),
                MediaType = MediaTypes.AnimeMediaType,
                Rating = t.Rating,
                TimeWatched = t.TimeWatched,
                Duration = t.Duration,
                IsFavorited = t.IsFavorited,
                GenreIds = genreMap.GetValueOrDefault(key: t.Id, defaultValue: []),
                KeywordIds = keywordMap.GetValueOrDefault(key: t.Id, defaultValue: []),
            })
            .ToList();
    }
}
