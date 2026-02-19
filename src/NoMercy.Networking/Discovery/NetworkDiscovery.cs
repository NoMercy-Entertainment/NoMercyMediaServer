using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Mono.Nat;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Networking.Discovery;

public class NetworkDiscovery : INetworkDiscovery
{
    private string? _internalIp;
    private string? _externalIp;
    private INatDevice? _device;
    private bool _hasFoundDevice;

    public string InternalIp
    {
        get => _internalIp ?? GetInternalIp();
        set
        {
            if (_internalIp == value) return;
            _internalIp = value;
        }
    }

    public string ExternalIp
    {
        get => _externalIp ?? "0.0.0.0";
        set
        {
            if (_externalIp == value) return;
            _externalIp = value;
        }
    }

    public string? InternalIpV6 => GetInternalIpV6();

    private string? _externalIpV6;

    public string? ExternalIpV6
    {
        get => _externalIpV6;
        set
        {
            if (_externalIpV6 == value) return;
            _externalIpV6 = value;
        }
    }

    public string InternalDomain => $"{InternalIp.SafeHost()}.{Info.DeviceId}.nomercy.tv";
    public string InternalAddress => $"https://{InternalDomain}:{Config.InternalServerPort}";

    public string ExternalDomain => $"{ExternalIp.SafeHost()}.{Info.DeviceId}.nomercy.tv";
    public string ExternalAddress => $"https://{ExternalDomain}:{Config.ExternalServerPort}";

    public string? ExternalAddressV6 => ExternalIpV6 is not null
        ? $"https://[{ExternalIpV6}]:{Config.ExternalServerPort}"
        : null;

    public bool Ipv6Enabled => CheckIpv6();

    private bool _discoveryCompleted;
    private readonly SemaphoreSlim _discoverySemaphore = new(1, 1);

    public async Task DiscoverExternalIpAsync()
    {
        await _discoverySemaphore.WaitAsync();
        try
        {
            if (_discoveryCompleted) return;

            Logger.Setup("Discovering Networking");

            NatUtility.DeviceFound += DeviceFound;
            NatUtility.UnknownDeviceFound += (_, _) => { };

            Logger.Setup("Discovering UPNP devices");

            _ = Task.Run(() => NatUtility.StartDiscovery());

            if (!_hasFoundDevice) await Task.Delay(TimeSpan.FromSeconds(5));
            if (!_hasFoundDevice) await Task.Delay(TimeSpan.FromSeconds(10));

            if (!_hasFoundDevice)
            {
                Logger.Setup("No UPNP device found");
            }

            if (string.IsNullOrEmpty(_externalIp))
            {
                try
                {
                    ExternalIp = await GetExternalIpAsync();
                }
                catch (Exception e)
                {
                    Logger.Setup($"Failed to get external IP from API: {e.Message}");
                }
            }

            // Discover external IPv6 address
            if (Ipv6Enabled)
            {
                try
                {
                    ExternalIpV6 = await GetExternalIpV6Async();
                    if (ExternalIpV6 is null)
                        Logger.Setup("No external IPv6 address available", LogEventLevel.Debug);
                }
                catch (Exception e)
                {
                    Logger.Setup($"Failed to get external IPv6: {e.Message}", LogEventLevel.Debug);
                }
            }

            _discoveryCompleted = true;
        }
        finally
        {
            _discoverySemaphore.Release();
        }
    }

    private void DeviceFound(object? sender, DeviceEventArgs args)
    {
        if (_hasFoundDevice) return;

        Logger.Setup("UPNP router Found: " + args.Device.DeviceEndpoint);

        _device = args.Device;
        _hasFoundDevice = true;

        ApplyNatStatus();
    }

    private void ApplyNatStatus()
    {
        if (_device == null)
        {
            Config.NatStatus = NatStatus.None;
            return;
        }

        try
        {
            Logger.Setup("Trying to add UPNP records");

            _device.CreatePortMap(new(
                Protocol.Tcp,
                Config.InternalServerPort,
                Config.ExternalServerPort,
                0,
                "NoMercy MediaServer (TCP)"));

            _device.CreatePortMap(new(
                Protocol.Udp,
                Config.InternalServerPort,
                Config.ExternalServerPort,
                0,
                "NoMercy MediaServer (UDP)"));

            string ip = _device.GetExternalIP().ToString();

            Logger.Setup($"IP address obtained from UPNP: {ip}");
            if (!string.IsNullOrEmpty(_externalIp))
                Logger.Setup($"IP address obtained from API: {_externalIp}");

            if (string.IsNullOrEmpty(_externalIp))
            {
                ExternalIp = ip;
            }
        }
        catch (Exception e)
        {
            Logger.Setup($"Failed to create UPNP records: {e.Message}");
            _hasFoundDevice = false;
            Config.NatStatus = NatStatus.Closed;
            return;
        }

        Config.NatStatus = NatStatus.Filtered;
    }

    public async Task<bool> IsPortOpenAsync()
    {
        int timeoutMilliseconds = 1500;

        using TcpClient client = new();
        Task connectTask = client.ConnectAsync(ExternalIp, Config.ExternalServerPort);
        Task delayTask = Task.Delay(timeoutMilliseconds);
        Task completedTask = await Task.WhenAny(connectTask, delayTask);

        if (completedTask == delayTask)
        {
            Logger.Setup($"Timeout checking {ExternalIp}:{Config.ExternalServerPort} after {timeoutMilliseconds}ms.",
                LogEventLevel.Verbose);
            return false;
        }

        try
        {
            await connectTask;
            return true;
        }
        catch (SocketException ex)
        {
            Logger.Setup(
                $"SocketException checking {ExternalIp}:{Config.ExternalServerPort}: {ex.SocketErrorCode} ({ex.Message})",
                LogEventLevel.Debug);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Setup($"Exception checking {ExternalIp}:{Config.ExternalServerPort}: {ex.Message}",
                LogEventLevel.Debug);
            return false;
        }
    }

