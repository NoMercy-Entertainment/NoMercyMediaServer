using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;

namespace NoMercy.Data.Repositories;

public class GenreHomeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TranslatedName { get; set; }
    public List<int> MovieIds { get; set; } = [];
    public List<int> TvIds { get; set; } = [];
}

public class HomeMovieCardDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSort { get; set; } = string.Empty;
    public string? TranslatedTitle { get; set; }
    public string? Overview { get; set; }
    public string? TranslatedOverview { get; set; }
    public string? Poster { get; set; }
    public string? Backdrop { get; set; }
    public string? Logo { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ColorPalette { get; set; }
    public int VideoFileCount { get; set; }
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
}

public class HomeTvCardDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSort { get; set; } = string.Empty;
    public string? TranslatedTitle { get; set; }
    public string? Overview { get; set; }
    public string? TranslatedOverview { get; set; }
    public string? Poster { get; set; }
    public string? Backdrop { get; set; }
    public string? Logo { get; set; }
    public DateTime? FirstAirDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ColorPalette { get; set; }
    public int NumberOfEpisodes { get; set; }
    public int EpisodesWithVideo { get; set; }
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
}

public class HomeRepository
{
    public async Task<List<HomeTvCardDto>> GetHomeTvs(MediaContext mediaContext, List<int> tvIds, string? language, string country, CancellationToken ct = default)
    {
        return await mediaContext.Tvs
            .AsNoTracking()
            .Where(tv => tvIds.Contains(tv.Id))
            .Where(tv => tv.Episodes.Any(e => e.VideoFiles.Any()))
            .Select(tv => new HomeTvCardDto
            {
                Id = tv.Id,
                Title = tv.Title,
                TitleSort = tv.TitleSort,
                TranslatedTitle = tv.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = tv.Overview,
                TranslatedOverview = tv.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = tv.Poster,
                Backdrop = tv.Backdrop,
                Logo = tv.Images
                    .Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                FirstAirDate = tv.FirstAirDate,
                CreatedAt = tv.CreatedAt,
                ColorPalette = tv._colorPalette,
                NumberOfEpisodes = tv.NumberOfEpisodes,
                EpisodesWithVideo = tv.Episodes
                    .Where(e => e.SeasonNumber > 0)
                    .Count(e => e.VideoFiles.Any(v => v.Folder != null)),
                CertificationRating = tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public async Task<List<HomeMovieCardDto>> GetHomeMovies(MediaContext mediaContext, List<int> movieIds, string? language, string country, CancellationToken ct = default)
    {
        return await mediaContext.Movies
            .AsNoTracking()
            .Where(movie => movieIds.Contains(movie.Id))
            .Where(movie => movie.VideoFiles.Any())
            .Select(movie => new HomeMovieCardDto
            {
                Id = movie.Id,
                Title = movie.Title,
                TitleSort = movie.TitleSort,
                TranslatedTitle = movie.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = movie.Overview,
                TranslatedOverview = movie.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = movie.Poster,
                Backdrop = movie.Backdrop,
                Logo = movie.Images
                    .Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                ReleaseDate = movie.ReleaseDate,
                CreatedAt = movie.CreatedAt,
                ColorPalette = movie._colorPalette,
                VideoFileCount = movie.VideoFiles.Count(v => v.Folder != null),
                CertificationRating = movie.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = movie.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public async Task<HashSet<UserData>> GetContinueWatchingAsync(MediaContext mediaContext, Guid userId, string language, string country, CancellationToken ct = default)
    {
        // Step 1: Project to minimal keys, deduplicate, and get unique UserData IDs
        // This avoids loading full entity trees for duplicates that get thrown away
        List<Ulid> uniqueIds = await mediaContext.UserData
            .AsNoTracking()
            .Where(ud => ud.UserId == userId)
            .Where(ud => ud.MovieId != null || ud.TvId != null || ud.CollectionId != null || ud.SpecialId != null)
            .OrderByDescending(ud => ud.LastPlayedDate)
            .Select(ud => new { ud.Id, ud.MovieId, ud.CollectionId, ud.TvId, ud.SpecialId })
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .DistinctBy(ud => new { ud.MovieId, ud.CollectionId, ud.TvId, ud.SpecialId })
                .Select(ud => ud.Id)
                .ToList(), ct);

        if (uniqueIds.Count == 0)
            return [];

        // Step 2: Hydrate only the unique entries with all includes
        List<UserData> userData = await mediaContext.UserData
            .AsNoTracking()
            .AsSplitQuery()
            .Where(ud => uniqueIds.Contains(ud.Id))
            .Include(ud => ud.VideoFile)
            // Movie includes - only what CardData needs
            .Include(ud => ud.Movie)
                .ThenInclude(m => m!.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en").Take(1))
            .Include(ud => ud.Movie)
                .ThenInclude(m => m!.VideoFiles)
            .Include(ud => ud.Movie)
                .ThenInclude(m => m!.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            // Tv includes - only what CardData needs
            .Include(ud => ud.Tv)
                .ThenInclude(tv => tv!.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en").Take(1))
            .Include(ud => ud.Tv)
                .ThenInclude(tv => tv!.Episodes.Where(e => e.SeasonNumber > 0))
                .ThenInclude(e => e.VideoFiles)
            .Include(ud => ud.Tv)
                .ThenInclude(tv => tv!.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            // Collection includes - only what CardData needs
            .Include(ud => ud.Collection)
                .ThenInclude(c => c!.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en").Take(1))
            .Include(ud => ud.Collection)
                .ThenInclude(c => c!.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.VideoFiles)
            .Include(ud => ud.Collection)
                .ThenInclude(c => c!.CollectionMovies)
                .ThenInclude(cm => cm.Movie)
                .ThenInclude(m => m.CertificationMovies
                    .Where(cert => cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            // Special includes - only what CardData needs
            .Include(ud => ud.Special)
                .ThenInclude(s => s!.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.VideoFiles)
            .Include(ud => ud.Special)
                .ThenInclude(s => s!.Items)
                .ThenInclude(item => item.Movie)
                .ThenInclude(m => m!.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            .Include(ud => ud.Special)
                .ThenInclude(s => s!.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.VideoFiles)
            .Include(ud => ud.Special)
                .ThenInclude(s => s!.Items)
                .ThenInclude(item => item.Episode)
                .ThenInclude(e => e!.Tv)
                .ThenInclude(tv => tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            .OrderByDescending(ud => ud.LastPlayedDate)
            .ToListAsync(ct);

        return userData.ToHashSet();
    }

    public Task<HashSet<Image>> GetScreensaverImagesAsync(MediaContext mediaContext, Guid userId, CancellationToken ct = default)
    {
        return mediaContext.Images
            .AsNoTracking()
            .Where(image => image.Movie!.Library.LibraryUsers.Any(u => u.UserId == userId) ||
                            image.Tv!.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(image => image._colorPalette != "")
            .Where(image =>
                (image.Type == "backdrop" && (image.Iso6391 == null || image.Iso6391 == "") && image.Height >= 1080) ||
                (image.Type == "logo" && image.Iso6391 == "en"))
            .OrderByDescending(image => image.Width)
            .ToHashSetAsync(ct);
    }

    public Task<List<Library>> GetLibrariesAsync(MediaContext mediaContext, Guid userId, CancellationToken ct = default)
    {
        return mediaContext.Libraries
            .AsNoTracking()
            .ForUser(userId)
            .ToListAsync(ct);
    }

    public Task<int> GetAnimeCountAsync(MediaContext mediaContext, Guid userId, CancellationToken ct = default)
    {
        return mediaContext.Tvs
            .AsNoTracking()
            .ForUser(userId)
            .CountAsync(tv => tv.Library.Type == Config.AnimeMediaType, ct);
    }

    public Task<int> GetMovieCountAsync(MediaContext mediaContext, Guid userId, CancellationToken ct = default)
    {
        return mediaContext.Movies
            .AsNoTracking()
            .ForUser(userId)
            .CountAsync(movie => movie.Library.Type == Config.MovieMediaType, ct);
    }

    public Task<int> GetTvCountAsync(MediaContext mediaContext, Guid userId, CancellationToken ct = default)
    {
        return mediaContext.Tvs
            .AsNoTracking()
            .ForUser(userId)
            .CountAsync(tv => tv.Library.Type == Config.TvMediaType, ct);
    }

    public async Task<List<GenreHomeDto>> GetHomeGenresAsync(MediaContext mediaContext, Guid userId, string? language, int take, int page = 0, CancellationToken ct = default)
    {
        return await mediaContext.Genres
            .AsNoTracking()
            .Where(genre =>
                genre.GenreMovies.Any(g => g.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)) ||
                genre.GenreTvShows.Any(g => g.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .OrderBy(genre => genre.Name)
            .Skip(page * take)
            .Take(take)
            .Select(genre => new GenreHomeDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TranslatedName = genre.Translations
                    .Where(t => t.Iso6391 == language)
                    .Select(t => t.Name)
                    .FirstOrDefault(),
                MovieIds = genre.GenreMovies
                    .Where(gm => gm.Movie.VideoFiles.Any())
                    .Select(gm => gm.MovieId)
                    .ToList(),
                TvIds = genre.GenreTvShows
                    .Where(gt => gt.Tv.Episodes.Any(e => e.VideoFiles.Any()))
                    .Select(gt => gt.TvId)
                    .ToList()
            })
            .ToListAsync(ct);
    }
}
