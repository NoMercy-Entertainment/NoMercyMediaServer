using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;


namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record GenreResponseDto
{
    [JsonProperty("nextId")] public long? NextId { get; set; }

    [JsonProperty("data")] public IOrderedEnumerable<GenreResponseItemDto>? Data { get; set; }

    public static readonly Func<MediaContext, Guid, int, string, int, int, Task<Genre>> GetGenre =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, int id, string language, int take, int page) =>
            mediaContext.Genres.AsNoTracking()
                .Where(genre => genre.Id == id)
                // .Where(genre => genre.GenresUsers
                //     .FirstOrDefault(u => u.UserId.Equals(userId) != null
                // )
                .Take(take)
                .Skip(page * take)
                .Include(genre => genre.GenreMovies
                    .Where(genreMovie => genreMovie.Movie.VideoFiles
                        .Any(videoFile => videoFile.Folder != null) == true
                    )
                )
                .ThenInclude(genreMovie => genreMovie.Movie)
                .ThenInclude(movie => movie.VideoFiles)
                .Include(genre => genre.GenreMovies)
                .ThenInclude(genreMovie => genreMovie.Movie.Media)
                .Include(genre => genre.GenreMovies)
                .ThenInclude(genreMovie => genreMovie.Movie.Images)
                .Include(genre => genre.GenreMovies)
                .ThenInclude(genreMovie => genreMovie.Movie.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(genre => genre.GenreMovies)
                .ThenInclude(genreMovie => genreMovie.Movie.CertificationMovies)
                .ThenInclude(certificationMovie => certificationMovie.Certification)
                .Include(genre => genre.GenreTvShows
                    .Where(genreTv => genreTv.Tv.Episodes
                        .Any(episode => episode.VideoFiles
                            .Any(videoFile => videoFile.Folder != null) == true
                        ) == true
                    )
                )
                .ThenInclude(genreTv => genreTv.Tv)
                .ThenInclude(tv => tv.Episodes
                    .Where(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0))
                .ThenInclude(episode => episode.VideoFiles)
                .Include(genre => genre.GenreTvShows)
                .ThenInclude(genreTv => genreTv.Tv.Media)
                .Include(genre => genre.GenreTvShows)
                .ThenInclude(genreTv => genreTv.Tv.Images)
                .Include(genre => genre.GenreTvShows)
                .ThenInclude(genreTv => genreTv.Tv.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(genre => genre.GenreTvShows)
                .ThenInclude(genreTv => genreTv.Tv.CertificationTvs)
                .ThenInclude(certificationTv => certificationTv.Certification)
                .First());
}
