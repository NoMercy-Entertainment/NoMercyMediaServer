using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Networking;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Server.app.Helper;

public static class Register
{
    public static string DeviceName()
    {
        MediaContext mediaContext = new();
        Configuration? device = mediaContext.Configuration.FirstOrDefault(device => device.Key == "serverName");
        return device?.Value ?? Environment.MachineName;
    }

    public static Task Init()
    {
        Dictionary<string, string> serverData = new()
        {
            { "server_id", Info.DeviceId.ToString() },
            { "server_name", DeviceName() },
            { "internal_ip", Networking.Networking.InternalIp },
            { "internal_port", Config.InternalServerPort.ToString() },
            { "external_port", Config.ExternalServerPort.ToString() },
            { "server_version", ApiInfo.ApplicationVersion },
            { "platform", Info.Platform }
        };

        Logger.Register("Registering Server, this takes a moment...");

        HttpClient client = new();
        client.BaseAddress = new(Config.ApiServerBaseUrl);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        string content = client.PostAsync("register",
                new FormUrlEncodedContent(serverData))
            .Result.Content.ReadAsStringAsync().Result;

        object? data = JsonConvert.DeserializeObject(content);
        
        if (data == null) throw new Exception("Failed to register Server");

        Logger.Register("Server registered successfully");

        AssignServer().Wait();

        return Task.CompletedTask;
    }

    private static Task AssignServer()
    {
        Dictionary<string, string> serverData = new()
        {
            { "server_id", Info.DeviceId.ToString() }
        };

        HttpClient client = new();
        client.BaseAddress = new Uri(Config.ApiServerBaseUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.AccessToken);
        
        string? content = client
            .PostAsync("assign", new FormUrlEncodedContent(serverData))
            .Result.Content.ReadAsStringAsync().Result;

        Logger.Register(serverData, LogEventLevel.Verbose);
        
        ServerRegisterResponse? data = JsonConvert.DeserializeObject<ServerRegisterResponse>(content);

        if (data is null || data.Status == "error")
        {
            Logger.Register(data, LogEventLevel.Error);
            throw new Exception("Failed to assign Server");
        }

        User newUser = new()
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
        mediaContext.Users.Upsert(newUser)
            .On(x => x.Id)
            .WhenMatched((oldUser, newUser) => new User
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

        ClaimsPrincipleExtensions.AddUser(newUser);

        Logger.Register("Server assigned successfully");

        Certificate.RenewSslCertificate().Wait();

        return Task.CompletedTask;
    }
}