using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.Data.Repositories;

public interface IVideoFileRepository
{
    Task<VideoFile?> GetByIdAsync(Ulid id, CancellationToken ct = default);

    Task<bool> ExistsAsync(Ulid id, CancellationToken ct = default);

    Task<List<Episode>> GetEncodedEpisodesForSeasonAsync(
        int seasonId,
        CancellationToken ct = default
    );
}
