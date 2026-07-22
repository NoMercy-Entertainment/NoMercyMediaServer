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
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class UserDataRepositoryTests : IDisposable
{
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;
    private readonly UserDataRepository _repository;

    public UserDataRepositoryTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateSeededFactory();
        _repository = new(contextFactory: _factory);
    }

    [Fact]
    public async Task GetUserDataAsync_ReturnsMovieUserData_ForMovieType()
    {
        List<UserData> result = await _repository.GetUserDataAsync(
            userId: SeedConstants.UserId,
            type: MediaTypes.MovieMediaType,
            intId: 129,
            ulidId: null
        );

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(expected: ud => ud.MovieId.Should().Be(expected: 129));
        result.Should().AllSatisfy(expected: ud => ud.UserId.Should().Be(expected: SeedConstants.UserId));
    }

    [Fact]
    public async Task GetUserDataAsync_ReturnsTvUserData_ForTvType()
    {
        List<UserData> result = await _repository.GetUserDataAsync(
            userId: SeedConstants.UserId,
            type: MediaTypes.TvMediaType,
            intId: 1399,
            ulidId: null
        );

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(expected: ud => ud.TvId.Should().Be(expected: 1399));
    }

    [Fact]
    public async Task GetUserDataAsync_ReturnsEmptyList_WhenUserHasNoData()
    {
        Guid otherUserId = Guid.NewGuid();

        List<UserData> result = await _repository.GetUserDataAsync(
            userId: otherUserId,
            type: MediaTypes.MovieMediaType,
            intId: 129,
            ulidId: null
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserDataAsync_ReturnsEmpty_ForUnknownMediaType()
    {
        List<UserData> result = await _repository.GetUserDataAsync(
            userId: SeedConstants.UserId,
            type: "unknown_type",
            intId: 999,
            ulidId: null
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserDataSingleAsync_ReturnsFirstUserData_ForMovieType()
    {
        UserData? result = await _repository.GetUserDataSingleAsync(
            userId: SeedConstants.UserId,
            type: MediaTypes.MovieMediaType,
            intId: 129,
            ulidId: null
        );

        result.Should().NotBeNull();
        result!.MovieId.Should().Be(expected: 129);
        result.UserId.Should().Be(expected: SeedConstants.UserId);
    }

    [Fact]
    public async Task GetUserDataSingleAsync_ReturnsNull_WhenNoDataExists()
    {
        Guid otherUserId = Guid.NewGuid();

        UserData? result = await _repository.GetUserDataSingleAsync(
            userId: otherUserId,
            type: MediaTypes.MovieMediaType,
            intId: 129,
            ulidId: null
        );

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserDataSingleAsync_ReturnsNull_ForUnknownMediaType()
    {
        UserData? result = await _repository.GetUserDataSingleAsync(
            userId: SeedConstants.UserId,
            type: "invalid_type",
            intId: 129,
            ulidId: null
        );

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserDataSingleAsync_ReturnsTvData()
    {
        UserData? result = await _repository.GetUserDataSingleAsync(
            userId: SeedConstants.UserId,
            type: MediaTypes.TvMediaType,
            intId: 1399,
            ulidId: null
        );

        result.Should().NotBeNull();
        result!.TvId.Should().Be(expected: 1399);
    }

    [Fact]
    public async Task DeleteUserDataAsync_RemovesSpecifiedRows()
    {
        await using MediaContext context = await _factory.CreateDbContextAsync();
        List<UserData> userData = await context.UserData.ToListAsync();
        if (userData.Any())
        {
            await _repository.DeleteUserDataAsync(userData: new() { userData[index: 0] });

            await using MediaContext verify = await _factory.CreateDbContextAsync();
            UserData? deleted = await verify.UserData.FirstOrDefaultAsync(predicate: ud =>
                ud.Id == userData[0].Id
            );

            deleted.Should().BeNull();
        }
    }

    [Fact]
    public async Task HideFromContinueWatchingAsync_SetsFlag_WithoutDeletingRow()
    {
        await using MediaContext context = await _factory.CreateDbContextAsync();
        UserData target = await context.UserData.FirstAsync(predicate: ud => ud.MovieId == 129);

        int affected = await _repository.HideFromContinueWatchingAsync(userData: new[] { target });

        affected.Should().Be(expected: 1);

        await using MediaContext verify = await _factory.CreateDbContextAsync();
        UserData? hidden = await verify.UserData.FirstOrDefaultAsync(predicate: ud => ud.Id == target.Id);

        // The row must survive — recommendations still read Time/Rating from it.
        hidden.Should().NotBeNull();
        hidden!.RemovedFromContinueWatching.Should().BeTrue();
        hidden.Time.Should().Be(expected: target.Time);
        hidden.LastPlayedDate.Should().Be(expected: target.LastPlayedDate);
    }

    [Fact]
    public async Task HideFromContinueWatchingAsync_ReturnsZero_WhenGivenNoRows()
    {
        int affected = await _repository.HideFromContinueWatchingAsync(userData: []);

        affected.Should().Be(expected: 0);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
