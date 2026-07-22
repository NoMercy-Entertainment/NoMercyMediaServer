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

public class PluginConsentRecord
{
    public List<Guid> GrantedPluginIds { get; init; } = [];
}

// Backed by IPluginConfiguration under a platform-scoped data folder (not a
// per-plugin one) so the granted set survives process restarts and is shared
// across every plugin's consent check.
public class ConfigPluginConsentStore(IPluginConfiguration configuration) : IPluginConsentStore
{
    public bool Contains(Guid pluginId)
    {
        PluginConsentRecord? record = configuration.GetConfiguration<PluginConsentRecord>();
        return record?.GrantedPluginIds.Contains(item: pluginId) ?? false;
    }

    public void Add(Guid pluginId)
    {
        PluginConsentRecord record = configuration.GetConfiguration<PluginConsentRecord>() ?? new();

        if (record.GrantedPluginIds.Contains(item: pluginId))
            return;

        record.GrantedPluginIds.Add(item: pluginId);
        configuration.SaveConfiguration(configuration: record);
    }

    public void Remove(Guid pluginId)
    {
        PluginConsentRecord? record = configuration.GetConfiguration<PluginConsentRecord>();
        if (record is null || !record.GrantedPluginIds.Remove(item: pluginId))
            return;

        configuration.SaveConfiguration(configuration: record);
    }
}
