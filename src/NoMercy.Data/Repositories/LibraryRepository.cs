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

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.DTOs;
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;

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

public class LibraryRepository(IDbContextFactory<MediaContext> contextFactory) : ILibraryRepository
{
    private static readonly string[] Letters =
    [
        "*",
        "#",
        "'",
        "\"",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8",
        "9",
        "0",
    ];

    public async Task<List<Library>> GetLibraries(Guid userId, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Libraries.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: library => library.Type != MediaTypes.InboxMediaType)
            .Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: fl => fl.Folder)
                    .ThenInclude(navigationPropertyPath: f => f.Driver)
            .Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: fl => fl.Folder)
                    .ThenInclude(navigationPropertyPath: f => f.EncodingPresetFolders)
                        .ThenInclude(navigationPropertyPath: link => link.Preset)
            .Include(navigationPropertyPath: library => library.LanguageLibraries)
                .ThenInclude(navigationPropertyPath: ll => ll.Language)
            .Include(navigationPropertyPath: library => library.LibraryMovies)
            .Include(navigationPropertyPath: library => library.LibraryTvs)
            .OrderBy(keySelector: library => library.Order)
            .ThenBy(keySelector: library => library.Id)
            .ToListAsync(cancellationToken: ct);
    }

    /// <summary>
    /// Lightweight library query for endpoints that don't need LibraryMovies/LibraryTvs collections.
    /// Use this in Mobile/TV/Home endpoints to avoid loading thousands of join entities into memory.
    /// </summary>
    public async Task<List<Library>> GetLibrariesLite(Guid userId, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Libraries.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: library => library.Type != MediaTypes.InboxMediaType)
            .OrderBy(keySelector: library => library.Order)
            .ThenBy(keySelector: library => library.Id)
            .ToListAsync(cancellationToken: ct);
    }

    /// <summary>
    /// Returns total item count (movies + TV) per library via cheap SQL COUNT.
    /// Used for pagination mode decisions without loading join entities.
    /// </summary>
    public async Task<Dictionary<Ulid, int>> GetLibraryItemCountsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Libraries.AsNoTracking()
            .ForUser(userId: userId)
            .Select(selector: library => new
            {
                library.Id,
                Total = library.LibraryMovies.Count + library.LibraryTvs.Count,
            })
            .ToDictionaryAsync(keySelector: x => x.Id, elementSelector: x => x.Total, cancellationToken: ct);
    }

    /// <summary>
    /// Gets a library with its movies and TV shows limited to <paramref name="take"/> items each.
    /// The .Take(take) inside Include() is intentional: it limits items per-carousel (e.g. 10 for mobile, 6 for TV)
    /// to prevent loading the entire library into memory. The <paramref name="page"/> parameter is currently unused.
    /// Prefer <see cref="GetLibraryMovieCardsAsync"/> and <see cref="GetLibraryTvCardsAsync"/> for new code —
    /// they use projection and proper Skip/Take pagination.
    /// </summary>
    public async Task<Library?> GetLibraryByIdAsync(
        Ulid libraryId,
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
            .Libraries.AsNoTracking()
            .Where(predicate: library => library.Id == libraryId)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: library =>
                library
                    .LibraryMovies.Where(lm => lm.Movie.VideoFiles.Any(v => v.Folder != null))
                    .OrderBy(lm => lm.Movie.TitleSort)
                    .ThenBy(lm => lm.MovieId)
                    .Take(take)
            )
                .ThenInclude(navigationPropertyPath: lm => lm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: library => library.LibraryMovies)
                .ThenInclude(navigationPropertyPath: lm => lm.Movie)
                    .ThenInclude(navigationPropertyPath: m => m.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: library => library.LibraryMovies)
                .ThenInclude(navigationPropertyPath: lm => lm.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                            .OrderByDescending(i => i.VoteAverage)
                            .ThenBy(i => i.Id)
                            .Take(1)
                    )
            .Include(navigationPropertyPath: library => library.LibraryMovies)
                .ThenInclude(navigationPropertyPath: lm => lm.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m.CertificationMovies.Where(c =>
                                c.Certification.Iso31661 == "US"
                                || c.Certification.Iso31661 == country
                            )
                            .OrderBy(c => c.CertificationId)
                            .Take(1)
                    )
                        .ThenInclude(navigationPropertyPath: c => c.Certification)
            .Include(navigationPropertyPath: library =>
                library
                    .LibraryTvs.Where(lt =>
                        lt.Tv.Episodes.Any(e =>
                            (
                                e.VideoFiles.Any(v => v.Folder != null)
                                || e.Tv.Episodes.Any(o =>
                                    o.SeasonNumber == e.SeasonNumber
                                    && o.VideoFiles.Any(w =>
                                        w.Folder != null
                                        && w.LastEpisodeNumber != null
                                        && o.EpisodeNumber <= e.EpisodeNumber
                                        && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                                    )
                                )
                            )
                        )
                    )
                    .OrderBy(lt => lt.Tv.TitleSort)
                    .ThenBy(lt => lt.TvId)
                    .Take(take)
            )
                .ThenInclude(navigationPropertyPath: lt => lt.Tv)
                    .ThenInclude(navigationPropertyPath: tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: library => library.LibraryTvs)
                .ThenInclude(navigationPropertyPath: lt => lt.Tv)
                    .ThenInclude(navigationPropertyPath: tv =>
                        tv.Episodes.Where(e =>
                            e.SeasonNumber > 0
                            && (
                                e.VideoFiles.Any(v => v.Folder != null)
                                || e.Tv.Episodes.Any(o =>
                                    o.SeasonNumber == e.SeasonNumber
                                    && o.VideoFiles.Any(w =>
                                        w.Folder != null
                                        && w.LastEpisodeNumber != null
                                        && o.EpisodeNumber <= e.EpisodeNumber
                                        && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                                    )
                                )
                            )
                        )
                    )
                        .ThenInclude(navigationPropertyPath: e => e.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: library => library.LibraryTvs)
                .ThenInclude(navigationPropertyPath: lt => lt.Tv)
                    .ThenInclude(navigationPropertyPath: tv =>
                        tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                            .OrderByDescending(i => i.VoteAverage)
                            .ThenBy(i => i.Id)
                            .Take(1)
                    )
            .Include(navigationPropertyPath: library => library.LibraryTvs)
                .ThenInclude(navigationPropertyPath: lt => lt.Tv)
                    .ThenInclude(navigationPropertyPath: tv =>
                        tv.CertificationTvs.Where(c =>
                                c.Certification.Iso31661 == "US"
                                || c.Certification.Iso31661 == country
                            )
                            .OrderBy(c => c.CertificationId)
                            .Take(1)
                    )
                        .ThenInclude(navigationPropertyPath: c => c.Certification)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    private static readonly Func<
        MediaContext,
        Guid,
        Ulid,
        string,
        int,
        int,
        Expression<Func<Movie, object>>?,
        string?,
        IAsyncEnumerable<Movie>
    > GetLibraryMoviesQuery = EF.CompileAsyncQuery(
        queryExpression: (
            MediaContext mediaContext,
            Guid userId,
            Ulid libraryId,
            string language,
            int take,
            int skip,
            Expression<Func<Movie, object>>? orderByExpression,
            string? direction
        ) =>
            mediaContext
                .Movies.AsNoTracking()
                .Where(movie => movie.Library.Id == libraryId)
                .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Where(libraryMovie => libraryMovie.VideoFiles.Any())
                .Include(movie => movie.VideoFiles)
                .Include(movie =>
                    movie.Media.Where(media => media.Iso6391 == language || media.Iso6391 == "en")
                )
                .Include(movie =>
                    movie.Images.Where(image => image.Iso6391 == language || image.Iso6391 == "en")
                )
                .Include(movie => movie.GenreMovies)
                    .ThenInclude(genreMovie => genreMovie.Genre)
                .Include(movie =>
                    movie.Translations.Where(translation =>
                        translation.Iso6391 == language || translation.Iso6391 == "en"
                    )
                )
                .Include(movie => movie.KeywordMovies)
                    .ThenInclude(keywordMovie => keywordMovie.Keyword)
                .Include(movie => movie.CertificationMovies)
                    .ThenInclude(certificationMovie => certificationMovie.Certification)
                .OrderByDescending(movie => movie.CreatedAt)
                .ThenBy(movie => movie.Id)
                .Skip(skip)
                .Take(take)
    );

    public IAsyncEnumerable<Movie> GetLibraryMovies(
        MediaContext mediaContext,
        Guid userId,
        Ulid libraryId,
        string language,
        int take,
        int skip,
        Expression<Func<Movie, object>>? orderByExpression,
        string? direction
    ) =>
        GetLibraryMoviesQuery(
            arg1: mediaContext,
            arg2: userId,
            arg3: libraryId,
            arg4: language,
            arg5: take,
            arg6: skip,
            arg7: orderByExpression,
            arg8: direction
        );

    // public async Task<List<Movie>> GetLibraryMovies(Guid userId, Ulid libraryId, string language, int take, int page)
    // {
    //     // First get movie IDs with pagination (no filtered includes)
    //     List<int> movieIds = await context.Movies
    //         .AsNoTracking()
    //         .Where(movie => movie.Library.Id == libraryId)
    //         .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Where(movie => movie.VideoFiles.Any(v => v.Folder != null))
    //         .OrderBy(movie => movie.TitleSort).ThenBy(movie => movie.Id)
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
    //         .Include(movie => movie.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage).ThenBy(i => i.Id).Take(1))
    //         .Include(movie => movie.Translations.Where(t => t.Iso6391 == language))
    //         .Include(movie => movie.CertificationMovies.Take(1))
    //             .ThenInclude(c => c.Certification)
    //         .ToListAsync();
    // }

    private static readonly Func<
        MediaContext,
        Guid,
        Ulid,
        string,
        int,
        int,
        Expression<Func<Tv, object>>?,
        string?,
        IAsyncEnumerable<Tv>
    > GetLibraryShowsQuery = EF.CompileAsyncQuery(
        queryExpression: (
            MediaContext mediaContext,
            Guid userId,
            Ulid libraryId,
            string language,
            int take,
            int skip,
            Expression<Func<Tv, object>>? orderByExpression,
            string? direction
        ) =>
            mediaContext
                .Tvs.AsNoTracking()
                .Where(tv => tv.Library.Id == libraryId)
                .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                .Where(libraryTv => libraryTv.Episodes.Any(episode => episode.VideoFiles.Any()))
                .Include(tv =>
                    tv.Episodes.Where(episode =>
                        episode.SeasonNumber > 0 && episode.VideoFiles.Any()
                    )
                )
                    .ThenInclude(episode => episode.VideoFiles)
                .Include(tv =>
                    tv.Media.Where(media => media.Iso6391 == language || media.Iso6391 == "en")
                )
                .Include(tv =>
                    tv.Images.Where(image => image.Iso6391 == language || image.Iso6391 == "en")
                )
                .Include(tv => tv.GenreTvs)
                    .ThenInclude(genreTv => genreTv.Genre)
                .Include(tv =>
                    tv.Translations.Where(translation =>
                        translation.Iso6391 == language || translation.Iso6391 == "en"
                    )
                )
                .Include(tv => tv.KeywordTvs)
                    .ThenInclude(keywordTv => keywordTv.Keyword)
                .Include(tv => tv.CertificationTvs)
                    .ThenInclude(certificationTv => certificationTv.Certification)
                .OrderByDescending(tv => tv.CreatedAt)
                .ThenBy(tv => tv.Id)
                .Skip(skip)
                .Take(take)
    );

    public IAsyncEnumerable<Tv> GetLibraryShows(
        MediaContext mediaContext,
        Guid userId,
        Ulid libraryId,
        string language,
        int take,
        int skip,
        Expression<Func<Tv, object>>? orderByExpression,
        string? direction
    ) =>
        GetLibraryShowsQuery(
            arg1: mediaContext,
            arg2: userId,
            arg3: libraryId,
            arg4: language,
            arg5: take,
            arg6: skip,
            arg7: orderByExpression,
            arg8: direction
        );

    // public async Task<List<Tv>> GetLibraryShows(Guid userId, Ulid libraryId, string language, int take, int page)
    // {
    //     // First get TV IDs with pagination (no filtered includes)
    //     List<int> tvIds = await context.Tvs
    //         .AsNoTracking()
    //         .Where(tv => tv.Library.Id == libraryId)
    //         .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Where(tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
    //         .OrderBy(tv => tv.TitleSort).ThenBy(tv => tv.Id)
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
    //         .Include(tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage).ThenBy(i => i.Id).Take(1))
    //         .Include(tv => tv.Translations.Where(t => t.Iso6391 == language))
    //         .Include(tv => tv.CertificationTvs.Take(1))
    //             .ThenInclude(c => c.Certification)
    //         .ToListAsync();
    // }

    // Optimized query using projection - only fetches what NmCardDto needs
    public async Task<List<MovieCardDto>> GetLibraryMovieCardsAsync(
        Guid userId,
        Ulid libraryId,
        string country,
        int take,
        int skip,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await GetLibraryMovieCardsAsync(mediaContext: context, userId: userId, libraryId: libraryId, country: country, take: take, skip: skip, ct: ct);
    }

    public Task<List<MovieCardDto>> GetLibraryMovieCardsAsync(
        MediaContext mediaContext,
        Guid userId,
        Ulid libraryId,
        string country,
        int take,
        int skip,
        CancellationToken ct = default
    )
    {
        return mediaContext
            .Movies.AsNoTracking()
            .Where(predicate: movie => movie.Library.Id == libraryId)
            .ForUser(userId: userId)
            .Where(predicate: movie => movie.VideoFiles.Any(v => v.Folder != null))
            .Include(navigationPropertyPath: tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en"))
            .OrderByDescending(keySelector: movie => movie.CreatedAt)
            .ThenBy(keySelector: movie => movie.Id)
            .Skip(count: skip)
            .Take(count: take)
            .Select(selector: movie => new MovieCardDto
            {
                Id = movie.Id,
                Title = movie.Title,
                TitleSort = movie.TitleSort,
                Overview = movie.Overview,
                Poster = movie.Poster,
                Backdrop = movie.Backdrop,
                Logo = movie
                    .Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                ReleaseDate = movie.ReleaseDate,
                CreatedAt = movie.CreatedAt,
                ColorPalette = movie._colorPalette,
                VideoFileCount = movie.VideoFiles.Count(v => v.Folder != null),
                CertificationRating = movie
                    .CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = movie
                    .CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    // Optimized query using projection - only fetches what NmCardDto needs
    public async Task<List<TvCardDto>> GetLibraryTvCardsAsync(
        Guid userId,
        Ulid libraryId,
        string country,
        int take,
        int skip,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await GetLibraryTvCardsAsync(mediaContext: context, userId: userId, libraryId: libraryId, country: country, take: take, skip: skip, ct: ct);
    }

    public Task<List<TvCardDto>> GetLibraryTvCardsAsync(
        MediaContext mediaContext,
        Guid userId,
        Ulid libraryId,
        string country,
        int take,
        int skip,
        CancellationToken ct = default
    )
    {
        return mediaContext
            .Tvs.AsNoTracking()
            .Where(predicate: tv => tv.Library.Id == libraryId)
            .ForUser(userId: userId)
            // "TV has a playable episode": a multi-episode video file lives on its own
            // episode, so that episode already passes the direct test. The nested
            // LastEpisodeNumber self-join never changed this TV-level filter, so the
            // direct check alone selects the identical set of shows.
            .Where(predicate: tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
            .Include(navigationPropertyPath: tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en"))
            .OrderByDescending(keySelector: tv => tv.CreatedAt)
            .ThenBy(keySelector: tv => tv.Id)
            .Skip(count: skip)
            .Take(count: take)
            .Select(selector: tv => new TvCardDto
            {
                Id = tv.Id,
                Title = tv.Title,
                TitleSort = tv.TitleSort,
                Overview = tv.Overview,
                Poster = tv.Poster,
                Backdrop = tv.Backdrop,
                Logo = tv
                    .Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                FirstAirDate = tv.FirstAirDate,
                CreatedAt = tv.CreatedAt,
                ColorPalette = tv._colorPalette,
                NumberOfEpisodes = tv.NumberOfEpisodes,
                EpisodesWithVideo = tv
                    .Episodes.Where(e => e.SeasonNumber > 0)
                    .Count(e =>
                        (
                            e.VideoFiles.Any(v => v.Folder != null)
                            || e.Tv.Episodes.Any(o =>
                                o.SeasonNumber == e.SeasonNumber
                                && o.VideoFiles.Any(w =>
                                    w.Folder != null
                                    && w.LastEpisodeNumber != null
                                    && o.EpisodeNumber <= e.EpisodeNumber
                                    && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                                )
                            )
                        )
                    ),
                CertificationRating = tv
                    .CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = tv
                    .CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Movie>> GetPaginatedLibraryMovies(
        Guid userId,
        Ulid libraryId,
        string letter,
        string language,
        string country,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Movies.AsNoTracking()
            .Where(predicate: movie => movie.Library.Id == libraryId)
            .ForUser(userId: userId)
            .Where(predicate: movie => movie.VideoFiles.Any(v => v.Folder != null))
            .Where(predicate: movie =>
                (letter == "_" || letter == "#")
                    ? Letters.Any(p => movie.TitleSort.StartsWith(p.ToLower()))
                    : movie.TitleSort.StartsWith(letter.ToLower())
            )
            .Include(navigationPropertyPath: movie => movie.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: movie => movie.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: movie =>
                movie
                    .Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
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
            .OrderBy(keySelector: movie => movie.TitleSort)
            .ThenBy(keySelector: movie => movie.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<Tv>> GetPaginatedLibraryShows(
        Guid userId,
        Ulid libraryId,
        string letter,
        string language,
        string country,
        int take,
        int page,
        Expression<Func<Tv, object>>? orderByExpression = null,
        string? direction = null,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Tvs.AsNoTracking()
            .Where(predicate: tv => tv.Library.Id == libraryId)
            .ForUser(userId: userId)
            .Where(predicate: tv =>
                tv.Episodes.Any(e =>
                    (
                        e.VideoFiles.Any(v => v.Folder != null)
                        || e.Tv.Episodes.Any(o =>
                            o.SeasonNumber == e.SeasonNumber
                            && o.VideoFiles.Any(w =>
                                w.Folder != null
                                && w.LastEpisodeNumber != null
                                && o.EpisodeNumber <= e.EpisodeNumber
                                && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                            )
                        )
                    )
                )
            )
            .Where(predicate: tv =>
                (letter == "_" || letter == "#")
                    ? Letters.Any(p => tv.TitleSort.StartsWith(p.ToLower()))
                    : tv.TitleSort.StartsWith(letter.ToLower())
            )
            .Include(navigationPropertyPath: tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: tv =>
                tv.Episodes.Where(e =>
                    e.SeasonNumber > 0
                    && (
                        e.VideoFiles.Any(v => v.Folder != null)
                        || e.Tv.Episodes.Any(o =>
                            o.SeasonNumber == e.SeasonNumber
                            && o.VideoFiles.Any(w =>
                                w.Folder != null
                                && w.LastEpisodeNumber != null
                                && o.EpisodeNumber <= e.EpisodeNumber
                                && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                            )
                        )
                    )
                )
            )
                .ThenInclude(navigationPropertyPath: e => e.VideoFiles.Where(v => v.Folder != null))
            .Include(navigationPropertyPath: tv =>
                tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .OrderByDescending(i => i.VoteAverage)
                    .ThenBy(i => i.Id)
                    .Take(1)
            )
            .Include(navigationPropertyPath: tv =>
                tv.CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .OrderBy(c => c.CertificationId)
                    .Take(1)
            )
                .ThenInclude(navigationPropertyPath: c => c.Certification)
            .OrderBy(keySelector: tv => tv.TitleSort)
            .ThenBy(keySelector: tv => tv.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<HomeMovieCardDto>> GetPaginatedLibraryMovieCardsAsync(
        Guid userId,
        Ulid libraryId,
        string letter,
        string language,
        string country,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Movies.AsNoTracking()
            .Where(predicate: movie => movie.Library.Id == libraryId)
            .ForUser(userId: userId)
            .Where(predicate: movie => movie.VideoFiles.Any(v => v.Folder != null))
            .Where(predicate: movie =>
                (letter == "_" || letter == "#")
                    ? Letters.Any(p => movie.TitleSort.StartsWith(p.ToLower()))
                    : movie.TitleSort.StartsWith(letter.ToLower())
            )
            .OrderBy(keySelector: movie => movie.TitleSort)
            .ThenBy(keySelector: movie => movie.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .Select(selector: movie => new HomeMovieCardDto
            {
                Id = movie.Id,
                Title = movie.Title,
                TitleSort = movie.TitleSort,
                TranslatedTitle = movie
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = movie.Overview,
                TranslatedOverview = movie
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = movie.Poster,
                Backdrop = movie.Backdrop,
                Logo = movie
                    .Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                ReleaseDate = movie.ReleaseDate,
                CreatedAt = movie.CreatedAt,
                ColorPalette = movie._colorPalette,
                VideoFileCount = movie.VideoFiles.Count(v => v.Folder != null),
                CertificationRating = movie
                    .CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = movie
                    .CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<HomeTvCardDto>> GetPaginatedLibraryTvCardsAsync(
        Guid userId,
        Ulid libraryId,
        string letter,
        string language,
        string country,
        int take,
        int page,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Tvs.AsNoTracking()
            .Where(predicate: tv => tv.Library.Id == libraryId)
            .ForUser(userId: userId)
            .Where(predicate: tv =>
                tv.Episodes.Any(e =>
                    (
                        e.VideoFiles.Any(v => v.Folder != null)
                        || e.Tv.Episodes.Any(o =>
                            o.SeasonNumber == e.SeasonNumber
                            && o.VideoFiles.Any(w =>
                                w.Folder != null
                                && w.LastEpisodeNumber != null
                                && o.EpisodeNumber <= e.EpisodeNumber
                                && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                            )
                        )
                    )
                )
            )
            .Where(predicate: tv =>
                (letter == "_" || letter == "#")
                    ? Letters.Any(p => tv.TitleSort.StartsWith(p.ToLower()))
                    : tv.TitleSort.StartsWith(letter.ToLower())
            )
            .OrderBy(keySelector: tv => tv.TitleSort)
            .ThenBy(keySelector: tv => tv.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .Select(selector: tv => new HomeTvCardDto
            {
                Id = tv.Id,
                Title = tv.Title,
                TitleSort = tv.TitleSort,
                TranslatedTitle = tv
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = tv.Overview,
                TranslatedOverview = tv
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = tv.Poster,
                Backdrop = tv.Backdrop,
                Logo = tv
                    .Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                FirstAirDate = tv.FirstAirDate,
                CreatedAt = tv.CreatedAt,
                ColorPalette = tv._colorPalette,
                NumberOfEpisodes = tv.NumberOfEpisodes,
                EpisodesWithVideo = tv
                    .Episodes.Where(e => e.SeasonNumber > 0)
                    .Count(e =>
                        (
                            e.VideoFiles.Any(v => v.Folder != null)
                            || e.Tv.Episodes.Any(o =>
                                o.SeasonNumber == e.SeasonNumber
                                && o.VideoFiles.Any(w =>
                                    w.Folder != null
                                    && w.LastEpisodeNumber != null
                                    && o.EpisodeNumber <= e.EpisodeNumber
                                    && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                                )
                            )
                        )
                    ),
                CertificationRating = tv
                    .CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = tv
                    .CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<Library?> GetLibraryByIdAsync(Ulid id)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        return await context
            .Libraries.AsNoTracking()
            .Include(navigationPropertyPath: library => library.LanguageLibraries)
            .Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: fl => fl.Folder)
                    .ThenInclude(navigationPropertyPath: f => f.Driver)
            .Include(navigationPropertyPath: library => library.LibraryMovies)
            .Include(navigationPropertyPath: library => library.LibraryTvs)
            .FirstOrDefaultAsync(predicate: library => library.Id == id);
    }

    public async Task<Library?> GetLibraryByIdLiteAsync(Ulid id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Libraries.AsNoTracking()
            .FirstOrDefaultAsync(predicate: library => library.Id == id, cancellationToken: ct);
    }

    public async Task<Library?> GetLibraryByTypeAsync(
        string type,
        string? fallbackType = null,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Library? library = await context
            .Libraries.AsNoTracking()
            .FirstOrDefaultAsync(predicate: lib => lib.Type == type, cancellationToken: ct);

        if (library is not null || fallbackType is null)
            return library;

        return await context
            .Libraries.AsNoTracking()
            .FirstOrDefaultAsync(predicate: lib => lib.Type == fallbackType, cancellationToken: ct);
    }

    public async Task<VideoSearchResults> SearchVideoByTitleAsync(
        string normalizedQuery,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        // Tv and Movie title searches run on separate factory contexts so the
        // two queries execute in parallel without sharing a context.
        Task<List<Tv>> tvsTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                return await ctx
                    .Tvs.Where(predicate: tv => tv.Title.ToLower().Contains(normalizedQuery))
                    .ToListAsync(cancellationToken: ct);
            },
            cancellationToken: ct
        );

        Task<List<Movie>> moviesTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                return await ctx
                    .Movies.Where(predicate: movie => movie.Title.ToLower().Contains(normalizedQuery))
                    .ToListAsync(cancellationToken: ct);
            },
            cancellationToken: ct
        );

        await Task.WhenAll(tasks: [tvsTask, moviesTask]);

        return new(Tvs: tvsTask.Result, Movies: moviesTask.Result);
    }

    public async Task<bool> HasCompletedSetupAsync(CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        bool hasLibrary = await context.Libraries.AnyAsync(cancellationToken: ct);
        if (!hasLibrary)
            return false;

        bool hasFolder = await context.Folders.AnyAsync(cancellationToken: ct);
        if (!hasFolder)
            return false;

        return await context.EncodingPresets.AnyAsync(cancellationToken: ct);
    }

    public async Task<List<Library>> GetAllLibrariesAsync()
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        return await context
            .Libraries.AsNoTracking()
            .Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: fl => fl.Folder)
                    .ThenInclude(navigationPropertyPath: f => f.Driver)
            .Include(navigationPropertyPath: library => library.LibraryMovies)
            .Include(navigationPropertyPath: library => library.LibraryTvs)
            .ToListAsync();
    }

    public async Task<List<FolderDto>> GetFoldersAsync()
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        return await context
            .Folders.AsNoTracking()
            .Include(navigationPropertyPath: f => f.Driver)
            .Select(selector: f => new FolderDto(f))
            .ToListAsync();
    }

    // public Task<Tv?> GetRandomTvShow(Guid userId, string language)
    // {
    //     return context.Tvs
    //         .AsNoTracking()
    //         .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Where(tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
    //         .Include(tv => tv.Translations.Where(t => t.Iso6391 == language))
    //         .Include(tv => tv.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage).ThenBy(i => i.Id).Take(1))
    //         .Include(tv => tv.Episodes.Where(e => e.SeasonNumber > 0 && e.VideoFiles.Any(v => v.Folder != null)))
    //             .ThenInclude(e => e.VideoFiles.Where(v => v.Folder != null))
    //         .Include(tv => tv.CertificationTvs.Take(1))
    //             .ThenInclude(c => c.Certification)
    //         .OrderBy(tv => EF.Functions.Random())
    //         .FirstOrDefaultAsync();
    // }

    private static readonly Func<MediaContext, Guid, string, Task<Tv?>> GetRandomTvShowQuery =
        EF.CompileAsyncQuery(
            queryExpression: (MediaContext mediaContext, Guid userId, string language) =>
                mediaContext
                    .Tvs.AsNoTracking()
                    .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                    .Include(tv =>
                        tv.Translations.Where(translation => translation.Iso6391 == language)
                    )
                    .Include(tv =>
                        tv.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en")
                    )
                    .Include(tv => tv.Media.Where(media => media.Site == "YouTube"))
                    .Include(tv => tv.KeywordTvs)
                        .ThenInclude(keywordTv => keywordTv.Keyword)
                    .Include(tv =>
                        tv.Episodes.Where(episode =>
                            episode.SeasonNumber > 0 && episode.VideoFiles.Any()
                        )
                    )
                        .ThenInclude(episode => episode.VideoFiles)
                    .Include(tv => tv.CertificationTvs)
                        .ThenInclude(certificationTv => certificationTv.Certification)
                    .OrderBy(tv => EF.Functions.Random())
                    .FirstOrDefault()
        );

    public async Task<Tv?> GetRandomTvShow(
        Guid userId,
        string language,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await GetRandomTvShowQuery(arg1: context, arg2: userId, arg3: language);
    }

    public async Task<HomeTvCardDto?> GetRandomTvCardAsync(
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Tvs.AsNoTracking()
            .Where(predicate: tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            // A multi-episode file lives on its own episode, which already passes the
            // direct test, so this TV-level "has a playable episode" filter needs only
            // the direct check.
            .Where(predicate: tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)))
            .OrderBy(keySelector: tv => EF.Functions.Random())
            .Select(selector: tv => new HomeTvCardDto
            {
                Id = tv.Id,
                Title = tv.Title,
                TitleSort = tv.TitleSort,
                TranslatedTitle = tv
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = tv.Overview,
                TranslatedOverview = tv
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = tv.Poster,
                Backdrop = tv.Backdrop,
                Logo = tv
                    .Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                FirstAirDate = tv.FirstAirDate,
                CreatedAt = tv.CreatedAt,
                ColorPalette = tv._colorPalette,
                NumberOfEpisodes = tv.NumberOfEpisodes,
                EpisodesWithVideo = tv
                    .Episodes.Where(e => e.SeasonNumber > 0)
                    .Count(e =>
                        (
                            e.VideoFiles.Any(v => v.Folder != null)
                            || e.Tv.Episodes.Any(o =>
                                o.SeasonNumber == e.SeasonNumber
                                && o.VideoFiles.Any(w =>
                                    w.Folder != null
                                    && w.LastEpisodeNumber != null
                                    && o.EpisodeNumber <= e.EpisodeNumber
                                    && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                                )
                            )
                        )
                    ),
                CertificationRating = tv
                    .CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = tv
                    .CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    private static readonly Func<MediaContext, Guid, string, Task<Movie?>> GetRandomMovieQuery =
        EF.CompileAsyncQuery(
            queryExpression: (MediaContext mediaContext, Guid userId, string language) =>
                mediaContext
                    .Movies.AsNoTracking()
                    .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId)))
                    .Include(movie =>
                        movie.Translations.Where(translation => translation.Iso6391 == language)
                    )
                    .Include(movie => movie.Media.Where(media => media.Site == "YouTube"))
                    .Include(movie =>
                        movie.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en")
                    )
                    .Include(movie => movie.VideoFiles)
                    .Include(movie => movie.KeywordMovies)
                        .ThenInclude(keywordMovie => keywordMovie.Keyword)
                    .Include(movie => movie.CertificationMovies)
                        .ThenInclude(certificationMovie => certificationMovie.Certification)
                    .OrderBy(movie => EF.Functions.Random())
                    .FirstOrDefault()
        );

    public async Task<Movie?> GetRandomMovie(
        Guid userId,
        string language,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await GetRandomMovieQuery(arg1: context, arg2: userId, arg3: language);
    }

    public async Task<HomeMovieCardDto?> GetRandomMovieCardAsync(
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Movies.AsNoTracking()
            .Where(predicate: movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(predicate: movie => movie.VideoFiles.Any(v => v.Folder != null))
            .OrderBy(keySelector: movie => EF.Functions.Random())
            .Select(selector: movie => new HomeMovieCardDto
            {
                Id = movie.Id,
                Title = movie.Title,
                TitleSort = movie.TitleSort,
                TranslatedTitle = movie
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Title)
                    .FirstOrDefault(),
                Overview = movie.Overview,
                TranslatedOverview = movie
                    .Translations.Where(t => t.Iso6391 == language)
                    .Select(t => t.Overview)
                    .FirstOrDefault(),
                Poster = movie.Poster,
                Backdrop = movie.Backdrop,
                Logo = movie
                    .Images.Where(i => i.Type == "logo" && i.Iso6391 == "en")
                    .Select(i => i.FilePath)
                    .FirstOrDefault(),
                ReleaseDate = movie.ReleaseDate,
                CreatedAt = movie.CreatedAt,
                ColorPalette = movie._colorPalette,
                VideoFileCount = movie.VideoFiles.Count(v => v.Folder != null),
                CertificationRating = movie
                    .CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Rating)
                    .FirstOrDefault(),
                CertificationCountry = movie
                    .CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .Select(c => c.Certification.Iso31661)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    // public Task<Movie?> GetRandomMovie(Guid userId, string language)
    // {
    //     return context.Movies
    //         .AsNoTracking()
    //         .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId == userId))
    //         .Where(movie => movie.VideoFiles.Any(v => v.Folder != null))
    //         .Include(movie => movie.Translations.Where(t => t.Iso6391 == language))
    //         .Include(movie => movie.Images.Where(i => i.Type == "logo" && i.Iso6391 == "en").OrderByDescending(i => i.VoteAverage).ThenBy(i => i.Id).Take(1))
    //         .Include(movie => movie.VideoFiles.Where(v => v.Folder != null))
    //         .Include(movie => movie.CertificationMovies.Take(1))
    //             .ThenInclude(c => c.Certification)
    //         .OrderBy(movie => EF.Functions.Random())
    //         .FirstOrDefaultAsync();
    // }

    #region CRUD Operations

    public async Task AddLibraryAsync(Library library, Guid userId)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        await context
            .Libraries.Upsert(entity: library)
            .On(match: l => new { l.Id })
            .WhenMatched(
                updater: (ls, li) =>
                    new()
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
                    }
            )
            .RunAsync();

        await context
            .LibraryUser.Upsert(entity: new() { LibraryId = library.Id, UserId = userId })
            .On(match: lu => new { lu.LibraryId, lu.UserId })
            .WhenMatched(updater: (lus, lui) => new() { LibraryId = lui.LibraryId, UserId = lui.UserId })
            .RunAsync();
    }

    public async Task UpdateLibraryAsync(Library library)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();

        Library? tracked = await context.Libraries.FirstOrDefaultAsync(predicate: l => l.Id == library.Id);
        if (tracked is null)
            return;

        context.Entry(entity: tracked).CurrentValues.SetValues(obj: library);
        await context.SaveChangesAsync();
    }

    public async Task SetLibraryLanguagesAsync(Ulid libraryId, IEnumerable<int> languageIds)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();

        List<int> wanted = languageIds.Distinct().ToList();

        await context
            .LanguageLibrary.Where(predicate: ll =>
                ll.LibraryId == libraryId && !wanted.Contains(ll.LanguageId)
            )
            .ExecuteDeleteAsync();

        if (wanted.Count == 0)
            return;

        await context
            .LanguageLibrary.UpsertRange(
                entities: wanted.Select(selector: languageId => new LanguageLibrary(languageId: languageId, libraryId: libraryId))
            )
            .On(match: ll => new { ll.LanguageId, ll.LibraryId })
            .WhenMatched(
                updater: (lls, lli) => new() { LanguageId = lli.LanguageId, LibraryId = lli.LibraryId }
            )
            .RunAsync();
    }

    public async Task DeleteLibraryAsync(Library library)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        await context.Libraries.Where(predicate: l => l.Id == library.Id).ExecuteDeleteAsync();
    }

    public async Task<int> AddEncodingPresetFolderAsync(EncodingPresetFolder encodingPresetFolder)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        return await context
            .EncodingPresetFolders.Upsert(entity: encodingPresetFolder)
            .On(match: link => new { link.FolderId, link.PresetId })
            .WhenMatched(
                updater: (source, input) => new() { FolderId = input.FolderId, PresetId = input.PresetId }
            )
            .RunAsync();
    }

    public async Task<int> AddEncodingPresetFolderAsync(
        List<EncodingPresetFolder> encodingPresetFolders
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        return await context
            .EncodingPresetFolders.UpsertRange(entities: encodingPresetFolders)
            .On(match: link => new { link.FolderId, link.PresetId })
            .WhenMatched(
                updater: (links, linki) => new() { FolderId = linki.FolderId, PresetId = linki.PresetId }
            )
            .RunAsync();
    }

    public async Task<int> AddEncodingPresetFolderAsync(
        EncodingPresetFolder[] encodingPresetFolders
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        return await context
            .EncodingPresetFolders.UpsertRange(entities: encodingPresetFolders)
            .On(match: link => new { link.FolderId, link.PresetId })
            .WhenMatched(
                updater: (source, input) => new() { FolderId = input.FolderId, PresetId = input.PresetId }
            )
            .RunAsync();
    }

    public async Task<int> AddLanguageLibraryAsync(LanguageLibrary[] languageLibraries)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        return await context
            .LanguageLibrary.UpsertRange(entities: languageLibraries)
            .On(match: ll => new { ll.LibraryId, ll.LanguageId })
            .WhenMatched(
                updater: (lls, lli) => new() { LibraryId = lli.LibraryId, LanguageId = lli.LanguageId }
            )
            .RunAsync();
    }

    #endregion

    public async Task<int> SyncEncodingPresetFolderAsync(
        List<EncodingPresetFolder> encodingPresetFolders,
        List<Folder> folders
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync();

        try
        {
            await context
                .EncodingPresetFolders.Where(predicate: link =>
                    folders.Select(f => f.Id).Contains(link.FolderId)
                )
                .ExecuteDeleteAsync();

            int result = await context
                .EncodingPresetFolders.UpsertRange(entities: encodingPresetFolders)
                .On(match: link => new { link.FolderId, link.PresetId })
                .WhenMatched(
                    updater: (links, linki) => new() { FolderId = linki.FolderId, PresetId = linki.PresetId }
                )
                .RunAsync();

            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
