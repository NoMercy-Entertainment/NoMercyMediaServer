using NoMercy.Database.Models;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Shows;

public interface IShowManager
{
    Task<TmdbTvShowAppends?> AddShowAsync(int id, Library library);
    Task UpdateShowAsync(int id, Library library);
    Task RemoveShowAsync(int id);
}