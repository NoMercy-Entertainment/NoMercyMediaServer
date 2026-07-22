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

namespace NoMercy.Data.Repositories;

/// <summary>
/// Read/write access for timeline annotations — intro/outro/recap/credits
/// segments the player uses to render "Skip Intro" / auto-advance buttons.
/// Writes come from the detector (chromaprint pipeline) or manual edits
/// from the dashboard; reads come from the player on every playback start.
/// </summary>
public class ContentSegmentRepository(MediaContext context) : IContentSegmentRepository
{
    public Task<List<ContentSegment>> GetForEpisodeAsync(int episodeId) =>
        context
            .ContentSegments.AsNoTracking()
            .Where(predicate: s => s.EpisodeId == episodeId)
            .OrderBy(keySelector: s => s.StartSeconds)
            .ThenBy(keySelector: s => s.Id)
            .ToListAsync();

    /// <summary>
    /// Paginated listing for the dashboard overview. Filter by type when
    /// supplied so "show me every detected outro" is a cheap query.
    /// </summary>
    public async Task<List<ContentSegment>> ListAsync(
        int pageSize,
        int pageIndex,
        ContentSegmentType? filterType = null
    )
    {
        if (pageSize <= 0)
            pageSize = 100;
        if (pageIndex < 0)
            pageIndex = 0;

        IQueryable<ContentSegment> query = context.ContentSegments.AsNoTracking();
        if (filterType.HasValue)
            query = query.Where(predicate: s => s.SegmentType == filterType.Value);

        return await query
            .OrderByDescending(keySelector: s => s.UpdatedAt)
            .ThenByDescending(keySelector: s => s.Id)
            .Skip(count: pageIndex * pageSize)
            .Take(count: pageSize)
            .ToListAsync();
    }

    public Task<int> GetTotalCountAsync() => context.ContentSegments.CountAsync();

    public Task<List<ContentSegment>> GetForMovieAsync(int movieId) =>
        context
            .ContentSegments.AsNoTracking()
            .Where(predicate: s => s.MovieId == movieId)
            .OrderBy(keySelector: s => s.StartSeconds)
            .ThenBy(keySelector: s => s.Id)
            .ToListAsync();

    public Task<ContentSegment?> GetByIdAsync(Ulid id) =>
        context.ContentSegments.AsNoTracking().FirstOrDefaultAsync(predicate: s => s.Id == id);

    public async Task<ContentSegment> CreateAsync(ContentSegment segment)
    {
        segment.CreatedAt = DateTime.UtcNow;
        segment.UpdatedAt = segment.CreatedAt;
        context.ContentSegments.Add(entity: segment);
        await context.SaveChangesAsync();
        return segment;
    }

    public async Task<ContentSegment?> UpdateAsync(Ulid id, Action<ContentSegment> apply)
    {
        ContentSegment? existing = await context.ContentSegments.FirstOrDefaultAsync(predicate: s =>
            s.Id == id
        );
        if (existing is null)
            return null;

        apply(obj: existing);
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Ulid id)
    {
        ContentSegment? existing = await context.ContentSegments.FirstOrDefaultAsync(predicate: s =>
            s.Id == id
        );
        if (existing is null)
            return false;

        context.ContentSegments.Remove(entity: existing);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Bulk replaces detector-written segments for an episode. Manual edits
    /// (Source != "detector") stay untouched so re-running detection doesn't
    /// clobber user overrides.
    /// </summary>
    public async Task ReplaceDetectorSegmentsForEpisodeAsync(
        int episodeId,
        IReadOnlyList<ContentSegment> newSegments
    )
    {
        List<ContentSegment> oldDetector = await context
            .ContentSegments.Where(predicate: s => s.EpisodeId == episodeId && s.Source == "detector")
            .ToListAsync();

        context.ContentSegments.RemoveRange(entities: oldDetector);
        foreach (ContentSegment seg in newSegments)
        {
            seg.EpisodeId = episodeId;
            seg.MovieId = null;
            seg.Source = "detector";
            seg.CreatedAt = DateTime.UtcNow;
            seg.UpdatedAt = seg.CreatedAt;
            context.ContentSegments.Add(entity: seg);
        }

        await context.SaveChangesAsync();
    }
}
