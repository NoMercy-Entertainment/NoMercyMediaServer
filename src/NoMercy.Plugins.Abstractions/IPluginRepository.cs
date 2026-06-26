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

public interface IPluginRepository
{
    IReadOnlyList<PluginRepositoryInfo> GetRepositories();
    Task AddRepositoryAsync(string name, string url, CancellationToken ct = default);
    Task RemoveRepositoryAsync(string name, CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
    IReadOnlyList<PluginRepositoryEntry> GetAvailablePlugins();
    PluginRepositoryEntry? FindPlugin(Guid pluginId);
    PluginVersionEntry? FindVersion(Guid pluginId, string version);
}

public class PluginRepositoryInfo
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public bool Enabled { get; set; } = true;
}
