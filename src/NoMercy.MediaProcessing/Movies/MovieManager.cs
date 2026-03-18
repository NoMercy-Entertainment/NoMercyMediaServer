using System.Collections.Concurrent;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Networks;
using NoMercy.Providers.TMDB.Models.Shared;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Movies;

public class MovieManager(
    IMovieRepository movieRepository,
    JobDispatcher jobDispatcher
) : BaseManager, IMovieManager
{
    public async Task<TmdbMovieAppends?> Add(int id, Library library)
    {
        Logger.MovieDb($"Movie: {id}: Adding to Library {library.Title}");

        using TmdbMovieClient movieClient = new(id);
        TmdbMovieAppends? movieAppends = await movieClient.WithAllAppends();

        if (movieAppends == null) return null;

        string baseUrl = BaseUrl(movieAppends.Title, movieAppends.ReleaseDate);

        DateTime folderCreatedAt = DateTime.UtcNow;

        foreach (FolderLibrary folderLibrary in library.FolderLibraries)
        {
            string folderName = Path.Combine(folderLibrary.Folder.Path, baseUrl.Replace("/", ""));

            if (!Directory.Exists(folderName))
            {
                string? match = Str.FindMatchingDirectory(folderLibrary.Folder.Path, baseUrl.Replace("/", ""));
                if (match != null)
                    folderName = match;
            }

            if (!Directory.Exists(folderName)) continue;

            DirectoryInfo folderInfo = new(folderName);
            folderCreatedAt = folderInfo.CreationTimeUtc;

            if(folderCreatedAt != DateTime.UtcNow) break;
        }

        Movie movie = new()
        {
            LibraryId = library.Id,
            Folder = baseUrl,

            Id = movieAppends.Id,
            Title = movieAppends.Title,
            TitleSort = movieAppends.Title.TitleSort(movieAppends.ReleaseDate),
            Duration = movieAppends.Runtime,
            Adult = movieAppends.Adult,
            Backdrop = movieAppends.BackdropPath,
            Budget = movieAppends.Budget,
            Homepage = movieAppends.Homepage?.ToString(),
            ImdbId = movieAppends.ImdbId,
            OriginalTitle = movieAppends.OriginalTitle,
            OriginalLanguage = movieAppends.OriginalLanguage,
            Overview = movieAppends.Overview,
            Popularity = movieAppends.Popularity,
            Poster = movieAppends.PosterPath,
            ReleaseDate = movieAppends.ReleaseDate,
            Revenue = movieAppends.Revenue,
            Runtime = movieAppends.Runtime,
            Status = movieAppends.Status,
            Tagline = movieAppends.Tagline,
            Trailer = movieAppends.Video?.ToString(),
            Video = movieAppends.Video?.ToString(),
            VoteAverage = movieAppends.VoteAverage,
            VoteCount = movieAppends.VoteCount,
            
            CreatedAt = folderCreatedAt
        };

        await movieRepository.Add(movie);
        Logger.MovieDb($"Movie: {movie.Title}: Added to Database", LogEventLevel.Debug);

        await movieRepository.LinkToLibrary(library, movie);
        Logger.MovieDb($"Movie: {movie.Title}: Linked to Library {library.Title}", LogEventLevel.Debug);

        await Task.WhenAll(
            StoreTranslations(movieAppends),
            StoreGenres(movieAppends),
            StoreContentRatings(movieAppends)
        );

        Logger.MovieDb($"Movie: {movieAppends.Title}: Added to Library {library.Title}");

        jobDispatcher.DispatchJob<MovieExtrasJob, TmdbMovieAppends>(movieAppends);

        return movieAppends;
    }

    public Task Update(int id, Library library)
    {
        throw new NotImplementedException();
    }

    public Task Remove(int id, Library library)
    {
        throw new NotImplementedException();
    }

    public async Task StoreAlternativeTitles(TmdbMovieAppends movie)
    {
        IEnumerable<AlternativeTitle> alternativeTitles =
            movie.AlternativeTitles.Results.Select(tmdbMovieAlternativeTitles => new AlternativeTitle
            {
                Iso31661 = tmdbMovieAlternativeTitles.Iso31661,
                Title = tmdbMovieAlternativeTitles.Title,
                MovieId = movie.Id
            });

        await movieRepository.StoreAlternativeTitles(alternativeTitles);

        Logger.MovieDb($"Movie: {movie.Title}: AlternativeTitles stored", LogEventLevel.Debug);
    }

    public async Task StoreTranslations(TmdbMovieAppends movie)
    {
        IEnumerable<Translation> translations = movie.Translations.Translations
            .Select(translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                Title = translation.Data.Title == "" ? null : translation.Data.Title,
                Overview = translation.Data.Overview == "" ? null : translation.Data.Overview,
                EnglishName = translation.EnglishName,
                Homepage = translation.Data.Homepage?.ToString(),
                MovieId = movie.Id
            });

        await movieRepository.StoreTranslations(translations);

        Logger.MovieDb($"Movie: {movie.Title}: Translations stored", LogEventLevel.Debug);
    }

    public async Task StoreContentRatings(TmdbMovieAppends movie)
    {
        List<CertificationCriteria> certificationCriteria = movie.ReleaseDates.Results
            .Select(r => new CertificationCriteria
            {
                Iso31661 = r.Iso31661,
                Certification = r.ReleaseDates[0].Certification
            }).ToList();

        IEnumerable<CertificationMovie> certificationMovies = movieRepository
            .GetCertificationMovies(movie, certificationCriteria);

        await movieRepository.StoreContentRatings(certificationMovies);

        Logger.MovieDb($"Movie: {movie.Title}: Content Ratings stored", LogEventLevel.Debug);
    }

    public async Task StoreSimilar(TmdbMovieAppends movie)
    {
        IEnumerable<Similar> similar = movie.Similar.Results
            .Select(tmdbMovie => new Similar
            {
                Backdrop = tmdbMovie.BackdropPath,
                Overview = tmdbMovie.Overview,
                Poster = tmdbMovie.PosterPath,
                Title = tmdbMovie.Title,
                TitleSort = tmdbMovie.Title,
                MediaId = tmdbMovie.Id,
                MovieFromId = movie.Id
            })
            .ToArray();

        await movieRepository.StoreSimilar(similar);

        Logger.MovieDb($"Movie: {movie.Title}: Similar stored", LogEventLevel.Debug);
    }

    public async Task StoreRecommendations(TmdbMovieAppends movie)
    {
        IEnumerable<Recommendation> recommendations = movie.Recommendations.Results
            .Select(tmdbMovie => new Recommendation
            {
                Backdrop = tmdbMovie.BackdropPath,
                Overview = tmdbMovie.Overview,
                Poster = tmdbMovie.PosterPath,
                Title = tmdbMovie.Title,
                TitleSort = tmdbMovie.Title.TitleSort(),
                MediaId = tmdbMovie.Id,
                MovieFromId = movie.Id
            })
            .ToArray();

        await movieRepository.StoreRecommendations(recommendations);

        IEnumerable<Recommendation> jobItems = recommendations
            .Select(x => new Recommendation { MovieFromId = x.MovieFromId });
    }

    public async Task StoreVideos(TmdbMovieAppends movie)
    {
        IEnumerable<Media> videos = movie.Videos.Results
            .Select(media => new Media
            {
                Id = Ulid.NewUlid(),
                Iso6391 = media.Iso6391,
                Name = media.Name,
                Site = media.Site,
                Size = media.Size,
                Src = media.Key,
                Type = media.Type,
                MovieId = movie.Id
            });

        await movieRepository.StoreVideos(videos);
        Logger.MovieDb($"Movie: {movie.Title}: Videos stored", LogEventLevel.Debug);
    }

    public async Task StoreImages(TmdbMovieAppends movie)
    {
        IEnumerable<Image> posters = movie.Images.Posters
            .Select(image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                MovieId = movie.Id,
                Type = "poster",
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToArray();

        await movieRepository.StoreImages(posters);
        Logger.MovieDb($"Movie: {movie.Title}: Posters stored", LogEventLevel.Debug);

        IEnumerable<Image> backdrops = movie.Images.Backdrops
            .Select(image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                MovieId = movie.Id,
                Type = "backdrop",
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToArray();

        await movieRepository.StoreImages(backdrops);
        Logger.MovieDb($"Movie: {movie.Title}: backdrops stored", LogEventLevel.Debug);

        IEnumerable<Image> logos = movie.Images.Logos.Select(image => new Image
            {
                AspectRatio = image.AspectRatio,
                FilePath = image.FilePath,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                Width = image.Width,
                MovieId = movie.Id,
                Type = "logo",
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToArray();

        await movieRepository.StoreImages(logos);
        Logger.MovieDb($"Movie: {movie.Title}: Logos stored", LogEventLevel.Debug);
    }

    public async Task StoreKeywords(TmdbMovieAppends movie)
    {
        IEnumerable<Keyword> keywords = movie.Keywords.Results.Select(keyword => new Keyword
        {
            Id = keyword.Id,
            Name = keyword.Name
        });

        await movieRepository.StoreKeywords(keywords);
        Logger.MovieDb($"Movie: {movie.Title}: Keywords stored", LogEventLevel.Debug);

        IEnumerable<KeywordMovie> keywordMovies = movie.Keywords.Results.Select(keyword => new KeywordMovie
        {
            KeywordId = keyword.Id,
            MovieId = movie.Id
        });

        await movieRepository.LinkKeywordsToMovie(keywordMovies);
        Logger.MovieDb($"Movie: {movie.Title}: Keywords linked to Movie", LogEventLevel.Debug);
    }

    public async Task StoreGenres(TmdbMovieAppends movie)
    {
        IEnumerable<GenreMovie> genreMovies = movie.Genres.Select(genre => new GenreMovie
        {
            GenreId = genre.Id,
            MovieId = movie.Id
        });

        await movieRepository.StoreGenres(genreMovies);
        Logger.MovieDb($"Movie: {movie.Title}: Genres stored", LogEventLevel.Debug);
    }

    public async Task StoreWatchProviders(TmdbMovieAppends movie)
    {
        List<WatchProvider> watchProviders = [];
        List<WatchProviderMedia> watchProviderMedias = [];

        foreach ((string countryCode, string providerType, TmdbPaymentDetails provider, string? link) in TmdbWatchProviders.ExtractProviders(movie.WatchProviders.TmdbWatchProviderResults))
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
                MovieId = movie.Id,
                CountryCode = countryCode,
                ProviderType = providerType,
                Link = link
            });
        }

        if (watchProviders.Count != 0)
            await movieRepository.StoreWatchProviders(watchProviders);

        if (watchProviderMedias.Count != 0)
            await movieRepository.StoreWatchProviderMedias(watchProviderMedias);

        Logger.MovieDb($"Show {movie.Title}: WatchProviders stored", LogEventLevel.Debug);
    }

    public async Task StoreCompanies(TmdbMovieAppends movie)
    {
        if (movie.ProductionCompanies.Length == 0)
        {
            Logger.MovieDb($"Movie: {movie.Title}: No production companies found", LogEventLevel.Debug);
            return;
        }
        
        TmdbTvClient showClient = new(movie.Id);

        ConcurrentDictionary<int, Company> companiesDict = new();

        await Parallel.ForEachAsync(movie.ProductionCompanies, Config.ParallelOptions, async (productionCompany, _) =>
        {
            TmdbTmdbNetworkDetails? nw = await showClient.CompanyDetails(productionCompany.Id);
            if (nw == null) return;

            companiesDict.TryAdd(nw.Id, new()
            {
                Id = nw.Id,
                Name = nw.Name,
                Logo = nw.LogoPath,
                OriginCountry = nw.OriginCountry,
                Headquarters = nw.Headquarters,
                Homepage = nw.Homepage,
            });
        });

        List<Company> companies = companiesDict.Values.ToList();
        
        List<CompanyMovie> companyMovies = companies
            .Select(company => new CompanyMovie
            {
                CompanyId = company.Id,
                MovieId = movie.Id
            }).ToList();

        if (companies.Count != 0)
            await movieRepository.StoreCompanies(companies);

        if (companyMovies.Count != 0)
            await movieRepository.StoreCompanyMovies(companyMovies);

        Logger.MovieDb($"Movie: {movie.Title}: Companies stored", LogEventLevel.Debug);
    }
    
    
}