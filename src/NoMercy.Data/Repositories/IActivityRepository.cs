using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public interface IActivityRepository
{
    Task<List<ActivityLog>> GetPagedAsync(
        ActivityCategory? category,
        Guid? userId,
        Ulid? deviceId,
        Ulid? mediaId,
        DateTime? from,
        DateTime? to,
        bool? success,
        int skip,
        int take,
        CancellationToken ct = default
    );

    Task<int> DeleteAsync(
        ActivityCategory? category,
        DateTime? before,
        CancellationToken ct = default
    );
}
