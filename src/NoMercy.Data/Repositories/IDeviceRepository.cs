using Microsoft.EntityFrameworkCore.Query;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public interface IDeviceRepository
{
    IIncludableQueryable<Device, ICollection<ActivityLog>> GetDevices();

    Task AddDeviceAsync(Device device);

    Task DeleteDeviceAsync(Device device);

    Task<Device?> GetOwnerDeviceAsync(Ulid deviceId, Guid ownerUserId);

    Task<List<Device>> GetOwnerDevicesAsync(Guid ownerUserId);

    Task DeleteDeviceWithLogsAsync(Ulid deviceId);

    Task DeleteActivityLogsByOwnerAsync(Guid ownerUserId);

    Task<Device?> GetByIdAsync(Ulid deviceId);

    Task<List<Device>> GetAllAsync();

    Task DeleteAllActivityLogsAsync();
}
