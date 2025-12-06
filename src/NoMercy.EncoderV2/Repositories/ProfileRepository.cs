namespace NoMercy.EncoderV2.Repositories;

/// <summary>
/// Repository base interface
/// TODO: Implement actual profile repository for database persistence
/// </summary>
public interface IProfileRepository
{
    Task<object?> GetProfileAsync(string profileId);
    Task<List<object?>> GetAllProfilesAsync();
    Task SaveProfileAsync(object? profile);
    Task DeleteProfileAsync(string profileId);
}

/// <summary>
/// In-memory profile repository
/// </summary>
public class ProfileRepository : IProfileRepository
{
    public Task<object?> GetProfileAsync(string profileId)
    {
        return Task.FromResult<object?>(null);
    }

    public Task<List<object?>> GetAllProfilesAsync()
    {
        return Task.FromResult<List<object?>>(new());
    }

    public Task SaveProfileAsync(object? profile)
    {
        return Task.CompletedTask;
    }

    public Task DeleteProfileAsync(string profileId)
    {
        return Task.CompletedTask;
    }
}
