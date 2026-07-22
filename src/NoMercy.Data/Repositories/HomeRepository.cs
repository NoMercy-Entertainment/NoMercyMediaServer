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
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;

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

public class HomeRepository(MediaContext context, IDbContextFactory<MediaContext> contextFactory)
    : IHomeRepository
{
    public async Task<List<Genre>> GetHome(Guid userId, string? language, int take, int page = 0)
    {
        IOrderedQueryable<Genre> query = context
            .Genres.AsNoTracking()
            .OrderBy(keySelector: genre => genre.Name)
            .ThenBy(keySelector: genre => genre.Id)
            .Where(predicate: genre =>
                genre.GenreMovies.Any(g =>
                    g.Movie.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId))
                    != null
                )
                || genre.GenreTvShows.Any(g =>
                    g.Tv.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId)) != null
                )
            )
            .Include(navigationPropertyPath: genre =>
                genre.GenreMovies.Where(genreTv =>
                    genreTv.Movie.VideoFiles.Any(videoFile => videoFile.Folder != null) == true
                )
            )
            .Include(navigationPropertyPath: genre =>
                genre.GenreTvShows.Where(genreTv =>
                    genreTv.Tv.Episodes.Any(episode =>
                        episode.VideoFiles.Any(videoFile => videoFile.Folder != null)
                    ) == true
                )
            )
            .Include(navigationPropertyPath: movie =>
                movie.Translations.Where(translation => translation.Iso6391 == language)
            )
            .OrderBy(keySelector: genre => genre.Name)
            .ThenBy(keySelector: genre => genre.Id);

        List<Genre> genres = await query.Skip(count: page * take).Take(count: take).ToListAsync();

        return genres;
    }

    public async Task<List<HomeTvCardDto>> GetHomeTvs(
        List<int> tvIds,
        string? language,
        string country,
        CancellationToken ct = default
    )
    {
        return await context
            .Tvs.AsNoTracking()
            .Where(predicate: tv => tvIds.Contains(tv.Id))
            .Where(predicate: tv => tv.Episodes.Any(e => e.VideoFiles.Any()))
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

    public async Task<List<HomeMovieCardDto>> GetHomeMovies(
        List<int> movieIds,
        string? language,
        string country,
        CancellationToken ct = default
    )
    {
        return await context
            .Movies.AsNoTracking()
            .Where(predicate: movie => movieIds.Contains(movie.Id))
            .Where(predicate: movie => movie.VideoFiles.Any())
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

    public async Task<HashSet<UserData>> GetContinueWatchingAsync(
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        // Step 1: Project to minimal keys, deduplicate, and get unique UserData IDs
        // This avoids loading full entity trees for duplicates that get thrown away
        List<Ulid> uniqueIds = await context
            .UserData.AsNoTracking()
            .Where(predicate: ud => ud.UserId == userId)
            .Where(predicate: ud => !ud.RemovedFromContinueWatching)
            .Where(predicate: ud =>
                ud.MovieId != null
                || ud.TvId != null
                || ud.CollectionId != null
                || ud.SpecialId != null
            )
            .OrderByDescending(keySelector: ud => ud.LastPlayedDate)
            .ThenByDescending(keySelector: ud => ud.Id)
            .Select(selector: ud => new
            {
                ud.Id,
                ud.MovieId,
                ud.CollectionId,
                ud.TvId,
                ud.SpecialId,
            })
            .ToListAsync(cancellationToken: ct)
            .ContinueWith(
                continuationFunction: t =>
                    t.Result.DistinctBy(keySelector: ud => new
                        {
                            ud.MovieId,
                            ud.CollectionId,
                            ud.TvId,
                            ud.SpecialId,
                        })
                        .Select(selector: ud => ud.Id)
                        .ToList(),
                cancellationToken: ct
            );

        if (uniqueIds.Count == 0)
            return [];

        // Step 2: Hydrate only the unique entries with all includes
        List<UserData> userData = await context
            .UserData.AsNoTracking()
            .AsSplitQuery()
            .Where(predicate: ud => uniqueIds.Contains(ud.Id))
            .Include(navigationPropertyPath: ud => ud.VideoFile)
            // Movie includes - only what CardData needs
            .Include(navigationPropertyPath: ud => ud.Movie)
                .ThenInclude(navigationPropertyPath: m =>
                    m!
                        .Images.Where(image => image.Type == "logo" && image.Iso6391 == "en")
                        .OrderByDescending(image => image.VoteAverage)
                        .ThenBy(image => image.Id)
                        .Take(1)
                )
            .Include(navigationPropertyPath: ud => ud.Movie)
                .ThenInclude(navigationPropertyPath: m => m!.VideoFiles)
            .Include(navigationPropertyPath: ud => ud.Movie)
                .ThenInclude(navigationPropertyPath: m =>
                    m!
                        .CertificationMovies.Where(c =>
                            c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                        )
                        .OrderBy(c => c.CertificationId)
                        .Take(1)
                )
                    .ThenInclude(navigationPropertyPath: c => c.Certification)
            // Tv includes - only what CardData needs
            .Include(navigationPropertyPath: ud => ud.Tv)
                .ThenInclude(navigationPropertyPath: tv =>
                    tv!
                        .Images.Where(image => image.Type == "logo" && image.Iso6391 == "en")
                        .OrderByDescending(image => image.VoteAverage)
                        .ThenBy(image => image.Id)
                        .Take(1)
                )
            .Include(navigationPropertyPath: ud => ud.Tv)
                .ThenInclude(navigationPropertyPath: tv => tv!.Episodes.Where(e => e.SeasonNumber > 0))
                    .ThenInclude(navigationPropertyPath: e => e.VideoFiles)
            .Include(navigationPropertyPath: ud => ud.Tv)
                .ThenInclude(navigationPropertyPath: tv =>
                    tv!
                        .CertificationTvs.Where(c =>
                            c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                        )
                        .OrderBy(c => c.CertificationId)
                        .Take(1)
                )
                    .ThenInclude(navigationPropertyPath: c => c.Certification)
            // Collection includes - only what CardData needs
            .Include(navigationPropertyPath: ud => ud.Collection)
                .ThenInclude(navigationPropertyPath: c =>
                    c!
                        .Images.Where(image => image.Type == "logo" && image.Iso6391 == "en")
                        .OrderByDescending(image => image.VoteAverage)
                        .ThenBy(image => image.Id)
                        .Take(1)
                )
            .Include(navigationPropertyPath: ud => ud.Collection)
                .ThenInclude(navigationPropertyPath: c => c!.CollectionMovies)
                    .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                        .ThenInclude(navigationPropertyPath: m => m.VideoFiles)
            .Include(navigationPropertyPath: ud => ud.Collection)
                .ThenInclude(navigationPropertyPath: c => c!.CollectionMovies)
                    .ThenInclude(navigationPropertyPath: cm => cm.Movie)
                        .ThenInclude(navigationPropertyPath: m =>
                            m.CertificationMovies.Where(cert =>
                                    cert.Certification.Iso31661 == "US"
                                    || cert.Certification.Iso31661 == country
                                )
                                .OrderBy(cert => cert.CertificationId)
                                .Take(1)
                        )
                            .ThenInclude(navigationPropertyPath: c => c.Certification)
            // Special includes - only what CardData needs
            .Include(navigationPropertyPath: ud => ud.Special)
                .ThenInclude(navigationPropertyPath: s => s!.Items)
                    .ThenInclude(navigationPropertyPath: item => item.Movie)
                        .ThenInclude(navigationPropertyPath: m => m!.VideoFiles)
            .Include(navigationPropertyPath: ud => ud.Special)
                .ThenInclude(navigationPropertyPath: s => s!.Items)
                    .ThenInclude(navigationPropertyPath: item => item.Movie)
                        .ThenInclude(navigationPropertyPath: m =>
                            m!
                                .CertificationMovies.Where(c =>
                                    c.Certification.Iso31661 == "US"
                                    || c.Certification.Iso31661 == country
                                )
                                .OrderBy(c => c.CertificationId)
                                .Take(1)
                        )
                            .ThenInclude(navigationPropertyPath: c => c.Certification)
            .Include(navigationPropertyPath: ud => ud.Special)
                .ThenInclude(navigationPropertyPath: s => s!.Items)
                    .ThenInclude(navigationPropertyPath: item => item.Episode)
                        .ThenInclude(navigationPropertyPath: e => e!.VideoFiles)
            .Include(navigationPropertyPath: ud => ud.Special)
                .ThenInclude(navigationPropertyPath: s => s!.Items)
                    .ThenInclude(navigationPropertyPath: item => item.Episode)
                        .ThenInclude(navigationPropertyPath: e => e!.Tv)
                            .ThenInclude(navigationPropertyPath: tv =>
                                tv.CertificationTvs.Where(c =>
                                        c.Certification.Iso31661 == "US"
                                        || c.Certification.Iso31661 == country
                                    )
                                    .OrderBy(c => c.CertificationId)
                                    .Take(1)
                            )
                                .ThenInclude(navigationPropertyPath: c => c.Certification)
            .OrderByDescending(keySelector: ud => ud.LastPlayedDate)
            .ThenByDescending(keySelector: ud => ud.Id)
            .ToListAsync(cancellationToken: ct);

        return userData.ToHashSet();
    }

    /// <summary>
    /// Loads the current user's favorited video media — movies, tv shows,
    /// collections and specials — from the per-user like join tables
    /// (<c>MovieUser</c>/<c>TvUser</c>/<c>CollectionUser</c>/<c>SpecialUser</c>).
    /// Each entity is hydrated with exactly the includes its
    /// <c>NmCardDto</c> constructor needs; the caller maps and merges.
    /// Queries run sequentially against the shared context — favorites lists
    /// are small, and EF's DbContext is not safe for concurrent use.
    /// </summary>
    public async Task<FavoritesData> GetFavoritesAsync(
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        List<Movie> movies = await context
            .Movies.AsNoTracking()
            .Where(predicate: movie => movie.MovieUser.Any(mu => mu.UserId == userId))
            .Include(navigationPropertyPath: movie => movie.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: movie =>
                movie
                    .Images.Where(image => image.Type == "logo" && image.Iso6391 == "en")
                    .OrderByDescending(image => image.VoteAverage)
                    .ThenBy(image => image.Id)
                    .Take(1)
            )
            .Include(navigationPropertyPath: movie => movie.VideoFiles)
            .Include(navigationPropertyPath: movie =>
                movie
                    .CertificationMovies.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .OrderBy(c => c.CertificationId)
                    .Take(1)
            )
                .ThenInclude(navigationPropertyPath: c => c.Certification)
            .ToListAsync(cancellationToken: ct);

        List<Tv> tvShows = await context
            .Tvs.AsNoTracking()
            .Where(predicate: tv => tv.TvUser.Any(tu => tu.UserId == userId))
            .Include(navigationPropertyPath: tv => tv.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: tv =>
                tv.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en")
                    .OrderByDescending(image => image.VoteAverage)
                    .ThenBy(image => image.Id)
                    .Take(1)
            )
            .Include(navigationPropertyPath: tv => tv.Episodes.Where(episode => episode.SeasonNumber > 0))
                .ThenInclude(navigationPropertyPath: episode => episode.VideoFiles)
            .Include(navigationPropertyPath: tv =>
                tv.CertificationTvs.Where(c =>
                        c.Certification.Iso31661 == "US" || c.Certification.Iso31661 == country
                    )
                    .OrderBy(c => c.CertificationId)
                    .Take(1)
            )
                .ThenInclude(navigationPropertyPath: c => c.Certification)
            .ToListAsync(cancellationToken: ct);

        List<Collection> collections = await context
            .Collections.AsNoTracking()
            .Where(predicate: collection => collection.CollectionUser.Any(cu => cu.UserId == userId))
            .Include(navigationPropertyPath: collection => collection.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: collection =>
                collection
                    .Images.Where(image => image.Type == "logo" && image.Iso6391 == "en")
                    .OrderByDescending(image => image.VoteAverage)
                    .ThenBy(image => image.Id)
                    .Take(1)
            )
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: collectionMovie => collectionMovie.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie.VideoFiles)
            .Include(navigationPropertyPath: collection => collection.CollectionMovies)
                .ThenInclude(navigationPropertyPath: collectionMovie => collectionMovie.Movie)
                    .ThenInclude(navigationPropertyPath: movie =>
                        movie
                            .CertificationMovies.Where(c =>
                                c.Certification.Iso31661 == "US"
                                || c.Certification.Iso31661 == country
                            )
                            .OrderBy(c => c.CertificationId)
                            .Take(1)
                    )
                        .ThenInclude(navigationPropertyPath: c => c.Certification)
            .ToListAsync(cancellationToken: ct);

        List<Special> specials = await context
            .Specials.AsNoTracking()
            .AsSplitQuery()
            .Where(predicate: special => special.SpecialUser.Any(su => su.UserId == userId))
            .Include(navigationPropertyPath: special => special.Items)
                .ThenInclude(navigationPropertyPath: item => item.Movie)
                    .ThenInclude(navigationPropertyPath: m => m!.VideoFiles)
            .Include(navigationPropertyPath: special => special.Items)
                .ThenInclude(navigationPropertyPath: item => item.Movie)
                    .ThenInclude(navigationPropertyPath: m =>
                        m!
                            .CertificationMovies.Where(c =>
                                c.Certification.Iso31661 == "US"
                                || c.Certification.Iso31661 == country
                            )
                            .OrderBy(c => c.CertificationId)
                            .Take(1)
                    )
                        .ThenInclude(navigationPropertyPath: c => c.Certification)
            .Include(navigationPropertyPath: special => special.Items)
                .ThenInclude(navigationPropertyPath: item => item.Episode)
                    .ThenInclude(navigationPropertyPath: e => e!.VideoFiles)
            .Include(navigationPropertyPath: special => special.Items)
                .ThenInclude(navigationPropertyPath: item => item.Episode)
                    .ThenInclude(navigationPropertyPath: e => e!.Tv)
            .ToListAsync(cancellationToken: ct);

        return new(Movies: movies, TvShows: tvShows, Collections: collections, Specials: specials);
    }

    public Task<HashSet<Image>> GetScreensaverImagesAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return context
            .Images.AsNoTracking()
            .Where(predicate: image =>
                image.Movie!.Library.LibraryUsers.Any(u => u.UserId == userId)
                || image.Tv!.Library.LibraryUsers.Any(u => u.UserId == userId)
            )
            .Where(predicate: image => image._colorPalette != "")
            .Where(predicate: image =>
                (
                    image.Type == "backdrop"
                    && (image.Iso6391 == null || image.Iso6391 == "")
                    && image.Height >= 1080
                ) || (image.Type == "logo" && image.Iso6391 == "en")
            )
            .OrderByDescending(keySelector: image => image.Width)
            .ThenBy(keySelector: image => image.Id)
            .ToHashSetAsync(cancellationToken: ct);
    }

    public Task<List<Library>> GetLibrariesAsync(Guid userId, CancellationToken ct = default)
    {
        return context
            .Libraries.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: library => library.Type != MediaTypes.InboxMediaType)
            .ToListAsync(cancellationToken: ct);
    }

    public Task<int> GetAnimeCountAsync(Guid userId, CancellationToken ct = default)
    {
        return context
            .Tvs.AsNoTracking()
            .ForUser(userId: userId)
            .CountAsync(predicate: tv => tv.Library.Type == MediaTypes.AnimeMediaType, cancellationToken: ct);
    }

    public Task<int> GetMovieCountAsync(Guid userId, CancellationToken ct = default)
    {
        return context
            .Movies.AsNoTracking()
            .ForUser(userId: userId)
            .CountAsync(predicate: movie => movie.Library.Type == MediaTypes.MovieMediaType, cancellationToken: ct);
    }

    public Task<int> GetTvCountAsync(Guid userId, CancellationToken ct = default)
    {
        return context
            .Tvs.AsNoTracking()
            .ForUser(userId: userId)
            .CountAsync(predicate: tv => tv.Library.Type == MediaTypes.TvMediaType, cancellationToken: ct);
    }

    public async Task<List<GenreHomeDto>> GetHomeGenresAsync(
        Guid userId,
        string? language,
        int take,
        int page = 0,
        CancellationToken ct = default
    )
    {
        // Step 1: Fetch genre base data with translations — no client-side collections in projection
        // (SQLite does not support APPLY, so .ToList() inside .Select() is forbidden)
        List<Genre> genres = await context
            .Genres.AsNoTracking()
            .Where(predicate: genre =>
                genre.GenreMovies.Any(g =>
                    g.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || genre.GenreTvShows.Any(g =>
                    g.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Include(navigationPropertyPath: genre => genre.Translations.Where(t => t.Iso6391 == language))
            .Include(navigationPropertyPath: genre =>
                genre.GenreMovies.Where(gm =>
                    gm.Movie.Library.LibraryUsers.Any(u => u.UserId == userId)
                    && gm.Movie.VideoFiles.Any()
                )
            )
            .Include(navigationPropertyPath: genre =>
                genre.GenreTvShows.Where(gt =>
                    gt.Tv.Library.LibraryUsers.Any(u => u.UserId == userId)
                    && gt.Tv.Episodes.Any(e => e.VideoFiles.Any())
                )
            )
            .OrderBy(keySelector: genre => genre.Name)
            .ThenBy(keySelector: genre => genre.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);

        // Step 2: Project to DTO in memory — safe to call .ToList() on in-memory collections
        return genres
            .Select(selector: genre => new GenreHomeDto
            {
                Id = genre.Id,
                Name = genre.Name,
                TranslatedName = genre.Translations.FirstOrDefault()?.Name,
                MovieIds = genre.GenreMovies.Select(selector: gm => gm.MovieId).ToList(),
                TvIds = genre.GenreTvShows.Select(selector: gt => gt.TvId).ToList(),
            })
            .ToList();
    }

    public async Task<HomeParallelData> GetHomeParallelDataAsync(
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        // Each task creates its own DbContext — EF DbContext is not thread-safe
        Task<HashSet<UserData>> continueWatchingTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                HomeRepository repo = new(context: ctx, contextFactory: contextFactory);
                return await repo.GetContinueWatchingAsync(userId: userId, language: language, country: country, ct: ct);
            },
            cancellationToken: ct
        );

        Task<List<GenreHomeDto>> genreItemsTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                HomeRepository repo = new(context: ctx, contextFactory: contextFactory);
                return await repo.GetHomeGenresAsync(
                    userId: userId,
                    language: language,
                    take: UiLimits.MaximumItemsPerPage,
                    page: 0,
                    ct: ct
                );
            },
            cancellationToken: ct
        );

        Task<List<Library>> librariesTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                HomeRepository repo = new(context: ctx, contextFactory: contextFactory);
                return await repo.GetLibrariesAsync(userId: userId, ct: ct);
            },
            cancellationToken: ct
        );

        Task<int> animeCountTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                HomeRepository repo = new(context: ctx, contextFactory: contextFactory);
                return await repo.GetAnimeCountAsync(userId: userId, ct: ct);
            },
            cancellationToken: ct
        );

        Task<int> movieCountTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                HomeRepository repo = new(context: ctx, contextFactory: contextFactory);
                return await repo.GetMovieCountAsync(userId: userId, ct: ct);
            },
            cancellationToken: ct
        );

        Task<int> tvCountTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                HomeRepository repo = new(context: ctx, contextFactory: contextFactory);
                return await repo.GetTvCountAsync(userId: userId, ct: ct);
            },
            cancellationToken: ct
        );

        await Task.WhenAll(tasks: [continueWatchingTask, genreItemsTask, librariesTask, animeCountTask, movieCountTask, tvCountTask]
        );

        return new(
            ContinueWatching: continueWatchingTask.Result,
            GenreItems: genreItemsTask.Result,
            Libraries: librariesTask.Result,
            AnimeCount: animeCountTask.Result,
            MovieCount: movieCountTask.Result,
            TvCount: tvCountTask.Result
        );
    }

    public async Task<HomeTvsAndMoviesData> GetHomeTvsAndMoviesAsync(
        List<int> tvIds,
        List<int> movieIds,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        // Each task creates its own DbContext — EF DbContext is not thread-safe
        Task<List<HomeTvCardDto>> tvDataTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                HomeRepository repo = new(context: ctx, contextFactory: contextFactory);
                return await repo.GetHomeTvs(tvIds: tvIds, language: language, country: country, ct: ct);
            },
            cancellationToken: ct
        );

        Task<List<HomeMovieCardDto>> movieDataTask = Task.Run(
            function: async () =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                HomeRepository repo = new(context: ctx, contextFactory: contextFactory);
                return await repo.GetHomeMovies(movieIds: movieIds, language: language, country: country, ct: ct);
            },
            cancellationToken: ct
        );

        await Task.WhenAll(tasks: [tvDataTask, movieDataTask]);

        return new(TvData: tvDataTask.Result, MovieData: movieDataTask.Result);
    }
}
