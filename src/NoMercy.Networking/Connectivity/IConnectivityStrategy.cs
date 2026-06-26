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

namespace NoMercy.Networking.Connectivity;

public interface IConnectivityStrategy
{
    string Name { get; }
    int Priority { get; }
    Task<bool> TryEstablishAsync(CancellationToken ct);
    Task TeardownAsync();
    ConnectivityType Type { get; }
}

public enum ConnectivityType
{
    DirectLan,
    PortForward,
    StunHolePunch,
    CloudflareTunnel,
    LocalOnly,
}

public enum ConnectivityState
{
    Starting,
    Evaluating,
    DirectAccess,
    HolePunched,
    Tunneled,
    LocalOnly,
}
