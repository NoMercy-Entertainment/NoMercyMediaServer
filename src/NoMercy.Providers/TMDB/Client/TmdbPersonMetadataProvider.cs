using NoMercy.Providers.TMDB.Models.People;

namespace NoMercy.Providers.TMDB.Client;

public class TmdbPersonMetadataProvider : IPersonMetadataProvider
{
    public async Task<TmdbPersonAppends?> GetPersonAsync(int id, CancellationToken ct = default)
    {
        using TmdbPersonClient tmdbPersonClient = new(id);
        return await tmdbPersonClient.WithAllAppends(true);
    }
}
