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

using Microsoft.Extensions.Logging;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Status;

namespace NoMercy.Networking.Connectivity.Strategies;

public class PortForwardStrategy(
    INetworkDiscovery networkDiscovery,
    IConnectivityStatus connectivityStatus,
    ILogger<PortForwardStrategy> logger
) : IConnectivityStrategy
{
    public string Name => "PortForward";
    public int Priority => 1;
    public ConnectivityType Type => ConnectivityType.PortForward;

    public async Task<bool> TryEstablishAsync(CancellationToken ct)
    {
        if (connectivityStatus.NatStatus == NatStatus.Open)
        {
            logger.LogInformation(
                "NAT status is open, you can access your server from outside your local network."
            );
            return true;
        }

        // UPnP port mapping succeeded — trust it even if the self-connect test fails.
        // Many routers don't support NAT hairpinning (connecting to your own external IP
        // from inside the LAN), so the TCP test would fail despite the port being open
        // to external clients.
        if (connectivityStatus.NatStatus == NatStatus.Filtered)
        {
            logger.LogInformation("UPnP port mapping active — port forwarding confirmed via UPnP.");
            connectivityStatus.PortForwarded = true;
            connectivityStatus.NatStatus = NatStatus.Open;
            return true;
        }

        // No UPnP — try direct TCP connect to external IP as a last resort
        connectivityStatus.PortForwarded = await networkDiscovery.IsPortOpenAsync();
        if (connectivityStatus.PortForwarded)
        {
            logger.LogInformation(
                "Your server is port forwarded, you can access your server from outside your local network."
            );
            connectivityStatus.NatStatus = NatStatus.Open;
            return true;
        }

        logger.LogDebug(
            "Port forward check failed — router may not support NAT hairpinning, but external clients may still be able to connect."
        );
        return false;
    }

    public Task TeardownAsync()
    {
        return Task.CompletedTask;
    }
}
