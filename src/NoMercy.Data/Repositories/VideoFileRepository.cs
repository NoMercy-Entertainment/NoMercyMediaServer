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
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        return await context
            .VideoFiles.AsNoTracking()
            .FirstOrDefaultAsync(file => file.Id == id, ct);
    }

    public async Task<bool> ExistsAsync(Ulid id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        return await context.VideoFiles.AsNoTracking().AnyAsync(file => file.Id == id, ct);
    }

    public async Task<List<Episode>> GetEncodedEpisodesForSeasonAsync(
        int seasonId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        return await context
            .Episodes.AsNoTracking()
            .Include(episode => episode.VideoFiles)
            .Where(episode => episode.SeasonId == seasonId && episode.VideoFiles.Count > 0)
            .OrderBy(episode => episode.EpisodeNumber)
            .ToListAsync(ct);
    }
}
