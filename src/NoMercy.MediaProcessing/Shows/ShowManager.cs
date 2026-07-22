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

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Shows;

public class ShowManager(
    IShowRepository showRepository,
    JobDispatcher jobDispatcher,
    IStorageFactory storageFactory,
    IMediaTypeClassifier mediaTypeClassifier,
    ILogger<ShowManager> logger
) : BaseManager, IShowManager
{
    public async Task<TmdbTvShowAppends?> AddShowAsync(
        int id,
        Library library,
        bool? priority = false
    )
    {
        logger.LogInformation(message: "Show {Id}: Adding to Library {Title}", args: [id, library.Title]);

        using TmdbTvClient showClient = new(id: id);
        TmdbTvShowAppends? showAppends = await MetadataRetry.FetchAsync(
            fetch: () => showClient.WithAllAppends(priority: priority),
            description: $"TMDB tv {id}"
        );

        if (showAppends == null)
            return null;

        string baseUrl = BaseUrl(title: showAppends.Name, releaseDate: showAppends.FirstAirDate);
        string mediaType = await mediaTypeClassifier.ClassifyAsync(show: showAppends);

        DateTime folderCreatedAt = DateTime.UtcNow;

        foreach (FolderLibrary folderLibrary in library.FolderLibraries ?? [])
        {
            if (storageFactory == null)
                continue;

            IStorage folderStorage = storageFactory.For(
                folderId: folderLibrary.Folder.Id,
                driverId: folderLibrary.Folder.DriverId,
                subPath: string.Empty
            );
            string folderRoot = FolderRootPath(storage: folderStorage, path: folderLibrary.Folder.Path);
            string folderName = folderStorage.CombinePath(parent: folderRoot, child: baseUrl.Replace(oldValue: "/", newValue: ""));

            if (!folderStorage.Exists(path: folderName))
            {
                string? match = FileNameSanitizer.FindMatchingDirectory(
                    driver: folderStorage.Driver,
                    rootPath: folderRoot,
                    expectedFolderName: baseUrl.Replace(oldValue: "/", newValue: "")
                );
                if (match != null)
                    folderName = match;
            }

            if (!folderStorage.Exists(path: folderName))
                continue;

            folderCreatedAt = folderStorage.Driver.GetCreationTimeUtc(path: folderName);

            if (folderCreatedAt != DateTime.UtcNow)
                break;
        }

        Tv show = new()
        {
            LibraryId = library.Id,
            Folder = baseUrl,
            MediaType = mediaType,

            Id = showAppends.Id,
            Backdrop = showAppends.BackdropPath,
            FirstAirDate = showAppends.FirstAirDate,
            HaveEpisodes = 0,
            Homepage = showAppends.Homepage?.ToString(),
            ImdbId = showAppends.ExternalIds.ImdbId,
            InProduction = showAppends.InProduction,
            LastEpisodeToAir = showAppends.LastEpisodeToAir?.Id,
            NextEpisodeToAir = showAppends.NextEpisodeToAir?.Id,
            NumberOfEpisodes = showAppends.NumberOfEpisodes,
            NumberOfSeasons = showAppends.NumberOfSeasons,
            OriginalLanguage = showAppends.OriginalLanguage,
            Overview = showAppends.Overview,
            Popularity = showAppends.Popularity,
            Poster = showAppends.PosterPath,
            Status = showAppends.Status,
            Tagline = showAppends.Tagline,
            Title = showAppends.Name,
            TitleSort = showAppends.Name?.TitleSort(date: showAppends.FirstAirDate) ?? string.Empty,
            TvdbId = showAppends.ExternalIds.TvdbId,
            Type = showAppends.Type,
            VoteAverage = showAppends.VoteAverage,
            VoteCount = showAppends.VoteCount,

            CreatedAt = folderCreatedAt,

            Duration =
                showAppends.EpisodeRunTime?.Length > 0
                    ? (int?)showAppends.EpisodeRunTime?.Average()
                    : 0,
            OriginCountry =
                showAppends.OriginCountry.Length > 0 ? showAppends.OriginCountry[0] : null,
            SpokenLanguages =
                showAppends.SpokenLanguages.Length > 0 ? showAppends.SpokenLanguages[0].Name : null,
            Trailer =
                showAppends.Videos.Results.Length > 0 ? showAppends.Videos.Results[0].Key : null,
        };

        await showRepository.AddAsync(show: show);
        logger.LogDebug(message: "Show {Title}: Added to Database", args: show.Title);

        await showRepository.LinkToLibrary(library: library, show: show);
        logger.LogDebug(message: "Show {Title}: Linked to Library {Title2}", args: [show.Title, library.Title]);

        await StoreGenres(show: showAppends);
        await StoreContentRatings(show: showAppends);
        await StoreTranslations(show: showAppends);

        logger.LogInformation(
            message: "Show {Name}: Added to Library {Title}", args: [showAppends.Name, library.Title]
        );

        jobDispatcher.DispatchColorPaletteJob(entityType: "tv", entityId: show.Id.ToString());
        jobDispatcher.DispatchJob<ShowExtrasJob, TmdbTvShowAppends>(data: showAppends);

        return showAppends;
    }

    public Task UpdateShowAsync(int id, Library library)
    {
        // Re-importing refreshes all metadata; every write is an idempotent
        // upsert, so re-running AddShowAsync updates existing rows in place.
        return AddShowAsync(id: id, library: library);
    }

    public async Task RemoveShowAsync(int id)
    {
        await showRepository.Remove(id: id);
        logger.LogDebug(message: "Show {Id}: Removed from Database", args: id);
    }

    internal async Task StoreAlternativeTitles(TmdbTvShowAppends show)
    {
        IEnumerable<AlternativeTitle> alternativeTitles = show.AlternativeTitles.Results.Select(
            selector: tmdbShowAlternativeTitles => new AlternativeTitle
            {
                Iso31661 = tmdbShowAlternativeTitles.Iso31661,
                Title = tmdbShowAlternativeTitles.Title,
                TvId = show.Id,
            }
        );

        await showRepository.StoreAlternativeTitles(alternativeTitles: alternativeTitles);

        logger.LogDebug(message: "Show {Name}: AlternativeTitles stored", args: show.Name);
    }

    internal async Task StoreTranslations(TmdbTvShowAppends show)
    {
        List<Translation> translations = show
            .Translations.Translations.Select(selector: translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                Title = translation.Data.Title == "" ? null : translation.Data.Title,
                Overview = translation.Data.Overview == "" ? null : translation.Data.Overview,
                EnglishName = translation.EnglishName,
                Homepage = translation.Data.Homepage?.ToString(),
                Biography = translation.Data.Biography,
                TvId = show.Id,
            })
            .ToList();

        await showRepository.StoreTranslations(translations: translations);

        logger.LogDebug(message: "Show {Name}: Translations stored", args: show.Name);
    }

    internal async Task StoreContentRatings(TmdbTvShowAppends show)
    {
        List<CertificationCriteria> certificationCriteria = show
            .ContentRatings.Results.Select(selector: r => new CertificationCriteria
            {
                Iso31661 = r.Iso31661,
                Certification = r.Rating,
            })
            .ToList();

        IEnumerable<CertificationTv> certificationTvs = showRepository.GetCertificationTvs(
            show: show,
            certificationCriteria: certificationCriteria
        );

        await showRepository.StoreContentRatings(certifications: certificationTvs);

        logger.LogDebug(message: "Show {Name}: Content Ratings stored", args: show.Name);
    }

    internal async Task StoreSimilar(TmdbTvShowAppends show)
    {
        IEnumerable<Similar> similar = show
            .Similar.Results.Select(selector: similar => new Similar
            {
                Backdrop = similar.BackdropPath,
                Overview = similar.Overview,
                Poster = similar.PosterPath,
                Title = similar.Name,
                TitleSort = similar.Name,
                MediaId = similar.Id,
                TvFromId = show.Id,
            })
            .ToArray();

        await showRepository.StoreSimilar(similar: similar);

        logger.LogDebug(message: "Show {Name}: Similar stored", args: show.Name);

        await using MediaContext db = new();
        List<int> similarIds = await db
            .Similar.AsNoTracking()
            .Where(predicate: s => s.TvFromId == show.Id && (s._colorPalette == null || s._colorPalette == ""))
            .Select(selector: s => s.Id)
            .ToListAsync();

        foreach (int id in similarIds)
            jobDispatcher.DispatchColorPaletteJob(entityType: "similar", entityId: id.ToString());
    }

    internal async Task StoreRecommendations(TmdbTvShowAppends show)
    {
        IEnumerable<Recommendation> recommendations = show
            .Recommendations.Results.Select(selector: recommendation => new Recommendation
            {
                Backdrop = recommendation.BackdropPath,
                Overview = recommendation.Overview,
                Poster = recommendation.PosterPath,
                Title = recommendation.Name,
                TitleSort = recommendation.Name.TitleSort(),
                MediaId = recommendation.Id,
                TvFromId = show.Id,
            })
            .ToArray();

        await showRepository.StoreRecommendations(recommendations: recommendations);

        logger.LogDebug(message: "Show {Name}: Recommendations stored", args: show.Name);

        await using MediaContext db = new();
        List<int> recommendationIds = await db
            .Recommendations.AsNoTracking()
            .Where(predicate: r => r.TvFromId == show.Id && (r._colorPalette == null || r._colorPalette == ""))
            .Select(selector: r => r.Id)
            .ToListAsync();

        foreach (int id in recommendationIds)
            jobDispatcher.DispatchColorPaletteJob(entityType: "recommendation", entityId: id.ToString());
    }

    internal async Task StoreVideos(TmdbTvShowAppends show)
    {
        IEnumerable<Media> videos = show.Videos.Results.Select(selector: media => new Media
        {
            Id = Ulid.NewUlid(),
            Iso6391 = media.Iso6391,
            Name = media.Name,
            Site = media.Site,
            Size = media.Size,
            Src = media.Key,
            Type = media.Type,
            TvId = show.Id,
        });

        await showRepository.StoreVideos(videos: videos);

        logger.LogDebug(message: "Show {Name}: Videos stored", args: show.Name);
    }

    internal async Task StoreImages(TmdbTvShowAppends show)
    {
        IEnumerable<Image> posters = show
            .Images.Posters.Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                FilePath = image.FilePath,
                Width = image.Width,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                TvId = show.Id,
                Type = "poster",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToArray();

        await showRepository.StoreImages(images: posters);

        IEnumerable<Image> backdrops = show
            .Images.Backdrops.Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                FilePath = image.FilePath,
                Width = image.Width,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                TvId = show.Id,
                Type = "backdrop",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToArray();

        await showRepository.StoreImages(images: backdrops);
        logger.LogDebug(message: "Show {Name}: backdrops stored", args: show.Name);

        IEnumerable<Image> logos = show
            .Images.Logos.Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                FilePath = image.FilePath,
                Width = image.Width,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                TvId = show.Id,
                Type = "logo",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToArray();

        await showRepository.StoreImages(images: logos);
        logger.LogDebug(message: "Show {Name}: Logos stored", args: show.Name);

        await using MediaContext db = new();
        List<int> imageIds = await db
            .Images.AsNoTracking()
            .Where(predicate: i => i.TvId == show.Id && (i._colorPalette == null || i._colorPalette == ""))
            .Select(selector: i => i.Id)
            .ToListAsync();

        foreach (int id in imageIds)
            jobDispatcher.DispatchColorPaletteJob(entityType: "image", entityId: id.ToString());
    }

    internal async Task StoreKeywords(TmdbTvShowAppends show)
    {
        IEnumerable<Keyword> keywords = show.Keywords.Results.Select(selector: keyword => new Keyword
        {
            Id = keyword.Id,
            Name = keyword.Name,
        });

        await showRepository.StoreKeywords(keywords: keywords);
        logger.LogDebug(message: "Show {Name}: Keywords stored", args: show.Name);

        IEnumerable<KeywordTv> keywordTvs = show.Keywords.Results.Select(selector: keyword => new KeywordTv
        {
            KeywordId = keyword.Id,
            TvId = show.Id,
        });

        await showRepository.LinkKeywordsToTv(keywordTvs: keywordTvs);
        logger.LogDebug(message: "Show {Name}: Keywords linked to Show", args: show.Name);
    }

    internal async Task StoreGenres(TmdbTvShowAppends show)
    {
        IEnumerable<GenreTv> genreShows = show.Genres.Select(selector: genre => new GenreTv
        {
            GenreId = genre.Id,
            TvId = show.Id,
        });

        await showRepository.StoreGenres(genreTvs: genreShows);
        logger.LogDebug(message: "Show {Name}: Genres stored", args: show.Name);
    }

    internal async Task StoreWatchProviders(TmdbTvShowAppends show)
    {
        List<WatchProvider> watchProviders = [];
        List<WatchProviderMedia> watchProviderMedias = [];

        foreach (
            (
                string countryCode,
                string providerType,
                TmdbPaymentDetails provider,
                string? link
            ) in TmdbWatchProviders.ExtractProviders(results: show.WatchProviders.TmdbWatchProviderResults)
        )
        {
            if (watchProviders.All(predicate: wp => wp.Id != provider.ProviderId))
            {
                watchProviders.Add(
                    item: new()
                    {
                        Id = provider.ProviderId,
                        Name = provider.ProviderName,
                        Logo = provider.LogoPath,
                        DisplayPriority = provider.DisplayPriority,
                    }
                );
            }

            watchProviderMedias.Add(
                item: new()
                {
                    WatchProviderId = provider.ProviderId,
                    TvId = show.Id,
                    CountryCode = countryCode,
                    ProviderType = providerType,
                    Link = link,
                }
            );
        }

        if (watchProviders.Count != 0)
            await showRepository.StoreWatchProviders(watchProviders: watchProviders);

        if (watchProviderMedias.Count != 0)
            await showRepository.StoreWatchProviderMedias(watchProviderMedias: watchProviderMedias);

        logger.LogDebug(message: "Show {Name}: WatchProviders stored", args: show.Name);
    }

    internal async Task StoreNetworks(TmdbTvShowAppends show)
    {
        if (show.Networks.Length == 0)
        {
            logger.LogDebug(message: "Show {Name}: No networks found", args: show.Name);
            return;
        }

        TmdbTvClient showClient = new(id: show.Id);

        List<Network> networks = [];

        foreach (TmdbNetwork network in show.Networks)
        {
            TmdbTmdbNetworkDetails? nw = await showClient.NetworkDetails(id: network.Id);
            if (nw == null)
                continue;

            if (networks.All(predicate: n => n.Id != nw.Id))
            {
                networks.Add(
                    item: new()
                    {
                        Id = nw.Id,
                        Name = nw.Name,
                        Logo = nw.LogoPath,
                        OriginCountry = nw.OriginCountry,
                        Headquarters = nw.Headquarters,
                        Homepage = nw.Homepage,
                    }
                );
            }
        }

        List<NetworkTv> networkTvs = show
            .Networks.Select(selector: network => new NetworkTv { NetworkId = network.Id, TvId = show.Id })
            .ToList();

        if (networks.Count != 0)
            await showRepository.StoreNetworks(networks: networks);

        if (networkTvs.Count != 0)
            await showRepository.StoreNetworkTvs(networkTvs: networkTvs);

        logger.LogDebug(message: "Show {Name}: Networks stored", args: show.Name);
    }

    internal async Task StoreCompanies(TmdbTvShowAppends show)
    {
        if (show.ProductionCompanies.Length == 0)
        {
            logger.LogDebug(message: "Show {Name}: No production companies found", args: show.Name);
            return;
        }

        TmdbTvClient showClient = new(id: show.Id);

        ConcurrentDictionary<int, Company> companiesDict = new();

        await Parallel.ForEachAsync(
            source: show.ProductionCompanies,
            parallelOptions: SystemParallelism.Options,
            body: async (productionCompany, _) =>
            {
                TmdbTmdbNetworkDetails? nw = await showClient.CompanyDetails(id: productionCompany.Id);
                if (nw == null)
                    return;

                companiesDict.TryAdd(
                    key: nw.Id,
                    value: new()
                    {
                        Id = nw.Id,
                        Name = nw.Name,
                        Logo = nw.LogoPath,
                        OriginCountry = nw.OriginCountry,
                        Headquarters = nw.Headquarters,
                        Homepage = nw.Homepage,
                    }
                );
            }
        );

        List<Company> companies = companiesDict.Values.ToList();

        List<CompanyTv> companyTvs = companies
            .Select(selector: company => new CompanyTv { CompanyId = company.Id, TvId = show.Id })
            .ToList();

        if (companies.Count != 0)
            await showRepository.StoreCompanies(companies: companies);

        if (companyTvs.Count != 0)
            await showRepository.StoreCompanyTvs(companyTvs: companyTvs);

        logger.LogDebug(message: "Show {Name}: Companies stored", args: show.Name);
    }
}
