using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

// Lightweight DTOs for library card display - only what's needed for NmCardDto
public class MovieCardDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSort { get; set; } = string.Empty;
    public string? Overview { get; set; }
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

public class TvCardDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSort { get; set; } = string.Empty;
    public string? Overview { get; set; }
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

public class LibraryRepository(MediaContext context)
{
    private static readonly string[] Letters = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    public Task<List<Library>> GetLibraries(Guid userId)
    {
        return context.Libraries
            .AsNoTracking()
            .Where(library => library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(library => library.FolderLibraries)
                .ThenInclude(fl => fl.Folder)
                .ThenInclude(f => f.EncoderProfileFolder)
                .ThenInclude(epf => epf.EncoderProfile)
            .Include(library => library.LanguageLibraries)
                .ThenInclude(ll => ll.Language)
            .Include(library => library.LibraryMovies)
            .Include(library => library.LibraryTvs)
            .OrderBy(library => library.Order)
            .ToListAsync();
    }

    public Task<Library?> GetLibraryByIdAsync(Ulid libraryId, Guid userId, string language, string country, int take, int page)
    {
        return context.Libraries
            .AsNoTracking()
            .Where(library => library.Id == libraryId)
            .Where(library => library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(library => library.LibraryMovies
                .Where(lm => lm.Movie.VideoFiles.Any(v => v.Folder != null))
                .Take(take))
                .ThenInclude(lm => lm.Movie)
                .ThenInclude(m => m.Translations.Where(t => t.Iso6391 == language))
            .Include(library => library.LibraryMovies)
                .ThenInclude(lm => lm.Movie)
                .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
            .Include(library => library.LibraryMovies)
                .ThenInclude(lm => lm.Movie)
                .ThenInclude(m => m.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").Take(1))
            .Include(library => library.LibraryMovies)
                .ThenInclude(lm => lm.Movie)
                .ThenInclude(m => m.CertificationMovies
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            .Include(library => library.LibraryTvs
                .Where(lt => lt.Tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
                .Take(take))
                .ThenInclude(lt => lt.Tv)
                .ThenInclude(tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(library => library.LibraryTvs)
                .ThenInclude(lt => lt.Tv)
                .ThenInclude(tv => tv.Episodes.Where(e => e.SeasonNumber > 0 && e.VideoFiles.Any(v => v.Folder != null)))
                .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
            .Include(library => library.LibraryTvs)
                .ThenInclude(lt => lt.Tv)
                .ThenInclude(tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").Take(1))
            .Include(library => library.LibraryTvs)
                .ThenInclude(lt => lt.Tv)
                .ThenInclude(tv => tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Take(1))
                .ThenInclude(c => c.Certification)
            .FirstOrDefaultAsync();
    }

    public readonly Func<MediaContext, Guid, Ulid, string, int, int, Expression<Func<Movie, object>>?, string?, IAsyncEnumerable<Movie>> GetLibraryMovies =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid libraryId, string language, int take, int skip, Expression<Func<Movie, object>>? orderByExpression, string? direction) =>
            context.Movies.AsNoTracking()
                .Where(movie => movie.Library.Id == libraryId)
                .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Where(libraryMovie => libraryMovie.VideoFiles.Count > 0)
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
                .Include(movie => movie.KeywordMovies)
                .ThenInclude(keywordMovie => keywordMovie.Keyword)
                .Include(movie => movie.CertificationMovies)
                .ThenInclude(certificationMovie => certificationMovie.Certification)
                .OrderByDescending(movie => movie.CreatedAt)
                .Skip(skip)
                .Take(take));
    
    // public async Task<List<Movie>> GetLibraryMovies(Guid userId, Ulid libraryId, string language, int take, int page)
    // {
    //     // First get movie IDs with pagination (no filtered includes)
    //     List<int> movieIds = await context.Movies
    //         .AsNoTracking()
    //         .Where(movie => movie.Library.Id == libraryId)
    //         .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Where(movie => movie.VideoFiles.Any(v => v.Folder != null))
    //         .OrderBy(movie => movie.TitleSort)
    //         .Skip(page * take)
    //         .Take(take)
    //         .Select(movie => movie.Id)
    //         .ToListAsync();
    //
    //     if (movieIds.Count == 0)
    //         return [];
    //
    //     // Then fetch full data with filtered includes (no Skip/Take)
    //     return await context.Movies
    //         .AsNoTracking()
    //         .Where(movie => movieIds.Contains(movie.Id))
    //         .Include(movie => movie.VideoFiles.Where(v => v.Folder != null))
    //         .Include(movie => movie.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").Take(1))
    //         .Include(movie => movie.Translations.Where(t => t.Iso6391 == language))
    //         .Include(movie => movie.CertificationMovies.Take(1))
    //             .ThenInclude(c => c.Certification)
    //         .ToListAsync();
    // }

    public readonly Func<MediaContext, Guid, Ulid, string, int, int, Expression<Func<Tv, object>>?, string?, IAsyncEnumerable<Tv>> GetLibraryShows =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid libraryId, string language, int take, int skip, Expression<Func<Tv, object>>? orderByExpression, string? direction) 
            => mediaContext.Tvs.AsNoTracking()
                .Where(tv => tv.Library.Id == libraryId)
                .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Where(libraryTv => libraryTv.Episodes
                    .Any(episode => episode.VideoFiles.Count > 0))
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
                .Include(tv => tv.KeywordTvs)
                .ThenInclude(keywordTv => keywordTv.Keyword)
                .Include(tv => tv.CertificationTvs)
                .ThenInclude(certificationTv => certificationTv.Certification)
                .OrderByDescending(tv => tv.CreatedAt)
                .Skip(skip)
                .Take(take));

    // public async Task<List<Tv>> GetLibraryShows(Guid userId, Ulid libraryId, string language, int take, int page)
    // {
    //     // First get TV IDs with pagination (no filtered includes)
    //     List<int> tvIds = await context.Tvs
    //         .AsNoTracking()
    //         .Where(tv => tv.Library.Id == libraryId)
    //         .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Where(tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
    //         .OrderBy(tv => tv.TitleSort)
    //         .Skip(page * take)
    //         .Take(take)
    //         .Select(tv => tv.Id)
    //         .ToListAsync();
    //
    //     if (tvIds.Count == 0)
    //         return [];
    //
    //     // Then fetch full data with filtered includes (no Skip/Take)
    //     return await context.Tvs
    //         .AsNoTracking()
    //         .Where(tv => tvIds.Contains(tv.Id))
    //         .Include(tv => tv.Episodes.Where(e => e.SeasonNumber > 0 && e.VideoFiles.Any(v => v.Folder != null)))
    //             .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
    //         .Include(tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").Take(1))
    //         .Include(tv => tv.Translations.Where(t => t.Iso6391 == language))
    //         .Include(tv => tv.CertificationTvs.Take(1))
    //             .ThenInclude(c => c.Certification)
    //         .ToListAsync();
    // }

    // Optimized query using projection - only fetches what NmCardDto needs
    public Task<List<MovieCardDto>> GetLibraryMovieCardsAsync(Guid userId, Ulid libraryId, string country, int take, int skip)
    {
        return context.Movies
            .AsNoTracking()
            .Where(movie => movie.Library.Id == libraryId)
            .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(movie => movie.VideoFiles.Any(v => v.Folder != null))
            .Include(tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en"))
            .OrderByDescending(movie => movie.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(movie => new MovieCardDto
            {
                Id = movie.Id,
                Title = movie.Title,
                TitleSort = movie.TitleSort,
                Overview = movie.Overview,
                Poster = movie.Poster,
                Backdrop = movie.Backdrop,
                Logo = movie.Images.Select(i => i.FilePath).FirstOrDefault(),
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
            .ToListAsync();
    }

    // Optimized query using projection - only fetches what NmCardDto needs
    public Task<List<TvCardDto>> GetLibraryTvCardsAsync(Guid userId, Ulid libraryId, string country, int take, int skip)
    {
        return context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Library.Id == libraryId)
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
            .Include(tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en"))
            .OrderByDescending(tv => tv.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(tv => new TvCardDto
            {
                Id = tv.Id,
                Title = tv.Title,
                TitleSort = tv.TitleSort,
                Overview = tv.Overview,
                Poster = tv.Poster,
                Backdrop = tv.Backdrop,
                Logo = tv.Images.Select(i => i.FilePath).FirstOrDefault(),
                FirstAirDate = tv.FirstAirDate,
                CreatedAt = tv.CreatedAt,
                ColorPalette = tv._colorPalette,
                NumberOfEpisodes = tv.NumberOfEpisodes,
                EpisodesWithVideo = tv.Episodes
                    .Where(e=> e.SeasonNumber > 0)
                    .Count(e => e.VideoFiles
                        .Any(v => v.Folder != null)),
                CertificationRating = tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = tv.CertificationTvs
                    .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    public Task<List<Movie>> GetPaginatedLibraryMovies(Guid userId, Ulid libraryId, string letter, string language,
        string country, int take, int page)
    {
        return context.Movies
            .AsNoTracking()
            .Where(movie => movie.Library.Id == libraryId)
            .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(movie => movie.VideoFiles.Any(v => v.Folder != null))
            .Where(movie => (letter == "_" || letter == "#")
                ? Letters.Any(p => movie.TitleSort.StartsWith(p.ToLower()))
                : movie.TitleSort.StartsWith(letter.ToLower()))
            .Include(movie => movie.Translations.Where(t => t.Iso6391 == language))
            .Include(movie => movie.VideoFiles.Where(v => v.Folder != null))
            .Include(movie => movie.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").Take(1))
            .Include(movie => movie.CertificationMovies
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                .Take(1))
                .ThenInclude(c => c.Certification)
            .OrderBy(movie => movie.TitleSort)
            .Skip(page * take)
            .Take(take)
            .ToListAsync();
    }

    public Task<List<Tv>> GetPaginatedLibraryShows(Guid userId, Ulid libraryId, string letter, string language,
        string country, int take, int page, Expression<Func<Tv, object>>? orderByExpression = null, string? direction = null)
    {
        return context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Library.Id == libraryId)
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
            .Where(tv => (letter == "_" || letter == "#")
                ? Letters.Any(p => tv.TitleSort.StartsWith(p.ToLower()))
                : tv.TitleSort.StartsWith(letter.ToLower()))
            .Include(tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(tv => tv.Episodes.Where(e => e.SeasonNumber > 0 && e.VideoFiles.Any(v => v.Folder != null)))
                .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
            .Include(tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").Take(1))
            .Include(tv => tv.CertificationTvs
                .Where(c => c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country)
                .Take(1))
                .ThenInclude(c => c.Certification)
            .OrderBy(tv => tv.TitleSort)
            .Skip(page * take)
            .Take(take)
            .ToListAsync();
    }

    public Task<Library?> GetLibraryByIdAsync(Ulid id)
    {
        return context.Libraries
            .AsNoTracking()
            .Include(library => library.LanguageLibraries)
            .Include(library => library.FolderLibraries)
                .ThenInclude(fl => fl.Folder)
            .Include(library => library.LibraryMovies)
            .Include(library => library.LibraryTvs)
            .FirstOrDefaultAsync(library => library.Id == id);
    }

    public Task<List<Library>> GetAllLibrariesAsync()
    {
        return context.Libraries
            .AsNoTracking()
            .Include(library => library.FolderLibraries)
                .ThenInclude(fl => fl.Folder)
            .Include(library => library.LibraryMovies)
            .Include(library => library.LibraryTvs)
            .ToListAsync();
    }

    public Task<List<FolderDto>> GetFoldersAsync()
    {
        return context.Folders
            .AsNoTracking()
            .Select(f => new FolderDto(f))
            .ToListAsync();
    }

    // public Task<Tv?> GetRandomTvShow(Guid userId, string language)
    // {
    //     return context.Tvs
    //         .AsNoTracking()
    //         .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Where(tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
    //         .Include(tv => tv.Translations.Where(t => t.Iso6391 == language))
    //         .Include(tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").Take(1))
    //         .Include(tv => tv.Episodes.Where(e => e.SeasonNumber > 0 && e.VideoFiles.Any(v => v.Folder != null)))
    //             .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
    //         .Include(tv => tv.CertificationTvs.Take(1))
    //             .ThenInclude(c => c.Certification)
    //         .OrderBy(tv => EF.Functions.Random())
    //         .FirstOrDefaultAsync();
    // }

    private static readonly Func<MediaContext, Guid, string, Task<Tv?>> GetRandomTvShowQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, string language) =>
            mediaContext.Tvs.AsNoTracking()
                .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
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
                .OrderBy(tv => EF.Functions.Random())
                .FirstOrDefault());
    
    public async Task<Tv?> GetRandomTvShow(Guid userId, string language)
    {
        return await GetRandomTvShowQuery(context, userId, language);
    }

    private static readonly Func<MediaContext, Guid, string, Task<Movie?>> GetRandomMovieQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, string language) =>
            mediaContext.Movies.AsNoTracking()
                .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
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
                .OrderBy(movie => EF.Functions.Random())
                .FirstOrDefault());

    public async Task<Movie?> GetRandomMovie(Guid userId, string language)
    {
        return await GetRandomMovieQuery(context, userId, language);
    }

    // public Task<Movie?> GetRandomMovie(Guid userId, string language)
    // {
    //     return context.Movies
    //         .AsNoTracking()
    //         .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Where(movie => movie.VideoFiles.Any(v => v.Folder != null))
    //         .Include(movie => movie.Translations.Where(t => t.Iso6391 == language))
    //         .Include(movie => movie.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").Take(1))
    //         .Include(movie => movie.VideoFiles.Where(v => v.Folder != null))
    //         .Include(movie => movie.CertificationMovies.Take(1))
    //             .ThenInclude(c => c.Certification)
    //         .OrderBy(movie => EF.Functions.Random())
    //         .FirstOrDefaultAsync();
    // }

    #region CRUD Operations

    public async Task AddLibraryAsync(Library library, Guid userId)
    {
        await context.Libraries.Upsert(library)
            .On(l => new { l.Id })
            .WhenMatched((ls, li) => new()
            {
                Title = li.Title,
                AutoRefreshInterval = li.AutoRefreshInterval,
                ChapterImages = li.ChapterImages,
                ExtractChapters = li.ExtractChapters,
                ExtractChaptersDuring = li.ExtractChaptersDuring,
                PerfectSubtitleMatch = li.PerfectSubtitleMatch,
                Realtime = li.Realtime,
                SpecialSeasonName = li.SpecialSeasonName,
                Type = li.Type,
                Order = li.Order
            })
            .RunAsync();

        await context.LibraryUser.Upsert(new()
            {
                LibraryId = library.Id,
                UserId = userId
            })
            .On(lu => new { lu.LibraryId, lu.UserId })
            .WhenMatched((lus, lui) => new()
            {
                LibraryId = lui.LibraryId,
                UserId = lui.UserId
            })
            .RunAsync();
    }

    public Task UpdateLibraryAsync(Library library)
    {
        context.Libraries.Update(library);
        return context.SaveChangesAsync();
    }

    public Task DeleteLibraryAsync(Library library)
    {
        context.Libraries.Remove(library);
        return context.SaveChangesAsync();
    }

    public Task<int> AddEncoderProfileFolderAsync(EncoderProfileFolder encoderProfileFolder)
    {
        return context.EncoderProfileFolder.Upsert(encoderProfileFolder)
            .On(epf => new { epf.FolderId, epf.EncoderProfileId })
            .WhenMatched((source, input) => new()
            {
                FolderId = input.FolderId,
                EncoderProfileId = input.EncoderProfileId
            })
            .RunAsync();
    }

    public Task<int> AddEncoderProfileFolderAsync(List<EncoderProfileFolder> encoderProfileFolders)
    {
        return context.EncoderProfileFolder.UpsertRange(encoderProfileFolders)
            .On(epl => new { epl.FolderId, epl.EncoderProfileId })
            .WhenMatched((epls, epli) => new()
            {
                FolderId = epli.FolderId,
                EncoderProfileId = epli.EncoderProfileId
            })
            .RunAsync();
    }

    public Task<int> AddEncoderProfileFolderAsync(EncoderProfileFolder[] encoderProfileFolders)
    {
        return context.EncoderProfileFolder.UpsertRange(encoderProfileFolders)
            .On(epf => new { epf.FolderId, epf.EncoderProfileId })
            .WhenMatched((source, input) => new()
            {
                FolderId = input.FolderId,
                EncoderProfileId = input.EncoderProfileId
            })
            .RunAsync();
    }

    public Task<int> AddLanguageLibraryAsync(LanguageLibrary[] languageLibraries)
    {
        return context.LanguageLibrary.UpsertRange(languageLibraries)
            .On(ll => new { ll.LibraryId, ll.LanguageId })
            .WhenMatched((lls, lli) => new()
            {
                LibraryId = lli.LibraryId,
                LanguageId = lli.LanguageId
            })
            .RunAsync();
    }

    public Task SaveChangesAsync()
    {
        return context.SaveChangesAsync();
    }

    #endregion

    public async Task<int> SyncEncoderProfileFolderAsync(List<EncoderProfileFolder> encoderProfileFolders, List<Folder> folders)
    {
        await context.EncoderProfileFolder
            .Where(epf => folders.Select(f => f.Id).Contains(epf.FolderId))
            .ExecuteDeleteAsync();

        return await AddEncoderProfileFolderAsync(encoderProfileFolders);
    }
}
