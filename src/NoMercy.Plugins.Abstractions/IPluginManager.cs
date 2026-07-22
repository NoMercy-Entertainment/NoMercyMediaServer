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

namespace NoMercy.Plugins.Abstractions;

public interface IPluginManager
{
    IReadOnlyList<PluginInfo> GetInstalledPlugins();
    Task InstallPluginAsync(string packageUrl, CancellationToken ct = default);

    // Repository-backed install: verifies the supplied checksum before anything
    // is copied to disk. Default forwards to the checksum-less overload so
    // existing implementers (test doubles) keep compiling unchanged.
    Task InstallPluginAsync(
        string packageUrl,
        string? expectedChecksum,
        CancellationToken ct = default
    ) => InstallPluginAsync(packageUrl: packageUrl, ct: ct);

    Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default);
    Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default);
    Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default);

    // Boot-time scan: load all plugins in the plugins directory, isolating failures
    // per plugin so one bad plugin never blocks the others.
    Task<IReadOnlyList<PluginLoadResult>> LoadAllAsync(CancellationToken ct = default);

    // Loaded plugin instances implementing T (e.g. IEncoderPlugin). Empty when none.
    IEnumerable<T> GetPluginsOfType<T>()
        where T : IPlugin;
}
