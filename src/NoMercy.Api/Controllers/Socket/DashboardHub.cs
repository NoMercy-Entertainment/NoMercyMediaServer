using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Controllers.Socket;

public class DashboardHub : ConnectionHub
{
    private readonly IClientMessenger _clientMessenger;

    public DashboardHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IClientMessenger clientMessenger,
        IActivityLogger activityLogger
    )
        : base(httpContextAccessor, contextFactory, connectedClients, activityLogger)
    {
        _clientMessenger = clientMessenger;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        if (Context.User.IsModerator())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "moderators");
        }

        Logger.Socket("Dashboard client connected", LogEventLevel.Debug);
        LogBroadcaster.StartBroadcasting(_clientMessenger);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "moderators");
        Logger.Socket("Dashboard client disconnected", LogEventLevel.Debug);

        StopResources();
    }

    public void StartResources()
    {
        ResourceMonitor.StartBroadcasting(_clientMessenger);
    }

    public void StopResources()
    {
        if (ConnectedClients.Clients.Values.All(x => x.Endpoint != "/dashboardHub"))
        {
            ResourceMonitor.StopBroadcasting();
            LogBroadcaster.StopBroadcasting();
        }
    }
}
