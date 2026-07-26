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
    Task<ConnectivityResult> TryEstablishAsync(CancellationToken ct);
    Task TeardownAsync();
    ConnectivityType Type { get; }

    /// <summary>
    /// Whether the transport this strategy established is still carrying traffic. Defaults
    /// to true for strategies that hold no process or socket of their own; a strategy that
    /// does owns the answer. Without it a transport could die minutes after being chosen and
    /// the server would keep advertising it for the rest of its uptime.
    /// </summary>
    bool IsStillEstablished => true;
}

/// <summary>
/// How much a strategy actually knows about the connectivity it reports. The distinction
/// exists because "I started" and "a client can reach us" are different claims, and
/// treating the first as the second is what let an unverifiable port forward outrank a
/// working tunnel.
/// </summary>
public enum ConnectivityConfidence
{
    None,

    /// <summary>
    /// The transport reported success, but nothing confirmed that a client outside the
    /// network can reach the published address. Usable as a last resort, never as a reason
    /// to stop looking for something better.
    /// </summary>
    Assumed,

    /// <summary>
    /// A connection over this transport actually completed.
    /// </summary>
    Verified,
}

public readonly record struct ConnectivityResult(
    bool Established,
    ConnectivityConfidence Confidence
)
{
    public static ConnectivityResult Failed() => new(false, ConnectivityConfidence.None);

    public static ConnectivityResult Verified() => new(true, ConnectivityConfidence.Verified);

    public static ConnectivityResult Assumed() => new(true, ConnectivityConfidence.Assumed);
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
