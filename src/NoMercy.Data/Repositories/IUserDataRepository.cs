using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public interface IUserDataRepository
{
    Task<List<UserData>> GetUserDataAsync(
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId,
        CancellationToken ct = default
    );

    Task<UserData?> GetUserDataSingleAsync(
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId,
        CancellationToken ct = default
    );

    Task<int> DeleteUserDataAsync(List<UserData> userData, CancellationToken ct = default);
}
