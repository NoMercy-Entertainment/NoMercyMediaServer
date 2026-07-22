// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Users;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Domain;

namespace NoMercy.Data.Repositories;

public class MovieRepository(
    IDbContextFactory<MediaContext> contextFactory,
    ILogger<MovieRepository> logger
) : IMovieRepository
{
    public async Task<Movie?> GetMovieAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Movies.AsNoTracking()
            .Where(predicate: movie => movie.Id == id)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: movie => movie.MovieUser.Where(mu => mu.UserId == userId))
            .Include(navigationPropertyPath: movie => movie.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: movie =>
                movie
                    .Images.Where(i => i.Type == "logo")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
                    .Take(1)
            )
            .Include(navigationPropertyPath: movie =>
                movie
                    .CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .OrderBy(c => c.CertificationId)
                    .Take(1)
            )
                .ThenInclude(navigationPropertyPath: c => c.Certification)
            .Include(navigationPropertyPath: movie => movie.VideoFiles.Where(v => v.Folder != null))
                .ThenInclude(navigationPropertyPath: v => v.UserData.Where(ud => ud.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    private static readonly Func<
        MediaContext,
        Guid,
        int,
        string,
        string,
        Task<Movie?>
    > GetMovieDetailAsyncQuery = EF.CompileAsyncQuery(
        queryExpression: (MediaContext mediaContext, Guid userId, int id, string language, string country) =>
            mediaContext
                .Movies.AsNoTracking()
                .Where(movie => movie.Id == id)
                .Where(tv =>
                    tv.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId)) != null
                )
                .Include(movie =>
                    movie.MovieUser.Where(movieUser => movieUser.UserId.Equals(userId))
                )
                .Include(movie => movie.Cast)
                    .ThenInclude(castMovie => castMovie.Person)
                .Include(movie => movie.Cast)
                    .ThenInclude(castMovie => castMovie.Role)
                .Include(movie => movie.Crew)
                    .ThenInclude(crewMovie => crewMovie.Person)
                .Include(movie => movie.Crew)
                    .ThenInclude(crewMovie => crewMovie.Job)
                .Include(movie => movie.Library)
                    .ThenInclude(library => library.LibraryUsers)
                .Include(movie => movie.Media.Where(media => media.Type == "Trailer"))
                .Include(movie => movie.AlternativeTitles)
                .Include(movie =>
                    movie.Translations.Where(translation => translation.Iso6391 == language)
                )
                .Include(movie =>
                    movie
                        .Images.Where(image =>
                            (image.Type == "logo" && image.Iso6391 == "en")
                            || (
                                (image.Type == "backdrop" || image.Type == "poster")
                                && (image.Iso6391 == "en" || image.Iso6391 == null)
                            )
                        )
                        .OrderByDescending(image => image.VoteAverage)
                        .ThenBy(image => image.Id)
                )
                .Include(movie =>
                    movie.CertificationMovies.Where(certification =>
                        certification.Certification.Iso31661 == country
                        || certification.Certification.Iso31661 == "US"
                    )
                )
                    .ThenInclude(certificationMovie => certificationMovie.Certification)
                .Include(movie => movie.GenreMovies)
                    .ThenInclude(genreMovie => genreMovie.Genre)
                .Include(movie => movie.KeywordMovies)
                    .ThenInclude(keywordMovie => keywordMovie.Keyword)
                .Include(movie => movie.RecommendationFrom)
                .Include(movie => movie.SimilarFrom)
                .Include(movie => movie.VideoFiles)
                    .ThenInclude(file =>
                        file.UserData.Where(userData => userData.UserId.Equals(userId))
                    )
                .Include(movie => movie.WatchProviderMedia.Where(wpm => wpm.CountryCode == country))
                    .ThenInclude(wpm => wpm.WatchProvider)
                .Include(movie => movie.CompaniesMovies)
                    .ThenInclude(ctv => ctv.Company)
                .FirstOrDefault()
    );

    public async Task<Movie?> GetMovieDetailAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await GetMovieDetailAsyncQuery(arg1: context, arg2: userId, arg3: id, arg4: language, arg5: country);
    }

    public async Task<bool> GetMovieAvailableAsync(
        Guid userId,
        int id,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Movies.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: movie => movie.Id == id)
            .AnyAsync(predicate: movie => movie.VideoFiles.Any(v => v.Folder != null), cancellationToken: ct);
    }

    public async Task<List<Movie>> GetMoviePlaylistAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Movies.AsNoTracking()
            .Where(predicate: movie => movie.Id == id)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: movie =>
                movie.Media.Where(media => media.Type == "video" && media.Iso6391 == language)
            )
            .Include(navigationPropertyPath: movie =>
                movie.Images.Where(image =>
                    image.Type == "logo" && image.Iso6391 == "en" && image.Width > image.Height
                )
            )
            .Include(navigationPropertyPath: movie =>
                movie.Translations.Where(translation => translation.Iso6391 == language)
            )
            .Include(navigationPropertyPath: movie => movie.VideoFiles)
                .ThenInclude(navigationPropertyPath: videoFile => videoFile.Metadata)
            .Include(navigationPropertyPath: movie => movie.VideoFiles)
                .ThenInclude(navigationPropertyPath: file =>
                    file.UserData.Where(userData =>
                        userData.UserId.Equals(userId) && userData.Type == MediaTypes.MovieMediaType
                    )
                )
            .Include(navigationPropertyPath: movie =>
                movie.CertificationMovies.Where(certification =>
                    certification.Certification.Iso31661 == country
                    || certification.Certification.Iso31661 == "US"
                )
            )
                .ThenInclude(navigationPropertyPath: certificationMovie => certificationMovie.Certification)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<bool> LikeMovieAsync(
        int id,
        Guid userId,
        bool like,
        CancellationToken ct = default
    )
    {
        try
        {
            await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            MovieUser? movieUser = await context.MovieUser.FirstOrDefaultAsync(
                predicate: mu => mu.MovieId == id && mu.UserId == userId,
                cancellationToken: ct
            );

            if (like)
            {
                await context
                    .MovieUser.Upsert(entity: new(movieId: id, userId: userId))
                    .On(match: m => new { m.MovieId, m.UserId })
                    .WhenMatched(updater: m => new() { MovieId = m.MovieId, UserId = m.UserId })
                    .RunAsync();
            }
            else if (movieUser != null)
            {
                context.MovieUser.Remove(entity: movieUser);
                await context.SaveChangesAsync(cancellationToken: ct);
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
            return false;
        }
    }

    public async Task AddMovieAsync(int id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Library? movieLibrary = await context
            .Libraries.Where(predicate: f => f.Type == MediaTypes.MovieMediaType)
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (movieLibrary == null)
            return;

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<MovieImportJob>(id: id, libraryId: movieLibrary.Id);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        // SQLite schema uses DeleteBehavior.Restrict globally.
        // Temporarily disable FK enforcement so the movie and all its dependents
        // (video files, user data, genre links, library links) are removed atomically.
        //
        // PRAGMA foreign_keys is a per-connection setting; pin one connection
        // across the PRAGMA + DELETE + restore so the PRAGMA actually applies
        // to the DELETE that follows it. See TvShowRepository.DeleteAsync.
        bool ownsConnection =
            context.Database.GetDbConnection().State != System.Data.ConnectionState.Open;

        if (ownsConnection)
            await context.Database.OpenConnectionAsync(cancellationToken: ct);

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = OFF", cancellationToken: ct);
            try
            {
                await context.Movies.Where(predicate: movie => movie.Id == id).ExecuteDeleteAsync(cancellationToken: ct);
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = ON", cancellationToken: ct);
            }
        }
        finally
        {
            if (ownsConnection)
                await context.Database.CloseConnectionAsync();
        }
    }

    public async Task<bool> AddToWatchListAsync(
        int movieId,
        Guid userId,
        bool add = true,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Movie? movie = await context
            .Movies.AsNoTracking()
            .FirstOrDefaultAsync(predicate: m => m.Id == movieId, cancellationToken: ct);

        if (movie is null)
            return false;

        if (add)
        {
            // Find the movie's video file
            VideoFile? videoFile = await context
                .VideoFiles.Where(predicate: vf => vf.MovieId == movieId && vf.Folder != null)
                .FirstOrDefaultAsync(cancellationToken: ct);

            if (videoFile is not null)
            {
                // Check if userdata already exists for this video file
                UserData? existingUserData = await context.UserData.FirstOrDefaultAsync(
                    predicate: ud => ud.UserId == userId && ud.VideoFileId == videoFile.Id,
                    cancellationToken: ct
                );

                if (existingUserData is null)
                {
                    context.UserData.Add(
                        entity: new()
                        {
                            UserId = userId,
                            VideoFileId = videoFile.Id,
                            MovieId = movieId,
                            Time = 0,
                            LastPlayedDate = DateTime.UtcNow.ToString(format: "o"),
                            Type = MediaTypes.MovieMediaType,
                        }
                    );
                }
            }
        }
        else
        {
            // Remove all userdata for this movie
            List<UserData> userDataToRemove = await context
                .UserData.Where(predicate: ud => ud.UserId == userId && ud.MovieId == movieId)
                .ToListAsync(cancellationToken: ct);

            context.UserData.RemoveRange(entities: userDataToRemove);
        }

        await context.SaveChangesAsync(cancellationToken: ct);
        return true;
    }

    public async Task<Movie?> GetMovieForRescanAsync(int id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Movies.AsNoTracking()
            .Include(navigationPropertyPath: movie => movie.Library)
                .ThenInclude(navigationPropertyPath: library => library.FolderLibraries)
                    .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(predicate: movie => movie.Id == id, cancellationToken: ct);
    }

    public async Task<Movie?> GetMovieForRefreshAsync(int id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Movies.AsNoTracking()
            .Include(navigationPropertyPath: movie => movie.Library)
            .FirstOrDefaultAsync(predicate: movie => movie.Id == id, cancellationToken: ct);
    }
}
