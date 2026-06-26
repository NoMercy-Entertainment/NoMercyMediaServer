using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.Providers.TMDB.Client;

public interface IMovieMetadataProvider
{
    Task<TmdbMovieAppends?> GetMovieAsync(int id, string language, CancellationToken ct = default);
}
