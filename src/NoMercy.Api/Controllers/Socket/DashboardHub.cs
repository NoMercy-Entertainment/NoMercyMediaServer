using Microsoft.AspNetCore.Http;
using NoMercy.Networking;
using NoMercy.NmSystem;

namespace NoMercy.Api.Controllers.Socket;

public class DashboardHub : ConnectionHub
{
    public DashboardHub(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
    {
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
        {
            ResourceMonitor.StopBroadcasting();
        }
    }
}