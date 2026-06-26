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
using Newtonsoft.Json;
using NoMercy.Networking.Connectivity;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Networking.Discovery;

public class NetworkChangeMonitor : IHostedService, IDisposable
{
    private readonly INetworkDiscovery _networkDiscovery;
    private readonly IConnectivityManager _connectivityManager;
    private readonly SemaphoreSlim _reevaluationLock = new(1, 1);

    public NetworkChangeMonitor(
        INetworkDiscovery networkDiscovery,
        IConnectivityManager connectivityManager
    )
    {
        _networkDiscovery = networkDiscovery;
        _connectivityManager = connectivityManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        Logger.Setup("Network change monitor started", LogEventLevel.Debug);
        return Task.CompletedTask;
    }

    private async void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        if (!await _reevaluationLock.WaitAsync(0))
            return;

        try
        {
            string oldIp = _networkDiscovery.InternalIp;
            // Force re-discovery by reading from interfaces
            string newIp = GetCurrentInternalIp();

            if (newIp == oldIp)
                return;

            Logger.Setup($"Network address changed: {oldIp} → {newIp}");
            _networkDiscovery.InternalIp = newIp;

            // Re-discover external IP
            await _networkDiscovery.DiscoverExternalIpAsync();

            // Re-evaluate connectivity strategies
            await _connectivityManager.EvaluateAsync(CancellationToken.None);

            // Send update to NoMercy API
            await SendUpdate();
        }
        catch (Exception ex)
        {
            Logger.Setup($"Network change handling failed: {ex.Message}", LogEventLevel.Warning);
        }
        finally
        {
            _reevaluationLock.Release();
        }
    }

    private static string GetCurrentInternalIp()
    {
        // UDP socket trick: OS picks the outbound source address — always the real LAN IP.
        try
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            IPAddress? address = (socket.LocalEndPoint as IPEndPoint)?.Address;
            if (
                address is not null
                && !IPAddress.IsLoopback(address)
                && !address.Equals(IPAddress.Any)
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
                    if (IPAddress.IsLoopback(addr.Address))
                        continue;
                    if (addr.Address.Equals(IPAddress.Any))
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

    private async Task SendUpdate()
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
                { "internal_port", Config.InternalServerPort.ToString() },
                { "external_port", Config.ExternalServerPort.ToString() },
                { "version", Software.Version!.ToString() },
                { "platform", Info.Platform },
                { "stun_public_ip", Config.StunPublicIp.OrEmpty() },
                { "stun_public_port", (Config.StunPublicPort?.ToString()).OrEmpty() },
                { "stun_nat_type", Config.NatStatus.ToString() },
            };

            Logger.Register("Your IP address has changed, updating server information...");

            string? token = Globals.Globals.AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                Logger.Setup("Skipping network change ping — no auth token", LogEventLevel.Verbose);
                return;
            }

            GenericHttpClient authClient = new(Config.ApiServerBaseUrl);
            authClient.SetDefaultHeaders(Config.UserAgent, token);
            string response = await authClient.SendAndReadAsync(
                HttpMethod.Post,
                "ping",
                new FormUrlEncodedContent(serverData)
            );

            object? data = JsonConvert.DeserializeObject(response);

            if (data == null)
                throw new("Failed to update server information");

            Logger.Register("Server information updated successfully");
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to send IP update: {ex.Message}", LogEventLevel.Warning);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _reevaluationLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
