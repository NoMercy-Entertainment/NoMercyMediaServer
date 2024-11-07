using NoMercy.Database.Models;
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