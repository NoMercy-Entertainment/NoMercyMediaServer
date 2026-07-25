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
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Characterization")]
public class UserDataRepositoryTests : IDisposable
{
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;
    private readonly UserDataRepository _repository;

    public UserDataRepositoryTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateSeededFactory();
        _repository = new(_factory);
    }

    [Fact]
    public async Task GetUserDataAsync_ReturnsMovieUserData_ForMovieType()
    {
        List<UserData> result = await _repository.GetUserDataAsync(
            SeedConstants.UserId,
            MediaTypes.MovieMediaType,
            129,
            null
        );

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(ud => ud.MovieId.Should().Be(129));
        result.Should().AllSatisfy(ud => ud.UserId.Should().Be(SeedConstants.UserId));
    }

    [Fact]
    public async Task GetUserDataAsync_ReturnsTvUserData_ForTvType()
    {
        List<UserData> result = await _repository.GetUserDataAsync(
            SeedConstants.UserId,
            MediaTypes.TvMediaType,
            1399,
            null
        );

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(ud => ud.TvId.Should().Be(1399));
    }

    [Fact]
    public async Task GetUserDataAsync_ReturnsEmptyList_WhenUserHasNoData()
    {
        Guid otherUserId = Guid.NewGuid();

        List<UserData> result = await _repository.GetUserDataAsync(
            otherUserId,
            MediaTypes.MovieMediaType,
            129,
            null
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserDataAsync_ReturnsEmpty_ForUnknownMediaType()
    {
        List<UserData> result = await _repository.GetUserDataAsync(
            SeedConstants.UserId,
            "unknown_type",
            999,
            null
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserDataSingleAsync_ReturnsFirstUserData_ForMovieType()
    {
        UserData? result = await _repository.GetUserDataSingleAsync(
            SeedConstants.UserId,
            MediaTypes.MovieMediaType,
            129,
            null
        );

        result.Should().NotBeNull();
        result!.MovieId.Should().Be(129);
        result.UserId.Should().Be(SeedConstants.UserId);
    }

    [Fact]
    public async Task GetUserDataSingleAsync_ReturnsNull_WhenNoDataExists()
    {
        Guid otherUserId = Guid.NewGuid();

        UserData? result = await _repository.GetUserDataSingleAsync(
            otherUserId,
            MediaTypes.MovieMediaType,
            129,
            null
        );

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserDataSingleAsync_ReturnsNull_ForUnknownMediaType()
    {
        UserData? result = await _repository.GetUserDataSingleAsync(
            SeedConstants.UserId,
            "invalid_type",
            129,
            null
        );

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserDataSingleAsync_ReturnsTvData()
    {
        UserData? result = await _repository.GetUserDataSingleAsync(
            SeedConstants.UserId,
            MediaTypes.TvMediaType,
            1399,
            null
        );

        result.Should().NotBeNull();
        result!.TvId.Should().Be(1399);
    }

    [Fact]
    public async Task DeleteUserDataAsync_RemovesSpecifiedRows()
    {
        await using MediaContext context = await _factory.CreateDbContextAsync();
        List<UserData> userData = await context.UserData.ToListAsync();
        if (userData.Any())
        {
            await _repository.DeleteUserDataAsync(new() { userData[0] });

            await using MediaContext verify = await _factory.CreateDbContextAsync();
            UserData? deleted = await verify.UserData.FirstOrDefaultAsync(ud =>
                ud.Id == userData[0].Id
            );

            deleted.Should().BeNull();
        }
    }

    [Fact]
    public async Task HideFromContinueWatchingAsync_SetsFlag_WithoutDeletingRow()
    {
        await using MediaContext context = await _factory.CreateDbContextAsync();
        UserData target = await context.UserData.FirstAsync(ud => ud.MovieId == 129);

        int affected = await _repository.HideFromContinueWatchingAsync(new[] { target });

        affected.Should().Be(1);

        await using MediaContext verify = await _factory.CreateDbContextAsync();
        UserData? hidden = await verify.UserData.FirstOrDefaultAsync(ud => ud.Id == target.Id);

        // The row must survive — recommendations still read Time/Rating from it.
        hidden.Should().NotBeNull();
        hidden!.RemovedFromContinueWatching.Should().BeTrue();
        hidden.Time.Should().Be(target.Time);
        hidden.LastPlayedDate.Should().Be(target.LastPlayedDate);
    }

    [Fact]
    public async Task HideFromContinueWatchingAsync_ReturnsZero_WhenGivenNoRows()
    {
        int affected = await _repository.HideFromContinueWatchingAsync([]);

        affected.Should().Be(0);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
