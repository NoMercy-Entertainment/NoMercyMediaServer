using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.NmSystem;

namespace NoMercy.Networking;

[Authorize]
public class ConnectionHub : Hub
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string Endpoint { get; set; }

    protected ConnectionHub(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        Endpoint = _httpContextAccessor.HttpContext?.Request.Path.Value ?? "Unknown";
        // Logger.Socket($"Connected to {Endpoint}");
    }

    public string GetCountryFromContext()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["country"].FirstOrDefault() ?? "US";
    }
    
    public string GetLanguageFromContext()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage
                   .FirstOrDefault()?.Split("_")
                   .FirstOrDefault() ??
               LocalizationHelper.GlobalLocalizer.TargetLanguage;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        User? user = Context.User.User();
        if (user is null) return;

        Client client = new()
        {
            Sub = user.Id,
            Ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            Socket = Clients.Caller,
            Endpoint = Endpoint,
            IsActive = true
        };

        IQueryCollection? query = _httpContextAccessor.HttpContext?.Request.Query;
        if (query is not null && query.Count > 1)
        {
            if (query.TryGetValue("client_id", out StringValues value))
                client.DeviceId = value;
            
            if (query.TryGetValue("custom_name", out StringValues customName))
                client.CustomName = customName;
            
            if (query.TryGetValue("client_volume", out StringValues volumePercent))  
                client.VolumePercent = int.Parse(volumePercent);
    
            if (query.TryGetValue("client_name", out StringValues name))
                client.Name = name;
            
            if (query.TryGetValue("client_type", out StringValues type))
                client.Type = type;

            if (query.TryGetValue("client_version", out StringValues version))
                client.Version = version;
            
            if (query.TryGetValue("client_os", out StringValues os))
                client.Os = os;
            
            if (query.TryGetValue("client_browser", out StringValues browser))
                client.Browser = browser;
            
            if (query.TryGetValue("client_device", out StringValues model))
                client.Model = model;
            
        }

        await using MediaContext mediaContext = new();
        await mediaContext.Devices.Upsert(client)
            .On(x => x.DeviceId)
            .WhenMatched((ds, di) => new()
            {
                Browser = di.Browser,
                // CustomName = di.CustomName,
                DeviceId = di.DeviceId,
                Ip = di.Ip,
                Model = di.Model,
                Name = di.Name,
                Os = di.Os,
                Type = di.Type,
                Version = di.Version,
                VolumePercent = di.VolumePercent
            })
            .RunAsync();

        Device? device = mediaContext.Devices.FirstOrDefault(x => x.DeviceId == client.DeviceId);

        client.CustomName = device?.CustomName;
        client.VolumePercent = device?.VolumePercent ?? 0;
        client.IsActive = true;

        if (device is not null)
        {
            await mediaContext.Devices
                .Where(x => x.DeviceId == device.DeviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(d => d.IsActive, true));
            await mediaContext.SaveChangesAsync();

            await SaveActivityLog(mediaContext, new()
            {
                DeviceId = device.Id,
                Time = DateTime.Now,
                Type = "Connected to server",
                UserId = user.Id
            });
        }

        Networking.SocketClients.TryAdd(Context.ConnectionId, client);

        await Clients.All.SendAsync("ConnectedDevicesState", Devices());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);

        if (Networking.SocketClients.TryGetValue(Context.ConnectionId, out Client? client))
        {
            await using MediaContext mediaContext = new();
            Device? device = mediaContext.Devices.FirstOrDefault(x => x.DeviceId == client.DeviceId);
            if (device is not null)
            {
                await mediaContext.Devices
                    .Where(x => x.DeviceId == device.DeviceId)
                    .ExecuteUpdateAsync(x => x.SetProperty(d => d.IsActive, false));
                await mediaContext.SaveChangesAsync();

                await SaveActivityLog(mediaContext, new()
                {
                    DeviceId = device.Id,
                    Time = DateTime.Now,
                    Type = "Disconnected from server",
                    UserId = client.Sub
                });
            }

            Networking.SocketClients.Remove(Context.ConnectionId, out _);

            await Clients.All.SendAsync("ConnectedDevicesState", Devices());
        }
    }

    private static async Task SaveActivityLog(MediaContext mediaContext, ActivityLog log, int count = 0)
    {
        try
        {
            await mediaContext.ActivityLogs.AddAsync(log);
            await mediaContext.SaveChangesAsync();
        }
        catch (Exception)
        {
            if (count > 2) return; // 3 times

            count += 1;
            await Task.Delay(1000);
            await SaveActivityLog(mediaContext, log, count);
        }
    }

    public List<Device> Devices()
    {
        User? user = Context.User.User();
        if (user is null) return [];

        return Networking.SocketClients.Values
            .Where(x => x.Sub.Equals(user.Id))
            .Where(x => x.Endpoint == Endpoint)
            .Select(c => new Device
            {
                Name = c.Name,
                Ip = c.Ip,
                DeviceId = c.DeviceId,
                Browser = c.Browser,
                Os = c.Os,
                Model = c.Model,
                Type = c.Type,
                Version = c.Version,
                Id = c.Id,
                CustomName = c.CustomName,
                VolumePercent = c.VolumePercent
            })
            .ToList();
    }
}