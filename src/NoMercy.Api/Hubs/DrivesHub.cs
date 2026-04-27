using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Hubs;

/// <summary>
/// Broadcast-only hub for drive / disc-insertion events.
/// Clients connect to receive real-time drive state changes; there are no
/// client-invokable methods — all messages originate from the server via
/// <see cref="DriveMonitorEventHandler"/>.
/// </summary>
public class DrivesHub : ConnectionHub
{
    public DrivesHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IActivityLogger activityLogger
    )
        : base(httpContextAccessor, contextFactory, connectedClients, activityLogger) { }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Logger.Socket("Drives client connected");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        Logger.Socket("Drives client disconnected");
    }
}
