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
using Microsoft.Extensions.Logging;

namespace NoMercy.OpticalMedia.Capabilities;

/// <summary>
/// Deferred background service that runs the Blu-ray capability probe shortly
/// after application start. Checks whether the bundled nomercy-ffmpeg binary
/// has libbluray support and logs the result. Failure is swallowed — a missing
/// Blu-ray capability must not prevent the server from serving other media.
/// </summary>
public sealed class BluRayCapabilityStartupService(
    FfmpegBluRayCapability capability,
    IHostApplicationLifetime lifetime,
    ILogger<BluRayCapabilityStartupService> logger
) : BackgroundService
{
    private static readonly TimeSpan InitialGrace = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!lifetime.ApplicationStarted.IsCancellationRequested)
        {
            TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await using CancellationTokenRegistration startedReg =
                lifetime.ApplicationStarted.Register(() => tcs.TrySetResult());
            await using CancellationTokenRegistration cancelReg = stoppingToken.Register(() =>
                tcs.TrySetResult()
            );
            await tcs.Task.ConfigureAwait(false);
        }

        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            await Task.Delay(InitialGrace, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            await capability.ProbeAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Blu-ray capability probe cancelled during shutdown");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Blu-ray capability probe failed — Blu-ray features may be unavailable"
            );
        }
    }
}
