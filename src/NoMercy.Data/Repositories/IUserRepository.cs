/*
 * Copyright (c) NoMercy Entertainment. All Rights Reserved.
 */

using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public interface IUserRepository
{
    Task<List<User>> GetAllWithLibrariesAsync();

    Task<User?> GetByIdAsync(Guid userId);

    Task<User?> GetByIdWithLibrariesAsync(Guid userId);

    Task<User?> GetByIdWithNotificationsAsync(Guid userId);

    Task<bool> ExistsAsync(Guid userId);

    Task AddAsync(User user);

    Task<User?> GetByIdWithLibrariesAfterAddAsync(Guid userId);

    Task DeleteAsync(Guid userId);

    Task UpdatePermissionsAsync(
        Guid targetUserId,
        Guid actingUserId,
        bool allowed,
        bool audioTranscoding,
        bool videoTranscoding,
        bool noTranscoding,
        bool? manage,
        IEnumerable<Ulid> libraryIds
    );
}
