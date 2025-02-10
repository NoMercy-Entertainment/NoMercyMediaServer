using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;


namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record LibraryResponseDto
{
    [JsonProperty("cursor")] public long? Cursor { get; set; }

    [JsonProperty("data")] public List<LibraryResponseItemDto> Data { get; set; } = [];

    public static async Task<List<Movie>> GetLibraryMovies(Guid userId, Ulid libraryId, string language, int take,
        int page = 0)
    {
        await using MediaContext mediaContext = new();

        IIncludableQueryable<Movie, Certification> query = mediaContext.Movies
                .AsNoTracking()
                .Where(movie => movie.Library.Id == libraryId)
                .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Where(libraryMovie => libraryMovie.VideoFiles
                    .Any(videoFile => videoFile.Folder != null) == true
                )
                .Include(movie => movie.VideoFiles)
                .Include(movie => movie.Media
                    .Where(media => media.Iso6391 == language || media.Iso6391 == "en")
                )
                .Include(movie => movie.Images
                    .Where(image => image.Iso6391 == language || image.Iso6391 == "en")
                )
                .Include(movie => movie.GenreMovies)
                .ThenInclude(genreMovie => genreMovie.Genre)
                .Include(movie => movie.Translations
                    .Where(translation => translation.Iso6391 == language || translation.Iso6391 == "en")
                )
                .Include(movie => movie.CertificationMovies)
                .ThenInclude(certificationMovie => certificationMovie.Certification)
            ;

        List<Movie> movies = await query
            .OrderBy(collection => collection.TitleSort)
            .Skip(page * take)
            .Take(take)
            .ToListAsync();

        return movies;
    }

    public static async Task<List<Tv>> GetLibraryShows(Guid userId, Ulid libraryId, string language, int take,
        int page = 0)
    {
        await using MediaContext mediaContext = new();

        IIncludableQueryable<Tv, Certification> query = mediaContext.Tvs.AsNoTracking()
                .Where(tv => tv.Library.Id == libraryId)
                .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Where(libraryTv => libraryTv.Episodes
                    .Any(episode => episode.VideoFiles
                        .Any(videoFile => videoFile.Folder != null) == true
                    ) == true)
                .Include(tv => tv.Episodes
                    .Where(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0)
                )
                .ThenInclude(episode => episode.VideoFiles)
                .Include(tv => tv.Media
                    .Where(media => media.Iso6391 == language || media.Iso6391 == "en")
                )
                .Include(tv => tv.Images
                    .Where(image => image.Iso6391 == language || image.Iso6391 == "en")
                )
                .Include(tv => tv.GenreTvs)
                .ThenInclude(genreTv => genreTv.Genre)
                .Include(tv => tv.Translations
                    .Where(translation => translation.Iso6391 == language || translation.Iso6391 == "en")
                )
                .Include(tv => tv.CertificationTvs)
                .ThenInclude(certificationTv => certificationTv.Certification)
            ;

        List<Tv> shows = await query
            .OrderBy(collection => collection.TitleSort)
            .Skip(page * take)
            .Take(take)
            .ToListAsync();

        return shows;
    }

    public static readonly Func<MediaContext, Guid, Ulid, string, int, int, Task<Library>> GetLibrary =
        EF.CompileAsyncQuery(
            (MediaContext mediaContext, Guid userId, Ulid id, string language, int take, int page = 0) =>
                mediaContext.Libraries.AsNoTracking()
                    .Where(library => library.Id == id)
                    .Where(library => library.LibraryUsers
                        .FirstOrDefault(u => u.UserId.Equals(userId)) != null
                    )
                    .Take(take)
                    .Skip(page * take)
                    .Include(library => library.LibraryMovies
                        .Where(libraryMovie => libraryMovie.Movie.VideoFiles
                            .Any(videoFile => videoFile.Folder != null) == true
                        )
                    )
                    .ThenInclude(libraryMovie => libraryMovie.Movie)
                    .ThenInclude(movie => movie.VideoFiles)
                    .Include(library => library.LibraryMovies)
                    .ThenInclude(libraryMovie => libraryMovie.Movie.Media
                        .Where(media => media.Iso6391 == language || media.Iso6391 == "en"))
                    .Include(library => library.LibraryMovies)
                    .ThenInclude(libraryMovie => libraryMovie.Movie.Images
                        .Where(image => image.Iso6391 == language || image.Iso6391 == "en"))
                    .Include(library => library.LibraryMovies)
                    .ThenInclude(libraryMovie => libraryMovie.Movie.GenreMovies)
                    .ThenInclude(genreMovie => genreMovie.Genre)
                    .Include(library => library.LibraryMovies)
                    .ThenInclude(libraryMovie => libraryMovie.Movie.Translations
                        .Where(translation => translation.Iso6391 == language || translation.Iso6391 == "en"))
                    .Include(library => library.LibraryMovies)
                    .ThenInclude(libraryMovie => libraryMovie.Movie.CertificationMovies)
                    .ThenInclude(certificationMovie => certificationMovie.Certification)
                    .Include(library => library.LibraryTvs
                        .Where(libraryTv => libraryTv.Tv.Episodes
                            .Any(episode => episode.VideoFiles
                                .Any(videoFile => videoFile.Folder != null) == true
                            ) == true
                        )
                    )
                    .ThenInclude(libraryTv => libraryTv.Tv)
                    .ThenInclude(tv => tv.Episodes
                        .Where(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0))
                    .ThenInclude(episode => episode.VideoFiles)
                    .Include(library => library.LibraryTvs)
                    .ThenInclude(libraryTv => libraryTv.Tv.Media
                        .Where(media => media.Iso6391 == language || media.Iso6391 == "en"))
                    .Include(library => library.LibraryTvs)
                    .ThenInclude(libraryTv => libraryTv.Tv.Images
                        .Where(image => image.Iso6391 == language || image.Iso6391 == "en"))
                    .Include(library => library.LibraryTvs)
                    .ThenInclude(libraryTv => libraryTv.Tv.GenreTvs)
                    .ThenInclude(genreTv => genreTv.Genre)
                    .Include(library => library.LibraryTvs)
                    .ThenInclude(libraryTv => libraryTv.Tv.Translations
                        .Where(translation => translation.Iso6391 == language || translation.Iso6391 == "en"))
                    .Include(library => library.LibraryTvs)
                    .ThenInclude(libraryTv => libraryTv.Tv.CertificationTvs)
                    .ThenInclude(certificationTv => certificationTv.Certification)
                    .First());
}
