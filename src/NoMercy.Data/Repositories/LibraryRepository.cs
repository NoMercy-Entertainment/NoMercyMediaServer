using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class LibraryRepository(MediaContext context)
{
    public IQueryable<Library> GetLibraries(Guid userId)
    {
        return context.Libraries
            .Where(library => library.LibraryUsers
                .FirstOrDefault(u => u.UserId.Equals(userId)) != null
            )
            .Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
                    .ThenInclude(folder => folder.EncoderProfileFolder)
                        .ThenInclude(library => library.EncoderProfile)
            .Include(library => library.LanguageLibraries)
                .ThenInclude(languageLibrary => languageLibrary.Language);
    }

    public Task<Library> GetLibraryByIdAsync(Ulid libraryId, Guid userId, string language, int take, int page)
    {
        return context.Libraries
            .AsNoTracking()
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
            .FirstAsync();
    }

    public IQueryable<Movie> GetLibraryMovies(Guid userId, Ulid libraryId, string language, int take, int page, Expression<Func<Movie, object>>? orderByExpression = null, string? direction = null)
    {
        IIncludableQueryable<Movie, Certification> x =  context.Movies
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
            .Include(movie => movie.KeywordMovies)
            .ThenInclude(keywordMovie => keywordMovie.Keyword)
            .Include(movie => movie.CertificationMovies)
            .ThenInclude(certificationMovie => certificationMovie.Certification);

        if (orderByExpression is not null && direction == "desc")
        {
            return x.OrderByDescending(orderByExpression)
                .Skip(page * take)
                .Take(take);
        }
        if (orderByExpression is not null)
        {
            return x.OrderBy(orderByExpression)
                .Skip(page * take)
                .Take(take);
        }

        return x.OrderBy(movie => movie.TitleSort)
            .Skip(page * take)
            .Take(take);
    }

    public IQueryable<Tv> GetLibraryShows(Guid userId, Ulid libraryId, string language, int take, int page, Expression<Func<Tv, object>>? orderByExpression = null, string? direction = null)
    {
        IIncludableQueryable<Tv, Certification> x = context.Tvs
            .AsNoTracking()
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
            .ThenInclude(certificationTv => certificationTv.Certification);

        if (orderByExpression is not null && direction == "desc")
        {
            return x.OrderByDescending(orderByExpression)
                .Skip(page * take)
                .Take(take);
        }
        if (orderByExpression is not null)
        {
            return x.OrderBy(orderByExpression)
                .Skip(page * take)
                .Take(take);
        }

        return x.OrderBy(tv => tv.TitleSort)
            .Skip(page * take)
            .Take(take);

    }

    public IOrderedQueryable<Library> GetDashboardLibrariesAsync(Guid userId)
    {
        return context.Libraries
            .AsNoTracking()
            .Include(library => library.LibraryUsers)
            .ThenInclude(libraryUser => libraryUser.User)
            .Include(library => library.LibraryTvs
                .Where(libraryTv => libraryTv.Library.Type == "anime" || libraryTv.Library.Type == "tv")
                .Where(libraryTv => libraryTv.Tv.Backdrop != null)
            )
            .ThenInclude(libraryTv => libraryTv.Tv)
            .Include(library => library.LibraryMovies
                .Where(libraryTv => libraryTv.Library.Type == "movie")
                .Where(libraryMovie => libraryMovie.Movie.Backdrop != null))
            .ThenInclude(libraryMovie => libraryMovie.Movie)
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .ThenInclude(library => library.EncoderProfileFolder)
            .ThenInclude(encoderProfileFolder => encoderProfileFolder.EncoderProfile)
            .Include(library => library.LanguageLibraries)
            .ThenInclude(languageLibrary => languageLibrary.Language)
            .Where(library => library.LibraryUsers
                .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
            .OrderBy(library => library.Order);
    }

    public Task<Library?> GetLibraryByIdAsync(Ulid id)
    {
        return context.Libraries
            .Include(library => library.LanguageLibraries)
            .Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
            .Include(library => library.LibraryMovies)
            .Include(library => library.LibraryTvs)
            .FirstOrDefaultAsync(library => library.Id == id);
    }

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
                Order = li.Order,
                UpdatedAt = li.UpdatedAt
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

    public Task<List<Library>> GetAllLibrariesAsync()
    {
        return context.Libraries
            .Include(library => library.FolderLibraries)
            .ThenInclude(folderLibrary => folderLibrary.Folder)
            .Include(library => library.LibraryMovies)
            .Include(library => library.LibraryTvs)
            .ToListAsync();
    }

    public Task AddEncoderProfileFolderAsync(EncoderProfileFolder encoderProfileFolder)
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

    public Task AddEncoderProfileFolderAsync(List<EncoderProfileFolder> encoderProfileFolders)
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

    public Task AddEncoderProfileFolderAsync(EncoderProfileFolder[] encoderProfileFolders)
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

    public Task AddLanguageLibraryAsync(LanguageLibrary[] languageLibraries)
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

    public List<FolderDto> GetFoldersAsync()
    {
        return context.Folders
            .Select(folder => new FolderDto
            {
                Id = folder.Id,
                Path = folder.Path,
                EncoderProfiles = folder.EncoderProfileFolder
                    .Select(e => e.EncoderProfile)
                    .ToArray()
            })
            .ToList();
    }
}
