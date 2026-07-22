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
/// NetworkDiscovery re-subscribed the static Mono.Nat handlers on every
/// rediscovery, leaking handlers on the static NatUtility. ShouldSubscribeOnce is
/// the guard that ensures the subscription happens a single time.
/// </summary>
public class NetworkDiscoverySubscribeOnceTests
{
    [Fact]
    public void ShouldSubscribeOnce_ReturnsTrueOnFirstCallOnly()
    {
        bool subscribed = false;

        Assert.True(condition: NetworkDiscovery.ShouldSubscribeOnce(alreadySubscribed: ref subscribed));
        Assert.False(condition: NetworkDiscovery.ShouldSubscribeOnce(alreadySubscribed: ref subscribed));
        Assert.False(condition: NetworkDiscovery.ShouldSubscribeOnce(alreadySubscribed: ref subscribed));
    }

    [Fact]
    public void ShouldSubscribeOnce_AlreadySubscribed_ReturnsFalse()
    {
        bool subscribed = true;
        Assert.False(condition: NetworkDiscovery.ShouldSubscribeOnce(alreadySubscribed: ref subscribed));
    }
}
