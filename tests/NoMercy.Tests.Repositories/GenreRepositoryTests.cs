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

using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class GenreRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly GenreRepository _repository;

    public GenreRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(context: _context);
    }

    [Fact]
    public async Task GetGenreAsync_ReturnsGenre_WhenUserHasAccess()
    {
        Genre? genre = await _repository.GetGenreAsync(userId: SeedConstants.UserId, id: 18, language: "en", country: "US", take: 10, page: 0);

        Assert.NotNull(@object: genre);
        Assert.Equal(expected: "Drama", actual: genre.Name);
    }

    [Fact]
    public async Task GetGenreAsync_ReturnsNull_WhenGenreDoesNotExist()
    {
        Genre? genre = await _repository.GetGenreAsync(
            userId: SeedConstants.UserId,
            id: 999,
            language: "en",
            country: "US",
            take: 10,
            page: 0
        );

        Assert.Null(@object: genre);
    }

    [Fact]
    public async Task GetGenreAsync_IncludesMoviesForUser()
    {
        Genre? genre = await _repository.GetGenreAsync(userId: SeedConstants.UserId, id: 18, language: "en", country: "US", take: 10, page: 0);

        Assert.NotNull(@object: genre);
        Assert.NotEmpty(collection: genre.GenreMovies);
    }

    [Fact]
    public async Task GetGenreAsync_IncludesTvShowsForUser()
    {
        Genre? genre = await _repository.GetGenreAsync(userId: SeedConstants.UserId, id: 18, language: "en", country: "US", take: 10, page: 0);

        Assert.NotNull(@object: genre);
        Assert.NotEmpty(collection: genre.GenreTvShows);
    }

    [Fact]
    public async Task GetGenres_ReturnsGenresForUser()
    {
        List<Genre> genres = await _repository.GetGenres(userId: SeedConstants.UserId, language: "en", take: 10, page: 0);

        Assert.Equal(expected: 2, actual: genres.Count);
        Assert.Contains(collection: genres, filter: g => g.Name == "Action");
        Assert.Contains(collection: genres, filter: g => g.Name == "Drama");
    }

    [Fact]
    public async Task GetGenres_ReturnsEmpty_WhenUserHasNoAccess()
    {
        List<Genre> genres = await _repository.GetGenres(userId: SeedConstants.OtherUserId, language: "en", take: 10, page: 0);

        Assert.Empty(collection: genres);
    }

    [Fact]
    public async Task GetGenresWithCountsAsync_ReturnsCorrectCounts()
    {
        List<GenreWithCountsDto> genres = await _repository.GetGenresWithCountsAsync(
            userId: SeedConstants.UserId,
            language: "en",
            take: 10,
            page: 0
        );

        GenreWithCountsDto? dramaGenre = genres.FirstOrDefault(predicate: g => g.Name == "Drama");
        Assert.NotNull(@object: dramaGenre);
        Assert.Equal(expected: 2, actual: dramaGenre.TotalMovies);
        Assert.Equal(expected: 1, actual: dramaGenre.TotalTvShows);
        Assert.Equal(expected: 2, actual: dramaGenre.MoviesWithVideo);
        Assert.Equal(expected: 1, actual: dramaGenre.TvShowsWithVideo);

        GenreWithCountsDto? actionGenre = genres.FirstOrDefault(predicate: g => g.Name == "Action");
        Assert.NotNull(@object: actionGenre);
        Assert.Equal(expected: 1, actual: actionGenre.TotalMovies);
        Assert.Equal(expected: 0, actual: actionGenre.TotalTvShows);
    }

    [Fact]
    public async Task GetGenres_RespectsPageAndTake()
    {
        List<Genre> genres = await _repository.GetGenres(userId: SeedConstants.UserId, language: "en", take: 1, page: 0);

        Assert.Single(collection: genres);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
