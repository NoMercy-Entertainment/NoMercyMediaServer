using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Networking.Connectivity.Strategies;

public class PortForwardStrategy(NetworkDiscovery networkDiscovery) : IConnectivityStrategy
{
    public string Name => "PortForward";
    public int Priority => 1;
    public ConnectivityType Type => ConnectivityType.PortForward;

    public async Task<bool> TryEstablishAsync(CancellationToken ct)
    {
        if (Config.NatStatus == NatStatus.Open)
        {
            Logger.Setup("NAT status is open, you can access your server from outside your local network.");
            return true;
        }

        // UPnP port mapping succeeded — trust it even if the self-connect test fails.
        // Many routers don't support NAT hairpinning (connecting to your own external IP
        // from inside the LAN), so the TCP test would fail despite the port being open
        // to external clients.
        if (Config.NatStatus == NatStatus.Filtered)
        {
            Logger.Setup("UPnP port mapping active — port forwarding confirmed via UPnP.");
            Config.PortForwarded = true;
            Config.NatStatus = NatStatus.Open;
            return true;
        }

        // No UPnP — try direct TCP connect to external IP as a last resort
        Config.PortForwarded = await networkDiscovery.IsPortOpenAsync();
        if (Config.PortForwarded)
        {
            Logger.Setup("Your server is port forwarded, you can access your server from outside your local network.");
            Config.NatStatus = NatStatus.Open;
            return true;
        }

        Logger.Setup("Port forward check failed — router may not support NAT hairpinning, " +
                      "but external clients may still be able to connect.", LogEventLevel.Debug);
        return false;
    }

    public Task TeardownAsync()
    {
        return Task.CompletedTask;
    }
}
