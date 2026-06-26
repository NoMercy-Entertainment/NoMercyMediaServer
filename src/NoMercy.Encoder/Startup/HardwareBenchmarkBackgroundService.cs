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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.Startup;

/// <summary>
/// Runs hardware benchmark calibration lazily, never during boot or during
/// user-visible work. Waits for <see cref="IHostApplicationLifetime.ApplicationStarted"/>,
/// then a short grace period, then polls <see cref="IEncoderActivityProbe"/>
/// until the encoder is idle. Only then does the benchmark run — so a user
/// who hits the server immediately after boot never competes with the
/// benchmark's ffmpeg processes for CPU/GPU. Gated by
/// <see cref="EncoderOptions.AutoCalibrate"/>.
/// </summary>
public sealed class HardwareBenchmarkBackgroundService : BackgroundService
{
    internal static readonly TimeSpan DefaultInitialGrace = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan DefaultBusyPollInterval = TimeSpan.FromSeconds(30);

    private readonly IHardwareBenchmark _benchmark;
    private readonly EncoderOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IEncoderActivityProbe _activityProbe;
    private readonly ILogger<HardwareBenchmarkBackgroundService> _logger;
    private readonly TimeSpan _initialGrace;
    private readonly TimeSpan _busyPollInterval;

    public HardwareBenchmarkBackgroundService(
        IHardwareBenchmark benchmark,
        EncoderOptions options,
        IHostApplicationLifetime lifetime,
        IEncoderActivityProbe activityProbe,
        ILogger<HardwareBenchmarkBackgroundService> logger
    )
        : this(
            benchmark,
            options,
            lifetime,
            activityProbe,
            logger,
            DefaultInitialGrace,
            DefaultBusyPollInterval
        ) { }

    internal HardwareBenchmarkBackgroundService(
        IHardwareBenchmark benchmark,
        EncoderOptions options,
        IHostApplicationLifetime lifetime,
        IEncoderActivityProbe activityProbe,
        ILogger<HardwareBenchmarkBackgroundService> logger,
        TimeSpan initialGrace,
        TimeSpan busyPollInterval
    )
    {
        _benchmark = benchmark;
        _options = options;
        _lifetime = lifetime;
        _activityProbe = activityProbe;
        _logger = logger;
        _initialGrace = initialGrace;
        _busyPollInterval = busyPollInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoCalibrate)
        {
            _logger.LogDebug("AutoCalibrate disabled — skipping benchmark");
            return;
        }

        await WaitForApplicationStartedAsync(stoppingToken).ConfigureAwait(false);
        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            await Task.Delay(_initialGrace, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!_benchmark.NeedsRecalibration())
        {
            _logger.LogDebug("SpeedIndex cache is fresh — skipping benchmark");
            return;
        }

        while (!stoppingToken.IsCancellationRequested && _activityProbe.IsBusy)
        {
            _logger.LogDebug(
                "Hardware benchmark deferred — encoder busy, retry in {Seconds}s",
                (int)_busyPollInterval.TotalSeconds
            );
            try
            {
                await Task.Delay(_busyPollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            _logger.LogInformation("Starting hardware benchmark calibration");
            SpeedIndex result = await _benchmark
                .CalibrateAsync(stoppingToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Hardware benchmark finished — captured {Count} measurements",
                result.Measurements.Count
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Hardware benchmark cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hardware benchmark failed");
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
