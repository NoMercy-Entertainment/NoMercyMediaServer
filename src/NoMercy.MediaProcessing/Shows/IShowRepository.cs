using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Shows;

public interface IShowRepository
{
    Task AddAsync(Tv show);
    Task LinkToLibrary(Library library, Tv show);
    Task StoreAlternativeTitles(IEnumerable<AlternativeTitle> alternativeTitles);
    Task StoreTranslations(IEnumerable<Translation> translations);
    Task StoreContentRatings(IEnumerable<CertificationTv> certifications);
    Task StoreSimilar(IEnumerable<Similar> similar);
    Task StoreRecommendations(IEnumerable<Recommendation> recommendations);
    Task StoreVideos(IEnumerable<Media> videos);
    Task StoreImages(IEnumerable<Image> images);
    Task StoreKeywords(IEnumerable<Keyword> keywords);
    Task LinkKeywordsToTv(IEnumerable<KeywordTv> keywordTvs);
    Task StoreGenres(IEnumerable<GenreTv> genreTvs);

    Task StoreWatchProviders(List<WatchProvider> watchProviders);
    Task StoreNetworks(IEnumerable<Network> networks);
    Task StoreNetworkTvs(IEnumerable<NetworkTv> networkTvs);
    Task StoreCompanies(List<Company> companies);

    IEnumerable<CertificationTv> GetCertificationTvs(TmdbTvShowAppends show,
        IEnumerable<CertificationCriteria> certificationCriteria);

    string GetMediaType(TmdbTvShowAppends show);
    Task StoreWatchProviderMedias(List<WatchProviderMedia> watchProviderMedias);
    Task StoreCompanyTvs(List<CompanyTv> companyTvs);
}