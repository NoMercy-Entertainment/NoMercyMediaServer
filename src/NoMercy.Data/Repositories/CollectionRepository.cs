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
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;

namespace NoMercy.Data.Repositories;

public class CollectionListDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSort { get; set; } = string.Empty;
    public string? TranslatedTitle { get; set; }
    public string? TranslatedOverview { get; set; }
    public string? Overview { get; set; }
    public string? Poster { get; set; }
    public string? Backdrop { get; set; }
    public ColorPalette ColorPalette { get; set; } = null!;
    public string? Logo { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? FirstMovieYear { get; set; }
    public int TotalMovies { get; set; }
    public int MoviesWithVideo { get; set; }
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
}

public class CollectionRepository(IDbContextFactory<MediaContext> contextFactory)
    : ICollectionRepository
{
    public async Task<List<Collection>> GetCollectionsAsync(
        Guid userId,
        string language,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        List<Collection> collections = await context
            .Collections.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: collection =>
                collection.CollectionMovies.Any(cm =>
                    cm.Movie.VideoFiles.Any(v => v.Folder != null)
                )
            )
            .Include(navigationPropertyPath: collection => collection.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: collection =>
                collection
                    .Images.Where(i => i.Type == "logo")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
                    .Take(1)
            )
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m.CertificationMovies.Where(cert => cert.Certification.Iso31661 == "US")
                            .OrderBy(cert => cert.CertificationId)
                            .Take(1)
                    )
                        .ThenInclude(navigationPropertyPath: cert => cert.Certification)
            .OrderBy(keySelector: collection => collection.TitleSort)
            .ThenBy(keySelector: collection => collection.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);

        return collections;
    }

    public async Task<List<CollectionListDto>> GetCollectionsListAsync(
        Guid userId,
        string language,
        string country,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Collections.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: collection =>
                collection.CollectionMovies.Any(cm =>
                    cm.Movie.VideoFiles.Any(v => v.Folder != null)
                )
            )
            .OrderBy(keySelector: collection => collection.TitleSort)
            .ThenBy(keySelector: collection => collection.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .Select(selector: collection => new CollectionListDto
            {
                Id = collection.Id,
                Title = collection.Title,
                TitleSort = collection.TitleSort ?? collection.Title,
                TranslatedTitle =
                    collection.Translations.FirstOrDefault(t => t.Iso6391 == language) != null
                        ? collection.Translations.First(t => t.Iso6391 == language).Title
                        : null,
                TranslatedOverview =
                    collection.Translations.FirstOrDefault(t => t.Iso6391 == language) != null
                        ? collection.Translations.First(t => t.Iso6391 == language).Overview
                        : null,
                Overview = collection.Overview,
                ColorPalette = collection.ColorPalette!,
                Poster = collection.Poster,
                Backdrop = collection.Backdrop,
                Logo =
                    collection.Images.FirstOrDefault(i => i.Type == "logo") != null
                        ? collection.Images.First(i => i.Type == "logo").FilePath
                        : null,
                CreatedAt = collection.CreatedAt,
                FirstMovieYear = collection
                    .CollectionMovies.Where(cm => cm.Movie.ReleaseDate != null)
                    .OrderBy(cm => cm.Movie.ReleaseDate)
                    .ThenBy(cm => cm.MovieId)
                    .Select(cm => cm.Movie.ReleaseDate!.Value.Year)
                    .FirstOrDefault(),
                TotalMovies = collection.CollectionMovies.Count,
                MoviesWithVideo = collection.CollectionMovies.Count(cm =>
                    cm.Movie.VideoFiles.Any(v => v.Folder != null)
                ),
                CertificationRating = collection
                    .CollectionMovies.SelectMany(cm => cm.Movie.CertificationMovies)
                    .Where(cm =>
                        cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country
                    )
                    .Select(cm => cm.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = collection
                    .CollectionMovies.SelectMany(cm => cm.Movie.CertificationMovies)
                    .Where(cm =>
                        cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country
                    )
                    .Select(cm => cm.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    // public Task<Collection?> GetCollectionAsync(Guid userId, int id, string? language, string country)
    // {
    //     return context.Collections
    //         .AsNoTracking()
    //         .Where(collection => collection.Id == id)
    //         .Where(collection => collection.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Include(collection => collection.CollectionUser.Where(x => x.UserId == userId))
    //         .Include(collection => collection.Translations.Where(t => t.Iso6391 == language))
    //         .Include(collection => collection.Images.Where(i => i.Type == "logo").Take(1))
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.Translations.Where(t => t.Iso6391 == language))
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.VideoFiles.Where(v => v.Folder != null))
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.MovieUser.Where(x => x.UserId == userId))
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.CertificationMovies
    //                 .Where(cert => cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country)
    //                 .Take(1))
    //             .ThenInclude(cert => cert.Certification)
    //         .Include(collection => collection.CollectionMovies)
    //             .ThenInclude(cm => cm.Movie)
    //             .ThenInclude(m => m.Images.Where(i => i.Type == "logo").Take(1))
    //         .FirstOrDefaultAsync();
    // }

    public async Task<Collection?> GetCollectionAsync(
        Guid userId,
        int id,
        string? language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        // Query 1: Core collection data — metadata, translations, images
        // Removed: Library.LibraryUsers Include (only needed in WHERE clause, not consumed by DTO)
        // Movie cast/crew split to Query 2 to reduce round-trips
        Collection? collection = await context
            .Collections.AsNoTracking()
            .Where(predicate: collection => collection.Id == id)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: collection => collection.CollectionUser.Where(x => x.UserId.Equals(userId)))
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: movie => movie.Movie)
                    .ThenInclude(navigationPropertyPath: movie =>
                        movie.Translations.Where(translation => translation.Iso6391 == language)
                    )
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: movie => movie.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie.VideoFiles)
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: movie => movie.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie.MovieUser.Where(x => x.UserId.Equals(userId)))
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: movie => movie.Movie)
                    .ThenInclude(navigationPropertyPath: movie =>
                        movie.CertificationMovies.Where(certificationMovie =>
                            certificationMovie.Certification.Iso31661 == "US"
                            || certificationMovie.Certification.Iso31661 == country
                        )
                    )
                        .ThenInclude(navigationPropertyPath: certificationMovie => certificationMovie.Certification)
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: movie => movie.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie.GenreMovies)
                        .ThenInclude(navigationPropertyPath: genreMovie => genreMovie.Genre)
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: movie => movie.Movie)
                    .ThenInclude(navigationPropertyPath: movie =>
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
                            .Take(30)
                    )
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: movie => movie.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie.KeywordMovies)
                        .ThenInclude(navigationPropertyPath: keywordMovie => keywordMovie.Keyword)
            .Include(navigationPropertyPath: collection =>
                collection.Translations.Where(translation => translation.Iso6391 == language)
            )
            .Include(navigationPropertyPath: collection =>
                collection
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
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (collection is null)
            return null;

        // Query 2: Movie-level cast/crew — loaded separately to reduce query complexity
        List<int> movieIds = collection.CollectionMovies.Select(selector: cm => cm.MovieId).ToList();
        List<Movie> moviesWithCastCrew = await context
            .Movies.AsNoTracking()
            .Where(predicate: m => movieIds.Contains(m.Id))
            .Include(navigationPropertyPath: m => m.Cast)
                .ThenInclude(navigationPropertyPath: c => c.Person)
            .Include(navigationPropertyPath: m => m.Cast)
                .ThenInclude(navigationPropertyPath: c => c.Role)
            .Include(navigationPropertyPath: m => m.Crew)
                .ThenInclude(navigationPropertyPath: c => c.Person)
            .Include(navigationPropertyPath: m => m.Crew)
                .ThenInclude(navigationPropertyPath: c => c.Job)
            .AsSplitQuery()
            .ToListAsync(cancellationToken: ct);

        // Merge movie cast/crew into the main query results
        Dictionary<int, Movie> movieLookup = moviesWithCastCrew.ToDictionary(keySelector: m => m.Id);
        foreach (CollectionMovie cm in collection.CollectionMovies)
        {
            if (movieLookup.TryGetValue(key: cm.MovieId, value: out Movie? loaded))
            {
                cm.Movie.Cast = loaded.Cast;
                cm.Movie.Crew = loaded.Crew;
            }
        }

        return collection;
    }

    public async Task<List<CollectionListDto>> GetCollectionItemCardsAsync(
        Guid userId,
        string? language,
        string country,
        int take = 1,
        int page = 0,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Collections.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: collection =>
                collection.CollectionMovies.Any(cm =>
                    cm.Movie.VideoFiles.Any(v => v.Folder != null)
                )
            )
            .OrderBy(keySelector: collection => collection.TitleSort)
            .ThenBy(keySelector: collection => collection.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .Select(selector: collection => new CollectionListDto
            {
                Id = collection.Id,
                Title = collection.Title,
                TitleSort = collection.TitleSort ?? collection.Title,
                TranslatedTitle =
                    collection.Translations.FirstOrDefault(t => t.Iso6391 == language) != null
                        ? collection.Translations.First(t => t.Iso6391 == language).Title
                        : null,
                TranslatedOverview =
                    collection.Translations.FirstOrDefault(t => t.Iso6391 == language) != null
                        ? collection.Translations.First(t => t.Iso6391 == language).Overview
                        : null,
                Overview = collection.Overview,
                ColorPalette = collection.ColorPalette!,
                Poster = collection.Poster,
                Backdrop = collection.Backdrop,
                Logo =
                    collection.Images.FirstOrDefault(i => i.Type == "logo") != null
                        ? collection.Images.First(i => i.Type == "logo").FilePath
                        : null,
                CreatedAt = collection.CreatedAt,
                FirstMovieYear = collection
                    .CollectionMovies.Where(cm => cm.Movie.ReleaseDate != null)
                    .OrderBy(cm => cm.Movie.ReleaseDate)
                    .ThenBy(cm => cm.MovieId)
                    .Select(cm => cm.Movie.ReleaseDate!.Value.Year)
                    .FirstOrDefault(),
                TotalMovies = collection.CollectionMovies.Count,
                MoviesWithVideo = collection.CollectionMovies.Count(cm =>
                    cm.Movie.VideoFiles.Any(v => v.Folder != null)
                ),
                CertificationRating = collection
                    .CollectionMovies.SelectMany(cm => cm.Movie.CertificationMovies)
                    .Where(cm =>
                        cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country
                    )
                    .Select(cm => cm.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = collection
                    .CollectionMovies.SelectMany(cm => cm.Movie.CertificationMovies)
                    .Where(cm =>
                        cm.Certification.Iso31661 == "US" || cm.Certification.Iso31661 == country
                    )
                    .Select(cm => cm.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Collection>> GetCollectionItems(
        Guid userId,
        string? language,
        string country,
        int take = 1,
        int page = 0,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Collections.AsNoTracking()
            .AsSplitQuery()
            .ForUser(userId: userId)
            .Where(predicate: collection =>
                collection.CollectionMovies.Any(cm =>
                    cm.Movie.VideoFiles.Any(v => v.Folder != null)
                )
            )
            .Include(navigationPropertyPath: collection => collection.CollectionUser.Where(x => x.UserId == userId))
            .Include(navigationPropertyPath: collection => collection.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: collection =>
                collection
                    .Images.Where(i => i.Type == "logo")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
                    .Take(1)
            )
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m.CertificationMovies.Where(cert =>
                                cert.Certification.Iso31661 == "US"
                                || cert.Certification.Iso31661 == country
                            )
                            .OrderBy(cert => cert.CertificationId)
                            .Take(1)
                    )
                        .ThenInclude(navigationPropertyPath: cert => cert.Certification)
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m.Images.Where(i => i.Type == "logo")
                            .OrderByDescending(i => i.VoteAverage)
                            .ThenBy(i => i.Id)
                            .Take(1)
                    )
            .OrderBy(keySelector: c => c.TitleSort)
            .ThenBy(keySelector: c => c.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<Collection?> GetAvailableCollectionAsync(
        Guid userId,
        int id,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Collections.AsNoTracking()
            .AsSplitQuery()
            .Where(predicate: collection => collection.Id == id)
            .ForUser(userId: userId)
            .Where(predicate: collection =>
                collection.CollectionMovies.Any(cm =>
                    cm.Movie.VideoFiles.Any(v => v.Folder != null)
                )
            )
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.VideoFiles.Where(v => v.Folder != null))
                        .ThenInclude(navigationPropertyPath: v => v.Metadata)
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.VideoFiles.Where(v => v.Folder != null))
                        .ThenInclude(navigationPropertyPath: v => v.UserData.Where(ud => ud.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<Collection?> GetCollectionPlaylistAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Collections.AsNoTracking()
            .AsSplitQuery()
            .Where(predicate: collection => collection.Id == id)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: collection => collection.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: collection =>
                collection
                    .Images.Where(i => i.Type == "logo")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
                    .Take(1)
            )
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m.Images.Where(i => i.Type == "logo")
                            .OrderByDescending(i => i.VoteAverage)
                            .ThenBy(i => i.Id)
                            .Take(1)
                    )
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.Media.Where(media => media.Type == "video"))
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.VideoFiles.Where(v => v.Folder != null))
                        .ThenInclude(navigationPropertyPath: v => v.Metadata)
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.VideoFiles.Where(v => v.Folder != null))
                        .ThenInclude(navigationPropertyPath: v =>
                            v.UserData.Where(ud => ud.UserId == userId && ud.Type == "collection")
                        )
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m.CertificationMovies.Where(cert =>
                                cert.Certification.Iso31661 == "US"
                                || cert.Certification.Iso31661 == country
                            )
                            .OrderBy(cert => cert.CertificationId)
                            .Take(1)
                    )
                        .ThenInclude(navigationPropertyPath: cert => cert.Certification)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<bool> LikeAsync(
        int id,
        Guid userId,
        bool like,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Collection? collection = await context
            .Collections.AsNoTracking()
            .Where(predicate: collection => collection.Id == id)
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (collection is null)
            return false;

        if (like)
        {
            await context
                .CollectionUser.Upsert(entity: new(collectionId: collection.Id, userId: userId))
                .On(match: m => new { m.CollectionId, m.UserId })
                .WhenMatched(updater: m => new() { CollectionId = m.CollectionId, UserId = m.UserId })
                .RunAsync();
        }
        else
        {
            CollectionUser? collectionUser = await context
                .CollectionUser.Where(predicate: collectionUser =>
                    collectionUser.CollectionId == collection.Id
                    && collectionUser.UserId.Equals(userId)
                )
                .FirstOrDefaultAsync(cancellationToken: ct);

            if (collectionUser is not null)
                context.CollectionUser.Remove(entity: collectionUser);

            await context.SaveChangesAsync(cancellationToken: ct);
        }

        return true;
    }

    public async Task<bool> AddToWatchListAsync(
        int collectionId,
        Guid userId,
        bool add = true,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Collection? collection = await context
            .Collections.AsNoTracking()
            .FirstOrDefaultAsync(predicate: c => c.Id == collectionId, cancellationToken: ct);

        if (collection is null)
            return false;

        if (add)
        {
            // Find the first movie in the collection with a video file
            CollectionMovie? firstMovieWithVideo = await context
                .CollectionMovie.Where(predicate: cm => cm.CollectionId == collectionId)
                .Include(navigationPropertyPath: cm => cm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.VideoFiles)
                .OrderBy(keySelector: cm => cm.Movie.TitleSort)
                .ThenBy(keySelector: cm => cm.MovieId)
                .FirstOrDefaultAsync(cancellationToken: ct);

            if (
                firstMovieWithVideo?.Movie.VideoFiles.FirstOrDefault(predicate: vf => vf.Folder != null) is
                { } videoFile
            )
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
                            CollectionId = collectionId,
                            Time = 0,
                            LastPlayedDate = DateTime.UtcNow.ToString(format: "o"),
                            Type = MediaTypes.CollectionMediaType,
                        }
                    );
                }
            }
        }
        else
        {
            // Remove all userdata for this collection
            List<UserData> userDataToRemove = await context
                .UserData.Where(predicate: ud => ud.UserId == userId && ud.CollectionId == collectionId)
                .ToListAsync(cancellationToken: ct);

            context.UserData.RemoveRange(entities: userDataToRemove);
        }

        await context.SaveChangesAsync(cancellationToken: ct);
        return true;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        // SQLite schema uses DeleteBehavior.Restrict globally.
        // Temporarily disable FK enforcement so the collection and all its dependents
        // are removed atomically.
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
                await context
                    .Collections.Where(predicate: collection => collection.Id == id)
                    .ExecuteDeleteAsync(cancellationToken: ct);
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

    public async Task<Collection?> GetCollectionForRescanAsync(
        int id,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Collections.AsNoTracking()
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: collectionMovie => collectionMovie.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie.Library)
                        .ThenInclude(navigationPropertyPath: library => library.FolderLibraries)
                            .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(predicate: collection => collection.Id == id, cancellationToken: ct);
    }

    public async Task<Collection?> GetCollectionWithMovieLibrariesAsync(
        int id,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Collections.AsNoTracking()
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: collectionMovie => collectionMovie.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie.Library)
            .FirstOrDefaultAsync(predicate: collection => collection.Id == id, cancellationToken: ct);
    }
}
