using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Api.WebSockets;
using NoMercy.Database;
using NoMercy.Database.Activity;
using NoMercy.Database.Models.Users;
using NoMercy.Encoder.Devices;
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
    private readonly IDeviceCapabilityRegistry _capabilityRegistry;
    private readonly ILogger<DeviceHub> _logger;

    public DeviceHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        DeviceBusRegistry busRegistry,
        IActivityLogger activityLogger,
        IDeviceCapabilityRegistry capabilityRegistry,
        ILogger<DeviceHub> logger
    )
        : base(httpContextAccessor, contextFactory, connectedClients, activityLogger)
    {
        _contextFactory = contextFactory;
        _busRegistry = busRegistry;
        _capabilityRegistry = capabilityRegistry;
        _logger = logger;
    }

    private string? ResolveDeviceIdFromContext()
    {
        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client))
            return null;
        return string.IsNullOrEmpty(client.DeviceId) ? null : client.DeviceId;
    }

    public async Task DeclareCapabilities(DeviceCapabilities payload)
    {
        string? deviceId = ResolveDeviceIdFromContext();
        if (deviceId is null)
            return; // unauthenticated or unknown — silently drop, never throw

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (device is null)
            return;

        device.CapabilitiesJson = JsonConvert.SerializeObject(payload);
        await ctx.SaveChangesAsync();

        _capabilityRegistry.Set(deviceId, payload);

        _logger.LogInformation(
            "Device {DeviceId} declared capabilities: channels={Channels} codecs=[{Codecs}] ramTier={Tier}",
            deviceId,
            payload.MaxAudioChannels,
            string.Join(",", payload.AudioCodecs),
            payload.RamTier
        );
    }

    public async Task<List<DeviceListItem>> GetDevices()
    {
        User? user = Context.User.User();
        if (user is null)
            return [];

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        List<Device> rows = await ctx
            .Devices.Where(d => d.OwnerUserId == user.Id && d.Fingerprint != null)
            .ToListAsync();

        return rows.Select(d =>
            {
                (bool Foreground, bool ScreenOn) s = _busRegistry.GetStatus(d.Id);
                return new DeviceListItem
                {
                    DeviceId = d.Id,
                    Fingerprint = d.Fingerprint!,
                    Name = d.CustomName ?? d.Name,
                    Type = d.Type,
                    Online = _busRegistry.IsOnline(d.Id),
                    LanIp = d.LanIp,
                    LastSeenAt = d.WsConnectedAt > d.MdnsSeenAt ? d.WsConnectedAt : d.MdnsSeenAt,
                    Foreground = s.Foreground,
                    ScreenOn = s.ScreenOn,
                };
            })
            .ToList();
    }

    public async Task<WakeResult> WakeForMusic(string deviceId)
    {
        User? user = Context.User.User();
        if (user is null)
            return new WakeResult("not_owned");

        if (!Ulid.TryParse(deviceId, out Ulid id))
            return new WakeResult("not_owned");

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

    public async Task<List<DeviceDropNoticeDto>> PendingNotices()
    {
        User? user = Context.User.User();
        if (user is null)
            return [];

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        List<DeviceDropNotice> notices = await ctx
            .DeviceDropNotices.Where(n => n.UserId == user.Id && !n.Acknowledged)
            .ToListAsync();

        foreach (DeviceDropNotice n in notices)
            n.Acknowledged = true;
        await ctx.SaveChangesAsync();

        return notices.Select(n => new DeviceDropNoticeDto(n.DeviceName, n.Reason)).ToList();
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        List<DeviceListItem> list = await GetDevices();
        await Clients.Caller.SendAsync("DeviceListChanged", list);
    }
}

public sealed record WakeResult([property: JsonProperty("status")] string Status);

public sealed record DeviceDropNoticeDto(
    [property: JsonProperty("device_name")] string DeviceName,
    [property: JsonProperty("reason")] string Reason
);
