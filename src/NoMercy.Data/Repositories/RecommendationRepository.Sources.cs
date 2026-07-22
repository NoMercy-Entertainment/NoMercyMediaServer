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
    public async Task<Dictionary<int, List<int>>> GetGenresForMovieIdsAsync(
        List<int> movieIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        if (movieIds.Count == 0)
            return new();

        return await context
            .GenreMovie.AsNoTracking()
            .Where(predicate: gm => movieIds.Contains(gm.MovieId))
            .GroupBy(keySelector: gm => gm.MovieId)
            .ToDictionaryAsync(keySelector: g => g.Key, elementSelector: g => g.Select(selector: gm => gm.GenreId).ToList(), cancellationToken: ct);
    }

    public async Task<Dictionary<int, List<int>>> GetGenresForTvIdsAsync(
        List<int> tvIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        if (tvIds.Count == 0)
            return new();

        return await context
            .GenreTv.AsNoTracking()
            .Where(predicate: gt => tvIds.Contains(gt.TvId))
            .GroupBy(keySelector: gt => gt.TvId)
            .ToDictionaryAsync(keySelector: g => g.Key, elementSelector: g => g.Select(selector: gt => gt.GenreId).ToList(), cancellationToken: ct);
    }

    public async Task<(List<Movie> Movies, string? ColorPalette)> GetSourceMoviesForMediaAsync(
        Guid userId,
        int mediaId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        // Get distinct source movie IDs and grab color palette from the same query
        var recRows = await context
            .Recommendations.AsNoTracking()
            .Where(predicate: r => r.MediaId == mediaId && r.MovieFromId != null)
            .Where(predicate: r => r.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: r => new { SourceId = r.MovieFromId!.Value, r._colorPalette })
            .ToListAsync(cancellationToken: ct);

        var simRows = await context
            .Similar.AsNoTracking()
            .Where(predicate: s => s.MediaId == mediaId && s.MovieFromId != null)
            .Where(predicate: s => s.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: s => new { SourceId = s.MovieFromId!.Value, s._colorPalette })
            .ToListAsync(cancellationToken: ct);

        var allRows = recRows.Concat(second: simRows).ToList();
        string? colorPalette = allRows
            .FirstOrDefault(predicate: r => !string.IsNullOrEmpty(value: r._colorPalette))
            ?._colorPalette;
        List<int> sourceIds = allRows.Select(selector: r => r.SourceId).Distinct().ToList();

        if (sourceIds.Count == 0)
            return ([], colorPalette);

        List<Movie> movies = await context
            .Movies.AsNoTracking()
            .Where(predicate: m => sourceIds.Contains(m.Id))
            .Where(predicate: m => m.VideoFiles.Any())
            .Include(navigationPropertyPath: m =>
                m.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
            )
            .Include(navigationPropertyPath: m => m.VideoFiles)
            .Include(navigationPropertyPath: m => m.KeywordMovies)
                .ThenInclude(navigationPropertyPath: km => km.Keyword)
            .ToListAsync(cancellationToken: ct);

        return (movies, colorPalette);
    }

    public async Task<(List<Tv> TvShows, string? ColorPalette)> GetSourceTvShowsForMediaAsync(
        Guid userId,
        int mediaId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        var recRows = await context
            .Recommendations.AsNoTracking()
            .Where(predicate: r => r.MediaId == mediaId && r.TvFromId != null)
            .Where(predicate: r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: r => new { SourceId = r.TvFromId!.Value, r._colorPalette })
            .ToListAsync(cancellationToken: ct);

        var simRows = await context
            .Similar.AsNoTracking()
            .Where(predicate: s => s.MediaId == mediaId && s.TvFromId != null)
            .Where(predicate: s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: s => new { SourceId = s.TvFromId!.Value, s._colorPalette })
            .ToListAsync(cancellationToken: ct);

        var allRows = recRows.Concat(second: simRows).ToList();
        string? colorPalette = allRows
            .FirstOrDefault(predicate: r => !string.IsNullOrEmpty(value: r._colorPalette))
            ?._colorPalette;
        List<int> sourceIds = allRows.Select(selector: r => r.SourceId).Distinct().ToList();

        if (sourceIds.Count == 0)
            return ([], colorPalette);

        List<Tv> tvShows = await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => sourceIds.Contains(t.Id))
            .Where(predicate: t => t.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Include(navigationPropertyPath: t =>
                t.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
            )
            .Include(navigationPropertyPath: t => t.Episodes)
                .ThenInclude(navigationPropertyPath: e => e.VideoFiles)
            .Include(navigationPropertyPath: t => t.KeywordTvs)
                .ThenInclude(navigationPropertyPath: kt => kt.Keyword)
            .ToListAsync(cancellationToken: ct);

        return (tvShows, colorPalette);
    }

    public async Task<List<Movie>> GetKeywordMovieSourcesForMovieAsync(
        Guid userId,
        int movieId,
        HashSet<int> excludeIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<int> targetKeywordIds = await context
            .KeywordMovie.AsNoTracking()
            .Where(predicate: km => km.MovieId == movieId)
            .Select(selector: km => km.KeywordId)
            .ToListAsync(cancellationToken: ct);

        if (targetKeywordIds.Count == 0)
            return [];

        List<int> matchingMovieIds = await context
            .KeywordMovie.AsNoTracking()
            .Where(predicate: km => targetKeywordIds.Contains(km.KeywordId))
            .Where(predicate: km => km.Movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: km => km.Movie.VideoFiles.Any())
            .Where(predicate: km => !excludeIds.Contains(km.MovieId))
            .Select(selector: km => km.MovieId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (matchingMovieIds.Count == 0)
            return [];

        return await context
            .Movies.AsNoTracking()
            .Where(predicate: m => matchingMovieIds.Contains(m.Id))
            .Include(navigationPropertyPath: m =>
                m.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
            )
            .Include(navigationPropertyPath: m => m.VideoFiles)
            .Include(navigationPropertyPath: m => m.KeywordMovies)
                .ThenInclude(navigationPropertyPath: km => km.Keyword)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Tv>> GetKeywordTvSourcesForTvAsync(
        Guid userId,
        int tvId,
        HashSet<int> excludeIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<int> targetKeywordIds = await context
            .KeywordTv.AsNoTracking()
            .Where(predicate: kt => kt.TvId == tvId)
            .Select(selector: kt => kt.KeywordId)
            .ToListAsync(cancellationToken: ct);

        if (targetKeywordIds.Count == 0)
            return [];

        List<int> matchingTvIds = await context
            .KeywordTv.AsNoTracking()
            .Where(predicate: kt => targetKeywordIds.Contains(kt.KeywordId))
            .Where(predicate: kt => kt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: kt => kt.Tv.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Where(predicate: kt => !excludeIds.Contains(kt.TvId))
            .Select(selector: kt => kt.TvId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (matchingTvIds.Count == 0)
            return [];

        return await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => matchingTvIds.Contains(t.Id))
            .Include(navigationPropertyPath: t =>
                t.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
            )
            .Include(navigationPropertyPath: t => t.Episodes)
                .ThenInclude(navigationPropertyPath: e => e.VideoFiles)
            .Include(navigationPropertyPath: t => t.KeywordTvs)
                .ThenInclude(navigationPropertyPath: kt => kt.Keyword)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Movie>> GetCrossTypeMovieSourcesForTvAsync(
        Guid userId,
        int tvId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<int> tvKeywordIds = await context
            .KeywordTv.AsNoTracking()
            .Where(predicate: kt => kt.TvId == tvId)
            .Select(selector: kt => kt.KeywordId)
            .ToListAsync(cancellationToken: ct);

        if (tvKeywordIds.Count == 0)
            return [];

        List<int> movieIds = await context
            .KeywordMovie.AsNoTracking()
            .Where(predicate: km => tvKeywordIds.Contains(km.KeywordId))
            .Where(predicate: km => km.Movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: km => km.Movie.VideoFiles.Any())
            .Select(selector: km => km.MovieId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (movieIds.Count == 0)
            return [];

        return await context
            .Movies.AsNoTracking()
            .Where(predicate: m => movieIds.Contains(m.Id))
            .Include(navigationPropertyPath: m =>
                m.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
            )
            .Include(navigationPropertyPath: m => m.VideoFiles)
            .Include(navigationPropertyPath: m => m.KeywordMovies)
                .ThenInclude(navigationPropertyPath: km => km.Keyword)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Tv>> GetCrossTypeTvSourcesForMovieAsync(
        Guid userId,
        int movieId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<int> movieKeywordIds = await context
            .KeywordMovie.AsNoTracking()
            .Where(predicate: km => km.MovieId == movieId)
            .Select(selector: km => km.KeywordId)
            .ToListAsync(cancellationToken: ct);

        if (movieKeywordIds.Count == 0)
            return [];

        List<int> tvIds = await context
            .KeywordTv.AsNoTracking()
            .Where(predicate: kt => movieKeywordIds.Contains(kt.KeywordId))
            .Where(predicate: kt => kt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: kt => kt.Tv.Episodes.Any(e => e.SeasonNumber > 0 && e.VideoFiles.Any()))
            .Select(selector: kt => kt.TvId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (tvIds.Count == 0)
            return [];

        return await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => tvIds.Contains(t.Id))
            .Include(navigationPropertyPath: t =>
                t.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
            )
            .Include(navigationPropertyPath: t => t.Episodes)
                .ThenInclude(navigationPropertyPath: e => e.VideoFiles)
            .Include(navigationPropertyPath: t => t.KeywordTvs)
                .ThenInclude(navigationPropertyPath: kt => kt.Keyword)
            .ToListAsync(cancellationToken: ct);
    }
}
