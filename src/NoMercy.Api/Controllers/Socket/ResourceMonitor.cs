using NoMercy.Helpers.Monitoring;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Controllers.Socket;

public static class ResourceMonitor
{
    private static readonly Lock Sync = new();
    private static bool _broadcasting;
    private static CancellationTokenSource? _cancellationTokenSource;
    private static IClientMessenger? _clientMessenger;

    public static void StartBroadcasting(IClientMessenger clientMessenger)
    {
        lock (Sync)
        {
            if (_broadcasting)
                return;
            _clientMessenger = clientMessenger;
            _broadcasting = true;
            // Replace any leaked CTS from a previous Start/Stop cycle.
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new();
        }

        Logger.Socket("Starting resource monitoring broadcast");
        CancellationToken token = _cancellationTokenSource.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await BroadcastLoop(token);
            }
            catch (Exception ex)
            {
                Logger.Socket(
                    $"Resource monitor broadcast loop crashed: {ex.Message}",
                    LogEventLevel.Error
                );
            }
        });
    }

    public static void StopBroadcasting()
    {
        lock (Sync)
        {
            if (!_broadcasting)
                return;
            _broadcasting = false;
        }

        Logger.Socket("Stopping resource monitoring broadcast");
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CTS already disposed — nothing to cancel.
        }
    }

    private static async Task BroadcastLoop(CancellationToken cancellationToken)
    {
        while (_broadcasting && !cancellationToken.IsCancellationRequested)
        {
            DateTime time = DateTime.Now;
            try
            {
                Resource resourceData = Helpers.Monitoring.ResourceMonitor.Monitor();
                if (_clientMessenger != null)
                    await _clientMessenger.SendToAll(
                        "ResourceUpdate",
                        "dashboardHub",
                        resourceData
                    );

                int delay = 1000 - (int)(DateTime.Now - time).TotalMilliseconds;
                if (delay > 0)
                    await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Logger.Socket(
                    $"Error broadcasting resource data: {e.Message}",
                    LogEventLevel.Error
                );
            }
        }
    }
}
