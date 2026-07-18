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
[Trait("Category", "Characterization")]
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
            new UserData
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

        _repository = new(_factory);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<List<UserData>> RemainingAsync()
    {
        await using MediaContext ctx = _factory.CreateDbContext();
        return await ctx
            .UserData.AsNoTracking()
            .Where(u => u.UserId == SeedConstants.UserId)
            .ToListAsync();
    }

    [Fact]
    public async Task RemoveForItem_Movie_RemovesOnlyThatMovie_AndLeavesOtherMovieAndTv()
    {
        int deleted = await _repository.RemoveForItemAsync(
            SeedConstants.UserId,
            MediaTypes.MovieMediaType,
            129,
            null
        );

        Assert.Equal(2, deleted); // movie 129 had two rows

        List<UserData> remaining = await RemainingAsync();
        Assert.DoesNotContain(remaining, u => u.MovieId == 129);
        Assert.Contains(remaining, u => u.MovieId == 680); // other movie untouched
        Assert.Contains(remaining, u => u.TvId == 1399); // tv untouched
    }

    [Fact]
    public async Task RemoveForItem_Tv_RemovesOnlyTv_AndLeavesMovies()
    {
        int deleted = await _repository.RemoveForItemAsync(
            SeedConstants.UserId,
            MediaTypes.TvMediaType,
            1399,
            null
        );

        Assert.Equal(1, deleted);

        List<UserData> remaining = await RemainingAsync();
        Assert.DoesNotContain(remaining, u => u.TvId == 1399);
        Assert.Equal(3, remaining.Count); // movie 129 (2) + movie 680 (1)
    }

    [Fact]
    public async Task RemoveForItem_NullId_DeletesNothing()
    {
        List<UserData> before = await RemainingAsync();

        int deleted = await _repository.RemoveForItemAsync(
            SeedConstants.UserId,
            MediaTypes.MovieMediaType,
            null,
            null
        );

        Assert.Equal(0, deleted);
        List<UserData> after = await RemainingAsync();
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task RemoveForItem_UnknownType_DeletesNothing()
    {
        int deleted = await _repository.RemoveForItemAsync(
            SeedConstants.UserId,
            "not-a-type",
            129,
            null
        );

        Assert.Equal(0, deleted);
    }
}
