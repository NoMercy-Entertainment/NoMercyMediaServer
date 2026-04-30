using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.Socket;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.Networking.Devices;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.Hubs;

[Authorize]
public sealed class DeviceHub : ConnectionHub
{
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly DeviceBusRegistry _busRegistry;

    public DeviceHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        DeviceBusRegistry busRegistry
    )
        : base(httpContextAccessor, contextFactory, connectedClients)
    {
        _contextFactory = contextFactory;
        _busRegistry = busRegistry;
    }

    public async Task<List<DeviceListItem>> GetDevices()
    {
        User? user = Context.User.User();
        if (user is null) return [];

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        List<Device> rows = await ctx.Devices
            .Where(d => d.OwnerUserId == user.Id && d.Fingerprint != null)
            .ToListAsync();

        return rows
            .Select(d => new DeviceListItem
            {
                DeviceId = d.Id,
                Fingerprint = d.Fingerprint!,
                Name = d.CustomName ?? d.Name,
                Type = d.Type,
                Online = _busRegistry.IsOnline(d.Id),
                LanIp = d.LanIp,
                LastSeenAt = d.WsConnectedAt > d.MdnsSeenAt ? d.WsConnectedAt : d.MdnsSeenAt,
            })
            .ToList();
    }

    public async Task<WakeResult> WakeForMusic(string deviceId)
    {
        User? user = Context.User.User();
        if (user is null) return new WakeResult("not_owned");

        if (!Ulid.TryParse(deviceId, out Ulid id)) return new WakeResult("not_owned");

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FindAsync(id);
        if (device is null || device.OwnerUserId != user.Id)
            return new WakeResult("not_owned");

        if (_busRegistry.IsOnline(device.Id))
        {
            bool sent = await _busRegistry.SendAsync(
                device.Id,
                new { type = "wake_for_music", session_id = Guid.NewGuid().ToString() }
            );
            return new WakeResult(sent ? "wake_sent" : "no_route");
        }

        return new WakeResult("cast_fallback");
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        List<DeviceListItem> list = await GetDevices();
        await Clients.Caller.SendAsync("DeviceListChanged", list);
    }
}

public sealed record WakeResult([property: JsonProperty("status")] string Status);
