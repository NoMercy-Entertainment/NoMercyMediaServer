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
using NoMercy.NmSystem.Lifecycle;

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
    ILogger<BluRayCapabilityStartupService> logger,
    IServerPhaseTracker phaseTracker
) : BackgroundService
{
    private static readonly TimeSpan InitialGrace = TimeSpan.FromSeconds(seconds: 5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!lifetime.ApplicationStarted.IsCancellationRequested)
        {
            TaskCompletionSource tcs = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
            await using CancellationTokenRegistration startedReg =
                lifetime.ApplicationStarted.Register(callback: () => tcs.TrySetResult());
            await using CancellationTokenRegistration cancelReg = stoppingToken.Register(callback: () =>
                tcs.TrySetResult()
            );
            await tcs.Task.ConfigureAwait(continueOnCapturedContext: false);
        }

        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            await phaseTracker.WhenReachedAsync(stage: BootStage.All, ct: stoppingToken).ConfigureAwait(continueOnCapturedContext: false);
            await Task.Delay(delay: InitialGrace, cancellationToken: stoppingToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            await capability.ProbeAsync(ct: stoppingToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(message: "Blu-ray capability probe cancelled during shutdown");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Blu-ray capability probe failed — Blu-ray features may be unavailable"
            );
        }
    }
}
