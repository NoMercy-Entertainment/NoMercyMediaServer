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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.NmSystem.Lifecycle;

namespace NoMercy.Encoder.Startup;

public class HardwareInitializationService(
    IHardwareDetector hardwareDetector,
    FfmpegCapabilities ffmpegCapabilities,
    IDriverChangeDetector driverChangeDetector,
    IBenchmarkJobTracker benchmarkJobTracker,
    HardwareCapabilitiesHolder capabilitiesHolder,
    ILogger<HardwareInitializationService> logger,
    IServerPhaseTracker? phaseTracker = null,
    int probeRetryDelayMs = 2_000
) : IHostedService
{
    // Maximum number of times to retry a probe that returns an empty encoder
    // list — guards against transient binary-not-yet-ready races without
    // permanently stranding GPU encode jobs on hosts that do have NVENC/AMF/QSV.
    private const int MaxProbeRetries = 5;

    public bool IsReady { get; private set; }

    public IHardwareCapabilities? Capabilities
    {
        get => capabilitiesHolder.Current;
        private set => capabilitiesHolder.Current = value;
    }

    /// <summary>
    /// Exposed for tests (via InternalsVisibleTo) so test code can await the
    /// background detection task rather than polling <see cref="IsReady"/>.
    /// Not part of the public API.
    /// </summary>
    internal Task DetectionTask { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Returns immediately so the hosted-service start pipeline is never
    /// blocked. Hardware detection runs on a background task and gates on
    /// <see cref="BootStage.Binaries"/> so the ffmpeg binary is guaranteed
    /// to be on disk before the probe runs.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        DetectionTask = Task.Run(() => RunDetectionAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task RunDetectionAsync(CancellationToken ct)
    {
        // Wait until the binary-download/install task has finished placing the
        // ffmpeg executable on disk. Without this gate the probe races the
        // Binaries boot stage and can see a missing or partially-written binary,
        // causing ffmpeg -encoders to fail and the GPU codec list to be empty
        // for the entire server lifetime.
        if (phaseTracker is not null)
        {
            logger.LogInformation("Hardware detection waiting for BootStage.All (server ready)...");
            await phaseTracker.WhenReachedAsync(BootStage.All, ct).ConfigureAwait(false);
            logger.LogInformation("BootStage.All reached — starting hardware probe");
        }

        if (ct.IsCancellationRequested)
            return;

        logger.LogInformation("Starting hardware detection...");

        try
        {
            // Probe FFmpeg capabilities FIRST — GPU detection needs HasEncoder() to be populated.
            // Retry on an empty encoder result: a transient probe failure (locked or
            // still-being-replaced binary) returns an empty set that downstream would
            // misread as "software-only host". After MaxProbeRetries the server falls
            // back to CPU-only so a genuinely GPU-less host still works.
            await ProbeWithRetryAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "FFmpeg: {EncoderCount} encoders, {FilterCount} filters",
                ffmpegCapabilities.AvailableEncoders.Count,
                ffmpegCapabilities.AvailableFilters.Count
            );

            IReadOnlyList<GpuDevice> gpus = await hardwareDetector
                .DetectGpusAsync(ct)
                .ConfigureAwait(false);
            int cpuCores = await hardwareDetector.DetectCpuCoreCountAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "Detected {GpuCount} GPU(s), {CpuCores} CPU cores",
                gpus.Count,
                cpuCores
            );

            foreach (GpuDevice gpu in gpus)
                logger.LogInformation(
                    "GPU: {Vendor} {Name} ({VramMb}MB VRAM, max {Sessions} sessions)",
                    gpu.Vendor,
                    gpu.Name,
                    gpu.VramMb,
                    gpu.MaxEncoderSessions
                );

            Capabilities = new HardwareCapabilities(gpus, cpuCores);
            IsReady = true;
            logger.LogInformation("Hardware detection complete. Encoder ready.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Hardware detection failed — software-only fallback");
            Capabilities = new HardwareCapabilities(Gpus: [], CpuCores: Environment.ProcessorCount);
            IsReady = true;
        }

        // Driver change detection — runs in its own try-catch so a failure
        // here (fingerprint store unavailable, mock-of in tests, etc.) cannot
        // revert the Capabilities we just established. First boot:
        // HardwareBenchmarkBackgroundService handles initial calibration;
        // subsequent boots with a changed driver queue an immediate recalibration.
        try
        {
            DriverChangeResult driverResult = await driverChangeDetector
                .DetectAndPersistAsync(ct)
                .ConfigureAwait(false);

            if (driverResult.IsFirstBoot)
            {
                logger.LogInformation(
                    "Driver fingerprint: first boot (hash {Hash}) — initial calibration deferred to benchmark service",
                    driverResult.CurrentHash
                );
            }
            else if (driverResult.Changed)
            {
                logger.LogWarning(
                    "GPU driver change detected (prev={Prev}, curr={Curr}) — queuing benchmark recalibration",
                    driverResult.PreviousHash,
                    driverResult.CurrentHash
                );
                benchmarkJobTracker.Start(Array.Empty<VideoCodecType>(), Array.Empty<int>());
            }
            else
            {
                logger.LogInformation(
                    "Driver fingerprint unchanged (hash {Hash})",
                    driverResult.CurrentHash
                );
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Driver fingerprint check failed — continuing without recalibration trigger"
            );
        }

        phaseTracker?.MarkComplete(BootStage.Hardware);
    }

    /// <summary>
    /// Runs <see cref="FfmpegCapabilities.ProbeAsync"/> and retries up to
    /// <see cref="MaxProbeRetries"/> times when the encoder list comes back
    /// empty — which happens when the binary is still being written to disk.
    /// Throws on the final attempt so the caller's catch block handles the
    /// CPU-only fallback.
    /// </summary>
    private async Task ProbeWithRetryAsync(CancellationToken ct)
    {
        for (int attempt = 0; attempt <= MaxProbeRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await ffmpegCapabilities.ProbeAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt == MaxProbeRetries)
                    throw;

                logger.LogWarning(
                    ex,
                    "ffmpeg probe failed (attempt {Attempt}/{Max}), retrying in {DelayMs}ms",
                    attempt + 1,
                    MaxProbeRetries,
                    probeRetryDelayMs
                );
                await Task.Delay(probeRetryDelayMs, ct).ConfigureAwait(false);
                continue;
            }

            if (ffmpegCapabilities.AvailableEncoders.Count > 0)
                return;

            if (attempt == MaxProbeRetries)
            {
                logger.LogWarning(
                    "ffmpeg -encoders returned empty set after {Max} attempt(s) — proceeding with CPU-only capabilities",
                    MaxProbeRetries + 1
                );
                return;
            }

            logger.LogWarning(
                "ffmpeg -encoders returned empty set (attempt {Attempt}/{Max}), retrying in {DelayMs}ms",
                attempt + 1,
                MaxProbeRetries,
                probeRetryDelayMs
            );
            await Task.Delay(probeRetryDelayMs, ct).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
