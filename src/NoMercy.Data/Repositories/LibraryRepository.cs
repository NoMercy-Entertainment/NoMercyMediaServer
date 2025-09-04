using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class LibraryRepository(MediaContext context)
{
    #region Compiled Queries

    private static readonly Func<MediaContext, Guid, IAsyncEnumerable<Library>> GetLibrariesQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId) =>
            mediaContext.Libraries.AsNoTracking()
                .Where(library => library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null
                )
                .Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
                .ThenInclude(folder => folder.EncoderProfileFolder)
                .ThenInclude(library => library.EncoderProfile)
                .Include(library => library.LanguageLibraries)
                .ThenInclude(languageLibrary => languageLibrary.Language)
                .Include(library => library.LibraryMovies)
                .Include(library => library.LibraryTvs));

    private static readonly Func<MediaContext, Ulid, Guid, string, Task<Library?>> GetLibraryByIdAsyncQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext, Ulid libraryId, Guid userId, string language) =>
            mediaContext.Libraries.AsNoTracking()
                .Where(library => library.Id == libraryId)
                .Where(library => library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null
                )
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
                .FirstOrDefault());

    private static readonly Func<MediaContext, Guid, Ulid, string, int, int, IAsyncEnumerable<Movie>> GetLibraryMoviesQuery =
            EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid libraryId, string language, int skip, int take) =>
                mediaContext.Movies.AsNoTracking()
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
                    .Include(movie => movie.KeywordMovies)
                    .ThenInclude(keywordMovie => keywordMovie.Keyword)
                    .Include(movie => movie.CertificationMovies)
                    .ThenInclude(certificationMovie => certificationMovie.Certification)
                    .OrderBy(movie => movie.TitleSort)
                    .Skip(skip)
                    .Take(take));

    private static readonly Func<MediaContext, Guid, Ulid, string, int, int, IAsyncEnumerable<Tv>> GetLibraryShowsQuery =
            EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid libraryId, string language, int skip, int take) 
                => mediaContext.Tvs.AsNoTracking()
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
                    .Include(tv => tv.KeywordTvs)
                    .ThenInclude(keywordTv => keywordTv.Keyword)
                    .Include(tv => tv.CertificationTvs)
                    .ThenInclude(certificationTv => certificationTv.Certification)
                    .OrderBy(tv => tv.TitleSort)
                    .Skip(skip)
                    .Take(take));

    private static readonly string[] Letters = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    private static readonly Func<MediaContext, Guid, Ulid, string, string, int, int, IAsyncEnumerable<Movie>> GetPaginatedLibraryMoviesQuery =
            EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid libraryId, string letter, string language, int skip, int take) =>
                mediaContext.Movies.AsNoTracking()
                    .Where(movie => movie.Library.Id == libraryId)
                    .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                    .Where(libraryMovie => libraryMovie.VideoFiles
                        .Any(videoFile => videoFile.Folder != null) == true
                    )
                    .Where(movie => letter == "_"
                        ? Letters.Any(p => movie.TitleSort.StartsWith(p.ToLower()))
                        : movie.TitleSort.StartsWith(letter.ToLower())
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
                    .Include(movie => movie.KeywordMovies)
                    .ThenInclude(keywordMovie => keywordMovie.Keyword)
                    .Include(movie => movie.CertificationMovies)
                    .ThenInclude(certificationMovie => certificationMovie.Certification)
                    .OrderBy(movie => movie.TitleSort)
                    .Skip(skip)
                    .Take(take));

    private static readonly Func<MediaContext, Guid, Ulid, string, string, int, int, IAsyncEnumerable<Tv>> GetPaginatedLibraryShowsQuery =
            EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid libraryId, string letter, string language, int skip, int take) =>
                mediaContext.Tvs.AsNoTracking()
                    .Where(tv => tv.Library.Id == libraryId)
                    .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                    .Where(libraryTv => libraryTv.Episodes
                        .Any(episode => episode.VideoFiles
                            .Any(videoFile => videoFile.Folder != null) == true
                        ) == true)
                    .Where(tv => letter == "_"
                        ? Letters.Any(p => tv.TitleSort.StartsWith(p.ToLower()))
                        : tv.TitleSort.StartsWith(letter.ToLower())
                    )
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
                    .OrderBy(tv => tv.TitleSort)
                    .Skip(skip)
                    .Take(take));

    private static readonly Func<MediaContext, Ulid, Task<Library?>> GetLibraryByIdAsyncSimpleQuery = 
        (mediaContext, id) => mediaContext.Libraries.AsNoTracking()
                .Include(library => library.LanguageLibraries)
                .Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
                .Include(library => library.LibraryMovies)
                .Include(library => library.LibraryTvs)
                .FirstOrDefaultAsync(library => library.Id == id);

    private static readonly Func<MediaContext, Task<List<Library>>> GetAllLibrariesAsyncQuery = 
        (mediaContext) => mediaContext.Libraries.AsNoTracking()
                .Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
                .Include(library => library.LibraryMovies)
                .Include(library => library.LibraryTvs)
                .ToListAsync();

    private static readonly Func<MediaContext, Task<List<FolderDto>>> GetFoldersAsyncQuery =
        EF.CompileAsyncQuery((MediaContext mediaContext) => mediaContext.Folders.AsNoTracking()
            .Select(folder => new FolderDto(folder))
            .ToList());

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

    #endregion

    #region Public Methods

    public async Task<List<Library>> GetLibraries(Guid userId)
    {
        return await context.Libraries
            .AsNoTracking()
            .Where(library => library.LibraryUsers
                .FirstOrDefault(u => u.UserId.Equals(userId)) != null
            )
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .ThenInclude(folder => folder.EncoderProfileFolder)
            .ThenInclude(library => library.EncoderProfile)
            .Include(library => library.LanguageLibraries)
            .ThenInclude(languageLibrary => languageLibrary.Language)
            .Include(library => library.LibraryMovies)
            .Include(library => library.LibraryTvs)
            .OrderBy(library => library.Order)
            .ToListAsync();
    }

    public Task<Library?> GetLibraryByIdAsync(Ulid libraryId, Guid userId, string language, int take, int page)
    {
        return GetLibraryByIdAsyncQuery(context, libraryId, userId, language);
    }

    public async Task<IEnumerable<Movie>> GetLibraryMovies(Guid userId, Ulid libraryId, string language, int take,
        int page, Expression<Func<Movie, object>>? orderByExpression = null, string? direction = null)
    {
        IQueryable<Movie> x = context.Movies.AsNoTracking()
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
            .Include(movie => movie.KeywordMovies)
            .ThenInclude(keywordMovie => keywordMovie.Keyword)
            .Include(movie => movie.CertificationMovies)
            .ThenInclude(certificationMovie => certificationMovie.Certification);
            
        if (orderByExpression is not null && direction == "desc")
            return x.OrderByDescending(orderByExpression)
                .Skip(page * take)
                .Take(take);
        if (orderByExpression is not null)
            return x.OrderBy(orderByExpression)
                .Skip(page * take)
                .Take(take);

        return x.OrderBy(special => special.TitleSort)
            .Skip(page * take)
            .Take(take);
    }

    public async Task<IEnumerable<Tv>> GetLibraryShows(Guid userId, Ulid libraryId, string language, int take, 
        int page, Expression<Func<Tv, object>>? orderByExpression = null, string? direction = null)
    {
        IQueryable<Tv> x = context.Tvs.AsNoTracking()
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
            .Include(tv => tv.KeywordTvs)
            .ThenInclude(keywordTv => keywordTv.Keyword)
            .Include(tv => tv.CertificationTvs)
            .ThenInclude(certificationTv => certificationTv.Certification)
            .OrderBy(tv => tv.TitleSort);
            
            if (orderByExpression is not null && direction == "desc")
                return x.OrderByDescending(orderByExpression)
                    .Skip(page * take)
                    .Take(take);
            if (orderByExpression is not null)
                return x.OrderBy(orderByExpression)
                    .Skip(page * take)
                    .Take(take);

            return x.OrderBy(special => special.TitleSort)
                .Skip(page * take)
                .Take(take);
        }
    

    public async Task<IEnumerable<Movie>> GetPaginatedLibraryMovies(Guid userId, Ulid libraryId, string letter, string language, 
        int take, int page, Expression<Func<Movie, object>>? orderByExpression = null, string? direction = null)
    {
        List<Movie> movies = [];
        await foreach (Movie movie in GetPaginatedLibraryMoviesQuery(context, userId, libraryId, letter, language,
                           page * take, take)) movies.Add(movie);
        return movies;
    }

    public async Task<IEnumerable<Tv>> GetPaginatedLibraryShows(Guid userId, Ulid libraryId, string letter, string language,
        int take, int page, Expression<Func<Tv, object>>? orderByExpression = null, string? direction = null)
    {
        List<Tv> shows = [];
        await foreach (Tv show in GetPaginatedLibraryShowsQuery(context, userId, libraryId, letter, language,
                           page * take, take)) shows.Add(show);
        return shows;
    }

    public Task<Library?> GetLibraryByIdAsync(Ulid id)
    {
        return GetLibraryByIdAsyncSimpleQuery(context, id);
    }

    public Task<List<Library>> GetAllLibrariesAsync()
    {
        return GetAllLibrariesAsyncQuery(context);
    }

    public async Task<List<FolderDto>> GetFoldersAsync()
    {
        return await GetFoldersAsyncQuery(context);
    }
    
    public async Task<Tv?> GetRandomTvShow(Guid userId, string language)
    {
        return await GetRandomTvShowQuery(context, userId, language);
    }

    public async Task<Movie?> GetRandomMovie(Guid userId, string language)
    {
        return await GetRandomMovieQuery(context, userId, language);
    }

    #endregion

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

    public async Task<IEnumerable<Movie>> GetMissingLibraryMovies(Guid userId, Ulid libraryId, string language, int requestTake, int requestPage)
    {
        List<Movie> movies = [];
        await foreach (Movie movie in GetLibraryMoviesQuery(context, userId, libraryId, language, requestPage * requestTake, requestTake))
        {
            if (movie.VideoFiles.Count == 0)
                movies.Add(movie);
        }
        return movies;
    }

    public async Task<IEnumerable<Episode>> GetMissingLibraryShows(Guid userId, Ulid libraryId, string language, int requestTake, int requestPage)
    {
        List<Episode> episodes = [];
        
        await foreach (Tv tv in GetLibraryShowsQuery(context, userId, libraryId, language, requestPage * requestTake, requestTake))
        {
            foreach (Episode episode in tv.Episodes)
            {
                if (episode.VideoFiles.Count == 0)
                    episodes.Add(episode);
            }
        }
        
        return episodes;
    }

    public async Task<int> SyncEncoderProfileFolderAsync(List<EncoderProfileFolder> encoderProfileFolders, List<Folder> folders)
    {
        await context.EncoderProfileFolder
            .Where(epf => folders.Select(f => f.Id).Contains(epf.FolderId))
            .ExecuteDeleteAsync();
        
        return await AddEncoderProfileFolderAsync(encoderProfileFolders);
    }
}