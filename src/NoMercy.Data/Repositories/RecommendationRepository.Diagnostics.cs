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
    public async Task<RecommendationDiagnosticsDto> GetDiagnosticsAsync(
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        int animeByLibraryType = await context
            .Tvs.AsNoTracking()
            .CountAsync(predicate: t => t.Library.Type == MediaTypes.AnimeMediaType, cancellationToken: ct);

        int animeByMediaType = await context
            .Tvs.AsNoTracking()
            .CountAsync(predicate: t => t.MediaType == MediaTypes.AnimeMediaType, cancellationToken: ct);

        int totalRecsWithTv = await context
            .Recommendations.AsNoTracking()
            .CountAsync(predicate: r => r.TvFromId != null, cancellationToken: ct);

        int animeRecsByMediaType = await context
            .Recommendations.AsNoTracking()
            .CountAsync(
                predicate: r =>
                    r.TvFromId != null
                    && context.Tvs.Any(t =>
                        t.Id == r.TvFromId && t.MediaType == MediaTypes.AnimeMediaType
                    ),
                cancellationToken: ct
            );

        int totalSimWithTv = await context
            .Similar.AsNoTracking()
            .CountAsync(predicate: s => s.TvFromId != null, cancellationToken: ct);

        int animeSimByMediaType = await context
            .Similar.AsNoTracking()
            .CountAsync(
                predicate: s =>
                    s.TvFromId != null
                    && context.Tvs.Any(t =>
                        t.Id == s.TvFromId && t.MediaType == MediaTypes.AnimeMediaType
                    ),
                cancellationToken: ct
            );

        List<string> libraries = await context
            .Libraries.AsNoTracking()
            .Select(selector: l => l.Title + " (" + l.Type + ")")
            .ToListAsync(cancellationToken: ct);

        List<int> sampleAnimeIds = await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => t.MediaType == MediaTypes.AnimeMediaType)
            .OrderBy(keySelector: t => t.Id)
            .Take(count: 5)
            .Select(selector: t => t.Id)
            .ToListAsync(cancellationToken: ct);

        int sampleRecsCount =
            sampleAnimeIds.Count > 0
                ? await context
                    .Recommendations.AsNoTracking()
                    .CountAsync(predicate: r => sampleAnimeIds.Contains(r.TvFromId!.Value), cancellationToken: ct)
                : 0;

        return new()
        {
            Libraries = libraries,
            AnimeByLibraryType = animeByLibraryType,
            AnimeByMediaType = animeByMediaType,
            TotalRecsWithTv = totalRecsWithTv,
            AnimeRecsByMediaType = animeRecsByMediaType,
            TotalSimWithTv = totalSimWithTv,
            AnimeSimByMediaType = animeSimByMediaType,
            SampleAnimeIds = sampleAnimeIds,
            SampleRecsCount = sampleRecsCount,
        };
    }
}
