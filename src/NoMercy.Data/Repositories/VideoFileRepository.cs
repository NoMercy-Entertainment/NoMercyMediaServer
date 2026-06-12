using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.Data.Repositories;

public class VideoFileRepository(MediaContext context) : IVideoFileRepository
{
    public Task<VideoFile?> GetByIdAsync(Ulid id, CancellationToken ct = default)
    {
        return context.VideoFiles.AsNoTracking().FirstOrDefaultAsync(file => file.Id == id, ct);
    }

    public Task<bool> ExistsAsync(Ulid id, CancellationToken ct = default)
    {
        return context.VideoFiles.AsNoTracking().AnyAsync(file => file.Id == id, ct);
    }

    public Task<List<Episode>> GetEncodedEpisodesForSeasonAsync(
        int seasonId,
        CancellationToken ct = default
    )
    {
        return context
            .Episodes.AsNoTracking()
            .Include(episode => episode.VideoFiles)
            .Where(episode => episode.SeasonId == seasonId && episode.VideoFiles.Count > 0)
            .OrderBy(episode => episode.EpisodeNumber)
            .ToListAsync(ct);
    }
}
