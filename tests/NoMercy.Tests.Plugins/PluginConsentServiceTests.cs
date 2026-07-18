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

using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Capabilities;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginConsentServiceTests
{
    private static PluginCapabilities Caps(params string[] hooks) => new() { Hooks = [.. hooks] };

    [Fact]
    public void IsBaseline_NullOrMediaUiOnly_IsBaseline()
    {
        PluginConsentService service = new(new InMemoryConsentStore());
        Assert.True(service.IsBaseline(null));
        Assert.True(service.IsBaseline(Caps("mediaSource", "ui")));
        Assert.True(service.IsBaseline(Caps("metadata")));
    }

    [Fact]
    public void IsBaseline_AuthOrNetworkOrRest_IsElevated()
    {
        PluginConsentService service = new(new InMemoryConsentStore());
        Assert.False(service.IsBaseline(Caps("auth")));
        Assert.False(service.IsBaseline(new() { Hooks = ["ui"], Rest = true }));
        Assert.False(
            service.IsBaseline(
                new()
                {
                    Hooks = ["ui"],
                    Network = new() { Hosts = ["x"] },
                }
            )
        );
    }

    [Fact]
    public void Consent_RoundTrips()
    {
        PluginConsentService service = new(new InMemoryConsentStore());
        Guid id = Guid.NewGuid();
        Assert.False(service.HasConsent(id));
        service.GrantConsent(id);
        Assert.True(service.HasConsent(id));
    }
}
