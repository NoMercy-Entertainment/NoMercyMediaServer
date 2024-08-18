using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public interface IMovieRepository
{
    Task<Movie?> GetMovieAsync(Guid userId, int id, string language);
    Task<bool> GetMovieAvailableAsync(Guid userId, int id);
    IEnumerable<Movie> GetMoviePlaylistAsync(Guid userId, int id, string language);
    Task<bool> LikeMovieAsync(int id, Guid userId, bool like);
}