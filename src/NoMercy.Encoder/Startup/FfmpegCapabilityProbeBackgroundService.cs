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

namespace NoMercy.Encoder.Startup;

/// <summary>
/// Deferred background service that runs the FFmpeg capability probe
/// <see cref="DefaultInitialGrace"/> after <c>ApplicationStarted</c> fires.
/// Probe failure is logged and swallowed — a missing optional dependency
/// must never prevent the server from serving users.
/// </summary>
public sealed class FfmpegCapabilityProbeBackgroundService : BackgroundService
{
    internal static readonly TimeSpan DefaultInitialGrace = TimeSpan.FromSeconds(5);

    private readonly IFfmpegCapabilityProbe _probe;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<FfmpegCapabilityProbeBackgroundService> _logger;
    private readonly TimeSpan _initialGrace;
    private readonly IServerPhaseTracker _phaseTracker;

    public FfmpegCapabilityProbeBackgroundService(
        IFfmpegCapabilityProbe probe,
        IHostApplicationLifetime lifetime,
        ILogger<FfmpegCapabilityProbeBackgroundService> logger,
        IServerPhaseTracker phaseTracker
    )
        : this(probe, lifetime, logger, phaseTracker, DefaultInitialGrace) { }

    internal FfmpegCapabilityProbeBackgroundService(
        IFfmpegCapabilityProbe probe,
        IHostApplicationLifetime lifetime,
        ILogger<FfmpegCapabilityProbeBackgroundService> logger,
        IServerPhaseTracker phaseTracker,
        TimeSpan initialGrace
    )
    {
        _probe = probe;
        _lifetime = lifetime;
        _logger = logger;
        _phaseTracker = phaseTracker;
        _initialGrace = initialGrace;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForApplicationStartedAsync(stoppingToken).ConfigureAwait(false);
        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            await _phaseTracker.WhenReachedAsync(BootStage.Binaries, stoppingToken).ConfigureAwait(false);
            await Task.Delay(_initialGrace, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            await _probe.ProbeAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("FFmpeg capability probe cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "FFmpeg capability probe failed — capabilities endpoint will return probe_pending"
            );
        }
    }

    private async Task WaitForApplicationStartedAsync(CancellationToken ct)
    {
        if (_lifetime.ApplicationStarted.IsCancellationRequested)
            return;

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using CancellationTokenRegistration startedReg =
            _lifetime.ApplicationStarted.Register(() => tcs.TrySetResult());
        await using CancellationTokenRegistration cancelReg = ct.Register(() => tcs.TrySetResult());
        await tcs.Task.ConfigureAwait(false);
    }
}
