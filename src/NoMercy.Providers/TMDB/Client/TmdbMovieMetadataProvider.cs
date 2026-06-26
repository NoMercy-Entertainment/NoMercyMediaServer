using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbMovieMetadataProvider : IMovieMetadataProvider
{
    public async Task<TmdbMovieAppends?> GetMovieAsync(int id, string language, CancellationToken ct = default)
    {
        using TmdbMovieClient tmdbMovieClient = new(id, language: language);
        return await tmdbMovieClient.WithAllAppends(true);
    }
}
