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
        _connection = new(connectionString: "Data Source=:memory:");
        _connection.Open();

        using (SqliteCommand fkOff = _connection.CreateCommand())
        {
            fkOff.CommandText = "PRAGMA foreign_keys = OFF;";
            fkOff.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<MediaContext>().UseSqlite(connection: _connection).Options;

        using MediaContext ctx = new(options: _options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MediaContext OpenContext()
    {
        return new(options: _options);
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
        seedCtx.Users.AddRange(entities: [MakeUser(id: withLibrary, email: "haslib@example.com"), MakeUser(id: withoutLibrary, email: "nolib@example.com")]
        );
        seedCtx.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "Movies",
                Type = "movie",
                Order = 1,
            }
        );
        seedCtx.LibraryUser.Add(entity: new(libraryId: libraryId, userId: withLibrary));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(context: queryCtx, contextFactory: null!);

        List<User> result = await repository.GetAllWithLibrariesAsync();

        result.Should().HaveCount(expected: 2);
        result
            .Single(predicate: u => u.Id == withLibrary)
            .LibraryUser.Should()
            .ContainSingle(predicate: lu => lu.LibraryId == libraryId);
        result.Single(predicate: u => u.Id == withoutLibrary).LibraryUser.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        await using MediaContext ctx = OpenContext();
        UserRepository repository = new(context: ctx, contextFactory: null!);

        User? result = await repository.GetByIdAsync(userId: Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_KnownId_ReturnsThatUser()
    {
        Guid userId = Guid.NewGuid();
        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.Add(entity: MakeUser(id: userId, email: "known@example.com"));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(context: queryCtx, contextFactory: null!);

        User? result = await repository.GetByIdAsync(userId: userId);

        result.Should().NotBeNull();
        result!.Email.Should().Be(expected: "known@example.com");
    }

    [Fact]
    public async Task GetByIdWithLibrariesAsync_UnknownId_ReturnsNull()
    {
        await using MediaContext ctx = OpenContext();
        UserRepository repository = new(context: ctx, contextFactory: null!);

        User? result = await repository.GetByIdWithLibrariesAsync(userId: Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithLibrariesAsync_KnownId_IncludesLibraryAndItsLibraryEntity()
    {
        Guid userId = Guid.NewGuid();
        Ulid libraryId = Ulid.NewUlid();

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.Add(entity: MakeUser(id: userId, email: "libs@example.com"));
        seedCtx.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "TV Shows",
                Type = "tv",
                Order = 1,
            }
        );
        seedCtx.LibraryUser.Add(entity: new(libraryId: libraryId, userId: userId));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(context: queryCtx, contextFactory: null!);

        User? result = await repository.GetByIdWithLibrariesAsync(userId: userId);

        result.Should().NotBeNull();
        result!.LibraryUser.Should().ContainSingle();
        result.LibraryUser.Single().Library.Title.Should().Be(expected: "TV Shows");
    }

    [Fact]
    public async Task GetByIdWithNotificationsAsync_IncludesOnlyThatUsersNotifications()
    {
        Guid userId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        Ulid notificationId = Ulid.NewUlid();

        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.AddRange(entities: [MakeUser(id: userId, email: "notif@example.com"), MakeUser(id: otherUserId, email: "other@example.com")]
        );
        seedCtx.Notifications.Add(entity: new() { Id = notificationId });
        seedCtx.NotificationUser.Add(entity: new(notificationId: notificationId, userId: userId));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(context: queryCtx, contextFactory: null!);

        User? result = await repository.GetByIdWithNotificationsAsync(userId: userId);
        User? otherResult = await repository.GetByIdWithNotificationsAsync(userId: otherUserId);

        result.Should().NotBeNull();
        result!.NotificationUser.Should().ContainSingle(predicate: nu => nu.NotificationId == notificationId);
        result.NotificationUser.Single().Notification.Id.Should().Be(expected: notificationId);

        otherResult.Should().NotBeNull();
        otherResult!.NotificationUser.Should().BeEmpty();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueOnlyForASeededUser()
    {
        Guid userId = Guid.NewGuid();
        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.Add(entity: MakeUser(id: userId, email: "exists@example.com"));
        await seedCtx.SaveChangesAsync();

        await using MediaContext queryCtx = OpenContext();
        UserRepository repository = new(context: queryCtx, contextFactory: null!);

        (await repository.ExistsAsync(userId: userId)).Should().BeTrue();
        (await repository.ExistsAsync(userId: Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdWithLibrariesAfterAddAsync_ReturnsTheJustAddedUser()
    {
        Guid userId = Guid.NewGuid();
        await using MediaContext ctx = OpenContext();
        UserRepository repository = new(context: ctx, contextFactory: null!);

        await repository.AddAsync(user: MakeUser(id: userId, email: "created@example.com"));

        User? result = await repository.GetByIdWithLibrariesAfterAddAsync(userId: userId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(expected: userId);
        result.Email.Should().Be(expected: "created@example.com");
        result.LibraryUser.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_DoesNotThrowAndLeavesOtherUsersIntact()
    {
        Guid keep = Guid.NewGuid();
        await using MediaContext seedCtx = OpenContext();
        seedCtx.Users.Add(entity: MakeUser(id: keep, email: "keep@example.com"));
        await seedCtx.SaveChangesAsync();

        // DeleteAsync opens its own context via the injected IDbContextFactory rather
        // than the constructor-injected one, so it needs a real factory bound to the
        // same in-memory connection/schema as the rest of the suite.
        TestDbContextFactory factory = new(options: _options);
        UserRepository repository = new(context: factory.CreateDbContext(), contextFactory: factory);

        Func<Task> act = () => repository.DeleteAsync(userId: Guid.NewGuid());
        await act.Should().NotThrowAsync();

        await using MediaContext verifyCtx = OpenContext();
        (await verifyCtx.Users.AnyAsync(predicate: u => u.Id == keep)).Should().BeTrue();
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
        seedCtx.Users.Add(entity: MakeUser(id: userId, email: "todelete@example.com"));
        seedCtx.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "Movies",
                Type = "movie",
                Order = 1,
            }
        );
        seedCtx.Movies.Add(
            entity: new()
            {
                Id = 900,
                Title = "Owned Movie",
                TitleSort = "owned movie",
                LibraryId = libraryId,
            }
        );
        seedCtx.LibraryUser.Add(entity: new(libraryId: libraryId, userId: userId));
        seedCtx.MovieUser.Add(entity: new(movieId: 900, userId: userId));
        seedCtx.Playlists.Add(
            entity: new()
            {
                Id = playlistId,
                Name = "Owned Playlist",
                UserId = userId,
            }
        );
        seedCtx.PlaylistTrack.Add(entity: new(playlistId: playlistId, trackId: Guid.NewGuid()));
        seedCtx.UserPlaylists.Add(entity: new() { Name = "Owned UserPlaylist", UserId = userId });
        seedCtx.MusicPlays.Add(entity: new(userId: userId, trackId: Guid.NewGuid()));
        seedCtx.UserData.Add(
            entity: new()
            {
                Type = "movie",
                UserId = userId,
                VideoFileId = Ulid.NewUlid(),
            }
        );
        seedCtx.ActivityLogs.Add(
            entity: new()
            {
                Category = ActivityCategory.Connection,
                Time = DateTime.UtcNow,
                DeviceId = Ulid.NewUlid(),
                UserId = userId,
            }
        );
        seedCtx.DeviceDropNotices.Add(
            entity: new()
            {
                UserId = userId,
                DeviceName = "Old TV",
                Reason = "manual",
            }
        );
        await seedCtx.SaveChangesAsync();

        TestDbContextFactory factory = new(options: _options);
        UserRepository repository = new(context: factory.CreateDbContext(), contextFactory: factory);

        await repository.DeleteAsync(userId: userId);

        await using MediaContext verifyCtx = OpenContext();
        (await verifyCtx.Users.AnyAsync(predicate: u => u.Id == userId)).Should().BeFalse();
        (await verifyCtx.LibraryUser.AnyAsync(predicate: lu => lu.UserId == userId)).Should().BeFalse();
        (await verifyCtx.MovieUser.AnyAsync(predicate: mu => mu.UserId == userId)).Should().BeFalse();
        (await verifyCtx.Playlists.AnyAsync(predicate: p => p.UserId == userId)).Should().BeFalse();
        (await verifyCtx.PlaylistTrack.AnyAsync(predicate: pt => pt.PlaylistId == playlistId))
            .Should()
            .BeFalse();
        (await verifyCtx.UserPlaylists.AnyAsync(predicate: up => up.UserId == userId)).Should().BeFalse();
        (await verifyCtx.MusicPlays.AnyAsync(predicate: mp => mp.UserId == userId)).Should().BeFalse();
        (await verifyCtx.UserData.AnyAsync(predicate: ud => ud.UserId == userId)).Should().BeFalse();
        (await verifyCtx.ActivityLogs.AnyAsync(predicate: al => al.UserId == userId)).Should().BeFalse();
        (await verifyCtx.DeviceDropNotices.AnyAsync(predicate: dn => dn.UserId == userId)).Should().BeFalse();
        // The movie itself, owned by nobody in particular, must survive the user delete.
        (await verifyCtx.Movies.AnyAsync(predicate: m => m.Id == 900))
            .Should()
            .BeTrue();
    }
}
