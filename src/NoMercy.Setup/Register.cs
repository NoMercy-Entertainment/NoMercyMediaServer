using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using Serilog.Events;
using NoMercy.Helpers;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Config = NoMercy.NmSystem.Information.Config;

namespace NoMercy.Setup;

public static class Register
{
    private static string DeviceName()
    {
        MediaContext mediaContext = new();
        Configuration? device = mediaContext.Configuration.FirstOrDefault(device => device.Key == "serverName");
        return device?.Value ?? Environment.MachineName;
    }

    public static async Task Init()
    {
        Dictionary<string, string> serverData = new()
        {
            { "id", Info.DeviceId.ToString() },
            { "name", DeviceName() },
            { "internal_ip", Networking.Networking.InternalIp },
            { "internal_port", Config.InternalServerPort.ToString() },
            { "external_port", Config.ExternalServerPort.ToString() },
            { "version", Software.Version!.ToString() },
            { "platform", Info.Platform }
        };

        Logger.Register("Registering Server, this takes a moment...");
        
        GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "register", new FormUrlEncodedContent(serverData));

        object? data = JsonConvert.DeserializeObject(response);
        
        if (data == null) throw new("Failed to register Server");

        Logger.Register("Server registered successfully");

        // await AssignServer();
    }

    private static async Task AssignServer()
    {
        Dictionary<string, string> serverData = new()
        {
            { "id", Info.DeviceId.ToString() }
        };
        
        Logger.Register("Assigning Server, this takes a moment...");
        GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "assign", new FormUrlEncodedContent(serverData));

        ServerRegisterResponseData? data = response.FromJson<ServerRegisterResponse>()?.Data;

        if (data is null || data.Status == "error")
        {
            throw new("Failed to assign Server");
        }

        User user = new()
        {
            Id = data.User.Id,
            Name = data.User.Name,
            Email = data.User.Email,
            Owner = true,
            Allowed = true,
            Manage = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            AudioTranscoding = true,
            NoTranscoding = true,
            VideoTranscoding = true
        };

        await using MediaContext mediaContext = new();
        await mediaContext.Users.Upsert(user)
            .On(x => x.Id)
            .WhenMatched((oldUser, newUser) => new()
            {
                Id = newUser.Id,
                Name = newUser.Name,
                Email = newUser.Email,
                Owner = newUser.Owner,
                Allowed = newUser.Allowed,
                AudioTranscoding = newUser.AudioTranscoding,
                NoTranscoding = newUser.NoTranscoding,
                VideoTranscoding = newUser.VideoTranscoding,
                Manage = newUser.Manage,
                UpdatedAt = newUser.UpdatedAt,
            })
            .RunAsync();

        ClaimsPrincipleExtensions.AddUser(user);

        Logger.Register("Server assigned successfully");

#pragma warning disable CS0618 // Type or member is obsolete
        Certificate.RenewSslCertificate().Wait();
#pragma warning restore CS0618 // Type or member is obsolete

    }

    public static async Task GetTunnelAvailability()
    {
        Dictionary<string, string> serverData = new()
        {
            ["server_id"] = Info.DeviceId.ToString()
        };

        GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "tunnel", new FormUrlEncodedContent(serverData));
        
        ServerTunnelAvailabilityResponse? data = JsonConvert.DeserializeObject<ServerTunnelAvailabilityResponse>(response);

        if (data is null || !data.Allowed || data.Token is null) return;
        
        Config.CloudflareTunnelToken = data.Token;
        Logger.Register("Cloudflare tunnel is available", LogEventLevel.Verbose);

    }
}