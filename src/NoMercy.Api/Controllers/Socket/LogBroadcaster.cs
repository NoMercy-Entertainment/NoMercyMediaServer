using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Controllers.Socket;

public static class LogBroadcaster
{
    private static bool _broadcasting;
    private static IClientMessenger? _clientMessenger;

    public static void StartBroadcasting(IClientMessenger clientMessenger)
    {
        if (_broadcasting) return;
        _clientMessenger = clientMessenger;
        _broadcasting = true;
        Logger.LogEmitted += OnLogEmitted;
    }

    public static void StopBroadcasting()
    {
        _broadcasting = false;
        Logger.LogEmitted -= OnLogEmitted;
        _clientMessenger = null;
    }

    private static void OnLogEmitted(LogEntry entry)
    {
        _clientMessenger?.SendToAll("NewLog", "dashboardHub", entry);
    }
}
