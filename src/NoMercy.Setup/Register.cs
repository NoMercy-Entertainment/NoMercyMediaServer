using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Users;
using Serilog.Events;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;

namespace NoMercy.Setup;

public static class Register
{
    public static INetworkDiscovery? Discovery { get; set; }

    private static string GetDeviceName()
    {
        try
        {
            MediaContext mediaContext = new();
            Configuration? device = mediaContext.Configuration
                .FirstOrDefault(device => device.Key == "serverName");

            Info.DeviceName = device?.Value ?? Environment.MachineName;
        }
        catch (Exception)
        {
            // Table may not exist yet on first boot — fall back to machine name
            Info.DeviceName = Environment.MachineName;
        }

        return Info.DeviceName;
    }

    private static Dictionary<string, string> GetServerInfo()
    {
        Dictionary<string, string> serverData = new()
        {
            { "id", Info.DeviceId.ToString() },
            { "name", GetDeviceName() },
            { "internal_ip", Discovery?.InternalIp ?? "0.0.0.0" },
            { "internal_ipv6", Discovery?.InternalIpV6 ?? "" },
            { "external_ipv6", Discovery?.ExternalIpV6 ?? "" },
            { "internal_port", Config.InternalServerPort.ToString() },
            { "external_port", Config.ExternalServerPort.ToString() },
            { "version", Software.Version!.ToString() },
            { "platform", Info.Platform },
            { "stun_public_ip", Config.StunPublicIp ?? "" },
            { "stun_public_port", Config.StunPublicPort?.ToString() ?? "" },
            { "stun_nat_type", Config.NatStatus.ToString() }
        };

        return serverData;
    }

    private const int DefaultMaxRetries = 5;
    private static readonly int[] BackoffSeconds = [2, 5, 15, 30, 60];
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static DateTime _lastFailureUtc = DateTime.MinValue;
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(60);
    public static bool IsRegistered { get; private set; }

    public static async Task Init(int maxRetries = DefaultMaxRetries)
    {
        // Prevent rapid retries after a recent failure
        TimeSpan sinceLastFailure = DateTime.UtcNow - _lastFailureUtc;
        if (sinceLastFailure < FailureCooldown)
        {
            Logger.Register(
                $"Registration failed recently, cooling down for {(FailureCooldown - sinceLastFailure).TotalSeconds:F0}s");
            throw new InvalidOperationException("Registration on cooldown after recent failure");
        }

        if (!await InitLock.WaitAsync(0))
        {
            Logger.Register("Registration already in progress, skipping duplicate call");
            return;
        }

        try
        {
            await RegisterServer(maxRetries);
            await AssignServerWithRetry(maxRetries);
            await Certificate.RenewSslCertificate();
            IsRegistered = true;
        }
        catch
        {
            _lastFailureUtc = DateTime.UtcNow;
            throw;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static async Task RegisterServer(int maxRetries)
    {
        Logger.Register("Registering Server, this takes a moment...");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Dictionary<string, string> serverData = GetServerInfo();
                GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
                authClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
                string response = await authClient.SendAndReadAsync(
                    HttpMethod.Post, "register", new FormUrlEncodedContent(serverData));

                object? data = JsonConvert.DeserializeObject(response);
                if (data == null)
                    throw new InvalidOperationException("Failed to register Server — empty response");

                Logger.Register("Server registered successfully");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                int delay = BackoffSeconds[Math.Min(attempt - 1, BackoffSeconds.Length - 1)];
                Logger.Register(
                    $"Registration failed: {ex.Message}, retrying in {delay}s (attempt {attempt}/{maxRetries})",
                    LogEventLevel.Warning);
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }
        }
    }

    private static async Task AssignServerWithRetry(int maxRetries)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await AssignServer();
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                int delay = BackoffSeconds[Math.Min(attempt - 1, BackoffSeconds.Length - 1)];
                Logger.Register(
                    $"Server assignment failed: {ex.Message}, retrying in {delay}s (attempt {attempt}/{maxRetries})",
                    LogEventLevel.Warning);
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }
        }
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
        try
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
        catch (Exception ex)
        {
            Logger.Register($"Tunnel check: {ex.Message}", LogEventLevel.Debug);
        }
    }
}