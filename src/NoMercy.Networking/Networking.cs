using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Mono.Nat;
using Newtonsoft.Json;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using Config = NoMercy.NmSystem.Information.Config;

namespace NoMercy.Networking;

public class Networking
{
    private static IHubContext<ConnectionHub> HubContext { get; set; } = null!;

    public Networking(IHubContext<ConnectionHub> hubContext)
    {
        HubContext = hubContext;
    }

    public Networking()
    {
        // GetExternalIp();
    }

    private static INatDevice? _device;

    private static bool HasFoundDevice { get; set; } = false;

    public static readonly ConcurrentDictionary<string, Client> SocketClients = new();
    
    public static async Task Discover()
    {
        Logger.Setup("Discovering Networking");

        NatUtility.DeviceFound += DeviceFound;
        NatUtility.UnknownDeviceFound += UnknownDeviceFound;

        NetworkChange.NetworkAddressChanged += NetworkAddressChanged;

        Logger.Setup("Discovering UPNP devices");
        
        _ = Task.Run(() => NatUtility.StartDiscovery());

        if (!HasFoundDevice) await Task.Delay(TimeSpan.FromSeconds(5));
        if (!HasFoundDevice) await Task.Delay(TimeSpan.FromSeconds(10));

        if (!HasFoundDevice)
        {
            Logger.Setup("No UPNP device found");
        }

        if (string.IsNullOrEmpty(_externalIp))
        {
            try
            {
                ExternalIp = await GetExternalIp();
            }
            catch (Exception e)
            {
                Logger.Setup($"Failed to get external IP from API: {e.Message}");
            }
        }

        Logger.App($"External Address: {ExternalAddress}");
    }

    private static void NetworkAddressChanged(object? sender, EventArgs e)
    { 
        string ip = GetInternalIp();
        if (ip == InternalIp) return;
        
        InternalIp = ip;
        
        _ = OnIpChanged(InternalIp);
    }

    private static string? _internalIp;

    public static string InternalIp
    {
        get => _internalIp ?? GetInternalIp();
        set
        {
            if (_internalIp == value) return;
            if(!string.IsNullOrEmpty(_internalIp))
            {
                _ = OnIpChanged(value);
            }
            _internalIp = value;
        }
    }

    private static string? _externalIp;

    public static string ExternalIp
    {
        get => _externalIp ?? "0.0.0.0";
        set
        {
            if (_externalIp == value) return;
                if(!string.IsNullOrEmpty(_externalIp))
                {
                    _ = OnIpChanged(value);
                }
                _externalIp = value;
        }
    }

    public static string InternalDomain => $"{InternalIp.SafeHost()}.{Info.DeviceId}.nomercy.tv";
    public static string InternalAddress => $"https://{InternalDomain}:{Config.InternalServerPort}";

    public static string ExternalDomain => $"{ExternalIp.SafeHost()}.{Info.DeviceId}.nomercy.tv";
    public static string ExternalAddress => $"https://{ExternalDomain}:{Config.ExternalServerPort}";

    public static bool Ipv6Enabled => CheckIpv6();

    private static string GetInternalIp()
    {
        // Primary: enumerate local network interfaces (no network needed)
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

        // Fallback: UDP socket trick (needs route to internet)
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

    private static string ExternalIpCacheFile =>
        Path.Combine(AppFiles.ConfigPath, "external_ip.cache");

    private static async Task<string> GetExternalIp()
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

        // 4. No external IP available — remote access won't work, but not fatal
        Logger.Setup("External IP unavailable — remote access disabled", LogEventLevel.Warning);
        return "";
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
    
    private static async Task OnIpChanged(string? newIp)
    {
        Logger.Setup($"Network address changed, updating IPs, IP changed to: {newIp}");

        if (string.IsNullOrEmpty(newIp))
        {
            Config.NatStatus = NatStatus.None;
            return;
        }

        await SendUpdate();
        
        Logger.Setup($"External Address: {ExternalAddress}");
        Logger.Setup($"Internal Address: {InternalAddress}");
    }
    
    private static async Task SendUpdate()
    {
        Dictionary<string, string> serverData = new()
        {
            { "id", Info.DeviceId.ToString() },
            { "name", Info.DeviceName },
            { "internal_ip", InternalIp },
            { "internal_port", Config.InternalServerPort.ToString() },
            { "external_port", Config.ExternalServerPort.ToString() },
            { "version", Software.Version!.ToString() },
            { "platform", Info.Platform }
        };

        Logger.Register("Your IP address has changed, updating server information...");

        GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
        string response =
            await authClient.SendAndReadAsync(HttpMethod.Post, "ping", new FormUrlEncodedContent(serverData));

        object? data = JsonConvert.DeserializeObject(response);

        if (data == null) throw new("Failed to update server information");

        Logger.Register("Server information updated successfully");
    }

    private static void DeviceFound(object? sender, DeviceEventArgs args)
    {
        if (HasFoundDevice) return;
        
        Logger.Setup("UPNP router Found: " + args.Device.DeviceEndpoint);

        _device = args.Device;

        HasFoundDevice = true;

        GetNatStatus();
    }

    private static void UnknownDeviceFound(object? sender, DeviceEventUnknownArgs args)
    {
    }

    public static bool SendToAll(string name, string endpoint, object? data = null)
    {
        foreach ((string _, Client client) in SocketClients.Where(client => client.Value.Endpoint == "/" + endpoint))
            try
            {
                if (data != null)
                    client.Socket.SendAsync(name, data).Wait();
                else
                    client.Socket.SendAsync(name).Wait();
            }
            catch (Exception)
            {
                return false;
            }

        return true;
    }

    public static async Task SendTo(string name, string endpoint, Guid userId, object? data = null)
    {
        foreach ((string _, Client client) in SocketClients.Where(client =>
                     client.Value.Sub.Equals(userId) && client.Value.Endpoint == "/" + endpoint))
            try
            {
                if (data != null)
                    await client.Socket.SendAsync(name, data);
                else
                    await client.Socket.SendAsync(name);
            }
            catch (Exception)
            {
                return;
            }

        await Task.CompletedTask;
    }

    private static async Task Reply(string name, string endpoint, HttpContext context, object? data = null)
    {
        Guid userId = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

        foreach ((string _, Client client) in SocketClients.Where(client =>
                     client.Value.Sub.Equals(userId) && client.Value.Endpoint == "/" + endpoint))
            try
            {
                if (data != null)
                    await client.Socket.SendAsync(name, data);
                else
                    await client.Socket.SendAsync(name);
            }
            catch (Exception)
            {
                return;
            }

        await Task.CompletedTask;
    }

    private static bool CheckIpv6()
    {
        if (!Socket.OSSupportsIPv6) return false;

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            if (nic.Supports(NetworkInterfaceComponent.IPv6))
                return true;

        return false;
    }

    private static void GetNatStatus()
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
            Logger.Setup($"IP address obtained from API: {_externalIp}");

            if(string.IsNullOrEmpty(_externalIp))
            {
                ExternalIp = ip;
            }
        }
        catch (Exception e)
        {
            Logger.Setup($"Failed to create UPNP records: {e.Message}");
            HasFoundDevice = false;
            Config.NatStatus = NatStatus.Closed;
            return;
        }

        Config.NatStatus = NatStatus.Filtered;
    }

    public static async Task<bool> IsPortOpenAsync()
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
                LogEventLevel.Error);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Setup($"Exception checking {ExternalIp}:{Config.ExternalServerPort}: {ex.Message}",
                LogEventLevel.Error);
            return false;
        }
    }
}