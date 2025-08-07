using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;


namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record HomeResponseDto<T>
{
    [JsonProperty("data")] public IEnumerable<GenreRowDto<T>> Data { get; set; } = [];
}

public abstract record HomeResponseDto
{
    public static async Task<List<Genre>> GetHome(MediaContext mediaContext, Guid userId, string? language, int take,
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

    public static readonly Func<MediaContext, List<int>, string?, IAsyncEnumerable<Tv>> GetHomeTvs =
        EF.CompileAsyncQuery((MediaContext mediaContext, List<int> tvIds, string? language) =>
            mediaContext.Tvs.AsNoTracking()
                .Where(tv => tvIds.Contains(tv.Id))
                .Include(movie => movie.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(tv => tv.Images
                    .Where(image => image.Type == "logo" && image.Iso6391 == "en")
                )
                .Include(movie => movie.Media
                    .Where(media => media.Site == "YouTube")
                )
                .Include(genreTv => genreTv.KeywordTvs)
                .ThenInclude(genre => genre.Keyword)
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
                    .Where(image => image.Type == "logo" && image.Iso6391 == "en")
                )
                .Include(movie => movie.VideoFiles)
                .Include(movie => movie.KeywordMovies)
                .ThenInclude(genre => genre.Keyword)
                .Include(movie => movie.CertificationMovies)
                .ThenInclude(certificationMovie => certificationMovie.Certification)
        );
}
