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
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.Status;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;

namespace NoMercy.Setup.Auth;

public class ServerRegistrationService : IServerRegistrationService
{
    private readonly IDbContextFactory<AppDbContext> _appDbContextFactory;
    private readonly IUserProvisioningService _userProvisioningService;
    private readonly IConnectivityStatus _connectivityStatus;
    private readonly INetworkDiscovery? _networkDiscovery;

    private static readonly int[] BackoffSeconds = [2, 5, 15, 30, 60];
    private readonly SemaphoreSlim _initLock = new(initialCount: 1, maxCount: 1);
    private Task? _inFlightInit;
    private DateTime _lastFailureUtc = DateTime.MinValue;
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(seconds: 60);

    private readonly IAuthTokenStore _authTokenStore;
    private readonly ICertificateService _certificateService;

    public ServerRegistrationService(
        IAuthTokenStore authTokenStore,
        IDbContextFactory<AppDbContext> appDbContextFactory,
        IUserProvisioningService userProvisioningService,
        IConnectivityStatus connectivityStatus,
        ICertificateService certificateService,
        INetworkDiscovery? networkDiscovery = null
    )
    {
        _authTokenStore = authTokenStore;
        _certificateService = certificateService;
        _appDbContextFactory = appDbContextFactory;
        _userProvisioningService = userProvisioningService;
        _connectivityStatus = connectivityStatus;
        _networkDiscovery = networkDiscovery;
    }

    /// <summary>
    /// Registers + assigns the server and renews its certificate. Concurrent
    /// callers share the SAME in-flight attempt instead of a "loser" returning
    /// early on a non-blocking lock: the loser used to advance to Registered
    /// (and check for a certificate) before the winner's attempt — including
    /// the certificate renewal — had actually finished, producing a spurious
    /// "certificate not acquired" downstream. A caller that joins an
    /// already-running attempt does not apply its own <paramref name="maxRetries"/>
    /// — only the value the FIRST caller passed seeds the shared attempt.
    /// </summary>
    public Task Init(int maxRetries = 5)
    {
        TimeSpan sinceLastFailure = DateTime.UtcNow - _lastFailureUtc;
        if (sinceLastFailure < FailureCooldown)
        {
            Logger.Register(
                message: $"Registration failed recently, cooling down for {(FailureCooldown - sinceLastFailure).TotalSeconds:F0}s"
            );
            throw new InvalidOperationException(message: "Registration on cooldown after recent failure");
        }

        _initLock.Wait();
        try
        {
            if (_inFlightInit is null || _inFlightInit.IsCompleted)
            {
                _inFlightInit = RunRegistrationAsync(maxRetries: maxRetries);
            }

            return _inFlightInit;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task RunRegistrationAsync(int maxRetries)
    {
        try
        {
            await RegisterServer(maxRetries: maxRetries);
            await AssignServerWithRetry(maxRetries: maxRetries);
            await _certificateService.RenewSslCertificate(accessToken: _authTokenStore.AccessToken);
        }
        catch
        {
            _lastFailureUtc = DateTime.UtcNow;
            throw;
        }
    }

    private async Task RegisterServer(int maxRetries)
    {
        Logger.Register(message: "Registering Server, this takes a moment...");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Dictionary<string, string> serverData = await GetServerInfo();
                GenericHttpClient authClient = new(baseUrl: ExternalServicesConfig.Current.ApiServerBaseUrl);
                authClient.SetDefaultHeaders(
                    userAgent: ExternalServicesConfig.Current.UserAgent,
                    bearerToken: _authTokenStore.AccessToken
                );
                string response = await authClient.SendAndReadAsync(
                    method: HttpMethod.Post,
                    endpoint: "register",
                    content: new FormUrlEncodedContent(nameValueCollection: serverData)
                );

                object? data = JsonConvert.DeserializeObject(value: response);
                if (data == null)
                    throw new InvalidOperationException(
                        message: "Failed to register Server — empty response"
                    );

                Logger.Register(message: "Server registered successfully");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                int delay = BackoffSeconds[Math.Min(val1: attempt - 1, val2: BackoffSeconds.Length - 1)];
                Logger.Register(
                    message: $"Registration failed: {ex.Message}, retrying in {delay}s (attempt {attempt}/{maxRetries})",
                    level: LogEventLevel.Warning
                );

                if (ex.Message.Contains(value: "401") || ex.Message.Contains(value: "Unauthorized"))
                    break;

                await Task.Delay(delay: TimeSpan.FromSeconds(seconds: delay));
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
                int delay = BackoffSeconds[Math.Min(val1: attempt - 1, val2: BackoffSeconds.Length - 1)];
                Logger.Register(
                    message: $"Server assignment failed: {ex.Message}, retrying in {delay}s (attempt {attempt}/{maxRetries})",
                    level: LogEventLevel.Warning
                );

                if (ex.Message.Contains(value: "401") || ex.Message.Contains(value: "Unauthorized"))
                    break;

                await Task.Delay(delay: TimeSpan.FromSeconds(seconds: delay));
            }
        }
    }

