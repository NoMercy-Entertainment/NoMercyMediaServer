using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Controllers.Socket;

public sealed class DeviceBusRegistry(IDbContextFactory<MediaContext> contextFactory)
{
    private readonly ConcurrentDictionary<Ulid, WebSocket> _live = new();

    public Task Register(Ulid deviceId, WebSocket ws)
    {
        _live[deviceId] = ws;
        return Task.CompletedTask;
    }

    public async Task Unregister(Ulid deviceId)
    {
        _live.TryRemove(deviceId, out _);

        await using MediaContext ctx = await contextFactory.CreateDbContextAsync();
        Device? device = await ctx.Devices.FindAsync(deviceId);
        if (device is null) return;
        device.WsConnectedAt = null;
        await ctx.SaveChangesAsync();
    }

    public bool IsOnline(Ulid deviceId) => _live.ContainsKey(deviceId);

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
}
