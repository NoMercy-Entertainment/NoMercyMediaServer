using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbTvShowMetadataProvider : ITvShowMetadataProvider
{
    public async Task<TmdbTvShowAppends?> GetTvShowAsync(
        int id,
        string language,
        CancellationToken ct = default
    )
    {
        using TmdbTvClient tmdbTvClient = new(id, language: language);
        return await tmdbTvClient.WithAllAppends(true);
    }

    public async Task<TmdbTvShowDetails?> GetTvShowDetailsAsync(
        int id,
        CancellationToken ct = default
    )
    {
        using TmdbTvClient tmdbTvClient = new(id);
        return await tmdbTvClient.Details(true);
    }
}
