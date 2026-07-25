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
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Setup.Auth;

namespace NoMercy.Tests.Setup.Auth;

[Trait("Category", "Data")]
public class UserProvisioningServiceTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];
    private readonly UserProvisioningService _userProvisioningService;
    private readonly IDbContextFactory<MediaContext> _mediaContextFactory;

    public UserProvisioningServiceTests()
    {
        _mediaContextFactory = CreateFactory();
        _userProvisioningService = new(_mediaContextFactory);
        UserCache.Current.Reset();
    }

    private IDbContextFactory<MediaContext> CreateFactory()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        _connections.Add(connection);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection)
            .Options;

        using (MediaContext init = new(options))
            init.Database.EnsureCreated();

        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new(options));

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

        await _userProvisioningService.ProvisionOwner(newUser);

        await using MediaContext verify = await _mediaContextFactory.CreateDbContextAsync();
        User? persisted = await verify
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        Assert.NotNull(persisted);
        Assert.Equal("Test Owner", persisted.Name);
        Assert.Equal("owner@test.local", persisted.Email);
        Assert.True(persisted.Owner);
        Assert.True(persisted.Allowed);
        Assert.True(persisted.Manage);
        Assert.True(persisted.AudioTranscoding);
        Assert.True(persisted.VideoTranscoding);
        Assert.True(persisted.NoTranscoding);
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
            seed.Users.Add(initialUser);
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

        await _userProvisioningService.ProvisionOwner(updatedUser);

        await using MediaContext verify = await _mediaContextFactory.CreateDbContextAsync();
        User? persisted = await verify
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        Assert.NotNull(persisted);
        Assert.Equal("Updated Name", persisted.Name);
        Assert.Equal("updated@test.local", persisted.Email);
        Assert.True(persisted.Owner);
        Assert.True(persisted.Allowed);
        Assert.True(persisted.Manage);
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
            seed.Users.Add(otherUser);
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

        await _userProvisioningService.ProvisionOwner(ownerUser);

        await using MediaContext verify = await _mediaContextFactory.CreateDbContextAsync();
        User? owner = await verify
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ownerUserId);
        User? other = await verify
            .Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == otherUserId);

        Assert.NotNull(owner);
        Assert.True(owner.Owner);
        Assert.Equal("owner@test.local", owner.Email);

        Assert.NotNull(other);
        Assert.False(other.Owner);
        Assert.Equal("other@test.local", other.Email);
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

        await _userProvisioningService.ProvisionOwner(newUser);

        User? cached = UserCache.Current.GetUser(userId);
        Assert.NotNull(cached);
        Assert.Equal("Cache Test", cached.Name);
        Assert.Equal("cache@test.local", cached.Email);
    }
}
