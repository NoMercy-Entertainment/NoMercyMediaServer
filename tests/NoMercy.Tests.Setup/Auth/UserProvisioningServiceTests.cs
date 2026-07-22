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
using Moq;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Setup.Auth;

namespace NoMercy.Tests.Setup.Auth;

[Trait(name: "Category", value: "Data")]
public class UserProvisioningServiceTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];
    private readonly UserProvisioningService _userProvisioningService;
    private readonly IDbContextFactory<MediaContext> _mediaContextFactory;

    public UserProvisioningServiceTests()
    {
        _mediaContextFactory = CreateFactory();
        _userProvisioningService = new(mediaContextFactory: _mediaContextFactory);
        UserCache.Current.Reset();
    }

    private IDbContextFactory<MediaContext> CreateFactory()
    {
        SqliteConnection connection = new(connectionString: "Data Source=:memory:");
        connection.Open();
        _connections.Add(item: connection);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection: connection)
            .Options;

        using (MediaContext init = new(options: options))
            init.Database.EnsureCreated();

        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(expression: x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: options));

        return mock.Object;
    }

    public void Dispose()
    {
        foreach (SqliteConnection connection in _connections)
        {
            connection?.Close();
            connection?.Dispose();
        }
        UserCache.Current.Reset();
    }

    [Fact]
    public async Task ProvisionOwner_inserts_new_user_with_all_permissions()
    {
        Guid userId = Guid.NewGuid();
        User newUser = new()
        {
            Id = userId,
            Name = "Test Owner",
            Email = "owner@test.local",
            Owner = true,
            Allowed = true,
            Manage = true,
            AudioTranscoding = true,
            VideoTranscoding = true,
            NoTranscoding = true,
        };

        await _userProvisioningService.ProvisionOwner(user: newUser);

        await using MediaContext verify = await _mediaContextFactory.CreateDbContextAsync();
        User? persisted = await verify
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(predicate: u => u.Id == userId);

        Assert.NotNull(@object: persisted);
        Assert.Equal(expected: "Test Owner", actual: persisted.Name);
        Assert.Equal(expected: "owner@test.local", actual: persisted.Email);
        Assert.True(condition: persisted.Owner);
        Assert.True(condition: persisted.Allowed);
        Assert.True(condition: persisted.Manage);
        Assert.True(condition: persisted.AudioTranscoding);
        Assert.True(condition: persisted.VideoTranscoding);
        Assert.True(condition: persisted.NoTranscoding);
    }

    [Fact]
    public async Task ProvisionOwner_updates_existing_user_permissions()
    {
        Guid userId = Guid.NewGuid();
        User initialUser = new()
        {
            Id = userId,
            Name = "Initial Name",
            Email = "initial@test.local",
            Owner = false,
            Allowed = false,
            Manage = false,
            AudioTranscoding = false,
            VideoTranscoding = false,
            NoTranscoding = false,
        };

        await using (MediaContext seed = await _mediaContextFactory.CreateDbContextAsync())
        {
            seed.Users.Add(entity: initialUser);
            await seed.SaveChangesAsync();
        }

        User updatedUser = new()
        {
            Id = userId,
            Name = "Updated Name",
            Email = "updated@test.local",
            Owner = true,
            Allowed = true,
            Manage = true,
            AudioTranscoding = true,
            VideoTranscoding = true,
            NoTranscoding = true,
        };

        await _userProvisioningService.ProvisionOwner(user: updatedUser);

        await using MediaContext verify = await _mediaContextFactory.CreateDbContextAsync();
        User? persisted = await verify
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(predicate: u => u.Id == userId);

        Assert.NotNull(@object: persisted);
        Assert.Equal(expected: "Updated Name", actual: persisted.Name);
        Assert.Equal(expected: "updated@test.local", actual: persisted.Email);
        Assert.True(condition: persisted.Owner);
        Assert.True(condition: persisted.Allowed);
        Assert.True(condition: persisted.Manage);
    }

    [Fact]
    public async Task ProvisionOwner_upsert_excludes_other_users()
    {
        Guid ownerUserId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        User otherUser = new()
        {
            Id = otherUserId,
            Name = "Other User",
            Email = "other@test.local",
            Owner = false,
            Allowed = true,
            Manage = false,
            AudioTranscoding = true,
            VideoTranscoding = true,
            NoTranscoding = false,
        };

        await using (MediaContext seed = await _mediaContextFactory.CreateDbContextAsync())
        {
            seed.Users.Add(entity: otherUser);
            await seed.SaveChangesAsync();
        }

        User ownerUser = new()
        {
            Id = ownerUserId,
            Name = "Owner",
            Email = "owner@test.local",
            Owner = true,
            Allowed = true,
            Manage = true,
            AudioTranscoding = true,
            VideoTranscoding = true,
            NoTranscoding = true,
        };

        await _userProvisioningService.ProvisionOwner(user: ownerUser);

        await using MediaContext verify = await _mediaContextFactory.CreateDbContextAsync();
        User? owner = await verify
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(predicate: u => u.Id == ownerUserId);
        User? other = await verify
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(predicate: u => u.Id == otherUserId);

        Assert.NotNull(@object: owner);
        Assert.True(condition: owner.Owner);
        Assert.Equal(expected: "owner@test.local", actual: owner.Email);

        Assert.NotNull(@object: other);
        Assert.False(condition: other.Owner);
        Assert.Equal(expected: "other@test.local", actual: other.Email);
    }

    [Fact]
    public async Task ProvisionOwner_adds_user_to_cache()
    {
        UserCache.Current.Reset();

        Guid userId = Guid.NewGuid();
        User newUser = new()
        {
            Id = userId,
            Name = "Cache Test",
            Email = "cache@test.local",
            Owner = true,
            Allowed = true,
            Manage = true,
            AudioTranscoding = true,
            VideoTranscoding = true,
            NoTranscoding = true,
        };

        await _userProvisioningService.ProvisionOwner(user: newUser);

        User? cached = UserCache.Current.GetUser(userId: userId);
        Assert.NotNull(@object: cached);
        Assert.Equal(expected: "Cache Test", actual: cached.Name);
        Assert.Equal(expected: "cache@test.local", actual: cached.Email);
    }
}
