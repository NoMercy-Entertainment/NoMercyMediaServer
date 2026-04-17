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
public class ContentSegmentRepository(MediaContext context)
{
    public Task<List<ContentSegment>> GetForEpisodeAsync(int episodeId) =>
        context
            .ContentSegments.AsNoTracking()
            .Where(s => s.EpisodeId == episodeId)
            .OrderBy(s => s.StartSeconds)
            .ToListAsync();

    public Task<List<ContentSegment>> GetForMovieAsync(int movieId) =>
        context
            .ContentSegments.AsNoTracking()
            .Where(s => s.MovieId == movieId)
            .OrderBy(s => s.StartSeconds)
            .ToListAsync();

    public Task<ContentSegment?> GetByIdAsync(Ulid id) =>
        context.ContentSegments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

    public async Task<ContentSegment> CreateAsync(ContentSegment segment)
    {
        segment.CreatedAt = DateTime.UtcNow;
        segment.UpdatedAt = segment.CreatedAt;
        context.ContentSegments.Add(segment);
        await context.SaveChangesAsync();
        return segment;
    }

    public async Task<ContentSegment?> UpdateAsync(Ulid id, Action<ContentSegment> apply)
    {
        ContentSegment? existing = await context.ContentSegments.FirstOrDefaultAsync(s =>
            s.Id == id
        );
        if (existing is null)
            return null;

        apply(existing);
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Ulid id)
    {
        ContentSegment? existing = await context.ContentSegments.FirstOrDefaultAsync(s =>
            s.Id == id
        );
        if (existing is null)
            return false;

        context.ContentSegments.Remove(existing);
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
            .ContentSegments.Where(s => s.EpisodeId == episodeId && s.Source == "detector")
            .ToListAsync();

        context.ContentSegments.RemoveRange(oldDetector);
        foreach (ContentSegment seg in newSegments)
        {
            seg.EpisodeId = episodeId;
            seg.MovieId = null;
            seg.Source = "detector";
            seg.CreatedAt = DateTime.UtcNow;
            seg.UpdatedAt = seg.CreatedAt;
            context.ContentSegments.Add(seg);
        }

        await context.SaveChangesAsync();
    }
}
