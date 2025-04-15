using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using Serilog.Events;
using NoMercy.Helpers;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using HttpClient = NoMercy.NmSystem.Extensions.HttpClient;

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
            { "server_id", Info.DeviceId.ToString() },
            { "server_name", DeviceName() },
            { "internal_ip", Networking.Networking.InternalIp },
            { "internal_port", Config.InternalServerPort.ToString() },
            { "external_port", Config.ExternalServerPort.ToString() },
            { "server_version", Software.Version!.ToString() },
            { "platform", Info.Platform }
        };

        Logger.Register("Registering Server, this takes a moment...");
        
        System.Net.Http.HttpClient client = HttpClient.WithDns();
        client.BaseAddress = new(Config.ApiServerBaseUrl);
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Globals.Globals.AccessToken);

        string content = client.PostAsync("register",
                new FormUrlEncodedContent(serverData))
            .Result.Content.ReadAsStringAsync().Result;

        object? data = JsonConvert.DeserializeObject(content);
        
        if (data == null) throw new("Failed to register Server");

        Logger.Register("Server registered successfully");

        await AssignServer();
    }

    private static Task AssignServer()
    {
        Dictionary<string, string> serverData = new()
        {
            { "server_id", Info.DeviceId.ToString() }
        };

        
        System.Net.Http.HttpClient client = HttpClient.WithDns();
        client.BaseAddress = new(Config.ApiServerBaseUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Globals.Globals.AccessToken);
        
        string content = client
            .PostAsync("assign", new FormUrlEncodedContent(serverData))
            .Result.Content.ReadAsStringAsync().Result;
        
        ServerRegisterResponse? data = JsonConvert.DeserializeObject<ServerRegisterResponse>(content);

        if (data is null || data.Status == "error")
        {
            throw new("Failed to assign Server");
        }
        
        Logger.Register(data, LogEventLevel.Verbose);

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

        using MediaContext mediaContext = new();
        mediaContext.Users.Upsert(user)
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
            .Run();

        ClaimsPrincipleExtensions.AddUser(user);

        Logger.Register("Server assigned successfully");

#pragma warning disable CS0618 // Type or member is obsolete
        Certificate.RenewSslCertificate().Wait();
#pragma warning restore CS0618 // Type or member is obsolete

        return Task.CompletedTask;
    }

    public static Task GetTunnelAvailability()
    {
        System.Net.Http.HttpClient client = HttpClient.WithDns();
        client.BaseAddress = new(Config.ApiServerBaseUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Globals.Globals.AccessToken);
        
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, "tunnel")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["server_id"] = Info.DeviceId.ToString()
            })
        };

        string response = client
            .SendAsync(httpRequestMessage)
            .Result.Content.ReadAsStringAsync().Result;

        ServerTunnelAvailabilityResponse? data = JsonConvert.DeserializeObject<ServerTunnelAvailabilityResponse>(response);

        if (data is null || !data.Allowed) return Task.CompletedTask;
        
        Config.CloudflareTunnelToken = data.Token;
        Logger.Register("Cloudflare tunnel is available", LogEventLevel.Verbose);

        return Task.CompletedTask;
        
    }
}