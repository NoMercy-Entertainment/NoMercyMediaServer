using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.MediaProcessing.Collections;

public interface ICollectionRepository
{
    public Task Store(Collection collection);
    public Task LinkToLibrary(Library library, Collection collection);
    public Task LinkToMovies(TmdbCollectionAppends collection);
    public Task StoreAlternativeTitles(IEnumerable<AlternativeTitle> alternativeTitles);
    public Task StoreTranslations(IEnumerable<Translation> translations);
    public Task StoreImages(IEnumerable<Image> images);
}