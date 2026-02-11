using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

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