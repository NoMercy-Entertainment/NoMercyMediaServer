using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Networking;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.Socket;

public class DashboardHub : ConnectionHub
{
    public DashboardHub(IHttpContextAccessor httpContextAccessor, IDbContextFactory<MediaContext> contextFactory)
        : base(httpContextAccessor, contextFactory)
    {
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Logger.Socket("Dashboard client connected");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        Logger.Socket("Dashboard client disconnected");

        StopResources();
    }

    public void StartResources()
    {
        ResourceMonitor.StartBroadcasting();
    }

    public void StopResources()
    {
        if (Networking.Networking.SocketClients.Values.All(x => x.Endpoint != "/dashboardHub"))
            ResourceMonitor.StopBroadcasting();
    }
}