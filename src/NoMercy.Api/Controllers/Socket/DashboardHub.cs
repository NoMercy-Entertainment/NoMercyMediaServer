using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.Socket;

public class DashboardHub : ConnectionHub
{
    private readonly IClientMessenger _clientMessenger;

    public DashboardHub(IHttpContextAccessor httpContextAccessor, IDbContextFactory<MediaContext> contextFactory, ConnectedClients connectedClients, IClientMessenger clientMessenger)
        : base(httpContextAccessor, contextFactory, connectedClients)
    {
        _clientMessenger = clientMessenger;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Logger.Socket("Dashboard client connected");
        LogBroadcaster.StartBroadcasting(_clientMessenger);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        Logger.Socket("Dashboard client disconnected");

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