using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public static class Queries
{
    public static readonly Func<MediaContext, Guid, string, Task<Tv?>> GetRandomTvShow =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, string language) =>
            mediaContext.Tvs.AsNoTracking()
                .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Include(tv => tv.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(tv => tv.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en"))
                .Include(tv => tv.Media.Where(media => media.Site == "YouTube"))
                .Include(tv => tv.KeywordTvs)
                .ThenInclude(keywordTv => keywordTv.Keyword)
                .Include(tv => tv.Episodes
                    .Where(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0))
                .ThenInclude(episode => episode.VideoFiles)
                .Include(tv => tv.CertificationTvs)
                .ThenInclude(certificationTv => certificationTv.Certification)
                .OrderBy(tv => EF.Functions.Random())
                .FirstOrDefault());

    public static readonly Func<MediaContext, Guid, string, Task<Movie?>> GetRandomMovie =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, string language) =>
            mediaContext.Movies.AsNoTracking()
                .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Include(tv => tv.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(movie => movie.Media
                    .Where(media => media.Site == "YouTube"))
                .Include(movie => movie.Images
                    .Where(image => image.Type == "logo" && image.Iso6391 == "en"))
                .Include(movie => movie.VideoFiles)
                .Include(movie => movie.KeywordMovies)
                .ThenInclude(keywordMovie => keywordMovie.Keyword)
                .Include(movie => movie.CertificationMovies)
                .ThenInclude(certificationMovie => certificationMovie.Certification)
                .OrderBy(movie => EF.Functions.Random())
                .FirstOrDefault());

    public static readonly Func<MediaContext, List<int>, string?, IAsyncEnumerable<Tv>> GetHomeTvs =
        EF.CompileAsyncQuery((MediaContext mediaContext, List<int> tvIds, string? language) =>
            mediaContext.Tvs.AsNoTracking()
                .Where(tv => tvIds.Contains(tv.Id))
                .Include(tv => tv.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(tv => tv.Images
                    .Where(image => image.Type == "logo" && image.Iso6391 == "en"))
                .Include(tv => tv.Media
                    .Where(media => media.Site == "YouTube"))
                .Include(tv => tv.KeywordTvs)
                .ThenInclude(keywordTv => keywordTv.Keyword)
                .Include(tv => tv.Episodes
                    .Where(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0))
                .ThenInclude(episode => episode.VideoFiles)
                .Include(tv => tv.CertificationTvs)
                .ThenInclude(certificationTv => certificationTv.Certification)
        );

    public static readonly Func<MediaContext, List<int>, string?, IAsyncEnumerable<Movie>> GetHomeMovies =
        EF.CompileAsyncQuery((MediaContext mediaContext, List<int> movieIds, string? language) =>
            mediaContext.Movies.AsNoTracking()
                .Where(movie => movieIds.Contains(movie.Id))
                .Include(movie => movie.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(movie => movie.Media
                    .Where(media => media.Site == "YouTube"))
                .Include(movie => movie.Images
                    .Where(image => image.Type == "logo" && image.Iso6391 == "en"))
                .Include(movie => movie.VideoFiles)
                .Include(movie => movie.KeywordMovies)
                .ThenInclude(keywordMovie => keywordMovie.Keyword)
                .Include(movie => movie.CertificationMovies)
                .ThenInclude(certificationMovie => certificationMovie.Certification)
        );


    public static readonly Func<MediaContext, Guid, string, string, HashSet<UserData>> GetContinueWatching =
    (mediaContext, userId, _, country) =>
        mediaContext.UserData.AsNoTracking()
            .Where(user => user.UserId.Equals(userId))
            .Where(user => user.MovieId != null || user.TvId != null || user.CollectionId != null || user.SpecialId != null)
            .Include(userData => userData.Movie)
            .ThenInclude(movie => movie!.Media.Where(media => media.Site == "Youtube"))
            .Include(userData => userData.Movie)
            .ThenInclude(movie => movie!.CertificationMovies.Where(certificationMovie => certificationMovie.Certification.Iso31661 == country))
            .ThenInclude(certificationMovie => certificationMovie.Certification)
            .Include(userData => userData.Movie)
            .ThenInclude(movie => movie!.VideoFiles)
            .Include(userData => userData.Tv)
            .ThenInclude(tv => tv!.Media.Where(media => media.Site == "Youtube"))
            .Include(userData => userData.Tv)
            .ThenInclude(tv => tv!.CertificationTvs.Where(certificationTv => certificationTv.Certification.Iso31661 == country))
            .ThenInclude(certificationTv => certificationTv.Certification)
            .Include(userData => userData.Tv)
            .ThenInclude(tv => tv!.Episodes.Where(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0))
            .ThenInclude(episode => episode.VideoFiles)
            .Include(userData => userData.Collection)
            .ThenInclude(collection => collection!.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.CertificationMovies)
            .ThenInclude(certificationMovie => certificationMovie.Certification)
            .Include(userData => userData.Collection)
            .ThenInclude(collection => collection!.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.Media.Where(media => media.Site == "Youtube"))
            .Include(userData => userData.Collection)
            .ThenInclude(collection => collection!.CollectionMovies)
            .ThenInclude(collectionMovie => collectionMovie.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            .Include(userData => userData.Special)
            .OrderByDescending(userData => userData.UpdatedAt)
            .AsEnumerable()
            .DistinctBy(userData => new
            {
                userData.MovieId,
                userData.CollectionId,
                userData.TvId,
                userData.SpecialId
            })
            .ToHashSet();

    public static readonly Func<MediaContext, Guid, HashSet<Image>> GetScreensaverImages =
        (mediaContext, userId) =>
            mediaContext.Images.AsNoTracking()
                .Where(image => image.Movie!.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)) ||
                                image.Tv!.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Where(image => image.Height > 1080)
                .Where(image => image.Width > image.Height)
                .Where(image => image._colorPalette != "")
                .Where(image =>
                    (image.Type == "backdrop" && image.VoteAverage > 2 && image.Iso6391 == null) ||
                    (image.Type == "logo" && image.Iso6391 == "en"))
                .ToHashSet();

    public static HashSet<Genre> GetHome(MediaContext mediaContext, Guid userId, string? language, int take, int page = 0)
    {
        IOrderedQueryable<Genre>? query = mediaContext.Genres.AsNoTracking()
            .OrderBy(genre => genre.Name)
            .Where(genre => genre.GenreMovies.Any(g => g.Movie.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId)) != null) ||
                            genre.GenreTvShows.Any(g => g.Tv.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId)) != null))
            .Include(genre => genre.Translations.Where(translation => translation.Iso6391 == language))
            .Include(genre => genre.GenreMovies.Where(genreTv => genreTv.Movie.VideoFiles.Any(videoFile => videoFile.Folder != null) == true))
            .Include(genre => genre.GenreTvShows.Where(genreTv => genreTv.Tv.Episodes.Any(episode => episode.VideoFiles.Any(videoFile => videoFile.Folder != null)) == true))
            .OrderBy(genre => genre.Name);

        return query
            .Skip(page * take)
            .Take(take)
            .ToHashSet();

    }
}
