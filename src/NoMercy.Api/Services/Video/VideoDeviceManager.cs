using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
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

namespace NoMercy.Api.Services.Video;

public class VideoDeviceManager
{
    private readonly ConcurrentDictionary<Guid, Device> _currentDevices = new();
    private readonly MediaContext _mediaContext;

    public VideoDeviceManager(MediaContext mediaContext)
    {
        _mediaContext = mediaContext;
    }

    public Device? GetUserDevice(Guid userId)
    {
        return _currentDevices.TryGetValue(userId, out Device? device) ? device : null;
    }

    public void SetUserDevice(Guid userId, Device device)
    {
        _currentDevices.AddOrUpdate(userId, device, (_, _) => device);
    }

    public bool RemoveUserDevice(Guid userId)
    {
        return _currentDevices.TryRemove(userId, out _);
    }

    public async Task UpdateDeviceVolume(string deviceId, int volume)
    {
        await _mediaContext.Devices
            .Where(d => d.DeviceId == deviceId)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.VolumePercent, volume));
    }
}