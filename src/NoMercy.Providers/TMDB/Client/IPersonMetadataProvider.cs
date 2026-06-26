using NoMercy.Providers.TMDB.Models.People;

namespace NoMercy.Providers.TMDB.Client;

public interface IPersonMetadataProvider
{
    Task<TmdbPersonAppends?> GetPersonAsync(int id, CancellationToken ct = default);
}
