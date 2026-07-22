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
        PluginConsentService service = new(store: new InMemoryConsentStore());
        Assert.True(condition: service.IsBaseline(capabilities: null));
        Assert.True(condition: service.IsBaseline(capabilities: Caps(hooks: ["mediaSource", "ui"])));
        Assert.True(condition: service.IsBaseline(capabilities: Caps(hooks: "metadata")));
    }

    [Fact]
    public void IsBaseline_AuthOrNetworkOrRest_IsElevated()
    {
        PluginConsentService service = new(store: new InMemoryConsentStore());
        Assert.False(condition: service.IsBaseline(capabilities: Caps(hooks: "auth")));
        Assert.False(condition: service.IsBaseline(capabilities: new() { Hooks = ["ui"], Rest = true }));
        Assert.False(
            condition: service.IsBaseline(
                capabilities: new()
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
        PluginConsentService service = new(store: new InMemoryConsentStore());
        Guid id = Guid.NewGuid();
        Assert.False(condition: service.HasConsent(pluginId: id));
        service.GrantConsent(pluginId: id);
        Assert.True(condition: service.HasConsent(pluginId: id));
    }

    [Fact]
    public void RevokeConsent_RemovesGrantedConsent()
    {
        PluginConsentService service = new(store: new InMemoryConsentStore());
        Guid id = Guid.NewGuid();
        service.GrantConsent(pluginId: id);
        Assert.True(condition: service.HasConsent(pluginId: id));

        service.RevokeConsent(pluginId: id);

        Assert.False(condition: service.HasConsent(pluginId: id));
    }
}
