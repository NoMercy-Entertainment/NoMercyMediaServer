#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Mono.Nat;
using Newtonsoft.Json;

namespace NoMercy.Networking;

public class Networking
{
    private static IHubContext<ConnectionHub> HubContext { get; set; }

    public Networking(IHubContext<ConnectionHub> hubContext)
    {
        HubContext = hubContext;
    }

    public Networking()
    {
        // GetExternalIp();
    }

    private static INatDevice? _device;

    public static readonly ConcurrentDictionary<string, Client> SocketClients = new();


    public static Task Discover()
    {
        NatUtility.DeviceFound += DeviceFound;

        NatUtility.StartDiscovery();

        return Task.CompletedTask;
    }

    private static string? _internalIp;

    public static string InternalIp
    {
        get => _internalIp ?? GetInternalIp();
        set => _internalIp = value;
    }

    private static string? _externalIp;

    private static string ExternalIp
    {
        get => _externalIp ?? GetExternalIp();
        set => _externalIp = value;
    }

    public static string InternalAddress { get; private set; } = "";

    public static string ExternalAddress { get; private set; } = "";

    private static string GetInternalIp()
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);

        socket.Connect("1.1.1.1", 65530);

        IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;

        string? localIp = endPoint?.Address.ToString().Replace("\"", "");

        if (localIp == null) return "";

        InternalAddress =
            $"https://{Regex.Replace(localIp, "\\.", "-")}.{NmSystem.Info.DeviceId}.nomercy.tv:{Config.InternalServerPort}";

        return localIp;
    }

    private static string GetExternalIp()
    {
        HttpClient client = new();

        string externalIp = client.GetStringAsync($"{Config.ApiBaseUrl}/server/ip").Result.Replace("\"", "");

        ExternalAddress =
            $"https://{Regex.Replace(externalIp, "\\.", "-")}.{NmSystem.Info.DeviceId}.nomercy.tv:{Config.ExternalServerPort}";

        return externalIp;
    }

    private static void DeviceFound(object? sender, DeviceEventArgs args)
    {
        _device = args.Device;

        _device.CreatePortMap(new Mapping(Protocol.Tcp, Config.InternalServerPort, Config.ExternalServerPort, 9999999,
            "NoMercy MediaServer (TCP)"));
        _device.CreatePortMap(new Mapping(Protocol.Udp, Config.InternalServerPort, Config.ExternalServerPort, 9999999,
            "NoMercy MediaServer (UDP)"));

        ExternalIp = _device.GetExternalIP().ToString();

        if (ExternalIp == "")
            ExternalIp = GetExternalIp();
        else
            ExternalAddress =
                $"https://{Regex.Replace(ExternalIp, "\\.", "-")}.{NmSystem.Info.DeviceId}.nomercy.tv:{Config.ExternalServerPort}";
    }

    public static IWebHost TempServer()
    {
        return WebHost.CreateDefaultBuilder()
            .UseUrls("http://0.0.0.0:" + Config.InternalServerPort)
            .Configure(app =>
            {
                app.Run(async context =>
                {
                    string code = context.Request.Query["code"].ToString();

                    Auth.TokenByAuthorizationCode(code);

                    context.Response.Headers.Append("Content-Type", "text/html");
                    await context.Response.WriteAsync("<script>window.close();</script>");
                });
            }).Build();
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
            catch (Exception e)
            {
                return false;
            }

        return true;
    }

    private static bool SendTo(string name, string endpoint, Guid userId, object? data = null)
    {
        foreach ((string _, Client client) in SocketClients.Where(client =>
                     client.Value.Sub == userId && client.Value.Endpoint == "/" + endpoint))
            try
            {
                if (data != null)
                    client.Socket.SendAsync(name, data).Wait();
                else
                    client.Socket.SendAsync(name).Wait();
            }
            catch (Exception e)
            {
                return false;
            }

        return true;
    }

    private static bool Reply(string name, string endpoint, HttpContext context, object? data = null)
    {
        Guid userId = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

        foreach ((string _, Client client) in SocketClients.Where(client =>
                     client.Value.Sub == userId && client.Value.Endpoint == "/" + endpoint))
            try
            {
                if (data != null)
                    client.Socket.SendAsync(name, data).Wait();
                else
                    client.Socket.SendAsync(name).Wait();
            }
            catch (Exception e)
            {
                return false;
            }

        return true;
    }
}

public class RefreshLibraryDto
{
    [JsonProperty("queryKey")] public dynamic?[] QueryKey { get; set; } = [];
}