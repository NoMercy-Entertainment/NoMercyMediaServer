using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.MediaProcessing.Movies;

public interface IMovieRepository
{
    Task Add(Movie movie);
    Task LinkToLibrary(Library library, Movie movie);
    Task StoreAlternativeTitles(IEnumerable<AlternativeTitle> alternativeTitles);
    Task StoreTranslations(IEnumerable<Translation> translations);
    Task StoreContentRatings(IEnumerable<CertificationMovie> certifications);
    Task StoreSimilar(IEnumerable<Similar> similar);
    Task StoreRecommendations(IEnumerable<Recommendation> recommendations);
    Task StoreVideos(IEnumerable<Media> videos);
    Task StoreImages(IEnumerable<Image> images);
    Task StoreKeywords(IEnumerable<Keyword> keywords);
    Task LinkKeywordsToMovie(IEnumerable<KeywordMovie> keywordMovies);
    Task StoreGenres(IEnumerable<GenreMovie> genreMovies);

    Task StoreWatchProviders(List<WatchProvider> watchProviders);
    Task StoreCompanies(List<Company> companies);

    IEnumerable<CertificationMovie> GetCertificationMovies(TmdbMovieAppends movie,
        IEnumerable<CertificationCriteria> certificationCriteria);

    Task StoreWatchProviderMedias(List<WatchProviderMedia> watchProviderMedias);
    Task StoreCompanyMovies(List<CompanyMovie> companyMovies);
}