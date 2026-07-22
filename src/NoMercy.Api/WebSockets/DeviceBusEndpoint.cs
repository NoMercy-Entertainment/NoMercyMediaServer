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

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Api.Services.Music;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.WebSockets;

[ApiController]
[Authorize]
[Route(template: "ws/device-bus")]
public sealed class DeviceBusEndpoint(
    IDbContextFactory<MediaContext> contextFactory,
    DeviceBusRegistry registry,
    MusicPlayerStateManager musicPlayerStateManager,
    MusicPlaybackService musicPlaybackService,
    ConnectedClients connectedClients,
    ILogger<DeviceBusEndpoint> logger
) : ControllerBase
{
    [HttpGet]
    public async Task Connect()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        User? user = HttpContext
            .RequestServices.GetRequiredService<IUserCache>()
            .GetUser(userId: HttpContext.User.UserId());
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using WebSocket ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await Pump(ws: ws, user: user, ct: HttpContext.RequestAborted);
    }

    private async Task Pump(WebSocket ws, User user, CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        Device? device = null;

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result = await ws.ReceiveAsync(buffer: buffer, cancellationToken: ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                string json = Encoding.UTF8.GetString(bytes: buffer, index: 0, count: result.Count);

                // Malformed payloads (split frames during stream churn,
                // unicode boundary issues, or buggy clients) used to throw
                // JsonException out of the loop and tear the whole device-
                // bus connection down. Skip the bad frame and wait for the
                // next clean message instead.
                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(json: json);
                }
                catch (JsonException)
                {
                    logger.LogDebug(
                        message: "device-bus skipping malformed frame ({Bytes} bytes)",
                        args: result.Count
                    );
                    continue;
                }

                using JsonDocument _ = doc;
                if (
                    !doc.RootElement.TryGetProperty(propertyName: "type", value: out JsonElement typeElement)
                    || typeElement.ValueKind != JsonValueKind.String
                )
                    continue;

                string? type = typeElement.GetString();

                if (type == "hello")
                {
                    device = await HandleHello(root: doc.RootElement, user: user, ws: ws);
                    if (device is null)
                        break;
                }
                else if (type == "pong" && device is not null)
                {
                    registry.Touch(deviceId: device.Id);
                }
                else if (type == "status" && device is not null)
                {
                    bool foreground =
                        doc.RootElement.TryGetProperty(propertyName: "foreground", value: out JsonElement fe)
                        && fe.ValueKind == JsonValueKind.True;
                    bool screenOn =
                        doc.RootElement.TryGetProperty(propertyName: "screen_on", value: out JsonElement se)
                        && se.ValueKind == JsonValueKind.True;

                    registry.UpdateStatus(deviceId: device.Id, foreground: foreground, screenOn: screenOn);
                    if (device.OwnerUserId is not null)
                        await registry.BroadcastChange(ownerUserId: device.OwnerUserId.Value);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(exception: ex, message: "device-bus closed");
        }
        finally
        {
            if (device is not null)
            {
                if (
                    device.OwnerUserId is not null
                    && musicPlayerStateManager.TryGetValue(
                        userId: device.OwnerUserId.Value,
                        state: out MusicPlayerState? playerState
                    )
                    && string.Equals(
                        a: playerState.DeviceId,
                        b: device.DeviceId,
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    )
                    && !IsStillOnMusicHub(connectedClients: connectedClients, deviceId: device.DeviceId)
                )
                {
                    // device-bus is a secondary wake/status channel (30s ping cadence,
                    // TV-only, independent OkHttp socket) with its own reconnect churn —
                    // it going down is NOT proof the device stopped playing. MusicHub's own
                    // OnDisconnectedAsync already owns "is the active device really gone"
                    // for playback purposes, complete with the KMP double-connect survivor
                    // guard (see its otherConnectionForDeviceSurvives check). Clearing the
                    // active claim here too, unconditionally, meant a bare device-bus blip
                    // (a transient LAN hiccup, an OS-throttled background socket, mDNS churn
                    // from another device on the network coming online) paused a device that
                    // was still fully connected — and still playing — on MusicHub the whole
                    // time. Only fall through when MusicHub agrees the device is gone too.
                    logger.LogInformation(
                        message: "Active music device {DeviceName} disconnected from device-bus — clearing active",
                        args: device.Name
                    );
                    playerState.PlayState = false;
                    playerState.DeviceId = null;
                    try
                    {
                        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
                        User owner = await ctx.Users.FirstAsync(predicate: u =>
                            u.Id == device.OwnerUserId.Value
                        );
                        await musicPlaybackService.UpdatePlaybackState(user: owner, state: playerState);
                    }
                    catch
                    {
                        // Best-effort during teardown.
                    }
                }
                await registry.Unregister(deviceId: device.Id);
            }
        }
    }

    /// <summary>
    /// True when <paramref name="deviceId"/> still has a live MusicHub connection —
    /// the authoritative "is this device actually still around for playback purposes"
    /// signal, independent of this device-bus socket's own lifecycle. See the call
    /// site in <see cref="Pump"/> for why device-bus going down must never be treated
    /// as proof of that on its own. Internal + static (rather than an instance
    /// method closing over the injected <see cref="ConnectedClients"/>) so it is
    /// directly unit-testable without standing up a live WebSocket.
    /// </summary>
    internal static bool IsStillOnMusicHub(ConnectedClients connectedClients, string deviceId)
    {
        return connectedClients.Clients.Values.Any(predicate: c =>
            c.DeviceId.Equals(value: deviceId, comparisonType: StringComparison.OrdinalIgnoreCase)
            && c.Endpoint.Contains(value: "musicHub", comparisonType: StringComparison.OrdinalIgnoreCase)
        );
    }

    private async Task<Device?> HandleHello(JsonElement root, User user, WebSocket ws)
    {
        // Tolerate missing properties — legacy clients sometimes omit name /
        // device_type. Fingerprint is the only hard requirement.
        string? fingerprint = root.TryGetProperty(propertyName: "fingerprint", value: out JsonElement fpEl)
            ? fpEl.GetString()
            : null;
        if (string.IsNullOrEmpty(value: fingerprint))
            return null;

        string deviceName = root.TryGetProperty(propertyName: "name", value: out JsonElement nameEl)
            ? nameEl.GetString() ?? "Android TV"
            : "Android TV";
        string deviceType = root.TryGetProperty(propertyName: "device_type", value: out JsonElement typeEl)
            ? typeEl.GetString() ?? "tv"
            : "tv";

        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        (Device device, Guid? previousOwner) = await ResolveOwnedDeviceAsync(
            ctx: ctx,
            fingerprint: fingerprint,
            userId: user.Id,
            deviceName: deviceName,
            deviceType: deviceType
        );

        await registry.Register(deviceId: device.Id, ws: ws);

        // If the device just moved to a different account, refresh the previous
        // owner so the device disappears from their list immediately.
        if (previousOwner is { } previous && previous != user.Id)
            await registry.BroadcastChange(ownerUserId: previous);
        return device;
    }

    /// <summary>
    /// Resolves the device row for <paramref name="fingerprint"/> and pins its
    /// ownership to <paramref name="userId"/> — the account the device is currently
    /// authenticated as — creating the row when it does not exist. Returns the
    /// device together with the previous owner (<see langword="null"/> for a brand
    /// new device) so the caller can refresh a prior owner's list on a transfer.
    /// Ownership deliberately follows the session: a device logged into a new
    /// account must stop surfacing on the account that paired it first.
    /// </summary>
    public static async Task<(Device device, Guid? previousOwner)> ResolveOwnedDeviceAsync(
        MediaContext ctx,
        string fingerprint,
        Guid userId,
        string deviceName,
        string deviceType
    )
    {
        // DeviceId is globally unique (== ANDROID_ID for Android clients); the
        // Fingerprint fallback matches legacy device-bus rows that pre-date the ID
        // alignment. The lookup is intentionally NOT scoped by owner so a device
        // that moves accounts is found and re-owned rather than duplicated.
        Device? device = await ctx.Devices.FirstOrDefaultAsync(predicate: d =>
            d.DeviceId == fingerprint || d.Fingerprint == fingerprint
        );

        Guid? previousOwner = device?.OwnerUserId;

        if (device is null)
        {
            device = new()
            {
                DeviceId = fingerprint,
                Fingerprint = fingerprint,
                OwnerUserId = userId,
                Name = deviceName,
                Type = deviceType,
                IsActive = true,
            };
            ctx.Devices.Add(entity: device);
        }
        else
        {
            device.Fingerprint = fingerprint;
            device.OwnerUserId = userId;
            if (string.IsNullOrEmpty(value: device.Type))
                device.Type = deviceType;
        }

        device.WsConnectedAt = DateTime.UtcNow;
        device.IsActive = true;
        await ctx.SaveChangesAsync();
        return (device, previousOwner);
    }
}
