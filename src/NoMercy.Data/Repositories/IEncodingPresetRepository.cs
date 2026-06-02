using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

public interface IEncodingPresetRepository
{
    Task<List<EncodingPreset>> ListAsync(
        int pageSize = 100,
        int pageIndex = 0,
        string? tagFilter = null
    );

    Task<IReadOnlyList<string>> GetAllTagsAsync();

    Task<EncodingPreset?> GetByIdAsync(Ulid id);

    Task<EncodingPreset?> GetByNameAsync(string name);

    Task<EncodingPreset> CreateAsync(EncodingPreset preset);

    Task<EncodingPreset?> UpdateAsync(Ulid id, Action<EncodingPreset> apply);

    Task<bool> DeleteAsync(Ulid id);

    Task<int> GetTotalCountAsync();
}
