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

public interface IPluginConsentStore
{
    bool Contains(Guid pluginId);
    void Add(Guid pluginId);
    void Remove(Guid pluginId);
}

public class PluginConsentService(IPluginConsentStore store) : IPluginConsentService
{
    private static readonly HashSet<string> BaselineHooks = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        PluginHookCapability.MediaSource,
        PluginHookCapability.Metadata,
        PluginHookCapability.Ui,
    };

    public bool IsBaseline(PluginCapabilities? capabilities)
    {
        if (capabilities is null)
            return true;

        if (capabilities.Rest || capabilities.Ws || capabilities.Network is not null)
            return false;

        return capabilities.Hooks.All(predicate: hook => BaselineHooks.Contains(item: hook));
    }

    public bool HasConsent(Guid pluginId) => store.Contains(pluginId: pluginId);

    public void GrantConsent(Guid pluginId) => store.Add(pluginId: pluginId);

    public void RevokeConsent(Guid pluginId) => store.Remove(pluginId: pluginId);
}
