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
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Plugins.Hooks;

/// <summary>
/// Adapts an <see cref="IScheduledTaskPlugin"/> to <see cref="ICronJobExecutor"/>
/// so <c>CronWorker.RegisterExecutor</c> can schedule it directly as a runtime
/// instance — plugins have no DI registration for the type-based cron path.
/// </summary>
public class PluginCronExecutor(IScheduledTaskPlugin plugin) : ICronJobExecutor
{
    public string JobName => $"plugin:{plugin.Id}";

    public string CronExpression => plugin.CronExpression;

    public Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default) =>
        plugin.ExecuteAsync(ct: cancellationToken);
}
