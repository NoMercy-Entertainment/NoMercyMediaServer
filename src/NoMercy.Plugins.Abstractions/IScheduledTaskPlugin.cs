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

public interface IScheduledTaskPlugin : IPlugin
{
    string CronExpression { get; }
    Task ExecuteAsync(CancellationToken ct = default);

    /// <summary>
    /// Work that runs on its own cadence, when one expression per plugin is not
    /// enough.
    /// <para>
    /// A plugin with several kinds of periodic work at genuinely different
    /// cadences had one slot for all of them, so the workaround was to register
    /// the fastest one and gate the rest behind an internal scheduler. That
    /// costs every such plugin the same hand-rolled scheduler, and it shows the
    /// server's job list one opaque job in place of the real work.
    /// </para>
    /// <para>
    /// Defaulted to empty, so an existing plugin keeps working untouched. When
    /// it is empty the single <see cref="CronExpression"/> is registered as
    /// before; when it is not, each entry is registered separately and can be
    /// seen, timed and disabled on its own.
    /// </para>
    /// </summary>
    IReadOnlyList<PluginScheduledJob> Jobs => [];

    /// <summary>
    /// Runs one named job from <see cref="Jobs"/>. The default routes every
    /// name to <see cref="ExecuteAsync"/> so a plugin that declares jobs but
    /// has not split its entry point still behaves.
    /// </summary>
    Task ExecuteAsync(string jobName, CancellationToken ct = default) => ExecuteAsync(ct);
}

/// <param name="Name">Unique within the plugin. Becomes <c>plugin:{id}:{name}</c> in the job list.</param>
/// <param name="CronExpression">Standard cron, same dialect as <see cref="IScheduledTaskPlugin.CronExpression"/>.</param>
/// <param name="AllowConcurrent">
/// Whether a tick may start while the previous one is still running. False by
/// default: an expensive cycle overrunning its interval should skip, not pile
/// up, and a plugin that wants overlap has to say so.
/// </param>
public record PluginScheduledJob(string Name, string CronExpression, bool AllowConcurrent = false);
