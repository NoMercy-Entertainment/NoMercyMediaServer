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

namespace NoMercy.Data.Repositories;

public class VideoFileRepository(IDbContextFactory<MediaContext> contextFactory)
    : IVideoFileRepository
{
    public async Task<VideoFile?> GetByIdAsync(Ulid id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .VideoFiles.AsNoTracking()
            .FirstOrDefaultAsync(predicate: file => file.Id == id, cancellationToken: ct);
    }

    public async Task<bool> ExistsAsync(Ulid id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context.VideoFiles.AsNoTracking().AnyAsync(predicate: file => file.Id == id, cancellationToken: ct);
    }

    public async Task<List<Episode>> GetEncodedEpisodesForSeasonAsync(
        int seasonId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Episodes.AsNoTracking()
            .Include(navigationPropertyPath: episode => episode.VideoFiles)
            .Where(predicate: episode => episode.SeasonId == seasonId && episode.VideoFiles.Count > 0)
            .OrderBy(keySelector: episode => episode.EpisodeNumber)
            .ThenBy(keySelector: episode => episode.Id)
            .ToListAsync(cancellationToken: ct);
    }
}
