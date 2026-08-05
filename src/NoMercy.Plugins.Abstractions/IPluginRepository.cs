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
    /// <summary>
    /// Loads whatever was persisted. Defaulted: an implementation that keeps no
    /// state on disk has nothing to do here, and one written before this member
    /// existed keeps compiling.
    /// </summary>
    Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

    IReadOnlyList<PluginRepositoryInfo> GetRepositories();
    Task AddRepositoryAsync(string name, string url, CancellationToken ct = default);
    Task RemoveRepositoryAsync(string name, CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
    IReadOnlyList<PluginRepositoryEntry> GetAvailablePlugins();
    PluginRepositoryEntry? FindPlugin(Ulid pluginId);
    PluginVersionEntry? FindVersion(Ulid pluginId, string version);

    /// <summary>
    /// Whether this plugin is listed by a repository the owner marked trusted.
    /// <para>
    /// False by default, and false when no index could be read: a server that
    /// cannot reach the internet must not decide a plugin is trusted because it
    /// failed to check. The plugin then goes through the ordinary consent, which
    /// is the answer that was always safe.
    /// </para>
    /// </summary>
    bool IsFromTrustedRepository(Ulid pluginId) => false;
}

public class PluginRepositoryInfo
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether a plugin listed here is one the owner already trusts, so it
    /// enables on install instead of waiting to be approved one at a time.
    /// <para>
    /// Trust belongs to where a plugin came from, not to what its manifest says
    /// about itself: an author line is free text any file can copy, and a list
    /// of blessed plugin ids in the source would be a security decision that
    /// needs a rebuild to change. A repository is already a thing the owner adds,
    /// removes and can see.
    /// </para>
    /// <para>
    /// Set on the index we publish because the owner installing our server has
    /// already decided to trust us; every other repository starts untrusted and
    /// the owner turns it on if they mean to.
    /// </para>
    /// </summary>
    public bool Trusted { get; set; }
}
