using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.Providers.TMDB.Client;

public interface ICollectionMetadataProvider
{
    Task<TmdbCollectionAppends?> GetCollectionAsync(
        int id,
        string language,
        CancellationToken ct = default
    );
}
