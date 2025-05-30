using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.Socket;

public class DeviceManager
{
    private readonly ConcurrentDictionary<Guid, Device> _currentDevices = new();
    private readonly MediaContext _mediaContext;

    public DeviceManager(MediaContext mediaContext)
    {
        _mediaContext = mediaContext;
    }

    public Device? GetUserDevice(Guid userId) =>
        _currentDevices.TryGetValue(userId, out Device? device) ? device : null;

    public void SetUserDevice(Guid userId, Device device) =>
        _currentDevices.AddOrUpdate(userId, device, (_, _) => device);

    public bool RemoveUserDevice(Guid userId) =>
        _currentDevices.TryRemove(userId, out _);

    public async Task UpdateDeviceVolume(string deviceId, int volume)
    {
        await _mediaContext.Devices
            .Where(d => d.DeviceId == deviceId)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.VolumePercent, volume));
    }
}