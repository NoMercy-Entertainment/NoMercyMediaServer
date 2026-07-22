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
    public async Task<List<RecommendationCandidateDto>> GetKeywordCrossTypeTvCandidatesAsync(
        Guid userId,
        Dictionary<int, List<int>> movieKeywordMap,
        int minSharedKeywords = 3,
        int maxCandidates = 100,
        int maxKeywordFrequency = 50,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        if (movieKeywordMap.Count == 0)
            return [];

        HashSet<int> ownedMovieKeywordIds = movieKeywordMap
            .Values.SelectMany(selector: kws => kws)
            .ToHashSet();

        if (ownedMovieKeywordIds.Count == 0)
            return [];

        // Filter out overly common keywords — generic tags like "animation" or "cat" match too many items
        HashSet<int> commonKeywordIds = (
            await context
                .KeywordTv.AsNoTracking()
                .Where(predicate: kt => ownedMovieKeywordIds.Contains(kt.KeywordId))
                .GroupBy(keySelector: kt => kt.KeywordId)
                .Where(predicate: g => g.Count() > maxKeywordFrequency)
                .Select(selector: g => g.Key)
                .ToListAsync(cancellationToken: ct)
        ).ToHashSet();

        HashSet<int> specificKeywordIds = ownedMovieKeywordIds
            .Where(predicate: id => !commonKeywordIds.Contains(item: id))
            .ToHashSet();

        if (specificKeywordIds.Count == 0)
            return [];

        // Step 1: Flat server-side query — find KeywordTv rows matching specific movie keywords on unowned TV shows (excluding anime)
        var keywordTvRows = await context
            .KeywordTv.AsNoTracking()
            .Where(predicate: kt => specificKeywordIds.Contains(kt.KeywordId))
            .Where(predicate: kt =>
                context.Tvs.Any(t => t.Id == kt.TvId && t.MediaType != MediaTypes.AnimeMediaType)
            )
            .Where(predicate: kt =>
                !context.Tvs.Any(t =>
                    t.Id == kt.TvId && t.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Select(selector: kt => new { kt.TvId, kt.KeywordId })
            .ToListAsync(cancellationToken: ct);

        // Step 2: Client-side grouping — filter by minimum shared keyword count
        var tvKeywordGroups = keywordTvRows
            .GroupBy(keySelector: r => r.TvId)
            .Where(predicate: g => g.Count() >= minSharedKeywords)
            .OrderByDescending(keySelector: g => g.Count())
            .Take(count: maxCandidates)
            .ToList();

        if (tvKeywordGroups.Count == 0)
            return [];

        // Step 3: Fetch TV metadata for qualifying shows
        List<int> qualifyingTvIds = tvKeywordGroups.Select(selector: g => g.Key).ToList();

        var tvMetadata = await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => qualifyingTvIds.Contains(t.Id))
            .Select(selector: t => new
            {
                t.Id,
                t.Title,
                t.TitleSort,
                t.Overview,
                t.Poster,
                t.Backdrop,
                t._colorPalette,
            })
            .ToListAsync(cancellationToken: ct);

        Dictionary<
            int,
            (
                string Title,
                string TitleSort,
                string? Overview,
                string? Poster,
                string? Backdrop,
                string? Palette
            )
        > metaMap = tvMetadata.ToDictionary(
            keySelector: t => t.Id,
            elementSelector: t => (t.Title, t.TitleSort, t.Overview, t.Poster, t.Backdrop, t._colorPalette)
        );

        // Step 4: Build candidates with reverse-mapped source IDs
        return tvKeywordGroups
            .Where(predicate: g => metaMap.ContainsKey(key: g.Key))
            .Select(selector: g =>
            {
                (
                    string Title,
                    string TitleSort,
                    string? Overview,
                    string? Poster,
                    string? Backdrop,
                    string? Palette
                ) meta = metaMap[key: g.Key];
                HashSet<int> sharedKeywordIds = g.Select(selector: r => r.KeywordId).ToHashSet();
                List<int> sourceMovieIds = movieKeywordMap
                    .Where(predicate: kv => kv.Value.Any(predicate: kw => sharedKeywordIds.Contains(item: kw)))
                    .Select(selector: kv => kv.Key)
                    .Distinct()
                    .ToList();

                return new RecommendationCandidateDto
                {
                    MediaId = g.Key,
                    Title = meta.Title,
                    TitleSort = meta.TitleSort,
                    Overview = meta.Overview,
                    Poster = meta.Poster,
                    Backdrop = meta.Backdrop,
                    ColorPalette = meta.Palette.OrEmpty(),
                    MediaType = MediaTypes.TvMediaType,
                    SourceMediaType = MediaTypes.MovieMediaType,
                    SourceCount = sourceMovieIds.Count,
                    SourceIds = sourceMovieIds,
                };
            })
            .ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetKeywordCrossTypeAnimeCandidatesAsync(
        Guid userId,
        Dictionary<int, List<int>> movieKeywordMap,
        int minSharedKeywords = 3,
        int maxCandidates = 100,
        int maxKeywordFrequency = 50,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        if (movieKeywordMap.Count == 0)
            return [];

        HashSet<int> ownedMovieKeywordIds = movieKeywordMap
            .Values.SelectMany(selector: kws => kws)
            .ToHashSet();

        if (ownedMovieKeywordIds.Count == 0)
            return [];

        HashSet<int> commonKeywordIds = (
            await context
                .KeywordTv.AsNoTracking()
                .Where(predicate: kt => ownedMovieKeywordIds.Contains(kt.KeywordId))
                .GroupBy(keySelector: kt => kt.KeywordId)
                .Where(predicate: g => g.Count() > maxKeywordFrequency)
                .Select(selector: g => g.Key)
                .ToListAsync(cancellationToken: ct)
        ).ToHashSet();

        HashSet<int> specificKeywordIds = ownedMovieKeywordIds
            .Where(predicate: id => !commonKeywordIds.Contains(item: id))
            .ToHashSet();

        if (specificKeywordIds.Count == 0)
            return [];

        var keywordTvRows = await context
            .KeywordTv.AsNoTracking()
            .Where(predicate: kt => specificKeywordIds.Contains(kt.KeywordId))
            .Where(predicate: kt =>
                context.Tvs.Any(t => t.Id == kt.TvId && t.MediaType == MediaTypes.AnimeMediaType)
            )
            .Where(predicate: kt =>
                !context.Tvs.Any(t =>
                    t.Id == kt.TvId && t.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Select(selector: kt => new { kt.TvId, kt.KeywordId })
            .ToListAsync(cancellationToken: ct);

        var tvKeywordGroups = keywordTvRows
            .GroupBy(keySelector: r => r.TvId)
            .Where(predicate: g => g.Count() >= minSharedKeywords)
            .OrderByDescending(keySelector: g => g.Count())
            .Take(count: maxCandidates)
            .ToList();

        if (tvKeywordGroups.Count == 0)
            return [];

        List<int> qualifyingTvIds = tvKeywordGroups.Select(selector: g => g.Key).ToList();

        var tvMetadata = await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => qualifyingTvIds.Contains(t.Id))
            .Select(selector: t => new
            {
                t.Id,
                t.Title,
                t.TitleSort,
                t.Overview,
                t.Poster,
                t.Backdrop,
                t._colorPalette,
            })
            .ToListAsync(cancellationToken: ct);

        Dictionary<
            int,
            (
                string Title,
                string TitleSort,
                string? Overview,
                string? Poster,
                string? Backdrop,
                string? Palette
            )
        > metaMap = tvMetadata.ToDictionary(
            keySelector: t => t.Id,
            elementSelector: t => (t.Title, t.TitleSort, t.Overview, t.Poster, t.Backdrop, t._colorPalette)
        );

        return tvKeywordGroups
            .Where(predicate: g => metaMap.ContainsKey(key: g.Key))
            .Select(selector: g =>
            {
                (
                    string Title,
                    string TitleSort,
                    string? Overview,
                    string? Poster,
                    string? Backdrop,
                    string? Palette
                ) meta = metaMap[key: g.Key];
                HashSet<int> sharedKeywordIds = g.Select(selector: r => r.KeywordId).ToHashSet();
                List<int> sourceMovieIds = movieKeywordMap
                    .Where(predicate: kv => kv.Value.Any(predicate: kw => sharedKeywordIds.Contains(item: kw)))
                    .Select(selector: kv => kv.Key)
                    .Distinct()
                    .ToList();

                return new RecommendationCandidateDto
                {
                    MediaId = g.Key,
                    Title = meta.Title,
                    TitleSort = meta.TitleSort,
                    Overview = meta.Overview,
                    Poster = meta.Poster,
                    Backdrop = meta.Backdrop,
                    ColorPalette = meta.Palette.OrEmpty(),
                    MediaType = MediaTypes.AnimeMediaType,
                    SourceMediaType = MediaTypes.MovieMediaType,
                    SourceCount = sourceMovieIds.Count,
                    SourceIds = sourceMovieIds,
                };
            })
            .ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetKeywordCrossTypeMovieCandidatesAsync(
        Guid userId,
        Dictionary<int, List<int>> tvKeywordMap,
        int minSharedKeywords = 3,
        int maxCandidates = 100,
        int maxKeywordFrequency = 50,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        if (tvKeywordMap.Count == 0)
            return [];

        HashSet<int> ownedTvKeywordIds = tvKeywordMap.Values.SelectMany(selector: kws => kws).ToHashSet();

        if (ownedTvKeywordIds.Count == 0)
            return [];

        // Filter out overly common keywords — generic tags match too many items
        HashSet<int> commonKeywordIds = (
            await context
                .KeywordMovie.AsNoTracking()
                .Where(predicate: km => ownedTvKeywordIds.Contains(km.KeywordId))
                .GroupBy(keySelector: km => km.KeywordId)
                .Where(predicate: g => g.Count() > maxKeywordFrequency)
                .Select(selector: g => g.Key)
                .ToListAsync(cancellationToken: ct)
        ).ToHashSet();

        HashSet<int> specificKeywordIds = ownedTvKeywordIds
            .Where(predicate: id => !commonKeywordIds.Contains(item: id))
            .ToHashSet();

        if (specificKeywordIds.Count == 0)
            return [];

        // Step 1: Flat server-side query — find KeywordMovie rows matching specific TV keywords on unowned movies
        var keywordMovieRows = await context
            .KeywordMovie.AsNoTracking()
            .Where(predicate: km => specificKeywordIds.Contains(km.KeywordId))
            .Where(predicate: km =>
                !context.Movies.Any(m =>
                    m.Id == km.MovieId && m.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Select(selector: km => new { km.MovieId, km.KeywordId })
            .ToListAsync(cancellationToken: ct);

        // Step 2: Client-side grouping — filter by minimum shared keyword count
        var movieKeywordGroups = keywordMovieRows
            .GroupBy(keySelector: r => r.MovieId)
            .Where(predicate: g => g.Count() >= minSharedKeywords)
            .OrderByDescending(keySelector: g => g.Count())
            .Take(count: maxCandidates)
            .ToList();

        if (movieKeywordGroups.Count == 0)
            return [];

        // Step 3: Fetch movie metadata for qualifying movies
        List<int> qualifyingMovieIds = movieKeywordGroups.Select(selector: g => g.Key).ToList();

        var movieMetadata = await context
            .Movies.AsNoTracking()
            .Where(predicate: m => qualifyingMovieIds.Contains(m.Id))
            .Select(selector: m => new
            {
                m.Id,
                m.Title,
                m.TitleSort,
                m.Overview,
                m.Poster,
                m.Backdrop,
                m._colorPalette,
            })
            .ToListAsync(cancellationToken: ct);

        Dictionary<
            int,
            (
                string Title,
                string TitleSort,
                string? Overview,
                string? Poster,
                string? Backdrop,
                string? Palette
            )
        > metaMap = movieMetadata.ToDictionary(
            keySelector: m => m.Id,
            elementSelector: m => (m.Title, m.TitleSort, m.Overview, m.Poster, m.Backdrop, m._colorPalette)
        );

        // Step 4: Build candidates with reverse-mapped source IDs
        return movieKeywordGroups
            .Where(predicate: g => metaMap.ContainsKey(key: g.Key))
            .Select(selector: g =>
            {
                (
                    string Title,
                    string TitleSort,
                    string? Overview,
                    string? Poster,
                    string? Backdrop,
                    string? Palette
                ) meta = metaMap[key: g.Key];
                HashSet<int> sharedKeywordIds = g.Select(selector: r => r.KeywordId).ToHashSet();
                List<int> sourceTvIds = tvKeywordMap
                    .Where(predicate: kv => kv.Value.Any(predicate: kw => sharedKeywordIds.Contains(item: kw)))
                    .Select(selector: kv => kv.Key)
                    .Distinct()
                    .ToList();

                return new RecommendationCandidateDto
                {
                    MediaId = g.Key,
                    Title = meta.Title,
                    TitleSort = meta.TitleSort,
                    Overview = meta.Overview,
                    Poster = meta.Poster,
                    Backdrop = meta.Backdrop,
                    ColorPalette = meta.Palette.OrEmpty(),
                    MediaType = MediaTypes.MovieMediaType,
                    SourceMediaType = MediaTypes.TvMediaType,
                    SourceCount = sourceTvIds.Count,
                    SourceIds = sourceTvIds,
                };
            })
            .ToList();
    }
}
