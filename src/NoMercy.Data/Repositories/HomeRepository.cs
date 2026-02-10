using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Information;

namespace NoMercy.Data.Repositories;

public class HomeRepository
{
    public async Task<List<Tv>> GetHomeTvs(MediaContext mediaContext, List<int> tvIds, string? language, string country, CancellationToken ct = default)
    {
        return await mediaContext.Tvs
            .AsNoTracking()
            .Where(tv => tvIds.Contains(tv.Id))
            .Where(tv => tv.Episodes.Any(e => e.VideoFiles.Count > 0))
            .Include(tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(tv => tv.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en").Take(1))
            .Include(tv => tv.Media.Where(m => m.Site == "YouTube").Take(3))
            .Include(tv => tv.Episodes.Where(e => e.SeasonNumber > 0))
                .ThenInclude(e => e.VideoFiles)
            .Include(tv => tv.CertificationTvs
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                .Take(1))
                .ThenInclude(c => c.Certification)
            .ToListAsync(ct);
    }

    public async Task<List<Movie>> GetHomeMovies(MediaContext mediaContext, List<int> movieIds, string? language, string country, CancellationToken ct = default)
    {
        return await mediaContext.Movies
            .AsNoTracking()
            .Where(movie => movieIds.Contains(movie.Id))
            .Where(movie => movie.VideoFiles.Count > 0)
            .Include(movie => movie.Translations.Where(t => t.Iso6391 == language))
            .Include(movie => movie.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en").Take(1))
            .Include(movie => movie.Media.Where(m => m.Site == "YouTube").Take(3))
            .Include(movie => movie.VideoFiles)
            .Include(movie => movie.CertificationMovies
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                .Take(1))
                .ThenInclude(c => c.Certification)
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
            .Where(library => library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(library => library.LibraryUsers)
            .Include(library => library.FolderLibraries)
                .ThenInclude(fl => fl.Folder)
                .ThenInclude(f => f.EncoderProfileFolder)
                .ThenInclude(epf => epf.EncoderProfile)
            .Include(library => library.LanguageLibraries)
                .ThenInclude(ll => ll.Language)
            .Include(library => library.LibraryMovies)
            .Include(library => library.LibraryTvs)
            .ToListAsync(ct);
    }

    public Task<int> GetAnimeCountAsync(MediaContext mediaContext, Guid userId, CancellationToken ct = default)
    {
        return mediaContext.Tvs
            .AsNoTracking()
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .CountAsync(tv => tv.Library.Type == Config.AnimeMediaType, ct);
    }

    public Task<int> GetMovieCountAsync(MediaContext mediaContext, Guid userId, CancellationToken ct = default)
    {
        return mediaContext.Movies
            .AsNoTracking()
            .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .CountAsync(movie => movie.Library.Type == Config.MovieMediaType, ct);
    }

    public Task<int> GetTvCountAsync(MediaContext mediaContext, Guid userId, CancellationToken ct = default)
    {
        return mediaContext.Tvs
            .AsNoTracking()
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .CountAsync(tv => tv.Library.Type == Config.TvMediaType, ct);
    }

    public async Task<List<Genre>> GetHomeGenresAsync(MediaContext mediaContext, Guid userId, string? language, int take, int page = 0, CancellationToken ct = default)
    {
        return await mediaContext.Genres
            .AsNoTracking()
            .Where(genre =>
                genre.GenreMovies.Any(g => g.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)) ||
                genre.GenreTvShows.Any(g => g.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)))
            .Include(genre => genre.Translations.Where(t => t.Iso6391 == language))
            .Include(genre => genre.GenreMovies.Where(gm => gm.Movie.VideoFiles.Count > 0))
            .Include(genre => genre.GenreTvShows.Where(gt => gt.Tv.Episodes.Any(e => e.VideoFiles.Count > 0)))
            .OrderBy(genre => genre.Name)
            .Skip(page * take)
            .Take(take)
            .ToListAsync(ct);
    }
}
