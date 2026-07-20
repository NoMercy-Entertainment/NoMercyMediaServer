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
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Playlists;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

// Covers the UserRepository members QueryOutputTests/UserRepositoryTests do not touch:
// plain reads (GetAllWithLibrariesAsync/GetByIdAsync/ExistsAsync), the Notification
// eager-load path, and the Add -> immediate tracked re-read -> Delete lifecycle.
public class UserRepositoryUncoveredTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public UserRepositoryUncoveredTests()
    {
        _connection = new("Data Source=:memory:");
        _connection.Open();

        using (SqliteCommand fkOff = _connection.CreateCommand())
        {
            fkOff.CommandText = "PRAGMA foreign_keys = OFF;";
            fkOff.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(_connection).Options;

        using MediaContext ctx = new(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MediaContext OpenContext()
    {
        return new(_options);
    }

    private static User MakeUser(Guid id, string email)
    {
        return new()
        {
            Id = id,
            Email = email,
            Name = email,
        };
    }

    [Fact]
    public async Task GetAllWithLibrariesAsync_ReturnsEveryUser_WithTheirOwnLibrariesOnly()
    {
        Guid withLibrary = Guid.NewGuid();
        Guid withoutLibrary = Guid.NewGuid();
        Ulid libraryId = Ulid.NewUlid();

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.AddRange(
            MakeUser(withLibrary, "haslib@example.com"),
            MakeUser(withoutLibrary, "nolib@example.com")
        );
        seedCtx.Libraries.Add(
            new()
            {
                Id = libraryId,
                Title = "Movies",
                Type = "movie",
                Order = 1,
            }
        );
        seedCtx.LibraryUser.Add(new(libraryId, withLibrary));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(queryCtx, null!);

        List<User> result = await repository.GetAllWithLibrariesAsync();

        result.Should().HaveCount(2);
        result
            .Single(u => u.Id == withLibrary)
            .LibraryUser.Should()
            .ContainSingle(lu => lu.LibraryId == libraryId);
        result.Single(u => u.Id == withoutLibrary).LibraryUser.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        await using MediaContext ctx = OpenContext();
        UserRepository repository = new(ctx, null!);

        User? result = await repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_KnownId_ReturnsThatUser()
    {
        Guid userId = Guid.NewGuid();
        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.Add(MakeUser(userId, "known@example.com"));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(queryCtx, null!);

        User? result = await repository.GetByIdAsync(userId);

        result.Should().NotBeNull();
        result!.Email.Should().Be("known@example.com");
    }

    [Fact]
    public async Task GetByIdWithLibrariesAsync_UnknownId_ReturnsNull()
    {
        await using MediaContext ctx = OpenContext();
        UserRepository repository = new(ctx, null!);

        User? result = await repository.GetByIdWithLibrariesAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithLibrariesAsync_KnownId_IncludesLibraryAndItsLibraryEntity()
    {
        Guid userId = Guid.NewGuid();
        Ulid libraryId = Ulid.NewUlid();

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.Add(MakeUser(userId, "libs@example.com"));
        seedCtx.Libraries.Add(
            new()
            {
                Id = libraryId,
                Title = "TV Shows",
                Type = "tv",
                Order = 1,
            }
        );
        seedCtx.LibraryUser.Add(new(libraryId, userId));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(queryCtx, null!);

        User? result = await repository.GetByIdWithLibrariesAsync(userId);

        result.Should().NotBeNull();
        result!.LibraryUser.Should().ContainSingle();
        result.LibraryUser.Single().Library.Title.Should().Be("TV Shows");
    }

    [Fact]
    public async Task GetByIdWithNotificationsAsync_IncludesOnlyThatUsersNotifications()
    {
        Guid userId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        Ulid notificationId = Ulid.NewUlid();

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.AddRange(
            MakeUser(userId, "notif@example.com"),
            MakeUser(otherUserId, "other@example.com")
        );
        seedCtx.Notifications.Add(new() { Id = notificationId });
        seedCtx.NotificationUser.Add(new(notificationId, userId));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(queryCtx, null!);

        User? result = await repository.GetByIdWithNotificationsAsync(userId);
        User? otherResult = await repository.GetByIdWithNotificationsAsync(otherUserId);

        result.Should().NotBeNull();
        result!.NotificationUser.Should().ContainSingle(nu => nu.NotificationId == notificationId);
        result.NotificationUser.Single().Notification.Id.Should().Be(notificationId);

        otherResult.Should().NotBeNull();
        otherResult!.NotificationUser.Should().BeEmpty();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueOnlyForASeededUser()
    {
        Guid userId = Guid.NewGuid();
        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.Add(MakeUser(userId, "exists@example.com"));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(queryCtx, null!);

        (await repository.ExistsAsync(userId)).Should().BeTrue();
        (await repository.ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdWithLibrariesAfterAddAsync_ReturnsTheJustAddedUser()
    {
        Guid userId = Guid.NewGuid();
        await using MediaContext ctx = OpenContext();
        UserRepository repository = new(ctx, null!);

        await repository.AddAsync(MakeUser(userId, "created@example.com"));

        User? result = await repository.GetByIdWithLibrariesAfterAddAsync(userId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Email.Should().Be("created@example.com");
        result.LibraryUser.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_DoesNotThrowAndLeavesOtherUsersIntact()
    {
        Guid keep = Guid.NewGuid();
        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.Add(MakeUser(keep, "keep@example.com"));
        await seedCtx.SaveChangesAsync();

        // DeleteAsync opens its own context via the injected IDbContextFactory rather
        // than the constructor-injected one, so it needs a real factory bound to the
        // same in-memory connection/schema as the rest of the suite.
        TestDbContextFactory factory = new(_options);
        UserRepository repository = new(factory.CreateDbContext(), factory);

        Func<Task> act = () => repository.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();

        await using MediaContext verifyCtx = OpenContext();
        (await verifyCtx.Users.AnyAsync(u => u.Id == keep)).Should().BeTrue();
    }

    // Reproduces a real production 500: LibraryUser.UserId (and every sibling table that
    // references User — access-grant/preference join tables, plus the user's playback and
    // activity history) is a required FK whose delete behavior defaults to Restrict, so
    // removing the User row while any of those still reference it throws instead of
    // deleting. Deleting a self-hosted family member who has ever been granted library
    // access, saved a playlist, or played a track is an everyday admin action, not an edge
    // case — so this seeds a row in every User-owned table (including the transitive
    // PlaylistTrack -> Playlist edge, itself Restrict) and demands they are all gone.
    [Fact]
    public async Task DeleteAsync_KnownId_RemovesTheUserAndEveryOwnedRow()
    {
        Guid userId = Guid.NewGuid();
        Ulid libraryId = Ulid.NewUlid();
        Guid playlistId = Guid.NewGuid();

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.Add(MakeUser(userId, "todelete@example.com"));
        seedCtx.Libraries.Add(
            new()
            {
                Id = libraryId,
                Title = "Movies",
                Type = "movie",
                Order = 1,
            }
        );
        seedCtx.Movies.Add(
            new()
            {
                Id = 900,
                Title = "Owned Movie",
                TitleSort = "owned movie",
                LibraryId = libraryId,
            }
        );
        seedCtx.LibraryUser.Add(new(libraryId, userId));
        seedCtx.MovieUser.Add(new(900, userId));
        seedCtx.Playlists.Add(
            new()
            {
                Id = playlistId,
                Name = "Owned Playlist",
                UserId = userId,
            }
        );
        seedCtx.PlaylistTrack.Add(new(playlistId, Guid.NewGuid()));
        seedCtx.UserPlaylists.Add(new() { Name = "Owned UserPlaylist", UserId = userId });
        seedCtx.MusicPlays.Add(new(userId, Guid.NewGuid()));
        seedCtx.UserData.Add(
            new()
            {
                Type = "movie",
                UserId = userId,
                VideoFileId = Ulid.NewUlid(),
            }
        );
        seedCtx.ActivityLogs.Add(
            new()
            {
                Category = ActivityCategory.Connection,
                Time = DateTime.UtcNow,
                DeviceId = Ulid.NewUlid(),
                UserId = userId,
            }
        );
        seedCtx.DeviceDropNotices.Add(
            new()
            {
                UserId = userId,
                DeviceName = "Old TV",
                Reason = "manual",
            }
        );
        await seedCtx.SaveChangesAsync();

        TestDbContextFactory factory = new(_options);
        UserRepository repository = new(factory.CreateDbContext(), factory);

        await repository.DeleteAsync(userId);

        await using MediaContext verifyCtx = OpenContext();
        (await verifyCtx.Users.AnyAsync(u => u.Id == userId)).Should().BeFalse();
        (await verifyCtx.LibraryUser.AnyAsync(lu => lu.UserId == userId)).Should().BeFalse();
        (await verifyCtx.MovieUser.AnyAsync(mu => mu.UserId == userId)).Should().BeFalse();
        (await verifyCtx.Playlists.AnyAsync(p => p.UserId == userId)).Should().BeFalse();
        (await verifyCtx.PlaylistTrack.AnyAsync(pt => pt.PlaylistId == playlistId))
            .Should()
            .BeFalse();
        (await verifyCtx.UserPlaylists.AnyAsync(up => up.UserId == userId)).Should().BeFalse();
        (await verifyCtx.MusicPlays.AnyAsync(mp => mp.UserId == userId)).Should().BeFalse();
        (await verifyCtx.UserData.AnyAsync(ud => ud.UserId == userId)).Should().BeFalse();
        (await verifyCtx.ActivityLogs.AnyAsync(al => al.UserId == userId)).Should().BeFalse();
        (await verifyCtx.DeviceDropNotices.AnyAsync(dn => dn.UserId == userId)).Should().BeFalse();
        // The movie itself, owned by nobody in particular, must survive the user delete.
        (await verifyCtx.Movies.AnyAsync(m => m.Id == 900))
            .Should()
            .BeTrue();
    }
}
