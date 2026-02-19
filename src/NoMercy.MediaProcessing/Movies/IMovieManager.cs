using NoMercy.Database.Models.Libraries;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.MediaProcessing.Movies;

public interface IMovieManager
{
    Task<TmdbMovieAppends?> Add(int id, Library library);
    Task Update(int id, Library library);
    Task Remove(int id, Library library);
}