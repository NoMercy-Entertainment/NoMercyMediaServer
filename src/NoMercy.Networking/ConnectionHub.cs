using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using Hub = Microsoft.AspNetCore.SignalR.Hub;

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

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        
        User? user = Context.User.User();
        if (user is null) return;

        StringValues? accessToken = _httpContextAccessor.HttpContext?.Request.Query
            .FirstOrDefault(x => x.Key == "access_token")
            .Value;
        string[] result = accessToken.GetValueOrDefault().ToString().Split("&");

        Client client = new()
        {
            Sub = user.Id,
            Ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            Socket = Clients.Caller,
            Endpoint = Endpoint,
            IsActive = true
        };

        foreach (string item in result)
        {
            string[] keyValue = item.Split("=");

            if (keyValue.Length < 2) continue;

            keyValue[1] = keyValue[1].Replace("+", " ");

            switch (keyValue[0])
            {
                case "access_token":
                    continue;
                case "client_id":
                    client.DeviceId = keyValue[1];
                    break;
                case "client_name":
                    client.Name = keyValue[1];
                    break;
                case "client_type":
                    client.Type = keyValue[1];
                    break;
                case "client_version":
                    client.Version = keyValue[1];
                    break;
                case "client_os":
                    client.Os = keyValue[1];
                    break;
                case "client_browser":
                    client.Browser = keyValue[1];
                    break;
                case "client_device":
                    client.Model = keyValue[1];
                    break;
                case "custom_name":
                    client.CustomName = keyValue[1];
                    break;
                case "client_volume":
                    client.VolumePercent = int.Parse(keyValue[1]);
                    break;
            }
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
                UpdatedAt = di.UpdatedAt,
                VolumePercent = di.VolumePercent,
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
                VolumePercent = c.VolumePercent,
            })
            .ToList();
    }
}