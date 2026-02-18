using NoMercy.Helpers.Monitoring;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Controllers.Socket;

public static class ResourceMonitor
{
    private static bool _broadcasting;
    private static CancellationTokenSource? _cancellationTokenSource;
    private static IClientMessenger? _clientMessenger;

    public static void StartBroadcasting(IClientMessenger clientMessenger)
    {
        if (_broadcasting) return;
        _clientMessenger = clientMessenger;
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
                _clientMessenger?.SendToAll("ResourceUpdate", "dashboardHub", resourceData);

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