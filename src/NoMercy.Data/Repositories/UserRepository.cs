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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public class UserRepository(MediaContext context, IDbContextFactory<MediaContext> contextFactory)
    : IUserRepository
{
    public Task<List<User>> GetAllWithLibrariesAsync()
    {
        return context
            .Users.AsNoTracking()
            .Include(navigationPropertyPath: user => user.LibraryUser)
                .ThenInclude(navigationPropertyPath: libraryUser => libraryUser.Library)
            .ToListAsync();
    }

    public Task<User?> GetByIdAsync(Guid userId)
    {
        return context.Users.AsNoTracking().FirstOrDefaultAsync(predicate: user => user.Id == userId);
    }

    public Task<User?> GetByIdWithLibrariesAsync(Guid userId)
    {
        return context
            .Users.AsNoTracking()
            .Include(navigationPropertyPath: user => user.LibraryUser)
                .ThenInclude(navigationPropertyPath: libraryUser => libraryUser.Library)
            .FirstOrDefaultAsync(predicate: user => user.Id == userId);
    }

    public Task<User?> GetByIdWithNotificationsAsync(Guid userId)
    {
        return context
            .Users.AsNoTracking()
            .Where(predicate: user => user.Id == userId)
            .Include(navigationPropertyPath: user => user.LibraryUser)
            .Include(navigationPropertyPath: user => user.NotificationUser)
                .ThenInclude(navigationPropertyPath: notificationUser => notificationUser.Notification)
            .FirstOrDefaultAsync(predicate: user => user.Id == userId);
    }

    public Task<bool> ExistsAsync(Guid userId)
    {
        return context.Users.AnyAsync(predicate: user => user.Id == userId);
    }

    public async Task AddAsync(User user)
    {
        context.Users.Add(entity: user);
        await context.SaveChangesAsync();
    }

    public Task<User?> GetByIdWithLibrariesAfterAddAsync(Guid userId)
    {
        return context
            .Users.Include(navigationPropertyPath: user => user.LibraryUser)
            .FirstOrDefaultAsync(predicate: user => user.Id == userId);
    }

    public async Task DeleteAsync(Guid userId)
    {
        await using MediaContext deleteContext = await contextFactory.CreateDbContextAsync();

        bool exists = await deleteContext.Users.AnyAsync(predicate: user => user.Id == userId);
        if (!exists)
            return;

        // Every table that references User defaults to DeleteBehavior.Restrict
        // (MediaContext's schema-wide default), so deleting the User row while any of them
        // still reference it throws instead of deleting. Clear each Restrict dependent
        // first, deepest first, then delete the User; the database itself then resolves the
        // remaining Cascade/SetNull foreign keys (Device un-owns itself, PlaylistItem
        // cascades from its UserPlaylist). Dropping the account legitimately drops that
        // user's own grants, preferences and history and touches no other user's data.
        // The whole thing runs in one transaction so a mid-sweep failure never leaves a
        // half-deleted account behind.
        await using IDbContextTransaction transaction =
            await deleteContext.Database.BeginTransactionAsync();

        // PlaylistTrack -> Playlist is itself Restrict, so it has to go before the user's
        // Playlist rows can be removed.
        await deleteContext
            .PlaylistTrack.Where(predicate: pt =>
                deleteContext.Playlists.Any(p => p.Id == pt.PlaylistId && p.UserId == userId)
            )
            .ExecuteDeleteAsync();

        await deleteContext.LibraryUser.Where(predicate: lu => lu.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.MovieUser.Where(predicate: mu => mu.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.TvUser.Where(predicate: tu => tu.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.CollectionUser.Where(predicate: cu => cu.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.SpecialUser.Where(predicate: su => su.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.NotificationUser.Where(predicate: nu => nu.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.AlbumUser.Where(predicate: au => au.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.ArtistUser.Where(predicate: au => au.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.TrackUser.Where(predicate: tu => tu.UserId == userId).ExecuteDeleteAsync();
        await deleteContext
            .PlaybackPreferences.Where(predicate: pp => pp.UserId == userId)
            .ExecuteDeleteAsync();
        await deleteContext.MusicPlays.Where(predicate: mp => mp.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.UserData.Where(predicate: ud => ud.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.ActivityLogs.Where(predicate: al => al.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.DeviceDropNotices.Where(predicate: dn => dn.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.UserPlaylists.Where(predicate: up => up.UserId == userId).ExecuteDeleteAsync();
        await deleteContext.Playlists.Where(predicate: p => p.UserId == userId).ExecuteDeleteAsync();

        await deleteContext.Users.Where(predicate: user => user.Id == userId).ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }

    public async Task UpdatePermissionsAsync(
        Guid targetUserId,
        Guid actingUserId,
        bool allowed,
        bool audioTranscoding,
        bool videoTranscoding,
        bool noTranscoding,
        bool? manage,
        IEnumerable<Ulid> libraryIds
    )
    {
        await using MediaContext permContext = await contextFactory.CreateDbContextAsync();

        User? user = await permContext
            .Users.Include(navigationPropertyPath: user => user.LibraryUser)
            .FirstOrDefaultAsync(predicate: user => user.Id == targetUserId);

        if (user is null)
            return;

        if (manage.HasValue)
            user.Manage = manage.Value;

        user.Allowed = allowed;
        user.AudioTranscoding = audioTranscoding;
        user.VideoTranscoding = videoTranscoding;
        user.NoTranscoding = noTranscoding;

        user.LibraryUser.Clear();

        foreach (Ulid libraryId in libraryIds)
            user.LibraryUser.Add(item: new() { LibraryId = libraryId, UserId = targetUserId });

        await permContext.SaveChangesAsync();
    }
}
