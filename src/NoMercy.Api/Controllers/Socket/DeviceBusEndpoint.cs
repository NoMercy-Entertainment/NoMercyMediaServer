using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Api.Services.Music;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.Controllers.Socket;

[ApiController]
[Authorize]
[Route("ws/device-bus")]
public sealed class DeviceBusEndpoint(
    IDbContextFactory<MediaContext> contextFactory,
    DeviceBusRegistry registry,
    MusicPlayerStateManager musicPlayerStateManager,
    MusicPlaybackService musicPlaybackService,
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

        User? user = HttpContext.User.User();
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using WebSocket ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await Pump(ws, user, HttpContext.RequestAborted);
    }

    private async Task Pump(WebSocket ws, User user, CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        Device? device = null;

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // Malformed payloads (split frames during stream churn,
                // unicode boundary issues, or buggy clients) used to throw
                // JsonException out of the loop and tear the whole device-
                // bus connection down. Skip the bad frame and wait for the
                // next clean message instead.
                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(json);
                }
                catch (JsonException)
                {
                    logger.LogDebug(
                        "device-bus skipping malformed frame ({Bytes} bytes)",
                        result.Count
                    );
                    continue;
                }

                using JsonDocument _ = doc;
                if (
                    !doc.RootElement.TryGetProperty("type", out JsonElement typeElement)
                    || typeElement.ValueKind != JsonValueKind.String
                )
                    continue;

                string? type = typeElement.GetString();

                if (type == "hello")
                {
                    device = await HandleHello(doc.RootElement, user, ws);
                    if (device is null)
                        break;
                }
                else if (type == "pong" && device is not null)
                {
                    registry.Touch(device.Id);
                }
                else if (type == "status" && device is not null)
                {
                    bool foreground =
                        doc.RootElement.TryGetProperty("foreground", out JsonElement fe)
                        && fe.ValueKind == JsonValueKind.True;
                    bool screenOn =
                        doc.RootElement.TryGetProperty("screen_on", out JsonElement se)
                        && se.ValueKind == JsonValueKind.True;

                    registry.UpdateStatus(device.Id, foreground, screenOn);
                    if (device.OwnerUserId is not null)
                        await registry.BroadcastChange(device.OwnerUserId.Value);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "device-bus closed");
        }
        finally
        {
            if (device is not null)
            {
                if (
                    device.OwnerUserId is not null
                    && musicPlayerStateManager.TryGetValue(
                        device.OwnerUserId.Value,
                        out MusicPlayerState? playerState
                    )
                    && string.Equals(
                        playerState.DeviceId,
                        device.DeviceId,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    logger.LogInformation(
                        "Active music device {DeviceName} disconnected from device-bus — clearing active",
                        device.Name
                    );
                    playerState.PlayState = false;
                    playerState.DeviceId = null;
                    try
                    {
                        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
                        User owner = await ctx.Users.FirstAsync(u =>
                            u.Id == device.OwnerUserId.Value
                        );
                        await musicPlaybackService.UpdatePlaybackState(owner, playerState);
                    }
                    catch
                    {
                        // Best-effort during teardown.
                    }
                }
                await registry.Unregister(device.Id);
            }
        }
    }

    private async Task<Device?> HandleHello(JsonElement root, User user, WebSocket ws)
    {
        // Tolerate missing properties — legacy clients sometimes omit name /
        // device_type. Fingerprint is the only hard requirement.
        string? fingerprint = root.TryGetProperty("fingerprint", out JsonElement fpEl)
            ? fpEl.GetString()
            : null;
        if (string.IsNullOrEmpty(fingerprint))
            return null;

        string deviceName = root.TryGetProperty("name", out JsonElement nameEl)
            ? nameEl.GetString() ?? "Android TV"
            : "Android TV";
        string deviceType = root.TryGetProperty("device_type", out JsonElement typeEl)
            ? typeEl.GetString() ?? "tv"
            : "tv";

        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        // Look up by DeviceId first — that's the primary unique key MusicHub already
        // uses (== ANDROID_ID for Android clients). Falls back to Fingerprint match
        // for legacy device-bus rows that pre-date the ID alignment. Either way,
        // upsert by setting both columns + ownership so the row is whole.
        Device? device = await ctx.Devices.FirstOrDefaultAsync(d =>
            d.DeviceId == fingerprint || (d.Fingerprint == fingerprint && d.OwnerUserId == user.Id)
        );

        if (device is null)
        {
            device = new Device
            {
                DeviceId = fingerprint,
                Fingerprint = fingerprint,
                OwnerUserId = user.Id,
                Name = deviceName,
                Type = deviceType,
                IsActive = true,
            };
            ctx.Devices.Add(device);
        }
        else
        {
            // Existing MusicHub-registered row — backfill the device-bus columns.
            device.Fingerprint = fingerprint;
            device.OwnerUserId ??= user.Id;
            if (string.IsNullOrEmpty(device.Type))
                device.Type = deviceType;
        }

        device.WsConnectedAt = DateTime.UtcNow;
        device.IsActive = true;
        await ctx.SaveChangesAsync();

        await registry.Register(device.Id, ws);
        return device;
    }
}
