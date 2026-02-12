using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Music;

namespace NoMercy.Data.Repositories;

public class GenreWithCountsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalMovies { get; set; }
    public int TotalTvShows { get; set; }
    public int MoviesWithVideo { get; set; }
    public int TvShowsWithVideo { get; set; }
}

public class GenreRepository(MediaContext context)
{
    private static readonly string[] Letters = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];
    
    public async Task<Genre?> GetGenreAsync(Guid userId, int id, string language, string country, int take, int page, CancellationToken ct = default)
    {
        return await context.Genres
            .AsNoTracking()
            .Where(genre => genre.Id == id)
            .Include(genre => genre.Translations.Where(t => t.Iso6391 == language))
            .Include(genre => genre.GenreMovies
                .Where(gm => gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId))
                .Where(gm => gm.Movie.VideoFiles.Any(v => v.Folder != null))
                .Take(take))
                .ThenInclude(gm => gm.Movie)
                .ThenInclude(m => m.Translations.Where(t => t.Iso6391 == language))
            .Include(genre => genre.GenreMovies)
                .ThenInclude(gm => gm.Movie)
                .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
            .Include(genre => genre.GenreMovies)
                .ThenInclude(gm => gm.Movie)
                .ThenInclude(m => m.Images.Where(i => i.Type == "logo").Take(1))
            .Include(genre => genre.GenreMovies)
                .ThenInclude(gm => gm.Movie)
                .ThenInclude(m => m.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            .Include(genre => genre.GenreTvShows
                .Where(gt => gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId))
                .Where(gt => gt.Tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
                .Take(take))
                .ThenInclude(gt => gt.Tv)
                .ThenInclude(tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(genre => genre.GenreTvShows)
                .ThenInclude(gt => gt.Tv)
                .ThenInclude(tv => tv.Episodes.Where(e => e.SeasonNumber > 0 && e.VideoFiles.Any(v => v.Folder != null)))
                .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
            .Include(genre => genre.GenreTvShows)
                .ThenInclude(gt => gt.Tv)
                .ThenInclude(tv => tv.Images.Where(i => i.Type == "logo").Take(1))
            .Include(genre => genre.GenreTvShows)
                .ThenInclude(gt => gt.Tv)
                .ThenInclude(tv => tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            .FirstOrDefaultAsync(ct);
    }

    public IQueryable<Genre> GetGenres(Guid userId, string language, int take, int page)
    {
        return context.Genres
            .AsNoTracking()
            .Where(genre =>
                genre.GenreMovies.Any(g => g.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)) ||
                genre.GenreTvShows.Any(g => g.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Include(genre => genre.Translations.Where(t => t.Iso6391 == language))
            .Include(genre => genre.GenreMovies
                .Where(gm => gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Include(genre => genre.GenreTvShows
                .Where(gt => gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .OrderBy(genre => genre.Name)
            .Skip(page * take)
            .Take(take);
    }

    public async Task<List<GenreWithCountsDto>> GetGenresWithCountsAsync(Guid userId, string language, int take, int page, CancellationToken ct = default)
    {
        return await context.Genres
            .AsNoTracking()
            .Where(genre =>
                genre.GenreMovies.Any(g => g.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)) ||
                genre.GenreTvShows.Any(g => g.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .OrderBy(genre => genre.Name)
            .Skip(page * take)
            .Take(take)
            .Select(genre => new GenreWithCountsDto
            {
                Id = genre.Id,
                Name = genre.Translations.FirstOrDefault(t => t.Iso6391 == language) != null
                    ? genre.Translations.First(t => t.Iso6391 == language).Name ?? genre.Name
                    : genre.Name,
                TotalMovies = genre.GenreMovies.Count(gm => gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)),
                TotalTvShows = genre.GenreTvShows.Count(gt => gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)),
                MoviesWithVideo = genre.GenreMovies.Count(gm =>
                    gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId) &&
                    gm.Movie.VideoFiles.Any(v => v.Folder != null)),
                TvShowsWithVideo = genre.GenreTvShows.Count(gt =>
                    gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId) &&
                    gt.Tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
            })
            .ToListAsync(ct);
    }

    public Task<List<MusicGenre>> GetMusicGenresAsync(Guid userId, CancellationToken ct = default)
    {
        return context.MusicGenres
            .AsNoTracking()
            .Where(genre =>
                genre.AlbumMusicGenres.Any(g => g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)) ||
                genre.ArtistMusicGenres.Any(g => g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Where(genre => genre.MusicGenreTracks.Any())
            .Include(genre => genre.MusicGenreTracks)
            .OrderBy(genre => genre.Name)
            .ToListAsync(ct);
    }


    public Task<List<MusicGenre>> GetPaginatedMusicGenresAsync(Guid userId, string letter, int take, int page, CancellationToken ct = default)
    {
        return context.MusicGenres
            .AsNoTracking()
            .Where(genre =>
                genre.AlbumMusicGenres.Any(g => g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)) ||
                genre.ArtistMusicGenres.Any(g => g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Where(genre => genre.MusicGenreTracks.Any())
            .Where(genre => (letter == "_" || letter == "#")
                ? Letters.Any(p => genre.Name.StartsWith(p.ToLower()))
                : genre.Name.StartsWith(letter.ToLower()))
            .Include(genre => genre.MusicGenreTracks)
            .OrderBy(genre => genre.Name)
            .Skip(page * take)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<MusicGenre?> GetMusicGenreAsync(Guid userId, Guid genreId, CancellationToken ct = default)
    {
        return context.MusicGenres
            .AsNoTracking()
            .Where(genre =>
                genre.AlbumMusicGenres.Any(g => g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)) ||
                genre.ArtistMusicGenres.Any(g => g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Where(genre => genre.Id == genreId)
            .Where(genre => genre.MusicGenreTracks.Any())
            .Include(genre => genre.MusicGenreTracks)
                .ThenInclude(mgt => mgt.Track)
                .ThenInclude(track => track.TrackUser.Where(tu => tu.UserId == userId))
            .Include(genre => genre.MusicGenreTracks)
                .ThenInclude(mgt => mgt.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(at => at.Artist)
                .ThenInclude(artist => artist.Translations)
            .Include(genre => genre.MusicGenreTracks)
                .ThenInclude(mgt => mgt.Track)
                .ThenInclude(track => track.ArtistTrack)
                .ThenInclude(at => at.Artist)
                .ThenInclude(artist => artist.Images)

            .Include(genre => genre.MusicGenreTracks)
                .ThenInclude(mgt => mgt.Track)
                .ThenInclude(track => track.AlbumTrack)
                .ThenInclude(at => at.Album)
                .ThenInclude(album => album.Translations)
            .Include(genre => genre.MusicGenreTracks)
                .ThenInclude(mgt => mgt.Track)
                .ThenInclude(track => track.AlbumTrack)
                .ThenInclude(at => at.Album)
                .ThenInclude(album => album.AlbumArtist)
                .ThenInclude(aa => aa.Artist)
                .ThenInclude(artist => artist.Images)
            .FirstOrDefaultAsync(ct);
    }
}
