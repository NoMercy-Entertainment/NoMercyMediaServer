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

namespace NoMercy.Networking.Discovery;

/// <summary>
/// Configurable network probe / local-IP discovery targets. Defaults match the
/// previously hardcoded values; override via the "NetworkProbe" configuration section.
/// </summary>
public sealed class NetworkProbeConfig
{
    /// <summary>Hosts probed (TCP:443) to decide whether the server has internet access.</summary>
    public string[] ProbeTargets { get; set; } = ["api.nomercy.tv", "1.1.1.1", "8.8.8.8"];

    /// <summary>Anycast IPv4 used purely to discover the outbound LAN interface (no traffic is sent).</summary>
    public string LocalIpDiscoveryIpv4 { get; set; } = "8.8.8.8";

    /// <summary>Anycast IPv6 used purely to discover the outbound LAN interface (no traffic is sent).</summary>
    public string LocalIpDiscoveryIpv6 { get; set; } = "2001:4860:4860::8888";

    /// <summary>Ephemeral port used for the discovery socket connect.</summary>
    public int LocalIpDiscoveryPort { get; set; } = 65530;
}
