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

using NoMercy.Data.Services;

namespace NoMercy.Service.Workers;

/// <summary>
/// On boot, sweeps out the media rows earlier scans wrote with a path that
/// addresses no file. They are unplayable entries a rescan cannot repair, so
/// they linger in every client's library until removed. Runs on every start
/// rather than behind a one-shot flag: the sweep is a no-op on a healthy
/// database and stays a safety net if the shape ever reappears.
/// </summary>
public class UnresolvablePathRepairStartupService(
    IUnresolvablePathRepair repair,
    ILogger<UnresolvablePathRepairStartupService> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            int removed = await repair.RunAsync(cancellationToken);
            if (removed > 0)
                logger.LogInformation(
                    "Removed {Count} media rows whose stored path addressed no file",
                    removed
                );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sweep unresolvable media paths on startup; continuing");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
