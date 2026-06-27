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

public interface INetworkDiscovery
{
    string InternalIp { get; set; }

    /// <summary>
    /// The internal_ip value to send during registration and heartbeat: the discovered LAN IP
    /// when routable, otherwise the 0.0.0.0 sentinel the API accepts and resolves to the external
    /// domain. Never empty — an empty value fails the API's required|ip validation.
    /// </summary>
    string RegistrationInternalIp { get; }

    string ExternalIp { get; set; }
    string? InternalIpV6 { get; }
    string? ExternalIpV6 { get; set; }
    string InternalDomain { get; }
    string InternalAddress { get; }
    string ExternalDomain { get; }
    string ExternalAddress { get; }
    string? ExternalAddressV6 { get; }
    bool Ipv6Enabled { get; }
    Task DiscoverExternalIpAsync();

    /// <summary>
    /// Forces a fresh external-IP / NAT discovery after an OS network change,
    /// bypassing the one-shot completion gate (throttled internally).
    /// </summary>
    Task ForceRediscoveryAsync();
}
