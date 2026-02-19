using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.Providers.TMDB.Client;

public interface ITmdbMovieClient
{
    Task<TmdbMovieAppends?> WithAllAppends(bool? append);
}