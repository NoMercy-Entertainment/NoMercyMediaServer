using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
                if (result.MessageType == WebSocketMessageType.Close) break;

                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                using JsonDocument doc = JsonDocument.Parse(json);
                string? type = doc.RootElement.GetProperty("type").GetString();

                if (type == "hello")
                {
                    device = await HandleHello(doc.RootElement, user, ws);
                    if (device is null) break;
                }
                else if (type == "pong" && device is not null)
                {
                    registry.Touch(device.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "device-bus closed");
        }
        finally
        {
            if (device is not null) await registry.Unregister(device.Id);
        }
    }

    private async Task<Device?> HandleHello(JsonElement root, User user, WebSocket ws)
    {
        string? fingerprint = root.GetProperty("fingerprint").GetString();
        string deviceName = root.GetProperty("name").GetString() ?? "Android TV";
        string deviceType = root.GetProperty("device_type").GetString() ?? "tv";

        if (string.IsNullOrEmpty(fingerprint)) return null;

        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FirstOrDefaultAsync(
            d => d.Fingerprint == fingerprint && d.OwnerUserId == user.Id
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

        device.WsConnectedAt = DateTime.UtcNow;
        device.IsActive = true;
        await ctx.SaveChangesAsync();

        await registry.Register(device.Id, ws);
        return device;
    }
}
