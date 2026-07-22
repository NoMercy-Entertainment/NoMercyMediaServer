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
    public async Task<List<RecommendationCandidateDto>> GetUnownedMovieRecommendationsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        // Step 1: Group server-side for IDs only (avoids SQL APPLY)
        // Use NOT EXISTS against Movies table instead of ToId==null (ToId may not be set for older data)
        List<int> mediaIds = await context
            .Recommendations.AsNoTracking()
            .Where(predicate: r => r.MovieFromId != null)
            .Where(predicate: r => r.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: r =>
                !context.Movies.Any(m =>
                    m.Id == r.MediaId && m.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Select(selector: r => r.MediaId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (mediaIds.Count == 0)
            return [];

        // Step 2: Fetch metadata for each distinct MediaId
        Dictionary<int, RecommendationCandidateDto> metadataMap = await context
            .Recommendations.AsNoTracking()
            .Where(predicate: r => mediaIds.Contains(r.MediaId) && r.MovieFromId != null)
            .Where(predicate: r => r.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: r => new
            {
                r.MediaId,
                r.Title,
                r.TitleSort,
                r.Overview,
                r.Poster,
                r.Backdrop,
                r._colorPalette,
                r.MovieFromId,
            })
            .ToListAsync(cancellationToken: ct)
            .ContinueWith(
                continuationFunction: t =>
                    t.Result.GroupBy(keySelector: r => r.MediaId)
                        .ToDictionary(
                            keySelector: g => g.Key,
                            elementSelector: g =>
                            {
                                var first = g.First();
                                return new RecommendationCandidateDto
                                {
                                    MediaId = g.Key,
                                    Title = first.Title,
                                    TitleSort = first.TitleSort,
                                    Overview = first.Overview,
                                    Poster = first.Poster,
                                    Backdrop = first.Backdrop,
                                    ColorPalette = first._colorPalette.OrEmpty(),
                                    MediaType = MediaTypes.MovieMediaType,
                                    SourceCount = g.Select(selector: r => r.MovieFromId).Distinct().Count(),
                                    SourceIds = g.Select(selector: r => r.MovieFromId!.Value)
                                        .Distinct()
                                        .ToList(),
                                };
                            }
                        ),
                cancellationToken: ct
            );

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedTvRecommendationsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<int> mediaIds = await context
            .Recommendations.AsNoTracking()
            .Where(predicate: r => r.TvFromId != null)
            .Where(predicate: r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: r => r.TvFrom!.MediaType != MediaTypes.AnimeMediaType)
            .Where(predicate: r =>
                !context.Tvs.Any(t =>
                    t.Id == r.MediaId && t.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Select(selector: r => r.MediaId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (mediaIds.Count == 0)
            return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context
            .Recommendations.AsNoTracking()
            .Where(predicate: r => mediaIds.Contains(r.MediaId) && r.TvFromId != null)
            .Where(predicate: r => r.TvFrom!.MediaType != MediaTypes.AnimeMediaType)
            .Where(predicate: r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: r => new
            {
                r.MediaId,
                r.Title,
                r.TitleSort,
                r.Overview,
                r.Poster,
                r.Backdrop,
                r._colorPalette,
                r.TvFromId,
            })
            .ToListAsync(cancellationToken: ct)
            .ContinueWith(
                continuationFunction: t =>
                    t.Result.GroupBy(keySelector: r => r.MediaId)
                        .ToDictionary(
                            keySelector: g => g.Key,
                            elementSelector: g =>
                            {
                                var first = g.First();
                                return new RecommendationCandidateDto
                                {
                                    MediaId = g.Key,
                                    Title = first.Title,
                                    TitleSort = first.TitleSort,
                                    Overview = first.Overview,
                                    Poster = first.Poster,
                                    Backdrop = first.Backdrop,
                                    ColorPalette = first._colorPalette.OrEmpty(),
                                    MediaType = MediaTypes.TvMediaType,
                                    SourceCount = g.Select(selector: r => r.TvFromId).Distinct().Count(),
                                    SourceIds = g.Select(selector: r => r.TvFromId!.Value)
                                        .Distinct()
                                        .ToList(),
                                };
                            }
                        ),
                cancellationToken: ct
            );

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedAnimeRecommendationsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<int> mediaIds = await context
            .Recommendations.AsNoTracking()
            .Where(predicate: r => r.TvFromId != null)
            .Where(predicate: r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: r => r.TvFrom!.MediaType == MediaTypes.AnimeMediaType)
            .Where(predicate: r =>
                !context.Tvs.Any(t =>
                    t.Id == r.MediaId && t.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Select(selector: r => r.MediaId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (mediaIds.Count == 0)
            return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context
            .Recommendations.AsNoTracking()
            .Where(predicate: r => mediaIds.Contains(r.MediaId) && r.TvFromId != null)
            .Where(predicate: r => r.TvFrom!.MediaType == MediaTypes.AnimeMediaType)
            .Where(predicate: r => r.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: r => new
            {
                r.MediaId,
                r.Title,
                r.TitleSort,
                r.Overview,
                r.Poster,
                r.Backdrop,
                r._colorPalette,
                r.TvFromId,
            })
            .ToListAsync(cancellationToken: ct)
            .ContinueWith(
                continuationFunction: t =>
                    t.Result.GroupBy(keySelector: r => r.MediaId)
                        .ToDictionary(
                            keySelector: g => g.Key,
                            elementSelector: g =>
                            {
                                var first = g.First();
                                return new RecommendationCandidateDto
                                {
                                    MediaId = g.Key,
                                    Title = first.Title,
                                    TitleSort = first.TitleSort,
                                    Overview = first.Overview,
                                    Poster = first.Poster,
                                    Backdrop = first.Backdrop,
                                    ColorPalette = first._colorPalette.OrEmpty(),
                                    MediaType = MediaTypes.AnimeMediaType,
                                    SourceCount = g.Select(selector: r => r.TvFromId).Distinct().Count(),
                                    SourceIds = g.Select(selector: r => r.TvFromId!.Value)
                                        .Distinct()
                                        .ToList(),
                                };
                            }
                        ),
                cancellationToken: ct
            );

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedMovieSimilarAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<int> mediaIds = await context
            .Similar.AsNoTracking()
            .Where(predicate: s => s.MovieFromId != null)
            .Where(predicate: s => s.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: s =>
                !context.Movies.Any(m =>
                    m.Id == s.MediaId && m.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Select(selector: s => s.MediaId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (mediaIds.Count == 0)
            return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context
            .Similar.AsNoTracking()
            .Where(predicate: s => mediaIds.Contains(s.MediaId) && s.MovieFromId != null)
            .Where(predicate: s => s.MovieFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: s => new
            {
                s.MediaId,
                s.Title,
                s.TitleSort,
                s.Overview,
                s.Poster,
                s.Backdrop,
                s._colorPalette,
                s.MovieFromId,
            })
            .ToListAsync(cancellationToken: ct)
            .ContinueWith(
                continuationFunction: t =>
                    t.Result.GroupBy(keySelector: s => s.MediaId)
                        .ToDictionary(
                            keySelector: g => g.Key,
                            elementSelector: g =>
                            {
                                var first = g.First();
                                return new RecommendationCandidateDto
                                {
                                    MediaId = g.Key,
                                    Title = first.Title,
                                    TitleSort = first.TitleSort,
                                    Overview = first.Overview,
                                    Poster = first.Poster,
                                    Backdrop = first.Backdrop,
                                    ColorPalette = first._colorPalette.OrEmpty(),
                                    MediaType = MediaTypes.MovieMediaType,
                                    SourceCount = g.Select(selector: s => s.MovieFromId).Distinct().Count(),
                                    SourceIds = g.Select(selector: s => s.MovieFromId!.Value)
                                        .Distinct()
                                        .ToList(),
                                };
                            }
                        ),
                cancellationToken: ct
            );

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedTvSimilarAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<int> mediaIds = await context
            .Similar.AsNoTracking()
            .Where(predicate: s => s.TvFromId != null)
            .Where(predicate: s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: s => s.TvFrom!.MediaType != MediaTypes.AnimeMediaType)
            .Where(predicate: s =>
                !context.Tvs.Any(t =>
                    t.Id == s.MediaId && t.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Select(selector: s => s.MediaId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (mediaIds.Count == 0)
            return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context
            .Similar.AsNoTracking()
            .Where(predicate: s => mediaIds.Contains(s.MediaId) && s.TvFromId != null)
            .Where(predicate: s => s.TvFrom!.MediaType != MediaTypes.AnimeMediaType)
            .Where(predicate: s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: s => new
            {
                s.MediaId,
                s.Title,
                s.TitleSort,
                s.Overview,
                s.Poster,
                s.Backdrop,
                s._colorPalette,
                s.TvFromId,
            })
            .ToListAsync(cancellationToken: ct)
            .ContinueWith(
                continuationFunction: t =>
                    t.Result.GroupBy(keySelector: s => s.MediaId)
                        .ToDictionary(
                            keySelector: g => g.Key,
                            elementSelector: g =>
                            {
                                var first = g.First();
                                return new RecommendationCandidateDto
                                {
                                    MediaId = g.Key,
                                    Title = first.Title,
                                    TitleSort = first.TitleSort,
                                    Overview = first.Overview,
                                    Poster = first.Poster,
                                    Backdrop = first.Backdrop,
                                    ColorPalette = first._colorPalette.OrEmpty(),
                                    MediaType = MediaTypes.TvMediaType,
                                    SourceCount = g.Select(selector: s => s.TvFromId).Distinct().Count(),
                                    SourceIds = g.Select(selector: s => s.TvFromId!.Value)
                                        .Distinct()
                                        .ToList(),
                                };
                            }
                        ),
                cancellationToken: ct
            );

        return metadataMap.Values.ToList();
    }

    public async Task<List<RecommendationCandidateDto>> GetUnownedAnimeSimilarAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<int> mediaIds = await context
            .Similar.AsNoTracking()
            .Where(predicate: s => s.TvFromId != null)
            .Where(predicate: s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: s => s.TvFrom!.MediaType == MediaTypes.AnimeMediaType)
            .Where(predicate: s =>
                !context.Tvs.Any(t =>
                    t.Id == s.MediaId && t.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Select(selector: s => s.MediaId)
            .Distinct()
            .ToListAsync(cancellationToken: ct);

        if (mediaIds.Count == 0)
            return [];

        Dictionary<int, RecommendationCandidateDto> metadataMap = await context
            .Similar.AsNoTracking()
            .Where(predicate: s => mediaIds.Contains(s.MediaId) && s.TvFromId != null)
            .Where(predicate: s => s.TvFrom!.MediaType == MediaTypes.AnimeMediaType)
            .Where(predicate: s => s.TvFrom!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Select(selector: s => new
            {
                s.MediaId,
                s.Title,
                s.TitleSort,
                s.Overview,
                s.Poster,
                s.Backdrop,
                s._colorPalette,
                s.TvFromId,
            })
            .ToListAsync(cancellationToken: ct)
            .ContinueWith(
                continuationFunction: t =>
                    t.Result.GroupBy(keySelector: s => s.MediaId)
                        .ToDictionary(
                            keySelector: g => g.Key,
                            elementSelector: g =>
                            {
                                var first = g.First();
                                return new RecommendationCandidateDto
                                {
                                    MediaId = g.Key,
                                    Title = first.Title,
                                    TitleSort = first.TitleSort,
                                    Overview = first.Overview,
                                    Poster = first.Poster,
                                    Backdrop = first.Backdrop,
                                    ColorPalette = first._colorPalette.OrEmpty(),
                                    MediaType = MediaTypes.AnimeMediaType,
                                    SourceCount = g.Select(selector: s => s.TvFromId).Distinct().Count(),
                                    SourceIds = g.Select(selector: s => s.TvFromId!.Value)
                                        .Distinct()
                                        .ToList(),
                                };
                            }
                        ),
                cancellationToken: ct
            );

        return metadataMap.Values.ToList();
    }
}
