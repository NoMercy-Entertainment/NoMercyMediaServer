// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Common;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Shows;

public interface IShowRepository
{
    Task AddAsync(Tv show);
    Task Remove(int id);
    Task LinkToLibrary(Library library, Tv show);
    Task<Library?> GetLibraryByTypeAsync(string type);
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

    Task StoreAnimeThemes(IEnumerable<AnimeThemeTv> animeThemeTvs);
    Task StoreAnimeDemographics(IEnumerable<AnimeDemographicTv> animeDemographicTvs);
    Task StoreAnimeSeason(int tvId, int year, string quarter);
    Task<int> ResolveAnimeThemeIdAsync(string name);
    Task<int> ResolveAnimeDemographicIdAsync(string name);

    Task StoreWatchProviders(List<WatchProvider> watchProviders);
    Task StoreNetworks(IEnumerable<Network> networks);
    Task StoreNetworkTvs(IEnumerable<NetworkTv> networkTvs);
    Task StoreCompanies(List<Company> companies);

    IEnumerable<CertificationTv> GetCertificationTvs(
        TmdbTvShowAppends show,
        IEnumerable<CertificationCriteria> certificationCriteria
    );

    Task StoreWatchProviderMedias(List<WatchProviderMedia> watchProviderMedias);
    Task StoreCompanyTvs(List<CompanyTv> companyTvs);
}
