using System.Globalization;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
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

        string colorPalette = await MovieDbImageManager
            .MultiColorPalette([
                new("poster", movieAppends.PosterPath),
                new("backdrop", movieAppends.BackdropPath)
            ]);

        Movie movie = new()
        {
            LibraryId = library.Id,
            Folder = baseUrl,
            _colorPalette = colorPalette,

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
            Trailer = movieAppends.Video,
            Video = movieAppends.Video,
            VoteAverage = movieAppends.VoteAverage,
            VoteCount = movieAppends.VoteCount
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

        jobDispatcher.DispatchJob<AddMovieExtraDataJob, TmdbMovieAppends>(movieAppends);

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
        IEnumerable<AlternativeTitle> alternativeTitles = movie.AlternativeTitles.Results.Select(
            tmdbMovieAlternativeTitles => new AlternativeTitle
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

        IEnumerable<Similar> jobItems = similar.Select(x => new Similar { MovieFromId = x.MovieFromId });
        jobDispatcher.DispatchJob<SimilarPaletteJob, Similar>(movie.Id, jobItems);

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

        jobDispatcher.DispatchJob<RecommendationPaletteJob, Recommendation>(movie.Id, jobItems);
        Logger.MovieDb($"Movie: {movie.Title}: Recommendations stored", LogEventLevel.Debug);
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

        IEnumerable<Image> posterJobItems = posters
            .Select(x => new Image { FilePath = x.FilePath })
            .Where(e => e.Iso6391 == null || e.Iso6391 == "en" || e.Iso6391 == "" ||
                        e.Iso6391 == CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
            .ToArray();
        if (posterJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(movie.Id, posterJobItems);

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

        IEnumerable<Image> backdropJobItems = backdrops
            .Select(x => new Image { FilePath = x.FilePath })
            .Where(e => e.Iso6391 == null || e.Iso6391 == "en" || e.Iso6391 == "" ||
                        e.Iso6391 == CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
            .ToArray();
        if (backdropJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(movie.Id, backdropJobItems);

        IEnumerable<Image> logos = movie.Images.Logos.Select(
                image => new Image
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

        IEnumerable<Image> logosJobItems = logos
            .Where(x => !x.FilePath.EndsWith(".svg"))
            .Select(x => new Image { FilePath = x.FilePath })
            .Where(e => e.Iso6391 == null || e.Iso6391 == "en" || e.Iso6391 == "" ||
                        e.Iso6391 == CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
            .ToArray();
        if (logosJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(movie.Id, logosJobItems);
    }

    public async Task StoreKeywords(TmdbMovieAppends movie)
    {
        IEnumerable<Keyword> keywords = movie.Keywords.Results.Select(
            keyword => new Keyword
            {
                Id = keyword.Id,
                Name = keyword.Name
            });

        await movieRepository.StoreKeywords(keywords);
        Logger.MovieDb($"Movie: {movie.Title}: Keywords stored", LogEventLevel.Debug);

        IEnumerable<KeywordMovie> keywordMovies = movie.Keywords.Results.Select(
            keyword => new KeywordMovie
            {
                KeywordId = keyword.Id,
                MovieId = movie.Id
            });

        await movieRepository.LinkKeywordsToMovie(keywordMovies);
        Logger.MovieDb($"Movie: {movie.Title}: Keywords linked to Movie", LogEventLevel.Debug);
    }

    public async Task StoreGenres(TmdbMovieAppends movie)
    {
        IEnumerable<GenreMovie> genreMovies = movie.Genres.Select(
            genre => new GenreMovie
            {
                GenreId = genre.Id,
                MovieId = movie.Id
            });

        await movieRepository.StoreGenres(genreMovies);
        Logger.MovieDb($"Movie: {movie.Title}: Genres stored", LogEventLevel.Debug);
    }

    public async Task StoreWatchProviders(TmdbMovieAppends movie)
    {
        Logger.MovieDb($"Movie: {movie.Title}: WatchProviders stored", LogEventLevel.Debug);
        await Task.CompletedTask;
    }

    public async Task StoreNetworks(TmdbMovieAppends movie)
    {
        // List<Network> networks = movie.Networks.Results.ToList()
        //     .ConvertAll<Network>(x => new Network(x));
        //
        // await movieRepository.StoreNetworks(networks)

        Logger.MovieDb($"Movie: {movie.Title}: Networks stored", LogEventLevel.Debug);
        await Task.CompletedTask;
    }

    public async Task StoreCompanies(TmdbMovieAppends movie)
    {
        // List<Company> companies = movie.ProductionCompanies.Results.ToList()
        //     .ConvertAll<ProductionCompany>(x => new ProductionCompany(x));
        //
        // await movieRepository.StoreCompanies(companies)

        Logger.MovieDb($"Movie: {movie.Title}: Companies stored", LogEventLevel.Debug);
        await Task.CompletedTask;
    }
}