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

using NoMercy.Networking.Discovery;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: CheckConnectivity must try every configured probe target and
/// return false only once every one of them has failed to accept a TCP
/// connection on port 443. Real (not mocked) TCP connect attempts prove the
/// "all unreachable" path. The "at least one target reachable" success path
/// always connects on the hardcoded port 443 with no seam to redirect it to
/// a test listener (binding port 443 needs elevated privileges on both
/// Windows and Linux), so it is itemized as requiring either a real internet
/// path or a source refactor — see the coverage report. ProbeTargets is
/// static, process-wide mutable state shared with production code, so it is
/// restored after every test.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NetworkProbeTests : IDisposable
{
    private readonly string[] _originalTargets = NetworkProbe.ProbeTargets;

    public void Dispose() => NetworkProbe.ProbeTargets = _originalTargets;

    [Fact]
    public async Task CheckConnectivity_AllTargetsUnreachable_ReturnsFalse()
    {
        // TEST-NET-1 addresses are reserved for documentation — guaranteed to
        // never accept a connection without a live host at that address.
        NetworkProbe.ProbeTargets = ["192.0.2.1", "192.0.2.2"];

        bool result = await NetworkProbe.CheckConnectivity(500);

        Assert.False(result);
    }

    [Fact]
    public void ProbeTargets_DefaultsIncludeApiCloudflareAndGoogle()
    {
        Assert.Contains("api.nomercy.tv", _originalTargets);
        Assert.Contains("1.1.1.1", _originalTargets);
        Assert.Contains("8.8.8.8", _originalTargets);
    }

    [Fact]
    public void ProbeTargets_CanBeOverridden()
    {
        NetworkProbe.ProbeTargets = ["custom.example.com"];

        Assert.Single(NetworkProbe.ProbeTargets);
        Assert.Equal("custom.example.com", NetworkProbe.ProbeTargets[0]);
    }
}
