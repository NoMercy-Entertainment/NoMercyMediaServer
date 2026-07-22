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

namespace NoMercy.Plugins.Capabilities;

public static class PluginCapabilityGuard
{
    private static readonly HashSet<string> ImplicitBaseline = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        PluginHookCapability.MediaSource,
        PluginHookCapability.Metadata,
        PluginHookCapability.Ui,
    };

    public static bool DeclaresHook(PluginCapabilities? capabilities, string hook)
    {
        if (capabilities is null)
            return ImplicitBaseline.Contains(item: hook);

        return capabilities.Hooks.Contains(value: hook, comparer: StringComparer.OrdinalIgnoreCase);
    }
}
