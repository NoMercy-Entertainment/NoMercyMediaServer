using NoMercy.Database.Models.Movies;

namespace NoMercy.Data.Repositories;

public interface IMovieRepository
{
    Task<Movie?> GetMovieAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<Movie?> GetMovieDetailAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<bool> GetMovieAvailableAsync(Guid userId, int id, CancellationToken ct = default);

    Task<List<Movie>> GetMoviePlaylistAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<bool> LikeMovieAsync(int id, Guid userId, bool like, CancellationToken ct = default);

    Task AddMovieAsync(int id, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<bool> AddToWatchListAsync(
        int movieId,
        Guid userId,
        bool add = true,
        CancellationToken ct = default
    );

    Task<Movie?> GetMovieForRescanAsync(int id, CancellationToken ct = default);

    Task<Movie?> GetMovieForRefreshAsync(int id, CancellationToken ct = default);
}
