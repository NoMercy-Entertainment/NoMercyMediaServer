using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class DeviceRepository(MediaContext context)
{
    public IIncludableQueryable<Device, ICollection<ActivityLog>> GetDevices()
    {
        return context.Devices
            .Include(device => device.ActivityLogs);
    }

    public async Task AddDeviceAsync(Device device)
    {
        await context.Devices.AddAsync(device);
        await context.SaveChangesAsync();
    }

    public Task DeleteDeviceAsync(Device device)
    {
        context.Devices.Remove(device);
        return context.SaveChangesAsync();
    }
}