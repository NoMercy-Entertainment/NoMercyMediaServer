using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Client;

public interface ITvShowMetadataProvider
{
    Task<TmdbTvShowAppends?> GetTvShowAsync(
        int id,
        string language,
        CancellationToken ct = default
    );
    Task<TmdbTvShowDetails?> GetTvShowDetailsAsync(int id, CancellationToken ct = default);
}
