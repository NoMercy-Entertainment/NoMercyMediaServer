using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Shows;

public class ShowManager(
    IShowRepository showRepository,
    JobDispatcher jobDispatcher
) : BaseManager, IShowManager
{
    public async Task<TmdbTvShowAppends?> AddShowAsync(int id, Library library, bool? priority = false)
    {
        Logger.MovieDb($"Show {id}: Adding to Library {library.Title}");

        using TmdbTvClient showClient = new(id);
        TmdbTvShowAppends? showAppends = await showClient.WithAllAppends(priority);

        if (showAppends == null) return null;

        string baseUrl = BaseUrl(showAppends.Name, showAppends.FirstAirDate);
        string mediaType = showRepository.GetMediaType(showAppends);

        DateTime folderCreatedAt = DateTime.UtcNow;

        foreach (FolderLibrary folderLibrary in library.FolderLibraries)
        {
            string folderName = Path.Combine(folderLibrary.Folder.Path, baseUrl.Replace("/", ""));
            if(!Directory.Exists(folderName)) continue;
            
            DirectoryInfo folderInfo = new(folderName);
            folderCreatedAt = folderInfo.CreationTimeUtc;
            
            if(folderCreatedAt != DateTime.UtcNow) break;
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
            TitleSort = showAppends.Name.TitleSort(showAppends.FirstAirDate),
            TvdbId = showAppends.ExternalIds.TvdbId,
            Type = showAppends.Type,
            VoteAverage = showAppends.VoteAverage,
            VoteCount = showAppends.VoteCount,
            
            CreatedAt = folderCreatedAt,

            Duration = showAppends.EpisodeRunTime?.Length > 0
                ? (int?)showAppends.EpisodeRunTime?.Average()
                : 0,
            OriginCountry = showAppends.OriginCountry.Length > 0
                ? showAppends.OriginCountry[0]
                : null,
            SpokenLanguages = showAppends.SpokenLanguages.Length > 0
                ? showAppends.SpokenLanguages[0].Name
                : null,
            Trailer = showAppends.Videos.Results.Length > 0
                ? showAppends.Videos.Results[0].Key
                : null
        };

        await showRepository.AddAsync(show);
        Logger.MovieDb($"Show {show.Title}: Added to Database", LogEventLevel.Debug);

        await showRepository.LinkToLibrary(library, show);
        Logger.MovieDb($"Show {show.Title}: Linked to Library {library.Title}", LogEventLevel.Debug);
        
        ShowManager showManager = new(showRepository, jobDispatcher);
        await showManager.StoreGenres(showAppends);
        await showManager.StoreContentRatings(showAppends);
        await showManager.StoreTranslations(showAppends);

        Logger.MovieDb($"Show {showAppends.Name}: Added to Library {library.Title}");

        jobDispatcher.DispatchJob<AddShowExtraDataJob, TmdbTvShowAppends>(showAppends);

        return showAppends;
    }

    public Task UpdateShowAsync(int id, Library library)
    {
        throw new NotImplementedException();
    }

    public Task RemoveShowAsync(int id)
    {
        throw new NotImplementedException();
    }

    internal async Task StoreAlternativeTitles(TmdbTvShowAppends show)
    {
        IEnumerable<AlternativeTitle> alternativeTitles =
            show.AlternativeTitles.Results.Select(tmdbShowAlternativeTitles => new AlternativeTitle
            {
                Iso31661 = tmdbShowAlternativeTitles.Iso31661,
                Title = tmdbShowAlternativeTitles.Title,
                TvId = show.Id
            });

        await showRepository.StoreAlternativeTitles(alternativeTitles);

        Logger.MovieDb($"Show {show.Name}: AlternativeTitles stored", LogEventLevel.Debug);
    }

    internal async Task StoreTranslations(TmdbTvShowAppends show)
    {
        List<Translation> translations = show.Translations.Translations
            .Select(translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                Title = translation.Data.Title == "" ? null : translation.Data.Title,
                Overview = translation.Data.Overview == "" ? null : translation.Data.Overview,
                EnglishName = translation.EnglishName,
                Homepage = translation.Data.Homepage?.ToString(),
                Biography = translation.Data.Biography,
                TvId = show.Id
            }).ToList();
        
        await showRepository.StoreTranslations(translations);

        Logger.MovieDb($"Show {show.Name}: Translations stored", LogEventLevel.Debug);
    }

    internal async Task StoreContentRatings(TmdbTvShowAppends show)
    {
        List<CertificationCriteria> certificationCriteria = show.ContentRatings.Results
            .Select(r => new CertificationCriteria
            {
                Iso31661 = r.Iso31661,
                Certification = r.Rating
            }).ToList();

        IEnumerable<CertificationTv> certificationTvs = showRepository
            .GetCertificationTvs(show, certificationCriteria);

        await showRepository.StoreContentRatings(certificationTvs);

        Logger.MovieDb($"Show {show.Name}: Content Ratings stored", LogEventLevel.Debug);
    }

    internal async Task StoreSimilar(TmdbTvShowAppends show)
    {
        IEnumerable<Similar> similar = show.Similar.Results
            .Select(similar => new Similar
            {
                Backdrop = similar.BackdropPath,
                Overview = similar.Overview,
                Poster = similar.PosterPath,
                Title = similar.Name,
                TitleSort = similar.Name,
                MediaId = similar.Id,
                TvFromId = show.Id
            })
            .ToArray();

        await showRepository.StoreSimilar(similar);

        Logger.MovieDb($"Show {show.Name}: Similar stored", LogEventLevel.Debug);
    }

    internal async Task StoreRecommendations(TmdbTvShowAppends show)
    {
        IEnumerable<Recommendation> recommendations = show.Recommendations.Results
            .Select(recommendation => new Recommendation
            {
                Backdrop = recommendation.BackdropPath,
                Overview = recommendation.Overview,
                Poster = recommendation.PosterPath,
                Title = recommendation.Name,
                TitleSort = recommendation.Name.TitleSort(),
                MediaId = recommendation.Id,
                TvFromId = show.Id
            })
            .ToArray();

        await showRepository.StoreRecommendations(recommendations);

        Logger.MovieDb($"Show {show.Name}: Recommendations stored", LogEventLevel.Debug);
    }

    internal async Task StoreVideos(TmdbTvShowAppends show)
    {
        IEnumerable<Media> videos = show.Videos.Results
            .Select(media => new Media
            {
                Id = Ulid.NewUlid(),
                Iso6391 = media.Iso6391,
                Name = media.Name,
                Site = media.Site,
                Size = media.Size,
                Src = media.Key,
                Type = media.Type,
                TvId = show.Id
            });

        await showRepository.StoreVideos(videos);

        Logger.MovieDb($"Show {show.Name}: Videos stored", LogEventLevel.Debug);
    }

    internal async Task StoreImages(TmdbTvShowAppends show)
    {
        IEnumerable<Image> posters = show.Images.Posters
            .Select(image => new Image
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
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToArray();

        await showRepository.StoreImages(posters);

        IEnumerable<Image> backdrops = show.Images.Backdrops
            .Select(image => new Image
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
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToArray();

        await showRepository.StoreImages(backdrops);
        Logger.MovieDb($"Show {show.Name}: backdrops stored", LogEventLevel.Debug);

        IEnumerable<Image> logos = show.Images.Logos.Select(image => new Image
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
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToArray();

        await showRepository.StoreImages(logos);
        Logger.MovieDb($"Show {show.Name}: Logos stored", LogEventLevel.Debug);
   }

    internal async Task StoreKeywords(TmdbTvShowAppends show)
    {
        IEnumerable<Keyword> keywords = show.Keywords.Results.Select(keyword => new Keyword
        {
            Id = keyword.Id,
            Name = keyword.Name
        });

        await showRepository.StoreKeywords(keywords);
        Logger.MovieDb($"Show {show.Name}: Keywords stored", LogEventLevel.Debug);

        IEnumerable<KeywordTv> keywordTvs = show.Keywords.Results.Select(keyword => new KeywordTv
        {
            KeywordId = keyword.Id,
            TvId = show.Id
        });

        await showRepository.LinkKeywordsToTv(keywordTvs);
        Logger.MovieDb($"Show {show.Name}: Keywords linked to Show", LogEventLevel.Debug);
    }

    internal async Task StoreGenres(TmdbTvShowAppends show)
    {
        IEnumerable<GenreTv> genreShows = show.Genres.Select(genre => new GenreTv
        {
            GenreId = genre.Id,
            TvId = show.Id
        });

        await showRepository.StoreGenres(genreShows);
        Logger.MovieDb($"Show {show.Name}: Genres stored", LogEventLevel.Debug);
    }

    internal async Task StoreWatchProviders(TmdbTvShowAppends show)
    {
        List<WatchProvider> watchProviders = [];
        List<WatchProviderMedia> watchProviderMedias = [];

        foreach ((string countryCode, string providerType, TmdbPaymentDetails provider, string? link) in TmdbWatchProviders.ExtractProviders(show.WatchProviders.TmdbWatchProviderResults))
        {
            if (watchProviders.All(wp => wp.Id != provider.ProviderId))
            {
                watchProviders.Add(new()
                {
                    Id = provider.ProviderId,
                    Name = provider.ProviderName,
                    Logo = provider.LogoPath,
                    DisplayPriority = provider.DisplayPriority
                });
            }

            watchProviderMedias.Add(new()
            {
                WatchProviderId = provider.ProviderId,
                TvId = show.Id,
                CountryCode = countryCode,
                ProviderType = providerType,
                Link = link
            });
        }

        if (watchProviders.Count != 0)
            await showRepository.StoreWatchProviders(watchProviders);

        if (watchProviderMedias.Count != 0)
            await showRepository.StoreWatchProviderMedias(watchProviderMedias);

        Logger.MovieDb($"Show {show.Name}: WatchProviders stored", LogEventLevel.Debug);
    }

    internal async Task StoreNetworks(TmdbTvShowAppends show)
    {
        if (show.Networks.Length == 0) 
        {
            Logger.MovieDb($"Show {show.Name}: No networks found", LogEventLevel.Debug);
            return;
        }
        
        TmdbTvClient showClient = new(show.Id);

        List<Network> networks = [];

        foreach (TmdbNetwork network in show.Networks)   
        {
            TmdbTmdbNetworkDetails? nw = await showClient.NetworkDetails(network.Id);
            if (nw == null) continue;
            
            if (networks.All(n => n.Id != nw.Id))
            {
                networks.Add(new()
                {
                    Id = nw.Id,
                    Name = nw.Name,
                    Logo = nw.LogoPath,
                    OriginCountry = nw.OriginCountry,
                    Headquarters = nw.Headquarters,
                    Homepage = nw.Homepage,
                });
            }
        }

        List<NetworkTv> networkTvs = show.Networks
            .Select(network => new NetworkTv
            {
                NetworkId = network.Id,
                TvId = show.Id
            }).ToList();

        if (networks.Count != 0)
            await showRepository.StoreNetworks(networks);

        if (networkTvs.Count != 0)
            await showRepository.StoreNetworkTvs(networkTvs);

        Logger.MovieDb($"Show {show.Name}: Networks stored", LogEventLevel.Debug);
    }

    internal async Task StoreCompanies(TmdbTvShowAppends show)
    {
        if (show.ProductionCompanies.Length == 0)
        {
            Logger.MovieDb($"Show {show.Name}: No production companies found", LogEventLevel.Debug);
            return;
        }
        
        TmdbTvClient showClient = new(show.Id);

        List<Company> companies = [];

        await Parallel.ForEachAsync(show.ProductionCompanies, Config.ParallelOptions, async (productionCompany, _) =>
        {
            TmdbTmdbNetworkDetails? nw = await showClient.CompanyDetails(productionCompany.Id);
            if (nw == null) return;

            if (companies.All(n => n.Id != nw.Id))
            {
                companies.Add(new()
                {
                    Id = nw.Id,
                    Name = nw.Name,
                    Logo = nw.LogoPath,
                    OriginCountry = nw.OriginCountry,
                    Headquarters = nw.Headquarters,
                    Homepage = nw.Homepage,
                });
            }
        });

        List<CompanyTv> companyTvs = show.ProductionCompanies
            .Select(company => new CompanyTv
            {
                CompanyId = company.Id,
                TvId = show.Id
            }).ToList();

        if (companies.Count != 0)
            await showRepository.StoreCompanies(companies);

        if (companyTvs.Count != 0)
            await showRepository.StoreCompanyTvs(companyTvs);

        Logger.MovieDb($"Show {show.Name}: Companies stored", LogEventLevel.Debug);
    }
}