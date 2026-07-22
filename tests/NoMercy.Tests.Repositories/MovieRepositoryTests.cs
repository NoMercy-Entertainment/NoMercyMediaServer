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
using NoMercy.Database.Models.Movies;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class MovieRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;
    private readonly MovieRepository _repository;

    public MovieRepositoryTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateSeededFactory();
        _context = _factory.CreateDbContext();
        _repository = new(contextFactory: _factory, logger: NullLogger<MovieRepository>.Instance);
    }

    [Fact]
    public async Task GetMovieAsync_ReturnsMovie_WhenUserHasAccess()
    {
        Movie? movie = await _repository.GetMovieAsync(userId: SeedConstants.UserId, id: 129, language: "en", country: "US");

        Assert.NotNull(@object: movie);
        Assert.Equal(expected: 129, actual: movie.Id);
        Assert.Equal(expected: "Spirited Away", actual: movie.Title);
    }

    [Fact]
    public async Task GetMovieAsync_ReturnsNull_WhenUserHasNoAccess()
    {
        Movie? movie = await _repository.GetMovieAsync(userId: SeedConstants.OtherUserId, id: 129, language: "en", country: "US");

        Assert.Null(@object: movie);
    }

    [Fact]
    public async Task GetMovieAsync_ReturnsNull_WhenMovieDoesNotExist()
    {
        Movie? movie = await _repository.GetMovieAsync(userId: SeedConstants.UserId, id: 999999, language: "en", country: "US");

        Assert.Null(@object: movie);
    }

    [Fact]
    public async Task GetMovieAsync_IncludesVideoFiles()
    {
        Movie? movie = await _repository.GetMovieAsync(userId: SeedConstants.UserId, id: 129, language: "en", country: "US");

        Assert.NotNull(@object: movie);
        Assert.NotEmpty(collection: movie.VideoFiles);
        Assert.Contains(collection: movie.VideoFiles, filter: vf => vf.Filename == "Spirited.Away.2001.1080p.mkv");
    }

    [Fact]
    public async Task GetMovieAvailableAsync_ReturnsTrue_WhenMovieHasVideoFiles()
    {
        bool available = await _repository.GetMovieAvailableAsync(userId: SeedConstants.UserId, id: 129);

        Assert.True(condition: available);
    }

    [Fact]
    public async Task GetMovieAvailableAsync_ReturnsFalse_WhenUserHasNoAccess()
    {
        bool available = await _repository.GetMovieAvailableAsync(userId: SeedConstants.OtherUserId, id: 129);

        Assert.False(condition: available);
    }

    [Fact]
    public async Task GetMoviePlaylistAsync_ReturnsMovieWithVideoFiles()
    {
        List<Movie> playlist = await _repository.GetMoviePlaylistAsync(
            userId: SeedConstants.UserId,
            id: 129,
            language: "en",
            country: "US"
        );

        Assert.NotEmpty(collection: playlist);
        Assert.Equal(expected: 129, actual: playlist[index: 0].Id);
        Assert.NotEmpty(collection: playlist[index: 0].VideoFiles);
    }

    [Fact]
    public async Task DeleteMovieAsync_RemovesMovie()
    {
        await _repository.DeleteAsync(id: 129);

        Movie? movie = await _repository.GetMovieAsync(userId: SeedConstants.UserId, id: 129, language: "en", country: "US");

        Assert.Null(@object: movie);
    }

    [Fact]
    public async Task LikeMovieAsync_AddsMovieUser_WhenLikeIsTrue()
    {
        bool result = await _repository.LikeMovieAsync(id: 129, userId: SeedConstants.UserId, like: true);

        Assert.True(condition: result);

        MovieUser? movieUser = _context.MovieUser.FirstOrDefault(predicate: mu =>
            mu.MovieId == 129 && mu.UserId == SeedConstants.UserId
        );
        Assert.NotNull(@object: movieUser);
    }

    [Fact]
    public async Task LikeMovieAsync_RemovesMovieUser_WhenLikeIsFalse()
    {
        await _repository.LikeMovieAsync(id: 129, userId: SeedConstants.UserId, like: true);
        bool result = await _repository.LikeMovieAsync(id: 129, userId: SeedConstants.UserId, like: false);

        Assert.True(condition: result);

        MovieUser? movieUser = _context.MovieUser.FirstOrDefault(predicate: mu =>
            mu.MovieId == 129 && mu.UserId == SeedConstants.UserId
        );
        Assert.Null(@object: movieUser);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _connection.Dispose();
    }
}
