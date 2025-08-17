using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Data.Repositories;

public class HomeRepository
{
    public readonly Func<MediaContext, List<int>, string?, IAsyncEnumerable<Tv>> GetHomeTvsQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, List<int> tvIds, string? language) =>
            mediaContext.Tvs.AsNoTracking()
                .Where(tv => tvIds.Contains(tv.Id))
                .Include(tv => tv.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(tv => tv.Images
                    .Where(image => image.Type == "logo" && (image.Iso6391 == "en" || image.Iso6391 == language)))
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

    public readonly Func<MediaContext, List<int>, string?, IAsyncEnumerable<Movie>> GetHomeMoviesQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, List<int> movieIds, string? language) =>
            mediaContext.Movies.AsNoTracking()
                .Where(movie => movieIds.Contains(movie.Id))
                .Include(movie => movie.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(movie => movie.Media
                    .Where(media => media.Site == "YouTube"))
                .Include(movie => movie.Images
                    .Where(image => image.Type == "logo" && (image.Iso6391 == "en" || image.Iso6391 == language)))
                .Include(movie => movie.VideoFiles)
                .Include(movie => movie.KeywordMovies)
                .ThenInclude(keywordMovie => keywordMovie.Keyword)
                .Include(movie => movie.CertificationMovies)
                .ThenInclude(certificationMovie => certificationMovie.Certification)
        );

    public readonly Func<MediaContext, Guid, string, string, HashSet<UserData>> GetContinueWatching =
        (mediaContext, userId, _, country) =>
            mediaContext.UserData.AsNoTracking()
                .Where(user => user.UserId.Equals(userId))
                .Where(user => user.MovieId != null || user.TvId != null || user.CollectionId != null ||
                               user.SpecialId != null)

                .Include(userData => userData.Movie)
                .ThenInclude(movie => movie!.VideoFiles)

                .Include(collectionMovie => collectionMovie.Movie)
                .ThenInclude(movie => movie.CertificationMovies)
                .ThenInclude(certificationMovie => certificationMovie.Certification)

                .Include(userData => userData.Movie)
                .ThenInclude(movie => movie!.Media.Where(media => media.Site == "Youtube"))

                .Include(episode => episode.Tv)
                .ThenInclude(tv => tv.CertificationTvs
                    .Where(certificationTv => certificationTv.Certification.Iso31661 == country))
                .ThenInclude(certificationTv => certificationTv.Certification)

                .Include(userData => userData.Tv)
                .ThenInclude(tv => tv!.Episodes
                    .Where(episode => episode.VideoFiles.Count != 0)
                )
                .ThenInclude(episode => episode.VideoFiles)

                .Include(userData => userData.Tv)
                .ThenInclude(tv => tv!.Media.Where(media => media.Site == "Youtube"))

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
                .ThenInclude(special => special!.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie!.VideoFiles)

                .Include(userData => userData.Special)
                .ThenInclude(special => special!.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie =>
                    movie!.CertificationMovies.Where(certificationMovie =>
                        certificationMovie.Certification.Iso31661 == country))
                .ThenInclude(certificationMovie => certificationMovie.Certification)

                .Include(userData => userData.Special)
                .ThenInclude(special => special!.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(movie => movie!.VideoFiles)

                .Include(userData => userData.Special)
                .ThenInclude(special => special!.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode.Tv)
                .ThenInclude(tv => tv.CertificationTvs
                    .Where(certificationTv => certificationTv.Certification.Iso31661 == country))
                .ThenInclude(certificationTv => certificationTv.Certification)

                .Include(userData => userData.VideoFile)
                .OrderByDescending(userData => userData.UpdatedAt)
                .AsEnumerable()
                .DistinctBy(userData => new
                {
                    userData.MovieId,
                    userData.CollectionId,
                    userData.TvId,
                    userData.SpecialId
                })
                // .Where(user => 
                //     // Filter out items that have been finished watching, 90% for episodes, 80% for movies
                //     (user.VideoFile.Episode != null && user.Time < user.VideoFile.Duration.ToSeconds() * 0.9) || 
                //     (user.VideoFile.Movie != null && user.Time < user.VideoFile.Duration.ToSeconds() * 0.8))
                .ToHashSet();

    public readonly Func<MediaContext, Guid, Task<HashSet<Image>>> GetScreensaverImagesQuery =
        (mediaContext, userId) =>
            mediaContext.Images.AsNoTracking()
                .Where(image => image.Movie!.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)) ||
                                image.Tv!.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Where(image => image._colorPalette != "")
                .Where(image =>
                    (image.Type == "backdrop" && image.VoteAverage > 5 && (image.Iso6391 == null || image.Iso6391 == "") &&
                     image.Height >= 1080) ||
                    (image.Type == "logo" && image.Iso6391 == "en" && image.Width >= image.Height))
                .OrderByDescending(image => image.Width)
                .ToHashSetAsync();

    public readonly Func<MediaContext, Guid, Task<List<Library>>> GetLibrariesQuery =
        (mediaContext, userId) =>
            mediaContext.Libraries.AsNoTracking()
                .Include(library => library.LibraryUsers)
                .Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
                .ThenInclude(folder => folder.EncoderProfileFolder)
                .ThenInclude(library => library.EncoderProfile)
                .Include(library => library.LanguageLibraries)
                .ThenInclude(languageLibrary => languageLibrary.Language)
                .Include(library => library.LibraryMovies)
                .Include(library => library.LibraryTvs)
                .Where(library => library.LibraryUsers
                    .Any(u => u.UserId == userId))
                .ToListAsync();

    public readonly Func<MediaContext, Guid, Task<int>> GetAnimeCountQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId) =>
            mediaContext.Tvs.AsNoTracking()
                .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Count(tv => tv.Library.Type == "anime"));

    public readonly Func<MediaContext, Guid, Task<int>> GetMovieCountQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId) =>
            mediaContext.Movies.AsNoTracking()
                .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Count(movie => movie.Library.Type == "movie"));

    public readonly Func<MediaContext, Guid, Task<int>> GetTvCountQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId) =>
            mediaContext.Tvs.AsNoTracking()
                .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Count(tv => tv.Library.Type == "tv"));

    public readonly Func<MediaContext, Guid, string?, int, int, IAsyncEnumerable<Genre>> GetHomeGenresQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, string? language, int take, int page) =>
            mediaContext.Genres.AsNoTracking()
                .Where(genre =>
                    genre.GenreMovies.Any(g =>
                        g.Movie.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId)) != null) ||
                    genre.GenreTvShows.Any(g =>
                        g.Tv.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId)) != null))
                .Include(genre => genre.Translations.Where(translation => translation.Iso6391 == language))
                .Include(genre => genre.GenreMovies.Where(genreTv =>
                    genreTv.Movie.VideoFiles.Any(videoFile => videoFile.Folder != null) == true))
                .Include(genre => genre.GenreTvShows.Where(genreTv =>
                    genreTv.Tv.Episodes.Any(episode => episode.VideoFiles.Any(videoFile => videoFile.Folder != null)) ==
                    true))
                .OrderBy(genre => genre.Name)
                .Skip(page * take)
                .Take(take));

    public async Task<HashSet<Genre>> GetHomeGenres(MediaContext mediaContext, Guid userId, string? language, int take,
        int page = 0)
    {
        HashSet<Genre> genres = [];
        await foreach (Genre genre in GetHomeGenresQuery(mediaContext, userId, language, take, page)) genres.Add(genre);
        return genres;
    }

    public async Task<List<Genre>> GetHome(MediaContext mediaContext, Guid userId, string? language, int take,
        int page = 0)
    {
        IOrderedQueryable<Genre> query = mediaContext.Genres.AsNoTracking()
            .OrderBy(genre => genre.Name)
            .Where(genre =>
                genre.GenreMovies
                    .Any(g => g.Movie.Library.LibraryUsers
                        .FirstOrDefault(u => u.UserId.Equals(userId)) != null) ||
                genre.GenreTvShows
                    .Any(g => g.Tv.Library.LibraryUsers
                        .FirstOrDefault(u => u.UserId.Equals(userId)) != null))
            .Include(genre => genre.GenreMovies
                .Where(genreTv => genreTv.Movie.VideoFiles
                    .Any(videoFile => videoFile.Folder != null) == true
                )
            )
            .Include(genre => genre.GenreTvShows
                .Where(genreTv => genreTv.Tv.Episodes
                    .Any(episode => episode.VideoFiles
                        .Any(videoFile => videoFile.Folder != null)
                    ) == true
                )
            )
            .Include(movie => movie.Translations
                .Where(translation => translation.Iso6391 == language))
            .OrderBy(genre => genre.Name);

        List<Genre> genres = await query
            .Skip(page * take)
            .Take(take)
            .ToListAsync();

        return genres;
    }
}