using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Music;

namespace NoMercy.Data.Repositories;

public class GenreDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TranslatedName { get; set; }
}

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

    public async Task<(GenreDetailDto? Genre, List<HomeMovieCardDto> Movies, List<HomeTvCardDto> TvShows)> GetGenreCardsAsync(
        Guid userId, int id, string language, string country, int take, int page, CancellationToken ct = default)
    {
        GenreDetailDto? genreDetail = await context.Genres
            .AsNoTracking()
            .Where(genre => genre.Id == id)
            .Select(genre => new GenreDetailDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TranslatedName = genre.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Name)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (genreDetail is null)
            return (null, [], []);

        List<HomeMovieCardDto> movies = await context.GenreMovie
            .AsNoTracking()
            .Where(gm => gm.GenreId == id)
            .Where(gm => gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(gm => gm.Movie.VideoFiles.Any(v => v.Folder != null))
            .OrderBy(gm => gm.Movie.TitleSort)
            .Skip(page * take)
            .Take(take)
            .Select(gm => new HomeMovieCardDto
            {
                Id = gm.Movie.Id,
                Title = gm.Movie.Title,
                TitleSort = gm.Movie.TitleSort,
                TranslatedTitle = gm.Movie.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = gm.Movie.Overview,
                TranslatedOverview = gm.Movie.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = gm.Movie.Poster,
                Backdrop = gm.Movie.Backdrop,
                Logo = gm.Movie.Images
                    .Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                ReleaseDate = gm.Movie.ReleaseDate,
                CreatedAt = gm.Movie.CreatedAt,
                ColorPalette = gm.Movie._colorPalette,
                VideoFileCount = gm.Movie.VideoFiles.Count(v => v.Folder != null),
                CertificationRating = gm.Movie.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = gm.Movie.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        List<HomeTvCardDto> tvShows = await context.GenreTv
            .AsNoTracking()
            .Where(gt => gt.GenreId == id)
            .Where(gt => gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(gt => gt.Tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
            .OrderBy(gt => gt.Tv.TitleSort)
            .Skip(page * take)
            .Take(take)
            .Select(gt => new HomeTvCardDto
            {
                Id = gt.Tv.Id,
                Title = gt.Tv.Title,
                TitleSort = gt.Tv.TitleSort,
                TranslatedTitle = gt.Tv.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = gt.Tv.Overview,
                TranslatedOverview = gt.Tv.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = gt.Tv.Poster,
                Backdrop = gt.Tv.Backdrop,
                Logo = gt.Tv.Images
                    .Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                FirstAirDate = gt.Tv.FirstAirDate,
                CreatedAt = gt.Tv.CreatedAt,
                ColorPalette = gt.Tv._colorPalette,
                NumberOfEpisodes = gt.Tv.NumberOfEpisodes,
                EpisodesWithVideo = gt.Tv.Episodes
                    .Where(e => e.SeasonNumber > 0)
                    .Count(e => e.VideoFiles.Any(v => v.Folder != null)),
                CertificationRating = gt.Tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = gt.Tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return (genreDetail, movies, tvShows);
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

    public Task<List<MusicGenreCardDto>> GetMusicGenreCardsAsync(Guid userId, CancellationToken ct = default)
    {
        return context.MusicGenres
            .AsNoTracking()
            .Where(genre =>
                genre.AlbumMusicGenres.Any(g => g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)) ||
                genre.ArtistMusicGenres.Any(g => g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Where(genre => genre.MusicGenreTracks.Any())
            .OrderBy(genre => genre.Name)
            .Select(genre => new MusicGenreCardDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TrackCount = genre.MusicGenreTracks.Count()
            })
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

    public Task<List<MusicGenreCardDto>> GetPaginatedMusicGenreCardsAsync(Guid userId, string letter, int take, int page, CancellationToken ct = default)
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
            .OrderBy(genre => genre.Name)
            .Skip(page * take)
            .Take(take)
            .Select(genre => new MusicGenreCardDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TrackCount = genre.MusicGenreTracks.Count()
            })
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
