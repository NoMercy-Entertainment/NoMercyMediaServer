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

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Networking.Connectivity;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;

namespace NoMercy.Networking.Discovery;

public class NetworkChangeMonitor : IHostedService, IDisposable
{
    private readonly INetworkDiscovery _networkDiscovery;
    private readonly IConnectivityManager _connectivityManager;
    private readonly IConnectivityStatus _connectivityStatus;
    private readonly SemaphoreSlim _reevaluationLock = new(initialCount: 1, maxCount: 1);

    private readonly IAuthTokenStore _authTokenStore;

    private readonly ILogger<NetworkChangeMonitor> _logger;

    public NetworkChangeMonitor(
        ILogger<NetworkChangeMonitor> logger,
        IAuthTokenStore authTokenStore,
        INetworkDiscovery networkDiscovery,
        IConnectivityManager connectivityManager,
        IConnectivityStatus connectivityStatus
    )
    {
        _logger = logger;
        _authTokenStore = authTokenStore;
        _networkDiscovery = networkDiscovery;
        _connectivityManager = connectivityManager;
        _connectivityStatus = connectivityStatus;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _logger.LogDebug(message: "Network change monitor started");
        return Task.CompletedTask;
    }

    // Internal (not private) so the OS NetworkChange event handlers — which
    // this class can only otherwise reach by actually triggering a real NIC
    // address/availability change on the test machine — are directly
    // unit-testable against fake INetworkDiscovery/IConnectivityManager
    // collaborators.
    internal async void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        if (!await _reevaluationLock.WaitAsync(millisecondsTimeout: 0))
            return;

        try
        {
            string oldIp = _networkDiscovery.InternalIp;
            // Force re-discovery by reading from interfaces
            string newIp = GetCurrentInternalIp();

            if (newIp == oldIp)
                return;

            _logger.LogInformation(message: "Network address changed: {OldIp} → {NewIp}", args: [oldIp, newIp]);
            _networkDiscovery.InternalIp = newIp;

            // Re-discover external IP (force past the one-shot completion gate)
            await _networkDiscovery.ForceRediscoveryAsync();

            // Re-evaluate connectivity strategies
            await _connectivityManager.EvaluateAsync(ct: CancellationToken.None);

            // Send update to NoMercy API
            await SendUpdate();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(message: "Network change handling failed: {Message}", args: ex.Message);
        }
        finally
        {
            _reevaluationLock.Release();
        }
    }

    internal async void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable)
            return;

        // Share the single-flight lock with OnNetworkAddressChanged: a NIC flap
        // raises both events, and two concurrent EvaluateAsync calls would race on
        // the ConnectivityManager's active strategy (tear down / double-establish
        // the tunnel or port-forward against each other).
        if (!await _reevaluationLock.WaitAsync(millisecondsTimeout: 0))
            return;

        try
        {
            await _networkDiscovery.ForceRediscoveryAsync();
            await _connectivityManager.EvaluateAsync(ct: CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                message: "Network availability change handling failed: {Message}",
                args: ex.Message
            );
        }
        finally
        {
            _reevaluationLock.Release();
        }
    }

    // Internal so the real socket-trick + NIC-enumeration resolution can be
    // asserted against this machine's actual network stack directly.
    internal static string GetCurrentInternalIp()
    {
        // UDP socket trick: OS picks the outbound source address — always the real LAN IP.
        try
        {
            using Socket socket = new(addressFamily: AddressFamily.InterNetwork, socketType: SocketType.Dgram, protocolType: 0);
            socket.Connect(host: "8.8.8.8", port: 65530);
            IPAddress? address = (socket.LocalEndPoint as IPEndPoint)?.Address;
            if (
                address is not null
                && !IPAddress.IsLoopback(address: address)
                && !address.Equals(comparand: IPAddress.Any)
                && address.AddressFamily == AddressFamily.InterNetwork
            )
            {
                string socketIp = address.ToString();
                if (socketIp != "0.0.0.0")
                    return socketIp;
            }
        }
        catch
        {
            // Fall through to NIC enumeration
        }

        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (
                    nic.NetworkInterfaceType
                    is NetworkInterfaceType.Loopback
                        or NetworkInterfaceType.Tunnel
                )
                    continue;

                foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (IPAddress.IsLoopback(address: addr.Address))
                        continue;
                    if (addr.Address.Equals(comparand: IPAddress.Any))
                        continue;

                    string nicIp = addr.Address.ToString();
                    if (nicIp == "0.0.0.0")
                        continue;

                    return nicIp;
                }
            }
        }
        catch
        {
            // Fall through
        }

        return "127.0.0.1";
    }

    // Internal so the no-auth-token early-out (the only branch reachable
    // without a live POST to the NoMercy API) is directly unit-testable.
    internal async Task SendUpdate()
    {
        try
        {
            Dictionary<string, string> serverData = new()
            {
                { "id", Info.DeviceId.ToString() },
                { "name", Info.DeviceName },
                { "internal_ip", _networkDiscovery.RegistrationInternalIp },
                { "internal_ipv6", _networkDiscovery.InternalIpV6.OrEmpty() },
                { "external_ipv6", _networkDiscovery.ExternalIpV6.OrEmpty() },
                { "internal_port", RuntimeServerSettings.Current.InternalServerPort.ToString() },
                { "external_port", RuntimeServerSettings.Current.ExternalServerPort.ToString() },
                { "version", Software.Version!.ToString() },
                { "platform", Info.Platform },
                { "stun_public_ip", _connectivityStatus.StunPublicIp.OrEmpty() },
                { "stun_public_port", (_connectivityStatus.StunPublicPort?.ToString()).OrEmpty() },
                { "stun_nat_type", _connectivityStatus.NatStatus.ToString() },
            };

            _logger.LogInformation(message: "Your IP address has changed, updating server information...");

            string? token = _authTokenStore.AccessToken;
            if (string.IsNullOrEmpty(value: token))
            {
                _logger.LogTrace(message: "Skipping network change ping — no auth token");
                return;
            }

            GenericHttpClient authClient = new(baseUrl: ExternalServicesConfig.Current.ApiServerBaseUrl);
            authClient.SetDefaultHeaders(userAgent: ExternalServicesConfig.Current.UserAgent, bearerToken: token);
            string response = await authClient.SendAndReadAsync(
                method: HttpMethod.Post,
                endpoint: "ping",
                content: new FormUrlEncodedContent(nameValueCollection: serverData)
            );

            object? data = JsonConvert.DeserializeObject(value: response);

            if (data == null)
                throw new(message: "Failed to update server information");

            _logger.LogInformation(message: "Server information updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(message: "Failed to send IP update: {Message}", args: ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _reevaluationLock.Dispose();
        GC.SuppressFinalize(obj: this);
    }
}
