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

    public async Task<ConnectivityResult> TryEstablishAsync(CancellationToken ct)
    {
        // Probe on every pass. A NatStatus of Open left over from an earlier evaluation
        // describes the network the server used to be on, and a re-evaluation happens
        // precisely because that network changed.
        connectivityStatus.PortForwarded = await networkDiscovery.IsPortOpenAsync();

        if (connectivityStatus.PortForwarded)
        {
            logger.LogInformation(
                "Reached the server on its own external address — port forwarding confirmed."
            );
            connectivityStatus.NatStatus = NatStatus.Open;
            return ConnectivityResult.Verified();
        }

        // A mapping the router accepted over UPnP is a claim, not proof. Routers routinely
        // accept the SOAP call and drop the mapping, and routers that DO forward correctly
        // often refuse to hairpin, which fails the probe above on a port that is genuinely
        // open to the outside. Those two cases are indistinguishable from in here, so this
        // counts only as a fallback: good enough to keep a working user working, never good
        // enough to outrank a transport that can prove itself.
        if (connectivityStatus.NatStatus == NatStatus.Filtered)
        {
            logger.LogInformation(
                "UPnP reports a port mapping but it could not be confirmed from inside the network — treating port forwarding as unverified."
            );
            connectivityStatus.PortForwarded = true;
            return ConnectivityResult.Assumed();
        }

        logger.LogDebug(
            "No port forward found — nothing answered on the external address and no UPnP mapping was made."
        );
        return ConnectivityResult.Failed();
    }

    public Task TeardownAsync()
    {
        // Nothing to stop, but the flag must not outlive the strategy: another transport
        // winning while PortForwarded is still true reports a direct path that is not there.
        connectivityStatus.PortForwarded = false;
        return Task.CompletedTask;
    }
}
