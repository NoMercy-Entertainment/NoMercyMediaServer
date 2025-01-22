using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.MediaProcessing.Movies;

public class MovieRepository(MediaContext context) : IMovieRepository
{
    public Task Add(Movie movie)
    {
        return context.Movies.Upsert(movie)
            .On(v => new { v.Id })
            .WhenMatched((ts, ti) => new()
            {
                Id = ti.Id,
                Backdrop = ti.Backdrop,
                Duration = ti.Duration,
                ReleaseDate = ti.ReleaseDate,
                Homepage = ti.Homepage,
                ImdbId = ti.ImdbId,
                OriginalLanguage = ti.OriginalLanguage,
                Overview = ti.Overview,
                Popularity = ti.Popularity,
                Poster = ti.Poster,
                Status = ti.Status,
                Tagline = ti.Tagline,
                Title = ti.Title,
                TitleSort = ti.TitleSort,
                Trailer = ti.Trailer,
                VoteAverage = ti.VoteAverage,
                VoteCount = ti.VoteCount,
                Folder = ti.Folder,
                LibraryId = ti.LibraryId,
                UpdatedAt = ti.UpdatedAt,
                _colorPalette = ti._colorPalette,
            })
            .RunAsync();
    }

    public Task LinkToLibrary(Library library, Movie movie)
    {
        return context.LibraryMovie.Upsert(new(library.Id, movie.Id))
            .On(v => new { v.LibraryId, v.MovieId })
            .WhenMatched((lts, lti) => new()
            {
                LibraryId = lti.LibraryId,
                MovieId = lti.MovieId
            })
            .RunAsync();
    }

    public Task StoreAlternativeTitles(IEnumerable<AlternativeTitle> alternativeTitles)
    {
        return context.AlternativeTitles.UpsertRange(alternativeTitles)
            .On(a => new { a.Title, a.MovieId })
            .WhenMatched((ats, ati) => new()
            {
                Title = ati.Title,
                Iso31661 = ati.Iso31661,
                MovieId = ati.MovieId
            })
            .RunAsync();
    }

    public Task StoreTranslations(IEnumerable<Translation> translations)
    {
        return context.Translations
            .UpsertRange(translations.Where(translation => translation.Title != "" || translation.Overview != ""))
            .On(t => new { t.Iso31661, t.Iso6391, t.MovieId })
            .WhenMatched((ts, ti) => new()
            {
                Iso31661 = ti.Iso31661,
                Iso6391 = ti.Iso6391,
                Title = ti.Title,
                EnglishName = ti.EnglishName,
                Name = ti.Name,
                Overview = ti.Overview,
                Homepage = ti.Homepage,
                Biography = ti.Biography,
                MovieId = ti.MovieId,
                SeasonId = ti.SeasonId,
                EpisodeId = ti.EpisodeId,
                CollectionId = ti.CollectionId,
                PersonId = ti.PersonId,
                UpdatedAt = ti.UpdatedAt
            })
            .RunAsync();
    }

    public IEnumerable<CertificationMovie> GetCertificationMovies(TmdbMovieAppends movie,
        IEnumerable<CertificationCriteria> certificationCriteria)
    {
        return context.Certifications
            .AsEnumerable()
            .Where(c => certificationCriteria
                .Any(cc => cc.Iso31661 == c.Iso31661 && cc.Certification == c.Rating))
            .Select(c => new CertificationMovie
            {
                CertificationId = c.Id,
                MovieId = movie.Id
            });
    }

    public Task StoreContentRatings(IEnumerable<CertificationMovie> certifications)
    {
        return context.CertificationMovie.UpsertRange(certifications)
            .On(v => new { v.CertificationId, v.MovieId })
            .WhenMatched((ts, ti) => new()
            {
                CertificationId = ti.CertificationId,
                MovieId = ti.MovieId
            })
            .RunAsync();
    }

    public Task StoreSimilar(IEnumerable<Similar> similar)
    {
        return context.Similar.UpsertRange(similar)
            .On(v => new { v.MediaId, v.MovieFromId })
            .WhenMatched((ts, ti) => new()
            {
                MovieToId = ti.MovieToId,
                MovieFromId = ti.MovieFromId,
                Overview = ti.Overview,
                Title = ti.Title,
                TitleSort = ti.TitleSort,
                Backdrop = ti.Backdrop,
                Poster = ti.Poster,
                MediaId = ti.MediaId
            })
            .RunAsync();
    }

