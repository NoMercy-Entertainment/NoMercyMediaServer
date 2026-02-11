using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using Serilog.Events;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;

namespace NoMercy.Setup;

public static class Register
{
    private static string GetDeviceName()
    {
        MediaContext mediaContext = new();
        Configuration? device = mediaContext.Configuration
            .FirstOrDefault(device => device.Key == "serverName");
        
        Info.DeviceName = device?.Value ?? Environment.MachineName;
        
        return Info.DeviceName;
    }

    private static Dictionary<string, string> GetServerInfo()
    {
        Dictionary<string, string> serverData = new()
        {
            { "id", Info.DeviceId.ToString() },
            { "name", GetDeviceName() },
            { "internal_ip", Networking.Networking.InternalIp },
            { "internal_port", Config.InternalServerPort.ToString() },
            { "external_port", Config.ExternalServerPort.ToString() },
            { "version", Software.Version!.ToString() },
            { "platform", Info.Platform }
        };

        return serverData;
    }

    public static async Task Init()
    {
        Dictionary<string, string> serverData = GetServerInfo();
        
        Logger.Register("Registering Server, this takes a moment...");

        GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
        string response =
            await authClient.SendAndReadAsync(HttpMethod.Post, "register", new FormUrlEncodedContent(serverData));

        object? data = JsonConvert.DeserializeObject(response);

        if (data == null) throw new("Failed to register Server");

        Logger.Register("Server registered successfully");

        await AssignServer();
        
        await Certificate.RenewSslCertificate();
    }

    private static async Task AssignServer()
    {
        Dictionary<string, string> serverData = GetServerInfo();

        Logger.Register("Assigning Server, this takes a moment...");
        
        GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
        
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "assign", new FormUrlEncodedContent(serverData));

        ServerRegisterResponse? data = response.FromJson<ServerRegisterResponse>();

        if (data?.Data is null || data.Data.Status == "error") throw new("Failed to assign Server");

        User user = new()
        {
            Id = data.Data.User.Id,
            Name = data.Data.User.Name,
            Email = data.Data.User.Email,
            Owner = true,
            Allowed = true,
            Manage = true,
            CreatedAt = DateTime.Now,
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
                Manage = newUser.Manage
            })
            .RunAsync();

        ClaimsPrincipleExtensions.AddUser(user);

        Logger.Register("Server assigned successfully");
    }

    public static async Task GetTunnelAvailability()
    {
        Dictionary<string, string> serverData = GetServerInfo();

        GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
        
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "tunnel", new FormUrlEncodedContent(serverData));

        ServerTunnelAvailabilityResponse? data = response.FromJson<ServerTunnelAvailabilityResponse>();

        if (data is null || !data.Allowed || data.Token is null) return;

        Config.CloudflareTunnelToken = data.Token;
        
        Logger.Register("Cloudflare tunnel is available", LogEventLevel.Verbose);
    }
}