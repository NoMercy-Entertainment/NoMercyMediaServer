using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

public interface IEncoderRepository
{
    Task<List<EncoderProfile>> GetEncoderProfilesAsync();

    Task<EncoderProfile?> GetEncoderProfileByIdAsync(Ulid id);

    Task AddEncoderProfileAsync(EncoderProfile profile);

    Task DeleteEncoderProfileAsync(EncoderProfile profile);

    Task<int> GetEncoderProfileCountAsync();
}
