using System.Net.NetworkInformation;
using Microsoft.Extensions.Hosting;
using NoMercy.Networking.Connectivity;
using NoMercy.NmSystem;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Networking.Discovery;

public class NetworkChangeMonitor : IHostedService, IDisposable
{
    private readonly INetworkDiscovery _networkDiscovery;
    private readonly IConnectivityManager _connectivityManager;
    private readonly SemaphoreSlim _reevaluationLock = new(1, 1);

    public NetworkChangeMonitor(INetworkDiscovery networkDiscovery, IConnectivityManager connectivityManager)
    {
        _networkDiscovery = networkDiscovery;
        _connectivityManager = connectivityManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        Logger.Setup("Network change monitor started", LogEventLevel.Debug);
        return Task.CompletedTask;
    }

    private async void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        if (!await _reevaluationLock.WaitAsync(0)) return;

        try
        {
            string oldIp = _networkDiscovery.InternalIp;
            // Force re-discovery by reading from interfaces
            string newIp = GetCurrentInternalIp();

            if (newIp == oldIp) return;

            Logger.Setup($"Network address changed: {oldIp} → {newIp}");
            _networkDiscovery.InternalIp = newIp;

            // Re-discover external IP
            await _networkDiscovery.DiscoverExternalIpAsync();

            // Re-evaluate connectivity strategies
            await _connectivityManager.EvaluateAsync(CancellationToken.None);

            // Send update to NoMercy API
            await SendUpdate();
        }
        catch (Exception ex)
        {
            Logger.Setup($"Network change handling failed: {ex.Message}", LogEventLevel.Warning);
        }
        finally
        {
            _reevaluationLock.Release();
        }
    }

    private static string GetCurrentInternalIp()
    {
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel) continue;

                foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    if (System.Net.IPAddress.IsLoopback(addr.Address)) continue;

                    return addr.Address.ToString();
                }
            }
        }
        catch
        {
            // Fall through
        }

        return "127.0.0.1";
    }

    private async Task SendUpdate()
    {
        try
        {
            Dictionary<string, string> serverData = new()
            {
                { "id", NmSystem.Information.Info.DeviceId.ToString() },
                { "name", NmSystem.Information.Info.DeviceName },
                { "internal_ip", _networkDiscovery.InternalIp },
                { "internal_ipv6", _networkDiscovery.InternalIpV6 ?? "" },
                { "external_ipv6", _networkDiscovery.ExternalIpV6 ?? "" },
                { "internal_port", NmSystem.Information.Config.InternalServerPort.ToString() },
                { "external_port", NmSystem.Information.Config.ExternalServerPort.ToString() },
                { "version", NmSystem.Information.Software.Version!.ToString() },
                { "platform", NmSystem.Information.Info.Platform },
                { "stun_public_ip", NmSystem.Information.Config.StunPublicIp ?? "" },
                { "stun_public_port", NmSystem.Information.Config.StunPublicPort?.ToString() ?? "" },
                { "stun_nat_type", NmSystem.Information.Config.NatStatus.ToString() }
            };

            Logger.Register("Your IP address has changed, updating server information...");

            GenericHttpClient authClient = new(NmSystem.Information.Config.ApiServerBaseUrl);
            authClient.SetDefaultHeaders(NmSystem.Information.Config.UserAgent, Globals.Globals.AccessToken);
            string response =
                await authClient.SendAndReadAsync(HttpMethod.Post, "ping", new FormUrlEncodedContent(serverData));

            object? data = Newtonsoft.Json.JsonConvert.DeserializeObject(response);

            if (data == null) throw new("Failed to update server information");

            Logger.Register("Server information updated successfully");
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to send IP update: {ex.Message}", LogEventLevel.Warning);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _reevaluationLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