    private static string GetInternalIp()
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
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(addr.Address)) continue;

                    return addr.Address.ToString();
                }
            }
        }
        catch
        {
            // Fall through to socket method
        }

        try
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("1.1.1.1", 65530);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static string? GetInternalIpV6()
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
                    if (addr.Address.AddressFamily != AddressFamily.InterNetworkV6) continue;
                    if (addr.Address.IsIPv6LinkLocal) continue;
                    if (addr.Address.IsIPv6SiteLocal) continue;

                    return addr.Address.ToString();
                }
            }
        }
        catch
        {
            // No IPv6 available
        }

        try
        {
            using Socket socket = new(AddressFamily.InterNetworkV6, SocketType.Dgram, 0);
            socket.Connect("2001:4860:4860::8888", 65530);
            IPEndPoint? endpoint = socket.LocalEndPoint as IPEndPoint;
            if (endpoint?.Address is not null && !endpoint.Address.IsIPv6LinkLocal)
                return endpoint.Address.ToString();
        }
        catch
        {
            // IPv6 not routable
        }

        return null;
    }

    private static string ExternalIpCacheFile =>
        Path.Combine(AppFiles.ConfigPath, "external_ip.cache");

    private async Task<string> GetExternalIpAsync()
    {
        Logger.Setup("Getting external IP address");

        // 1. Try API
        try
        {
            GenericHttpClient apiClient = new(Config.ApiBaseUrl);
            apiClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
            using HttpResponseMessage response = await apiClient.SendAsync(HttpMethod.Get, "v1/ip");
            if (response.IsSuccessStatusCode)
            {
                string ip = (await response.Content.ReadAsStringAsync()).Replace("\"", "");
                if (!string.IsNullOrEmpty(ip))
                {
                    CacheExternalIp(ip);
                    return ip;
                }
            }
        }
        catch (Exception e)
        {
            Logger.Setup($"External IP API unavailable: {e.Message}", LogEventLevel.Warning);
        }

        // 2. Try UPnP device
        if (_device is not null)
        {
            try
            {
                string upnpIp = _device.GetExternalIP().ToString();
                if (!string.IsNullOrEmpty(upnpIp))
                {
                    CacheExternalIp(upnpIp);
                    return upnpIp;
                }
            }
            catch (Exception e)
            {
                Logger.Setup($"UPnP external IP unavailable: {e.Message}", LogEventLevel.Warning);
            }
        }

        // 3. Try file cache
        string? cached = LoadCachedExternalIp();
        if (cached is not null)
        {
            Logger.Setup($"Using cached external IP: {cached}");
            return cached;
        }

        // 4. No external IP available
        Logger.Setup("External IP unavailable — remote access disabled", LogEventLevel.Warning);
        return "";
    }

    private async Task<string?> GetExternalIpV6Async()
    {
        // 1. Try the NoMercy API over IPv6
        try
        {
            using System.Net.Http.HttpClient httpClient = new(new SocketsHttpHandler
            {
                ConnectCallback = async (context, ct) =>
                {
                    Socket socket = new(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                    socket.NoDelay = true;
                    try
                    {
                        await socket.ConnectAsync(context.DnsEndPoint, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            });
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            httpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            string ip = await httpClient.GetStringAsync($"{Config.ApiBaseUrl}v1/ip");
            ip = ip.Replace("\"", "").Trim();
            if (!string.IsNullOrEmpty(ip) && ip.Contains(':'))
                return ip;
        }
        catch (Exception e)
        {
            Logger.Setup($"External IPv6 API unavailable: {e.Message}", LogEventLevel.Debug);
        }

        // 2. Try well-known IPv6 services
        string[] ipv6Services =
        [
            "https://api64.ipify.org",
            "https://v6.ident.me",
            "https://ipv6.icanhazip.com"
        ];

        foreach (string service in ipv6Services)
        {
            try
            {
                using System.Net.Http.HttpClient httpClient = new(new SocketsHttpHandler
                {
                    ConnectCallback = async (context, ct) =>
                    {
                        Socket socket = new(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                        socket.NoDelay = true;
                        try
                        {
                            await socket.ConnectAsync(context.DnsEndPoint, ct);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    }
                });
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                string ip = (await httpClient.GetStringAsync(service)).Trim();
                if (!string.IsNullOrEmpty(ip) && ip.Contains(':'))
                    return ip;
            }
            catch
            {
                // Try next service
            }
        }

        // 3. No external IPv6 available
        return null;
    }

    private static void CacheExternalIp(string ip)
    {
        try
        {
            File.WriteAllText(ExternalIpCacheFile, ip);
        }
        catch (Exception e)
        {
            Logger.Setup($"Failed to cache external IP: {e.Message}", LogEventLevel.Warning);
        }
    }

    private static string? LoadCachedExternalIp()
    {
        try
        {
            if (!File.Exists(ExternalIpCacheFile)) return null;
            string cached = File.ReadAllText(ExternalIpCacheFile).Trim();
            return string.IsNullOrEmpty(cached) ? null : cached;
        }
        catch
        {
            return null;
        }
    }

    private static bool CheckIpv6()
    {
        if (!Socket.OSSupportsIPv6) return false;

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            if (nic.Supports(NetworkInterfaceComponent.IPv6))
                return true;

        return false;
    }
}
