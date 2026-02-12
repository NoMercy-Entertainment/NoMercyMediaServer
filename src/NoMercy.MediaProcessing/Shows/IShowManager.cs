using NoMercy.Database.Models.Libraries;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Shows;

public interface IShowManager
{
    Task<TmdbTvShowAppends?> AddShowAsync(int id, Library library, bool? priority = false);
    Task UpdateShowAsync(int id, Library library);
    Task RemoveShowAsync(int id);
}