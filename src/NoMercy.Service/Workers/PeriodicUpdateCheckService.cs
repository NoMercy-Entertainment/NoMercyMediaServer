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

using Microsoft.Extensions.Hosting;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Service.Workers;

// Replaces the static UpdateChecker.StartPeriodicUpdateCheck boot task: runs the
// update check on a fixed cadence so IUpdateStatus stays fresh for the dashboard.
public sealed class PeriodicUpdateCheckService(IUpdateChecker updateChecker) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let the host settle before the first network call.
            await Task.Delay(InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await updateChecker.IsUpdateAvailableAsync();

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down — exit cleanly.
        }
    }
}