    private async Task AssignServer()
    {
        Dictionary<string, string> serverData = await GetServerInfo();

        Logger.Register(message: "Assigning Server, this takes a moment...");

        GenericHttpClient authClient = new(baseUrl: ExternalServicesConfig.Current.ApiServerBaseUrl);
        authClient.SetDefaultHeaders(
            userAgent: ExternalServicesConfig.Current.UserAgent,
            bearerToken: _authTokenStore.AccessToken
        );

        string response = await authClient.SendAndReadAsync(
            method: HttpMethod.Post,
            endpoint: "assign",
            content: new FormUrlEncodedContent(nameValueCollection: serverData)
        );

        ServerRegisterResponse? data = response.FromJson<ServerRegisterResponse>();

        if (data?.Data is null || data.Data.Status == "error")
            throw new(message: "Failed to assign Server");

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

        await _userProvisioningService.ProvisionOwner(user: user);

        Logger.Register(message: "Server assigned successfully");
    }

    public async Task GetTunnelAvailability()
    {
        try
        {
            Dictionary<string, string> serverData = await GetServerInfo();

            GenericHttpClient authClient = new(baseUrl: ExternalServicesConfig.Current.ApiServerBaseUrl);
            authClient.SetDefaultHeaders(
                userAgent: ExternalServicesConfig.Current.UserAgent,
                bearerToken: _authTokenStore.AccessToken
            );

            string response = await authClient.SendAndReadAsync(
                method: HttpMethod.Post,
                endpoint: "tunnel",
                content: new FormUrlEncodedContent(nameValueCollection: serverData)
            );

            ServerTunnelAvailabilityResponse? data =
                response.FromJson<ServerTunnelAvailabilityResponse>();

            if (data is null || !data.Allowed || data.Token is null)
                return;

            _connectivityStatus.CloudflareTunnelToken = data.Token;

            Logger.Register(message: "Cloudflare tunnel is available", level: LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            Logger.Register(message: $"Tunnel check: {ex.Message}", level: LogEventLevel.Debug);
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
            { "internal_port", RuntimeServerSettings.Current.InternalServerPort.ToString() },
            { "external_port", RuntimeServerSettings.Current.ExternalServerPort.ToString() },
            { "version", Software.Version!.ToString() },
            { "platform", Info.Platform },
            { "stun_public_ip", _connectivityStatus.StunPublicIp.OrEmpty() },
            { "stun_public_port", (_connectivityStatus.StunPublicPort?.ToString()).OrEmpty() },
            { "stun_nat_type", _connectivityStatus.NatStatus.ToString() },
            { "dns_scheme", RuntimeServerSettings.Current.UseSynthesizedDns ? "srv" : "apex" },
        };
    }

    private async Task<string> GetDeviceName()
    {
        try
        {
            await using AppDbContext appContext = await _appDbContextFactory.CreateDbContextAsync();
            Configuration? device = await appContext.Configuration.FirstOrDefaultAsync(predicate: device =>
                device.Key == "serverName"
            );

            Info.DeviceName = device?.Value ?? Environment.MachineName;
        }
        catch (Exception)
        {
            Info.DeviceName = Environment.MachineName;
        }

        return Info.DeviceName;
    }
}
