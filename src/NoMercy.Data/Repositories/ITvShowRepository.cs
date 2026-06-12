using NoMercy.Database.Models.TvShows;

namespace NoMercy.Data.Repositories;

public interface ITvShowRepository
{
    Task<TvDetail?> GetTvAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<Tv?> GetTvWithLibraryAsync(int id, CancellationToken ct = default);

    Task<bool> GetTvAvailableAsync(Guid userId, int id, CancellationToken ct = default);

    Task<Tv?> GetPlaylistAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<bool> LikeAsync(int id, Guid userId, bool like, CancellationToken ct = default);

    Task AddTvShowAsync(int id);

    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<IEnumerable<Episode>> GetMissingLibraryShows(
        Guid userId,
        int id,
        string language,
        CancellationToken ct = default
    );

    Task<bool> AddToWatchListAsync(
        int tvId,
        Guid userId,
        bool add = true,
        CancellationToken ct = default
    );
}

public record TvDetail(Tv Tv, Tv[] Similars, Tv[] Recommendations);