    public Task StoreRecommendations(IEnumerable<Recommendation> recommendations)
    {
        return context.Recommendations.UpsertRange(recommendations)
            .On(v => new { v.MediaId, v.MovieFromId })
            .WhenMatched((ts, ti) => new()
            {
                MovieToId = ti.MovieToId,
                MovieFromId = ti.MovieFromId,
                Overview = ti.Overview,
                Title = ti.Title,
                TitleSort = ti.TitleSort,
                Backdrop = ti.Backdrop,
                Poster = ti.Poster,
                MediaId = ti.MediaId
            })
            .RunAsync();
    }

    public Task StoreVideos(IEnumerable<Media> videos)
    {
        return context.Medias.UpsertRange(videos)
            .On(v => new { v.Src, v.MovieId })
            .WhenMatched((ts, ti) => new()
            {
                Src = ti.Src,
                Iso6391 = ti.Iso6391,
                Type = ti.Type,
                MovieId = ti.MovieId,
                Name = ti.Name,
                Site = ti.Site,
                Size = ti.Size,
                UpdatedAt = ti.UpdatedAt
            })
            .RunAsync();
    }

    public Task StoreImages(IEnumerable<Image> images)
    {
        return context.Images.UpsertRange(images)
            .On(v => new { v.FilePath, v.MovieId })
            .WhenMatched((ts, ti) => new()
            {
                AspectRatio = ti.AspectRatio,
                FilePath = ti.FilePath,
                Height = ti.Height,
                Iso6391 = ti.Iso6391,
                Site = ti.Site,
                VoteAverage = ti.VoteAverage,
                VoteCount = ti.VoteCount,
                Width = ti.Width,
                Type = ti.Type,
                MovieId = ti.MovieId,
                UpdatedAt = ti.UpdatedAt
            })
            .RunAsync();
    }

    public Task StoreKeywords(IEnumerable<Keyword> keywords)
    {
        return context.Keywords.UpsertRange(keywords)
            .On(v => new { v.Id })
            .WhenMatched((ts, ti) => new()
            {
                Id = ti.Id,
                Name = ti.Name
            })
            .RunAsync();
    }

    public Task LinkKeywordsToMovie(IEnumerable<KeywordMovie> keywordMovies)
    {
        return context.KeywordMovie.UpsertRange(keywordMovies)
            .On(v => new { v.KeywordId, v.MovieId })
            .WhenMatched((ts, ti) => new()
            {
                KeywordId = ti.KeywordId,
                MovieId = ti.MovieId
            })
            .RunAsync();
    }

    public Task StoreGenres(IEnumerable<GenreMovie> genreMovies)
    {
        return context.GenreMovie.UpsertRange(genreMovies)
            .On(v => new { v.GenreId, v.MovieId })
            .WhenMatched((ts, ti) => new()
            {
                GenreId = ti.GenreId,
                MovieId = ti.MovieId
            })
            .RunAsync();
    }

    public Task StoreWatchProviders()
    {
        return Task.CompletedTask;
    }

    public Task StoreNetworks()
    {
        // List<Keyword> keywords = Movie?.Networks.Results.ToList()
        //     .ConvertAll<Network>(x => new Network(x)).ToArray() ?? [];
        //
        // return context.Networks.UpsertRange(keywords)
        //     .On(v => new { v.Id })
        //     .WhenMatched((ts, ti) => new Network
        //     {
        //         Id = ti.Id,
        //         Title = ti.Title,
        //     })
        //     .RunAsync();

        return Task.CompletedTask;
    }

    public Task StoreCompanies()
    {
        // List<Company> companies = Movie?.ProductionCompanies.Results.ToList()
        //     .ConvertAll<ProductionCompany>(x => new ProductionCompany(x)).ToArray() ?? [];
        //
        // return context.Companies.UpsertRange(companies)
        //     .On(v => new { v.Id })
        //     .WhenMatched((ts, ti) => new ProductionCompany
        //     {
        //         Id = ti.Id,
        //         Title = ti.Title,
        //     })
        //     .RunAsync();

        return Task.CompletedTask;
    }
}