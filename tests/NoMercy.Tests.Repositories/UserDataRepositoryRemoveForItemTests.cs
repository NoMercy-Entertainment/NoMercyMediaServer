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
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;
using NoMercy.Tests.Repositories.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// VideoHub.RemoveWatched used to OR MovieId/TvId/SpecialId/CollectionId == the
/// request ids; because a movie row has null Tv/Special/Collection ids, a null
/// request id matched EVERY row of that type, so finishing one item wiped the
/// user's whole continue-watching list. RemoveForItemAsync must delete ONLY the
/// requested item and must never mass-delete on a null id.
/// </summary>
[Trait(name: "Category", value: "Characterization")]
public class UserDataRepositoryRemoveForItemTests : IDisposable
{
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;
    private readonly UserDataRepository _repository;

    public UserDataRepositoryRemoveForItemTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateSeededFactory();

        // Seed has movie 129 (2 rows) + tv 1399 (1 row) in continue-watching.
        // Add a second distinct movie so "remove one movie leaves the other" is testable.
        using MediaContext ctx = _factory.CreateDbContext();
        ctx.UserData.Add(
            entity: new UserData
            {
                Id = Ulid.NewUlid(),
                UserId = SeedConstants.UserId,
                MovieId = 680,
                VideoFileId = SeedConstants.MovieVideoFile2Id,
                Type = MediaTypes.MovieMediaType,
                Time = 500,
                LastPlayedDate = "2026-03-01T00:00:00Z",
            }
        );
        ctx.SaveChanges();

        _repository = new(contextFactory: _factory);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(obj: this);
    }

    private async Task<List<UserData>> RemainingAsync()
    {
        await using MediaContext ctx = _factory.CreateDbContext();
        return await ctx
            .UserData.AsNoTracking()
            .Where(predicate: u => u.UserId == SeedConstants.UserId)
            .ToListAsync();
    }

    [Fact]
    public async Task RemoveForItem_Movie_RemovesOnlyThatMovie_AndLeavesOtherMovieAndTv()
    {
        int deleted = await _repository.RemoveForItemAsync(
            userId: SeedConstants.UserId,
            type: MediaTypes.MovieMediaType,
            intId: 129,
            ulidId: null
        );

        Assert.Equal(expected: 2, actual: deleted); // movie 129 had two rows

        List<UserData> remaining = await RemainingAsync();
        Assert.DoesNotContain(collection: remaining, filter: u => u.MovieId == 129);
        Assert.Contains(collection: remaining, filter: u => u.MovieId == 680); // other movie untouched
        Assert.Contains(collection: remaining, filter: u => u.TvId == 1399); // tv untouched
    }

    [Fact]
    public async Task RemoveForItem_Tv_RemovesOnlyTv_AndLeavesMovies()
    {
        int deleted = await _repository.RemoveForItemAsync(
            userId: SeedConstants.UserId,
            type: MediaTypes.TvMediaType,
            intId: 1399,
            ulidId: null
        );

        Assert.Equal(expected: 1, actual: deleted);

        List<UserData> remaining = await RemainingAsync();
        Assert.DoesNotContain(collection: remaining, filter: u => u.TvId == 1399);
        Assert.Equal(expected: 3, actual: remaining.Count); // movie 129 (2) + movie 680 (1)
    }

    [Fact]
    public async Task RemoveForItem_NullId_DeletesNothing()
    {
        List<UserData> before = await RemainingAsync();

        int deleted = await _repository.RemoveForItemAsync(
            userId: SeedConstants.UserId,
            type: MediaTypes.MovieMediaType,
            intId: null,
            ulidId: null
        );

        Assert.Equal(expected: 0, actual: deleted);
        List<UserData> after = await RemainingAsync();
        Assert.Equal(expected: before.Count, actual: after.Count);
    }

    [Fact]
    public async Task RemoveForItem_UnknownType_DeletesNothing()
    {
        int deleted = await _repository.RemoveForItemAsync(
            userId: SeedConstants.UserId,
            type: "not-a-type",
            intId: 129,
            ulidId: null
        );

        Assert.Equal(expected: 0, actual: deleted);
    }
}
