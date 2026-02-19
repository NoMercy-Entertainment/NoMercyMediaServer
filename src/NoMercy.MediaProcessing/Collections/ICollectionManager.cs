using NoMercy.Database.Models.Libraries;
using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.MediaProcessing.Collections;

public interface ICollectionManager
{
    Task<TmdbCollectionAppends?> Add(int id, Library library);
    Task UpdateCollectionAsync(int id, Library library);
    Task RemoveCollectionAsync(int id, Library library);
}