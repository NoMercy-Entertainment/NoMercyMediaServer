using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbCollectionMetadataProvider : ICollectionMetadataProvider
{
    public async Task<TmdbCollectionAppends?> GetCollectionAsync(
        int id,
        string language,
        CancellationToken ct = default
    )
    {
        using TmdbCollectionClient tmdbCollectionClient = new(id, language: language);
        return await tmdbCollectionClient.WithAllAppends(true);
    }
}
