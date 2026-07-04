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

namespace NoMercy.Plugins;

/// <summary>
/// Host-side options for plugin assembly loading. <see cref="SharedAssemblies"/>
/// are the assemblies whose type identity must be preserved across the
/// host/plugin boundary: a plugin's <see cref="PluginLoadContext"/> defers to
/// the default load context for these so types remain castable. Bind this from
/// configuration to add a shared framework package without a code change.
/// </summary>
public record PluginHostOptions
{
    public IReadOnlySet<string> SharedAssemblies { get; init; } = DefaultSharedAssemblies;

    /// <summary>The built-in shared-assembly set used when none is configured.</summary>
    public static IReadOnlySet<string> DefaultSharedAssemblies { get; } =
        new HashSet<string>
        {
            "NoMercy.Plugins.Abstractions",
            "NoMercy.Events",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.DependencyInjection",
        };
}
