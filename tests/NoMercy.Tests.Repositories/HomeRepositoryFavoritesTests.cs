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

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// Covers HomeRepository.GetFavoritesAsync — the data source behind
/// GET /api/v1/userData/favorites. Each test seeds the per-user "like" join
/// tables (MovieUser/TvUser/CollectionUser/SpecialUser) directly, the same
/// rows the MoviesController.Like-style endpoints persist.
/// </summary>
[Trait(name: "Category", value: "Characterization")]
public class HomeRepositoryFavoritesTests : IDisposable
{
    private const int CollectionId = 900001;
    private static readonly Ulid SpecialId = Ulid.NewUlid();

    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;

    public HomeRepositoryFavoritesTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateSeededFactory();
    }

    private async Task SeedCollectionAndSpecialAsync()
    {
        await using MediaContext context = await _factory.CreateDbContextAsync();
        context.Collections.Add(
            entity: new()
            {
                Id = CollectionId,
                Title = "Test Collection",
                TitleSort = "test collection",
                LibraryId = SeedConstants.MovieLibraryId,
                Parts = 1,
            }
        );
        context.Specials.Add(entity: new() { Id = SpecialId, Title = "Test Special" });
        await context.SaveChangesAsync();

        context.CollectionMovie.Add(entity: new(collectionId: CollectionId, movieId: 129));
        context.SpecialItems.Add(entity: new() { SpecialId = SpecialId, MovieId = 129 });
        await context.SaveChangesAsync();
    }

    private async Task<FavoritesData> GetFavoritesAsync(Guid userId)
    {
        await using MediaContext context = await _factory.CreateDbContextAsync();
        HomeRepository repository = new(context: context, contextFactory: _factory);
        return await repository.GetFavoritesAsync(userId: userId, language: "en", country: "US");
    }

    [Fact]
    public async Task GetFavoritesAsync_ReturnsEmpty_WhenUserHasNoFavorites()
    {
        FavoritesData favorites = await GetFavoritesAsync(userId: SeedConstants.UserId);

        favorites.Movies.Should().BeEmpty();
        favorites.TvShows.Should().BeEmpty();
        favorites.Collections.Should().BeEmpty();
        favorites.Specials.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFavoritesAsync_ReturnsFavoritedMovieTvCollectionAndSpecial()
    {
        await SeedCollectionAndSpecialAsync();

        await using (MediaContext context = await _factory.CreateDbContextAsync())
        {
            context.MovieUser.Add(entity: new(movieId: 129, userId: SeedConstants.UserId));
            context.TvUser.Add(entity: new(tvId: 1399, userId: SeedConstants.UserId));
            context.CollectionUser.Add(entity: new(collectionId: CollectionId, userId: SeedConstants.UserId));
            context.SpecialUser.Add(entity: new(specialId: SpecialId, userId: SeedConstants.UserId));
            await context.SaveChangesAsync();
        }

        FavoritesData favorites = await GetFavoritesAsync(userId: SeedConstants.UserId);

        favorites.Movies.Should().ContainSingle(predicate: m => m.Id == 129);
        favorites.TvShows.Should().ContainSingle(predicate: tv => tv.Id == 1399);
        favorites.Collections.Should().ContainSingle(predicate: c => c.Id == CollectionId);
        favorites.Specials.Should().ContainSingle(predicate: s => s.Id == SpecialId);

        // Unfavorited seed data (movie 680) must not leak into the result.
        favorites.Movies.Should().NotContain(predicate: m => m.Id == 680);
    }

    [Fact]
    public async Task GetFavoritesAsync_ExcludesOtherUsersFavorites()
    {
        await using (MediaContext context = await _factory.CreateDbContextAsync())
        {
            // MovieUser.UserId is a required FK — the other user needs a row
            // of their own before they can favorite anything.
            context.Users.Add(
                entity: new()
                {
                    Id = SeedConstants.OtherUserId,
                    Email = "other@nomercy.tv",
                    Name = "Other User",
                    Allowed = true,
                }
            );
            context.MovieUser.Add(entity: new(movieId: 129, userId: SeedConstants.OtherUserId));
            await context.SaveChangesAsync();
        }

        FavoritesData ownFavorites = await GetFavoritesAsync(userId: SeedConstants.UserId);
        FavoritesData otherUsersFavorites = await GetFavoritesAsync(userId: SeedConstants.OtherUserId);

        ownFavorites.Movies.Should().BeEmpty();
        otherUsersFavorites.Movies.Should().ContainSingle(predicate: m => m.Id == 129);
    }

    [Fact]
    public async Task GetFavoritesAsync_IncludesVideoFiles_ForHaveItemsCount()
    {
        await using (MediaContext context = await _factory.CreateDbContextAsync())
        {
            context.MovieUser.Add(entity: new(movieId: 129, userId: SeedConstants.UserId));
            await context.SaveChangesAsync();
        }

        FavoritesData favorites = await GetFavoritesAsync(userId: SeedConstants.UserId);

        Movie? movie = favorites.Movies.FirstOrDefault(predicate: m => m.Id == 129);
        movie.Should().NotBeNull();
        movie!.VideoFiles.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
