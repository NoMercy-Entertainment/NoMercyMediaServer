using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Shows;

public class ShowManager(
    IShowRepository showRepository,
    JobDispatcher jobDispatcher
) : BaseManager, IShowManager
{
    public async Task<TmdbTvShowAppends?> AddShowAsync(int id, Library library)
    {
        Logger.MovieDb($"Show {id}: Adding to Library {library.Title}");

        using TmdbTvClient showClient = new(id);
        TmdbTvShowAppends? showAppends = await showClient.WithAllAppends();

        if (showAppends == null) return null;

        string baseUrl = BaseUrl(showAppends.Name, showAppends.FirstAirDate);
        string mediaType = showRepository.GetMediaType(showAppends);

        string colorPalette = await MovieDbImageManager
            .MultiColorPalette([
                new("poster", showAppends.PosterPath),
                new("backdrop", showAppends.BackdropPath)
            ]);

        Tv show = new()
        {
            LibraryId = library.Id,
            Folder = baseUrl,
            MediaType = mediaType,
            _colorPalette = colorPalette,

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

        await Task.WhenAll(
            StoreTranslations(showAppends),
            StoreGenres(showAppends),
            StoreContentRatings(showAppends)
        );

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
        IEnumerable<AlternativeTitle> alternativeTitles = show.AlternativeTitles.Results.Select(
            tmdbShowAlternativeTitles => new AlternativeTitle
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
        IEnumerable<Translation> translations = show.Translations.Translations
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
            });

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

        IEnumerable<Similar> jobItems = similar.Select(x => new Similar { TvFromId = x.TvFromId });
        jobDispatcher.DispatchJob<SimilarPaletteJob, Similar>(show.Id, jobItems);

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

        IEnumerable<Recommendation> jobItems = recommendations
            .Select(x => new Recommendation { TvFromId = x.TvFromId });

        jobDispatcher.DispatchJob<RecommendationPaletteJob, Recommendation>(show.Id, jobItems);

        Logger.MovieDb($"Show {show.Name}: Recommendations stored", LogEventLevel.Debug);
    }

    internal async Task StoreVideos(TmdbTvShowAppends show)
    {
        IEnumerable<Media> videos = show.Videos.Results
            .Select(media => new Media
            {
                _type = "video",
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

        IEnumerable<Image> posterJobItems = posters
            .Select(x => new Image { FilePath = x.FilePath })
            .ToArray();
        if (posterJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(show.Id, posterJobItems);

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

        IEnumerable<Image> backdropJobItems = backdrops
            .Select(x => new Image { FilePath = x.FilePath })
            .ToArray();
        if (backdropJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(show.Id, backdropJobItems);

        IEnumerable<Image> logos = show.Images.Logos.Select(
                image => new Image
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

        IEnumerable<Image> logosJobItems = logos
            .Where(x => x.FilePath != null && !x.FilePath.EndsWith(".svg"))
            .Select(x => new Image { FilePath = x.FilePath })
            .ToArray();
        if (backdropJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(show.Id, logosJobItems);
    }

    internal async Task StoreKeywords(TmdbTvShowAppends show)
    {
        IEnumerable<Keyword> keywords = show.Keywords.Results.Select(
            keyword => new Keyword
            {
                Id = keyword.Id,
                Name = keyword.Name
            });

        await showRepository.StoreKeywords(keywords);
        Logger.MovieDb($"Show {show.Name}: Keywords stored", LogEventLevel.Debug);

        IEnumerable<KeywordTv> keywordTvs = show.Keywords.Results.Select(
            keyword => new KeywordTv
            {
                KeywordId = keyword.Id,
                TvId = show.Id
            });

        await showRepository.LinkKeywordsToTv(keywordTvs);
        Logger.MovieDb($"Show {show.Name}: Keywords linked to Show", LogEventLevel.Debug);
    }

    internal async Task StoreGenres(TmdbTvShowAppends show)
    {
        IEnumerable<GenreTv> genreShows = show.Genres.Select(
            genre => new GenreTv
            {
                GenreId = genre.Id,
                TvId = show.Id
            });

        await showRepository.StoreGenres(genreShows);
        Logger.MovieDb($"Show {show.Name}: Genres stored", LogEventLevel.Debug);
    }

    internal async Task StoreWatchProviders(TmdbTvShowAppends show)
    {
        Logger.MovieDb($"Show {show.Name}: WatchProviders stored", LogEventLevel.Debug);
        await Task.CompletedTask;
    }

    internal async Task StoreNetworks(TmdbTvShowAppends show)
    {
        // List<Network> networks = show.Networks.Results.ToList()
        //     .ConvertAll<Network>(x => new Network(x));
        //
        // await showRepository.StoreNetworks(networks)

        Logger.MovieDb($"Show {show.Name}: Networks stored", LogEventLevel.Debug);
        await Task.CompletedTask;
    }

    internal async Task StoreCompanies(TmdbTvShowAppends show)
    {
        // List<Company> companies = show.ProductionCompanies.Results.ToList()
        //     .ConvertAll<ProductionCompany>(x => new ProductionCompany(x));
        //
        // await showRepository.StoreCompanies(companies)

        Logger.MovieDb($"Show {show.Name}: Companies stored", LogEventLevel.Debug);
        await Task.CompletedTask;
    }

    internal async Task StoreCast(TmdbTvShowAppends show)
    {
        await Task.CompletedTask;
    }
    
    internal async Task StoreCrew(TmdbTvShowAppends show)
    {
        await Task.CompletedTask;
    }
}