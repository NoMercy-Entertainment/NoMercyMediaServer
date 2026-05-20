using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Hubs;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Devices;

namespace NoMercy.Api.Controllers.Socket;

public sealed class DeviceBusRegistry(
    IDbContextFactory<MediaContext> contextFactory,
    IHubContext<DeviceHub> hubContext
) : IDeviceListChangeNotifier
{
    private readonly ConcurrentDictionary<Ulid, WebSocket> _live = new();
    private readonly ConcurrentDictionary<Ulid, DeviceStatus> _status = new();

    public async Task Register(Ulid deviceId, WebSocket ws)
    {
        _live[deviceId] = ws;

        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FindAsync(deviceId);
        if (device?.OwnerUserId is not null)
            await BroadcastChange(device.OwnerUserId.Value);
    }

    public async Task Unregister(Ulid deviceId)
    {
        _live.TryRemove(deviceId, out _);
        _status.TryRemove(deviceId, out _);

        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FindAsync(deviceId);
        if (device is null)
            return;
        device.WsConnectedAt = null;
        await ctx.SaveChangesAsync();

        if (device.OwnerUserId is not null)
            await BroadcastChange(device.OwnerUserId.Value);
    }

    public bool IsOnline(Ulid deviceId) => _live.ContainsKey(deviceId);

    public DeviceStatus GetStatus(Ulid deviceId) =>
        _status.TryGetValue(deviceId, out DeviceStatus? status) ? status : DeviceStatus.Unknown;

    public async Task UpdateStatus(Ulid deviceId, bool foreground, bool screenOn)
    {
        // Phone-side picker reads foreground via DeviceListChanged to skip the
        // Cast SDK CEC wake when the TV's NoMercy app is already on screen.
        // Without this propagation, every TV row falls into the Standby path
        // and tap-to-cast fires the Web Receiver on a foregrounded app.
        DeviceStatus next = new(foreground, screenOn);
        DeviceStatus prev = _status.GetOrAdd(deviceId, DeviceStatus.Unknown);
        _status[deviceId] = next;

        if (prev.Foreground == next.Foreground && prev.ScreenOn == next.ScreenOn)
            return;

        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FindAsync(deviceId);
        if (device?.OwnerUserId is not null)
            await BroadcastChange(device.OwnerUserId.Value);
    }

    public async Task<bool> SendAsync(Ulid deviceId, object payload, CancellationToken ct = default)
    {
        if (!_live.TryGetValue(deviceId, out WebSocket? ws) || ws.State != WebSocketState.Open)
            return false;

        string json = JsonSerializer.Serialize(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        return true;
    }

    public void Touch(Ulid deviceId)
    {
        // pong received — presence confirmed by socket remaining in _live
    }

    public async Task BroadcastChange(Guid ownerUserId)
    {
        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        List<Device> rows = await ctx
            .Devices.Where(d => d.OwnerUserId == ownerUserId && d.Fingerprint != null)
            .ToListAsync();

        List<DeviceListItem> items = rows.Select(d =>
            {
                DeviceStatus status = GetStatus(d.Id);
                return new DeviceListItem
                {
                    DeviceId = d.Id,
                    Fingerprint = d.Fingerprint!,
                    Name = d.CustomName ?? d.Name,
                    Type = d.Type,
                    Online = IsOnline(d.Id),
                    Foreground = status.Foreground,
                    ScreenOn = status.ScreenOn,
                    LanIp = d.LanIp,
                    LastSeenAt = d.WsConnectedAt > d.MdnsSeenAt ? d.WsConnectedAt : d.MdnsSeenAt,
                };
            })
            .ToList();

        await hubContext.Clients.User(ownerUserId.ToString()).SendAsync("DeviceListChanged", items);
    }
}

public sealed record DeviceStatus(bool Foreground, bool ScreenOn)
{
    public static readonly DeviceStatus Unknown = new(false, false);
}
