/*
 * Copyright (c) NoMercy Entertainment. All Rights Reserved.
 */

using Microsoft.EntityFrameworkCore;
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
            .Include(user => user.LibraryUser)
                .ThenInclude(libraryUser => libraryUser.Library)
            .ToListAsync();
    }

    public Task<User?> GetByIdAsync(Guid userId)
    {
        return context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId);
    }

    public Task<User?> GetByIdWithLibrariesAsync(Guid userId)
    {
        return context
            .Users.AsNoTracking()
            .Include(user => user.LibraryUser)
                .ThenInclude(libraryUser => libraryUser.Library)
            .FirstOrDefaultAsync(user => user.Id == userId);
    }

    public Task<User?> GetByIdWithNotificationsAsync(Guid userId)
    {
        return context
            .Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Include(user => user.LibraryUser)
            .Include(user => user.NotificationUser)
                .ThenInclude(notificationUser => notificationUser.Notification)
            .FirstOrDefaultAsync(user => user.Id == userId);
    }

    public Task<bool> ExistsAsync(Guid userId)
    {
        return context.Users.AnyAsync(user => user.Id == userId);
    }

    public async Task AddAsync(User user)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }

    public Task<User?> GetByIdWithLibrariesAfterAddAsync(Guid userId)
    {
        return context
            .Users.Include(user => user.LibraryUser)
            .FirstOrDefaultAsync(user => user.Id == userId);
    }

    public async Task DeleteAsync(Guid userId)
    {
        await using MediaContext deleteContext = await contextFactory.CreateDbContextAsync();

        User? user = await deleteContext
            .Users.Include(user => user.LibraryUser)
            .FirstOrDefaultAsync(user => user.Id == userId);

        if (user is null)
            return;

        deleteContext.Users.Remove(user);
        await deleteContext.SaveChangesAsync();
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
            .Users.Include(user => user.LibraryUser)
            .FirstOrDefaultAsync(user => user.Id == targetUserId);

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
            user.LibraryUser.Add(new LibraryUser { LibraryId = libraryId, UserId = actingUserId });

        await permContext.SaveChangesAsync();
    }
}
