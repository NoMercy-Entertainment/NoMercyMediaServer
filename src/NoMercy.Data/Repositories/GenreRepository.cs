using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class GenreRepository(MediaContext context)
{
    public async Task<Genre> GetGenreAsync(Guid userId, int id, string language, int take, int page)
    {
        return await context.Genres.AsNoTracking()
            .Where(genre => genre.Id == id)
            .Include(genre => genre.GenreMovies
                .OrderBy(genreMovie => genreMovie.Movie.TitleSort)
                .Where(genreMovie => genreMovie.Movie.VideoFiles.Any(videoFile => videoFile.Folder != null))
                .Skip(page * take)
                .Take(take)
            )
            .ThenInclude(genreMovie => genreMovie.Movie)
            .ThenInclude(movie => movie.VideoFiles)
            .Include(genre => genre.GenreMovies)
            .ThenInclude(genreMovie => genreMovie.Movie.Media)
            .Include(genre => genre.GenreMovies)
            .ThenInclude(genreMovie => genreMovie.Movie.Images)
            .Include(genre => genre.GenreMovies)
            .ThenInclude(genreMovie =>
                genreMovie.Movie.Translations.Where(translation => translation.Iso6391 == language))
            .Include(genre => genre.GenreMovies)
            .ThenInclude(genreMovie => genreMovie.Movie.CertificationMovies)
            .ThenInclude(certificationMovie => certificationMovie.Certification)
            .Include(genre => genre.GenreTvShows
                .OrderBy(genreTv => genreTv.Tv.TitleSort)
                .Where(genreTv => genreTv.Tv.Episodes
                    .Any(episode => episode.VideoFiles.Any(videoFile => videoFile.Folder != null)))
                .Skip(page * take)
                .Take(take)
            )
            .ThenInclude(genreTv => genreTv.Tv)
            .ThenInclude(tv => tv.Episodes.Where(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0))
            .ThenInclude(episode => episode.VideoFiles)
            .Include(genre => genre.GenreTvShows)
            .ThenInclude(genreTv => genreTv.Tv.Media)
            .Include(genre => genre.GenreTvShows)
            .ThenInclude(genreTv => genreTv.Tv.Images)
            .Include(genre => genre.GenreTvShows)
            .ThenInclude(genreTv => genreTv.Tv.Translations.Where(translation => translation.Iso6391 == language))
            .Include(genre => genre.GenreTvShows)
            .ThenInclude(genreTv => genreTv.Tv.CertificationTvs)
            .ThenInclude(certificationTv => certificationTv.Certification)
            .FirstAsync();
    }

    public IQueryable<Genre> GetGenresAsync(Guid userId, string language, int take, int page)
    {
        return context.Genres
            .AsNoTracking()
            .Where(genre =>
                genre.GenreMovies.Any(g => g.Movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId))) ||
                genre.GenreTvShows.Any(g => g.Tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId))))
            .Select(genre => new Genre
            {
                Id = genre.Id,
                Name = genre.Name,
                GenreMovies = genre.GenreMovies
                    .Where(gm => gm.Movie.VideoFiles.Any(vf => vf.Folder != null))
                    .Select(gm => new GenreMovie
                    {
                        Movie = new()
                        {
                            Id = gm.Movie.Id,
                            Title = gm.Movie.Title,
                            TitleSort = gm.Movie.TitleSort,
                            VideoFiles = gm.Movie.VideoFiles
                                .Where(vf => vf.Folder != null)
                                .Select(vf => new VideoFile
                                {
                                    Id = vf.Id,
                                    Folder = vf.Folder
                                }).ToList(),
                            Translations = gm.Movie.Translations
                                .Where(t => t.Iso6391 == language)
                                .Select(t => new Translation
                                {
                                    Iso6391 = t.Iso6391,
                                    Title = t.Title
                                }).ToList()
                        }
                    }).ToList(),
                GenreTvShows = genre.GenreTvShows
                    .Where(gt => gt.Tv.Episodes.Any(e => e.VideoFiles.Any(vf => vf.Folder != null)))
                    .Select(gt => new GenreTv
                    {
                        Tv = new()
                        {
                            Id = gt.Tv.Id,
                            Title = gt.Tv.Title,
                            TitleSort = gt.Tv.TitleSort,
                            Episodes = gt.Tv.Episodes
                                .Where(e => e.SeasonNumber > 0 && e.VideoFiles.Any(vf => vf.Folder != null))
                                .Select(e => new Episode
                                {
                                    Id = e.Id,
                                    Title = e.Title,
                                    SeasonNumber = e.SeasonNumber,
                                    VideoFiles = e.VideoFiles
                                        .Where(vf => vf.Folder != null)
                                        .Select(vf => new VideoFile
                                        {
                                            Id = vf.Id,
                                            Folder = vf.Folder
                                        }).ToList()
                                }).ToList(),
                            Translations = gt.Tv.Translations
                                .Where(t => t.Iso6391 == language)
                                .Select(t => new Translation
                                {
                                    Iso6391 = t.Iso6391,
                                    Title = t.Title
                                }).ToList()
                        }
                    }).ToList()
            })
            .OrderBy(genre => genre.Name)
            .Skip(page * take)
            .Take(take);
    }
}
