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
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Status;
using NoMercy.Storage.Drivers.Local;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: IsPortOpenAsync is the port-forward decision primitive —
/// PortForwardStrategy trusts its result to decide whether the server is
/// reachable from outside the LAN. It must return true against a real
/// listening endpoint, false against a closed port, and false (not throw) on
/// a connection timeout. These use real loopback TCP sockets — no mock of the
/// method under test.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class NetworkDiscoveryPortCheckTests
{
    private static NetworkDiscovery BuildDiscovery(string externalIp, int externalPort)
    {
        NetworkDiscovery discovery = new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
        );
        discovery.ExternalIp = externalIp;
        RuntimeServerSettings.Current.ExternalServerPort = externalPort;
        return discovery;
    }

    [Fact]
    public async Task IsPortOpenAsync_AgainstRealListener_ReturnsTrue()
    {
        TcpListener listener = new(localaddr: IPAddress.Loopback, port: 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        int originalPort = RuntimeServerSettings.Current.ExternalServerPort;

        try
        {
            _ = Task.Run(function: async () =>
            {
                using TcpClient accepted = await listener.AcceptTcpClientAsync();
            });

            NetworkDiscovery discovery = BuildDiscovery(externalIp: "127.0.0.1", externalPort: port);

            bool result = await discovery.IsPortOpenAsync();

            Assert.True(condition: result);
        }
        finally
        {
            listener.Stop();
            RuntimeServerSettings.Current.ExternalServerPort = originalPort;
        }
    }

    [Fact]
    public async Task IsPortOpenAsync_AgainstClosedPort_ReturnsFalse()
    {
        // Bind, note the free port, then close it immediately — nothing is
        // listening there, so the connect attempt gets a real RST/refused.
        TcpListener listener = new(localaddr: IPAddress.Loopback, port: 0);
        listener.Start();
        int freePort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        int originalPort = RuntimeServerSettings.Current.ExternalServerPort;

        try
        {
            NetworkDiscovery discovery = BuildDiscovery(externalIp: "127.0.0.1", externalPort: freePort);

            bool result = await discovery.IsPortOpenAsync();

            Assert.False(condition: result);
        }
        finally
        {
            RuntimeServerSettings.Current.ExternalServerPort = originalPort;
        }
    }

    [Fact]
    public async Task IsPortOpenAsync_AgainstNonRoutableAddress_TimesOutAndReturnsFalse()
    {
        // TEST-NET-1 (RFC 5737, 192.0.2.0/24) is reserved for documentation —
        // guaranteed unreachable without hitting a live host, exercising the
        // OperationCanceledException timeout branch deterministically.
        int originalPort = RuntimeServerSettings.Current.ExternalServerPort;

        try
        {
            NetworkDiscovery discovery = BuildDiscovery(externalIp: "192.0.2.1", externalPort: 9);

            bool result = await discovery.IsPortOpenAsync();

            Assert.False(condition: result);
        }
        finally
        {
            RuntimeServerSettings.Current.ExternalServerPort = originalPort;
        }
    }
}
