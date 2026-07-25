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

public class PluginCapabilityGuardTests
{
    [Fact]
    public void NullCaps_ImplicitlyDeclaresBaselineHooksOnly()
    {
        Assert.True(PluginCapabilityGuard.DeclaresHook(null, PluginHookCapability.MediaSource));
        Assert.True(PluginCapabilityGuard.DeclaresHook(null, PluginHookCapability.Metadata));
        Assert.False(PluginCapabilityGuard.DeclaresHook(null, PluginHookCapability.Auth));
        Assert.False(PluginCapabilityGuard.DeclaresHook(null, PluginHookCapability.ScheduledTask));
    }

    [Fact]
    public void ExplicitCaps_OnlyDeclaredHooksAllowed()
    {
        PluginCapabilities caps = new() { Hooks = ["auth"] };
        Assert.True(PluginCapabilityGuard.DeclaresHook(caps, PluginHookCapability.Auth));
        Assert.False(PluginCapabilityGuard.DeclaresHook(caps, PluginHookCapability.MediaSource));
    }
}
