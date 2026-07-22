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
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class HomeRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly HomeRepository _repository;
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _factoryConnection;

    public HomeRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        (_factory, _factoryConnection) = TestMediaContextFactory.CreateFactory();
        _repository = new(context: _context, contextFactory: _factory);
    }

    [Fact]
    public async Task GetHomeMovies_ReturnsMoviesById()
    {
        List<HomeMovieCardDto> movies = await _repository.GetHomeMovies(movieIds: [129, 680], language: "en", country: "US");

        Assert.Equal(expected: 2, actual: movies.Count);
        Assert.Contains(collection: movies, filter: m => m.Id == 129);
        Assert.Contains(collection: movies, filter: m => m.Id == 680);
    }

    [Fact]
    public async Task GetHomeMovies_ReturnsEmpty_WhenIdsNotFound()
    {
        List<HomeMovieCardDto> movies = await _repository.GetHomeMovies(movieIds: [999999], language: "en", country: "US");

        Assert.Empty(collection: movies);
    }

    [Fact]
    public async Task GetHomeTvs_ReturnsTvShowsById()
    {
        List<HomeTvCardDto> shows = await _repository.GetHomeTvs(tvIds: [1399], language: "en", country: "US");

        Assert.Single(collection: shows);
        Assert.Equal(expected: 1399, actual: shows[index: 0].Id);
    }

    [Fact]
    public async Task GetMovieCountAsync_ReturnsCorrectCount()
    {
        int count = await _repository.GetMovieCountAsync(userId: SeedConstants.UserId);

        Assert.Equal(expected: 2, actual: count);
    }

    [Fact]
    public async Task GetTvCountAsync_ReturnsCorrectCount()
    {
        int count = await _repository.GetTvCountAsync(userId: SeedConstants.UserId);

        Assert.Equal(expected: 1, actual: count);
    }

    [Fact]
    public async Task GetMovieCountAsync_ReturnsZero_WhenUserHasNoAccess()
    {
        int count = await _repository.GetMovieCountAsync(userId: SeedConstants.OtherUserId);

        Assert.Equal(expected: 0, actual: count);
    }

    [Fact]
    public async Task GetLibrariesAsync_ReturnsLibrariesForUser()
    {
        List<Library> libraries = await _repository.GetLibrariesAsync(userId: SeedConstants.UserId);

        Assert.Equal(expected: 2, actual: libraries.Count);
    }

    [Fact]
    public async Task GetLibrariesAsync_ReturnsEmpty_WhenUserHasNoAccess()
    {
        List<Library> libraries = await _repository.GetLibrariesAsync(userId: SeedConstants.OtherUserId);

        Assert.Empty(collection: libraries);
    }

    [Fact]
    public async Task GetHomeGenresAsync_ReturnsGenresForUser()
    {
        List<GenreHomeDto> genres = await _repository.GetHomeGenresAsync(
            userId: SeedConstants.UserId,
            language: "en",
            take: 10
        );

        Assert.Equal(expected: 2, actual: genres.Count);
        Assert.Contains(collection: genres, filter: g => g.Name == "Action");
        Assert.Contains(collection: genres, filter: g => g.Name == "Drama");
    }

    [Fact]
    public async Task GetHomeGenresAsync_RespectsPageAndTake()
    {
        List<GenreHomeDto> genres = await _repository.GetHomeGenresAsync(
            userId: SeedConstants.UserId,
            language: "en",
            take: 1
        );

        Assert.Single(collection: genres);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_ReturnsDeduplicated()
    {
        // Seed has 3 UserData rows: 2 for movie 129 (duplicate), 1 for tv 1399
        // DistinctBy on { MovieId, CollectionId, TvId, SpecialId } should yield 2 unique entries
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            userId: SeedConstants.UserId,
            language: "en",
            country: "US"
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Contains(collection: result, filter: ud => ud.MovieId == 129);
        Assert.Contains(collection: result, filter: ud => ud.TvId == 1399);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_KeepsMostRecentPerGroup()
    {
        // The most recent entry for movie 129 has LastPlayedDate 2026-02-01
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            userId: SeedConstants.UserId,
            language: "en",
            country: "US"
        );

        UserData? movieEntry = result.FirstOrDefault(predicate: ud => ud.MovieId == 129);
        Assert.NotNull(@object: movieEntry);
        Assert.Equal(expected: "2026-02-01T10:00:00Z", actual: movieEntry.LastPlayedDate);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_IncludesVideoFile()
    {
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            userId: SeedConstants.UserId,
            language: "en",
            country: "US"
        );

        Assert.All(collection: result, action: ud => Assert.NotNull(@object: ud.VideoFile));
    }

    [Fact]
    public async Task GetContinueWatchingAsync_IncludesMovieData()
    {
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            userId: SeedConstants.UserId,
            language: "en",
            country: "US"
        );

        UserData? movieEntry = result.FirstOrDefault(predicate: ud => ud.MovieId == 129);
        Assert.NotNull(@object: movieEntry);
        Assert.NotNull(@object: movieEntry.Movie);
        Assert.NotEmpty(collection: movieEntry.Movie.VideoFiles);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_ReturnsEmpty_WhenNoUserData()
    {
        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            userId: SeedConstants.OtherUserId,
            language: "en",
            country: "US"
        );

        Assert.Empty(collection: result);
    }

    [Fact]
    public async Task GetContinueWatchingAsync_ExcludesRowsHiddenFromContinueWatching()
    {
        // Hide both movie 129 rows the way UserDataController.RemoveContinue does —
        // the rows must stay in the table, only disappear from the carousel query.
        List<UserData> movieRows = await _context
            .UserData.Where(predicate: ud => ud.MovieId == 129)
            .ToListAsync();
        foreach (UserData row in movieRows)
            row.RemovedFromContinueWatching = true;
        await _context.SaveChangesAsync();

        HashSet<UserData> result = await _repository.GetContinueWatchingAsync(
            userId: SeedConstants.UserId,
            language: "en",
            country: "US"
        );

        Assert.Single(collection: result);
        Assert.DoesNotContain(collection: result, filter: ud => ud.MovieId == 129);
        Assert.Contains(collection: result, filter: ud => ud.TvId == 1399);

        // The hidden rows must still exist for the recommendation engine.
        int survivingRows = await _context.UserData.CountAsync(predicate: ud => ud.MovieId == 129);
        Assert.Equal(expected: 2, actual: survivingRows);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _factoryConnection.Dispose();
    }
}
