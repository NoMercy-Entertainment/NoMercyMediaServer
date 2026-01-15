using NoMercy.Helpers.Monitoring;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Controllers.Socket;

public static class ResourceMonitor
{
    private static bool _broadcasting;
    private static CancellationTokenSource? _cancellationTokenSource;

    public static void StartBroadcasting()
    {
        if (_broadcasting) return;
        Logger.Socket("Starting resource monitoring broadcast");
        _broadcasting = true;
        _cancellationTokenSource = new();
        Task.Run(() => BroadcastLoop(_cancellationTokenSource.Token));
    }

    public static void StopBroadcasting()
    {
        Logger.Socket("Stopping resource monitoring broadcast");
        _broadcasting = false;

        _cancellationTokenSource?.Cancel();
    }

    private static async Task BroadcastLoop(CancellationToken cancellationToken)
    {
        while (_broadcasting && !cancellationToken.IsCancellationRequested)
        {
            DateTime time = DateTime.Now;
            try
            {
                Resource resourceData = Helpers.Monitoring.ResourceMonitor.Monitor();
                Networking.Networking.SendToAll("ResourceUpdate", "dashboardHub", resourceData);

                // at least one second between broadcasts
                int delay = 1000 - (int)(DateTime.Now - time).TotalMilliseconds;
                if (delay > 0) await Task.Delay(delay, cancellationToken);
            }
            catch (Exception e)
            {
                if (e.Message == "A task was canceled.") return;
                Logger.Socket($"Error broadcasting resource data: {e.Message}", LogEventLevel.Error);
            }
        }
    }
}