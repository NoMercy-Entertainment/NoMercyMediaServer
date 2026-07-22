// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Api.WebSockets;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Activity;
using NoMercy.Database.Models.Users;
using NoMercy.Encoder.Devices;
using NoMercy.Networking;
using NoMercy.Networking.Devices;
using NoMercy.Networking.Http;
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
        : base(httpContextAccessor: httpContextAccessor, contextFactory: contextFactory, connectedClients: connectedClients, activityLogger: activityLogger)
    {
        _contextFactory = contextFactory;
        _busRegistry = busRegistry;
        _capabilityRegistry = capabilityRegistry;
        _logger = logger;
    }

    private string? ResolveDeviceIdFromContext()
    {
        if (!ConnectedClients.Clients.TryGetValue(key: Context.ConnectionId, value: out Client? client))
            return null;
        return string.IsNullOrEmpty(value: client.DeviceId) ? null : client.DeviceId;
    }

    public async Task DeclareCapabilities(DeviceCapabilities payload)
    {
        string? deviceId = ResolveDeviceIdFromContext();
        if (deviceId is null)
            return; // unauthenticated or unknown — silently drop, never throw

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FirstOrDefaultAsync(predicate: d => d.DeviceId == deviceId);
        if (device is null)
            return;

        device.CapabilitiesJson = JsonConvert.SerializeObject(value: payload);
        await ctx.SaveChangesAsync();

        _capabilityRegistry.Set(deviceId: deviceId, capabilities: payload);

        _logger.LogInformation(
            message: "Device {DeviceId} declared capabilities: channels={Channels} codecs=[{Codecs}] ramTier={Tier}", args: [deviceId, payload.MaxAudioChannels, string.Join(separator: ",", value: payload.AudioCodecs), payload.RamTier]
        );
    }

    public async Task<List<DeviceListItem>> GetDevices()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return [];

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        List<Device> rows = await ctx
            .Devices.Where(predicate: d => d.OwnerUserId == user.Id && d.Fingerprint != null)
            .ToListAsync();

        return rows.Select(selector: d =>
            {
                (bool Foreground, bool ScreenOn) s = _busRegistry.GetStatus(deviceId: d.Id);
                return new DeviceListItem
                {
                    DeviceId = d.Id,
                    Fingerprint = d.Fingerprint!,
                    Name = d.CustomName ?? d.Name,
                    Type = d.Type,
                    Online = _busRegistry.IsOnline(deviceId: d.Id),
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
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return new(Status: "not_owned");

        if (!Ulid.TryParse(base32: deviceId, ulid: out Ulid id))
            return new(Status: "not_owned");

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FindAsync(keyValues: id);
        if (device is null || device.OwnerUserId != user.Id)
            return new(Status: "not_owned");

        if (_busRegistry.IsOnline(deviceId: device.Id))
        {
            bool sent = await _busRegistry.SendAsync(
                deviceId: device.Id,
                payload: new { type = "wake_for_music", session_id = Guid.NewGuid().ToString() }
            );
            return new(Status: sent ? "wake_sent" : "no_route");
        }

        return new(Status: "cast_fallback");
    }

    public async Task<WakeResult> WakeForVideo(string deviceId)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return new(Status: "not_owned");

        if (!Ulid.TryParse(base32: deviceId, ulid: out Ulid id))
            return new(Status: "not_owned");

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FindAsync(keyValues: id);
        if (device is null || device.OwnerUserId != user.Id)
            return new(Status: "not_owned");

        if (_busRegistry.IsOnline(deviceId: device.Id))
        {
            bool sent = await _busRegistry.SendAsync(
                deviceId: device.Id,
                payload: new { type = "wake_for_video", session_id = Guid.NewGuid().ToString() }
            );
            return new(Status: sent ? "wake_sent" : "no_route");
        }

        return new(Status: "cast_fallback");
    }

    public async Task<List<DeviceDropNoticeDto>> PendingNotices()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return [];

        await using MediaContext ctx = await _contextFactory.CreateDbContextAsync();
        List<DeviceDropNotice> notices = await ctx
            .DeviceDropNotices.Where(predicate: n => n.UserId == user.Id && !n.Acknowledged)
            .ToListAsync();

        foreach (DeviceDropNotice n in notices)
            n.Acknowledged = true;
        await ctx.SaveChangesAsync();

        return notices.Select(selector: n => new DeviceDropNoticeDto(DeviceName: n.DeviceName, Reason: n.Reason)).ToList();
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;

        // Wait briefly so the client's 'DeviceListChanged' handler is registered
        // before the broadcast lands. Mirrors the same fix on MusicHub — without
        // this delay the SignalR Java client drops the initial push because the
        // handler registration happens after the connection's Started callback.
        try
        {
            await Task.Delay(millisecondsDelay: 500, cancellationToken: Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        List<DeviceListItem> list = await GetDevices();

        // Broadcast to the WHOLE user group, not just the connecting client.
        // Clients.Caller only refreshed the newly-connected device's own picker;
        // every other already-connected device (e.g. a second TV, a phone) never
        // learned about the new device until it happened to reconnect itself.
        await Clients.User(userId: user.Id.ToString()).SendAsync(method: "DeviceListChanged", arg1: list);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());

        await base.OnDisconnectedAsync(exception: exception);

        if (user is null)
            return;

        List<DeviceListItem> list = await GetDevices();
        await Clients.User(userId: user.Id.ToString()).SendAsync(method: "DeviceListChanged", arg1: list);
    }
}

public sealed record WakeResult([property: JsonProperty(propertyName: "status")] string Status);

public sealed record DeviceDropNoticeDto(
    [property: JsonProperty(propertyName: "device_name")] string DeviceName,
    [property: JsonProperty(propertyName: "reason")] string Reason
);
