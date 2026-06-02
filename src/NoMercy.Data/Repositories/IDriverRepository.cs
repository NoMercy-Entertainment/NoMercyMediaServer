using NoMercy.Database.Models.Storage;

namespace NoMercy.Data.Repositories;

public interface IDriverRepository
{
    Task<List<Driver>> GetAllDriversAsync();

    Task<Driver?> GetDriverByIdAsync(Ulid id);

    Task<bool> DriverExistsAsync(Ulid id);

    Task<bool> NameExistsAsync(string name, Ulid? excludeId = null);

    Task<int> FolderCountAsync(Ulid driverId);

    Task<Driver> CreateDriverAsync(Driver driver);

    Task<Driver> UpdateDriverAsync(Driver driver);

    Task<int> DeleteDriverAsync(Driver driver);
}
