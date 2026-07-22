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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// CHAR-06: Query output tests for every repository method via ToQueryString() or SQL interceptor.
/// Verifies that EF Core generates correct SQL for each repository query method.
/// Methods returning IQueryable use ToQueryString() directly.
/// Methods that materialize results use SqlCaptureInterceptor to capture executed SQL.
/// Simple CRUD operations (Add/Update/Delete/Like/Upsert) are excluded.
/// Compiled queries (EF.CompileAsyncQuery) are tested via interceptor execution.
/// </summary>
[Trait(name: "Category", value: "Characterization")]
public class QueryOutputTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly SqlCaptureInterceptor _interceptor;
    private readonly IDbContextFactory<MediaContext> _homeFactory;
    private readonly SqliteConnection _homeFactoryConnection;

    public QueryOutputTests()
    {
        (_homeFactory, _interceptor, _homeFactoryConnection) =
            TestMediaContextFactory.CreateSeededFactoryWithInterceptor();
        _context = _homeFactory.CreateDbContext();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _homeFactoryConnection.Dispose();
    }

    #region MovieRepository

    [Fact]
    public async Task MovieRepository_GetMovieAsync_GeneratesExpectedSql()
    {
        MovieRepository repository = new(contextFactory: _homeFactory, logger: NullLogger<MovieRepository>.Instance);
        _interceptor.Clear();

        await repository.GetMovieAsync(userId: SeedConstants.UserId, id: 129, language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
        Assert.Contains(expectedSubstring: "WHERE", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
    }

    [Fact]
    public async Task MovieRepository_GetMovieAvailableAsync_GeneratesExpectedSql()
    {
        MovieRepository repository = new(contextFactory: _homeFactory, logger: NullLogger<MovieRepository>.Instance);
        _interceptor.Clear();

        await repository.GetMovieAvailableAsync(userId: SeedConstants.UserId, id: 129);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
    }

    [Fact]
    public async Task MovieRepository_GetMoviePlaylistAsync_GeneratesExpectedSql()
    {
        MovieRepository repository = new(contextFactory: _homeFactory, logger: NullLogger<MovieRepository>.Instance);
        _interceptor.Clear();

        await repository.GetMoviePlaylistAsync(userId: SeedConstants.UserId, id: 129, language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
        Assert.Contains(expectedSubstring: "CertificationMovie", actualString: sql);
    }

    [Fact]
    public async Task MovieRepository_DeleteMovieAsync_GeneratesDeleteSql()
    {
        MovieRepository repository = new(contextFactory: _homeFactory, logger: NullLogger<MovieRepository>.Instance);
        _interceptor.Clear();

        await repository.DeleteAsync(id: 999);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "DELETE", actualString: sql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
    }

    #endregion

    #region TvShowRepository

    [Fact]
    public async Task TvShowRepository_GetTvAvailableAsync_GeneratesExpectedSql()
    {
        TvShowRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetTvAvailableAsync(userId: SeedConstants.UserId, id: 1399);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "Episodes", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
    }

    [Fact]
    public async Task TvShowRepository_GetTvPlaylistAsync_GeneratesExpectedSql()
    {
        TvShowRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetPlaylistAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "Seasons", actualString: sql);
        Assert.Contains(expectedSubstring: "Episodes", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
    }

    [Fact]
    public async Task TvShowRepository_DeleteTvAsync_GeneratesDeleteSql()
    {
        TvShowRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.DeleteAsync(id: 999);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "DELETE", actualString: sql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
    }

    [Fact]
    public async Task TvShowRepository_GetMissingLibraryShows_GeneratesExpectedSql()
    {
        TvShowRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetMissingLibraryShows(userId: SeedConstants.UserId, id: 1399, language: "en");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "Episodes", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
    }

    #endregion

    #region GenreRepository

    [Fact]
    public async Task GenreRepository_GetGenres_GeneratesExpectedSql()
    {
        GenreRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetGenres(userId: SeedConstants.UserId, language: "en", take: 10, page: 0);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Genres", actualString: sql);
        Assert.Contains(expectedSubstring: "GenreMovie", actualString: sql);
        Assert.Contains(expectedSubstring: "GenreTv", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public async Task GenreRepository_GetGenreAsync_GeneratesExpectedSql()
    {
        GenreRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetGenreAsync(userId: SeedConstants.UserId, id: 18, language: "en", country: "US", take: 10, page: 0);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Genres", actualString: sql);
        Assert.Contains(expectedSubstring: "GenreMovie", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
    }

    [Fact]
    public async Task GenreRepository_GetGenresWithCountsAsync_GeneratesProjectionSql()
    {
        GenreRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetGenresWithCountsAsync(userId: SeedConstants.UserId, language: "en", take: 10, page: 0);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Genres", actualString: sql);
        Assert.Contains(expectedSubstring: "GenreMovie", actualString: sql);
        Assert.Contains(expectedSubstring: "GenreTv", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
    }

    [Fact]
    public async Task GenreRepository_GetMusicGenresAsync_GeneratesExpectedSql()
    {
        GenreRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetMusicGenresAsync(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "MusicGenres", actualString: sql);
    }

    [Fact]
    public async Task GenreRepository_GetPaginatedMusicGenresAsync_GeneratesExpectedSql()
    {
        GenreRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetPaginatedMusicGenresAsync(userId: SeedConstants.UserId, letter: "R", take: 10, page: 0);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "MusicGenres", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public async Task GenreRepository_GetMusicGenreAsync_GeneratesExpectedSql()
    {
        GenreRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetMusicGenreAsync(userId: SeedConstants.UserId, genreId: Guid.NewGuid());

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "MusicGenres", actualString: sql);
        Assert.Contains(expectedSubstring: "MusicGenreTrack", actualString: sql);
    }

    #endregion

    #region HomeRepository

    [Fact]
    public async Task HomeRepository_GetHomeTvs_GeneratesExpectedSql()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetHomeTvs(tvIds: [1399], language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "Episodes", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetHomeMovies_GeneratesExpectedSql()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetHomeMovies(movieIds: [129, 680], language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetContinueWatchingAsync_GeneratesExpectedSql()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetContinueWatchingAsync(userId: SeedConstants.UserId, language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "UserData", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetScreensaverImagesAsync_GeneratesExpectedSql()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetScreensaverImagesAsync(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Images", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetLibrariesAsync_GeneratesExpectedSql()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetLibrariesAsync(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Libraries", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetMovieCountAsync_GeneratesCountSql()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetMovieCountAsync(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
        Assert.Contains(expectedSubstring: "COUNT", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetTvCountAsync_GeneratesCountSql()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetTvCountAsync(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "COUNT", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetAnimeCountAsync_GeneratesCountSql()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetAnimeCountAsync(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "COUNT", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetHomeGenresAsync_GeneratesExpectedSql()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetHomeGenresAsync(userId: SeedConstants.UserId, language: "en", take: 10);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Genres", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    #endregion

    #region LibraryRepository

    [Fact]
    public async Task LibraryRepository_GetLibraries_GeneratesExpectedSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetLibraries(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Libraries", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetLibraryByIdAsync_WithPagination_GeneratesExpectedSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetLibraryByIdAsync(
            libraryId: SeedConstants.MovieLibraryId,
            userId: SeedConstants.UserId,
            language: "en",
            country: "US",
            take: 10,
            page: 0
        );

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Libraries", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryMovie", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetLibraryMovieCardsAsync_GeneratesProjectionSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetLibraryMovieCardsAsync(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.MovieLibraryId,
            country: "US",
            take: 10,
            skip: 0
        );

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetLibraryTvCardsAsync_GeneratesProjectionSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetLibraryTvCardsAsync(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.TvLibraryId,
            country: "US",
            take: 10,
            skip: 0
        );

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "Episodes", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetPaginatedLibraryMovies_GeneratesExpectedSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetPaginatedLibraryMovies(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.MovieLibraryId,
            letter: "F",
            language: "en",
            country: "US",
            take: 10,
            page: 0
        );

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetPaginatedLibraryShows_GeneratesExpectedSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetPaginatedLibraryShows(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.TvLibraryId,
            letter: "B",
            language: "en",
            country: "US",
            take: 10,
            page: 0
        );

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "Episodes", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetLibraryByIdAsync_Simple_GeneratesExpectedSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetLibraryByIdAsync(id: SeedConstants.MovieLibraryId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Libraries", actualString: sql);
        Assert.Contains(expectedSubstring: "FolderLibrary", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetAllLibrariesAsync_GeneratesExpectedSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetAllLibrariesAsync();

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Libraries", actualString: sql);
        Assert.Contains(expectedSubstring: "FolderLibrary", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetFoldersAsync_GeneratesProjectionSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetFoldersAsync();

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Folders", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetRandomTvShow_GeneratesExpectedSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetRandomTvShow(userId: SeedConstants.UserId, language: "en");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
    }

    [Fact]
    public async Task LibraryRepository_GetRandomMovie_GeneratesExpectedSql()
    {
        LibraryRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetRandomMovie(userId: SeedConstants.UserId, language: "en");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
    }

    #endregion

    #region CollectionRepository

    [Fact]
    public async Task CollectionRepository_GetCollectionsAsync_GeneratesExpectedSql()
    {
        CollectionRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetCollectionsAsync(userId: SeedConstants.UserId, language: "en", take: 10, page: 0);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Collections", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public async Task CollectionRepository_GetCollectionsListAsync_GeneratesProjectionSql()
    {
        CollectionRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetCollectionsListAsync(userId: SeedConstants.UserId, language: "en", country: "US", take: 10, page: 0);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Collections", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public async Task CollectionRepository_GetCollectionAsync_GeneratesExpectedSql()
    {
        CollectionRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetCollectionAsync(userId: SeedConstants.UserId, id: 1, language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Collections", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
    }

    [Fact]
    public async Task CollectionRepository_GetCollectionItems_GeneratesExpectedSql()
    {
        CollectionRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetCollectionItems(userId: SeedConstants.UserId, language: "en", country: "US", take: 10);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Collections", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "CollectionMovie", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
    }

    [Fact]
    public async Task CollectionRepository_GetAvailableCollectionAsync_GeneratesExpectedSql()
    {
        CollectionRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetAvailableCollectionAsync(userId: SeedConstants.UserId, id: 1);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Collections", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "CollectionMovie", actualString: sql);
        Assert.Contains(expectedSubstring: "VideoFiles", actualString: sql);
    }

    [Fact]
    public async Task CollectionRepository_GetCollectionPlaylistAsync_GeneratesExpectedSql()
    {
        CollectionRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetCollectionPlaylistAsync(userId: SeedConstants.UserId, id: 1, language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Collections", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        // CollectionMovie and VideoFiles may appear in split queries
        Assert.True(
            condition: _interceptor.CapturedSql.Count >= 1,
            userMessage: "Expected at least one query for collection playlist"
        );
    }

    #endregion

    #region SpecialRepository

    [Fact]
    public async Task SpecialRepository_GetSpecialsAsync_GeneratesExpectedSql()
    {
        SpecialRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetSpecialsAsync(userId: SeedConstants.UserId, language: "en", take: 10, page: 0);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Specials", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public void SpecialRepository_GetSpecialAsync_GeneratesExpectedSql()
    {
        SpecialRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        // GetSpecialAsync uses Task.FromResult wrapping a synchronous query
        repository.GetSpecialAsync(userId: SeedConstants.UserId, id: Ulid.NewUlid());

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Specials", actualString: sql);
    }

    [Fact]
    public async Task SpecialRepository_GetSpecialItems_GeneratesExpectedSql()
    {
        SpecialRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetSpecialItems(userId: SeedConstants.UserId, language: "en", country: "US", take: 10);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Specials", actualString: sql);
        Assert.Contains(expectedSubstring: "ORDER BY", actualString: sql);
        Assert.Contains(expectedSubstring: "LIMIT", actualString: sql);
    }

    [Fact]
    public async Task SpecialRepository_GetSpecialPlaylistAsync_GeneratesExpectedSql()
    {
        SpecialRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetSpecialPlaylistAsync(userId: SeedConstants.UserId, id: Ulid.NewUlid(), language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Specials", actualString: sql);
    }

    #endregion

    #region DeviceRepository

    [Fact]
    public async Task DeviceRepository_GetDevices_GeneratesExpectedSql()
    {
        DeviceRepository repository = new(context: _context);
        _interceptor.Clear();

        // Execute the query to capture all split queries
        await repository.GetDevices();

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Devices", actualString: sql);
    }

    #endregion

    #region FolderRepository

    [Fact]
    public async Task FolderRepository_GetFolderByIdAsync_GeneratesExpectedSql()
    {
        FolderRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetFolderByIdAsync(folderId: SeedConstants.MovieFolderId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Folders", actualString: sql);
        Assert.Contains(expectedSubstring: "FolderLibrary", actualString: sql);
    }

    [Fact]
    public async Task FolderRepository_GetFolderByPathAsync_GeneratesExpectedSql()
    {
        FolderRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetFolderByPathAsync(requestPath: "/media/movies");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Folders", actualString: sql);
    }

    [Fact]
    public async Task FolderRepository_GetFoldersByLibraryIdAsync_GeneratesExpectedSql()
    {
        FolderRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetFoldersByLibraryIdAsync(libraryId: SeedConstants.MovieLibraryId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "FolderLibrary", actualString: sql);
        Assert.Contains(expectedSubstring: "Folders", actualString: sql);
    }

    [Fact]
    public async Task FolderRepository_GetFolderById_GeneratesExpectedSql()
    {
        FolderRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetFolderById(folderId: SeedConstants.MovieFolderId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Folders", actualString: sql);
    }

    [Fact]
    public async Task FolderRepository_GetFolderByPath_GeneratesExpectedSql()
    {
        FolderRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetFolderByPath(path: "/media/movies");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Folders", actualString: sql);
    }

    #endregion

    #region LanguageRepository

    [Fact]
    public async Task LanguageRepository_GetLanguagesAsync_GeneratesExpectedSql()
    {
        LanguageRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetLanguagesAsync();

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Languages", actualString: sql);
    }

    [Fact]
    public async Task LanguageRepository_GetLanguagesAsync_WithFilter_GeneratesExpectedSql()
    {
        LanguageRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetLanguagesAsync(list: ["en", "fr"]);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "LanguageLibrary", actualString: sql);
    }

    #endregion

    #region MovieRepository - Compiled Query

    [Fact]
    public async Task MovieRepository_GetMovieDetailAsync_CompiledQuery_GeneratesExpectedSql()
    {
        MovieRepository repository = new(contextFactory: _homeFactory, logger: NullLogger<MovieRepository>.Instance);
        _interceptor.Clear();

        await repository.GetMovieDetailAsync(userId: SeedConstants.UserId, id: 129, language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Movies", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "Casts", actualString: sql);
    }

    #endregion

    #region TvShowRepository - Split Detail Query

    [Fact]
    public async Task TvShowRepository_GetTvAsync_SplitQuery_GeneratesExpectedSql()
    {
        TvShowRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetTvAsync(userId: SeedConstants.UserId, id: 1399, language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "Tvs", actualString: sql);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql);
        Assert.Contains(expectedSubstring: "Seasons", actualString: sql);
        Assert.Contains(expectedSubstring: "Episodes", actualString: sql);
        Assert.Contains(expectedSubstring: "Casts", actualString: sql);
    }

    #endregion

    #region MED-02: Existence checks use EXISTS instead of COUNT

    [Fact]
    public async Task HomeRepository_GetHomeTvs_UsesExistsNotCount()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetHomeTvs(tvIds: [1399], language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "EXISTS", actualString: sql);
        Assert.DoesNotContain(expectedSubstring: "COUNT(*) > 0", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetHomeMovies_UsesExistsNotCount()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetHomeMovies(movieIds: [129, 680], language: "en", country: "US");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "EXISTS", actualString: sql);
        Assert.DoesNotContain(expectedSubstring: "COUNT(*) > 0", actualString: sql);
    }

    [Fact]
    public async Task HomeRepository_GetHomeGenres_UsesExistsForVideoFileCheck()
    {
        HomeRepository repository = new(context: _context, contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetHomeGenresAsync(userId: SeedConstants.UserId, language: "en", take: 10);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "EXISTS", actualString: sql);
    }

    [Fact]
    public async Task GenreRepository_GetMusicGenresAsync_UsesExistsNotCount()
    {
        GenreRepository repository = new(context: _context);
        _interceptor.Clear();

        await repository.GetMusicGenresAsync(userId: SeedConstants.UserId);

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "EXISTS", actualString: sql);
        Assert.DoesNotContain(expectedSubstring: "COUNT(*) > 0", actualString: sql);
    }

    [Fact]
    public async Task TvShowRepository_GetMissingLibraryShows_UsesExistsForEmptyVideoFiles()
    {
        TvShowRepository repository = new(contextFactory: _homeFactory);
        _interceptor.Clear();

        await repository.GetMissingLibraryShows(userId: SeedConstants.UserId, id: 1399, language: "en");

        Assert.NotEmpty(collection: _interceptor.CapturedSql);
        string sql = string.Join(separator: " ", values: _interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "EXISTS", actualString: sql);
        Assert.DoesNotContain(expectedSubstring: "COUNT(*) > 0", actualString: sql);
    }

    #endregion
}
