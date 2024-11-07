using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
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
        ResourceMonitor.StopBroadcasting();
    }
}