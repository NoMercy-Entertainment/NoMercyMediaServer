using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.WebSockets;

public static class LogBroadcaster
{
    private static readonly Lock Sync = new();
    private static bool _broadcasting;
    private static IClientMessenger? _clientMessenger;

    public static void StartBroadcasting(IClientMessenger clientMessenger)
    {
        lock (Sync)
        {
            if (_broadcasting)
                return;
            _clientMessenger = clientMessenger;
            _broadcasting = true;
            Logger.LogEmitted += OnLogEmitted;
        }
    }

    public static void StopBroadcasting()
    {
        lock (Sync)
        {
            if (!_broadcasting)
                return;
            _broadcasting = false;
            Logger.LogEmitted -= OnLogEmitted;
            _clientMessenger = null;
        }
    }

    private static void OnLogEmitted(LogEntry entry)
    {
        // Fire-and-forget: Logger.LogEmitted is a sync Action<> delegate and cannot be awaited.
        // Log broadcasting is best-effort — a missed entry is not a failure.
        _ = _clientMessenger?.SendToAll("NewLog", "dashboardHub", entry);
    }
}
