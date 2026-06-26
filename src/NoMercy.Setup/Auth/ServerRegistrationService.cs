// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.NmSystem.Auth;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;

namespace NoMercy.Setup.Auth;

public class ServerRegistrationService : IServerRegistrationService
{
    private readonly IDbContextFactory<AppDbContext> _appDbContextFactory;
    private readonly IUserProvisioningService _userProvisioningService;
    private readonly INetworkDiscovery? _networkDiscovery;
    
    private static readonly int[] BackoffSeconds = [2, 5, 15, 30, 60];
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private DateTime _lastFailureUtc = DateTime.MinValue;
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(60);

    private readonly IAuthTokenStore _authTokenStore;

    public ServerRegistrationService(
        IAuthTokenStore authTokenStore,
        IDbContextFactory<AppDbContext> appDbContextFactory,
        IUserProvisioningService userProvisioningService,
        INetworkDiscovery? networkDiscovery = null
    )
    {
        _authTokenStore = authTokenStore;
        _appDbContextFactory = appDbContextFactory;
        _userProvisioningService = userProvisioningService;
        _networkDiscovery = networkDiscovery;
    }

    public async Task Init(int maxRetries = 5)
    {
        TimeSpan sinceLastFailure = DateTime.UtcNow - _lastFailureUtc;
        if (sinceLastFailure < FailureCooldown)
        {
            Logger.Register($"Registration failed recently, cooling down for {(FailureCooldown - sinceLastFailure).TotalSeconds:F0}s");
            throw new InvalidOperationException("Registration on cooldown after recent failure");
        }

        if (!await _initLock.WaitAsync(0))
        {
            Logger.Register("Registration already in progress, skipping duplicate call");
            return;
        }

        try
        {
            await RegisterServer(maxRetries);
            await AssignServerWithRetry(maxRetries);
            await Certificate.RenewSslCertificate(_authTokenStore.AccessToken);
        }
        catch
        {
            _lastFailureUtc = DateTime.UtcNow;
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task RegisterServer(int maxRetries)
    {
        Logger.Register("Registering Server, this takes a moment...");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Dictionary<string, string> serverData = await GetServerInfo();
                GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
                authClient.SetDefaultHeaders(Config.UserAgent, _authTokenStore.AccessToken);
                string response = await authClient.SendAndReadAsync(
                    HttpMethod.Post,
                    "register",
                    new FormUrlEncodedContent(serverData)
                );

                object? data = JsonConvert.DeserializeObject(response);
                if (data == null)
                    throw new InvalidOperationException("Failed to register Server — empty response");

                Logger.Register("Server registered successfully");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                int delay = BackoffSeconds[Math.Min(attempt - 1, BackoffSeconds.Length - 1)];
                Logger.Register($"Registration failed: {ex.Message}, retrying in {delay}s (attempt {attempt}/{maxRetries})", LogEventLevel.Warning);

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                    break;

                await Task.Delay(TimeSpan.FromSeconds(delay));
            }
        }
    }

    private async Task AssignServerWithRetry(int maxRetries)
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
                Logger.Register($"Server assignment failed: {ex.Message}, retrying in {delay}s (attempt {attempt}/{maxRetries})", LogEventLevel.Warning);

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                    break;

                await Task.Delay(TimeSpan.FromSeconds(delay));
            }
        }
    }

    private async Task AssignServer()
    {
        Dictionary<string, string> serverData = await GetServerInfo();

        Logger.Register("Assigning Server, this takes a moment...");

        GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent, _authTokenStore.AccessToken);

        string response = await authClient.SendAndReadAsync(
            HttpMethod.Post,
            "assign",
            new FormUrlEncodedContent(serverData)
        );

        ServerRegisterResponse? data = response.FromJson<ServerRegisterResponse>();

        if (data?.Data is null || data.Data.Status == "error")
            throw new("Failed to assign Server");

        User user = new()
        {
            Id = data.Data.User.Id,
            Name = data.Data.User.Name,
            Email = data.Data.User.Email,
            Owner = true,
            Allowed = true,
            Manage = true,
            CreatedAt = DateTime.UtcNow,
            AudioTranscoding = true,
            NoTranscoding = true,
            VideoTranscoding = true,
        };

        await _userProvisioningService.ProvisionOwner(user);

        Logger.Register("Server assigned successfully");
    }

    public async Task GetTunnelAvailability()
    {
        try
        {
            Dictionary<string, string> serverData = await GetServerInfo();

            GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
            authClient.SetDefaultHeaders(Config.UserAgent, _authTokenStore.AccessToken);

            string response = await authClient.SendAndReadAsync(
                HttpMethod.Post,
                "tunnel",
                new FormUrlEncodedContent(serverData)
            );

            ServerTunnelAvailabilityResponse? data = response.FromJson<ServerTunnelAvailabilityResponse>();

            if (data is null || !data.Allowed || data.Token is null)
                return;

            Config.CloudflareTunnelToken = data.Token;

            Logger.Register("Cloudflare tunnel is available", LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            Logger.Register($"Tunnel check: {ex.Message}", LogEventLevel.Debug);
        }
    }

    private async Task<Dictionary<string, string>> GetServerInfo()
    {
        return new()
        {
            { "id", Info.DeviceId.ToString() },
            { "name", await GetDeviceName() },
            { "internal_ip", _networkDiscovery?.RegistrationInternalIp ?? "0.0.0.0" },
            { "internal_ipv6", (_networkDiscovery?.InternalIpV6).OrEmpty() },
            { "external_ipv6", (_networkDiscovery?.ExternalIpV6).OrEmpty() },
            { "internal_port", Config.InternalServerPort.ToString() },
            { "external_port", Config.ExternalServerPort.ToString() },
            { "version", Software.Version!.ToString() },
            { "platform", Info.Platform },
            { "stun_public_ip", Config.StunPublicIp.OrEmpty() },
            { "stun_public_port", (Config.StunPublicPort?.ToString()).OrEmpty() },
            { "stun_nat_type", Config.NatStatus.ToString() },
        };
    }

    private async Task<string> GetDeviceName()
    {
        try
        {
            await using AppDbContext appContext = await _appDbContextFactory.CreateDbContextAsync();
            Configuration? device = await appContext.Configuration.FirstOrDefaultAsync(device => device.Key == "serverName");

            Info.DeviceName = device?.Value ?? Environment.MachineName;
        }
        catch (Exception)
        {
            Info.DeviceName = Environment.MachineName;
        }

        return Info.DeviceName;
    }
}
