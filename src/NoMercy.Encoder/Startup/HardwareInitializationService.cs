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
    IHardwareEncoderProbe hardwareEncoderProbe,
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
    /// <see cref="BootStage.All"/> so the ffmpeg binary is on disk and the
    /// server is fully ready before the deferred probe competes for CPU/GPU.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        DetectionTask = Task.Run(function: () => RunDetectionAsync(ct: cancellationToken), cancellationToken: cancellationToken);
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
            logger.LogInformation(message: "Hardware detection waiting for BootStage.All (server ready)...");
            await phaseTracker.WhenReachedAsync(stage: BootStage.All, ct: ct).ConfigureAwait(continueOnCapturedContext: false);
            logger.LogInformation(message: "BootStage.All reached — starting hardware probe");
        }

        if (ct.IsCancellationRequested)
            return;

        logger.LogInformation(message: "Starting hardware detection...");

        try
        {
            // Probe FFmpeg capabilities FIRST — GPU detection needs HasEncoder() to be populated.
            // Retry on an empty encoder result: a transient probe failure (locked or
            // still-being-replaced binary) returns an empty set that downstream would
            // misread as "software-only host". After MaxProbeRetries the server falls
            // back to CPU-only so a genuinely GPU-less host still works.
            await ProbeWithRetryAsync(ct: ct).ConfigureAwait(continueOnCapturedContext: false);

            IReadOnlyList<GpuDevice> gpus = await hardwareDetector
                .DetectGpusAsync(ct: ct)
                .ConfigureAwait(continueOnCapturedContext: false);
            int cpuCores = await hardwareDetector.DetectCpuCoreCountAsync(ct: ct).ConfigureAwait(continueOnCapturedContext: false);

            // No GPU of any vendor was detected, so every hardware-encoder init
            // probe would only spawn ffmpeg to fail on a missing device/driver.
            // DetectGpusAsync already covers the one case vendor detection can
            // miss — NVIDIA in a container without /dev/dri, found via
            // nvidia-smi — so an empty result here is a genuine software-only
            // host. Skip the probe rather than logging a wall of expected init
            // failures for encoders that cannot possibly run.
            IReadOnlySet<string> usableHardwareEncoders;
            if (gpus.Count == 0)
            {
                logger.LogDebug(
                    message: "No GPU detected — skipping hardware-encoder init probe (software-only host)"
                );
                usableHardwareEncoders = new HashSet<string>();
            }
            else
            {
                usableHardwareEncoders = await ProbeUsableHardwareEncodersAsync(ct: ct)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }

            Capabilities = new HardwareCapabilities(Gpus: gpus, CpuCores: cpuCores, UsableHardwareEncoders: usableHardwareEncoders);
            IsReady = true;

            System.Text.StringBuilder summary = new();
            summary.Append(value: "Hardware detection complete - encoder ready:");
            summary.Append(
                handler: $"\n  FFmpeg : {ffmpegCapabilities.AvailableEncoders.Count} encoders, "
                         + $"{ffmpegCapabilities.AvailableFilters.Count} filters"
            );
            summary.Append(handler: $"\n  CPU    : {cpuCores} cores");
            if (gpus.Count == 0)
                summary.Append(value: "\n  GPU    : none (software-only)");
            else
                foreach (GpuDevice gpu in gpus)
                    summary.Append(
                        handler: $"\n  GPU    : {gpu.Vendor} {gpu.Name} "
                                 + $"({gpu.VramMb}MB VRAM, max {gpu.MaxEncoderSessions} sessions)"
                    );
            summary.Append(
                value: usableHardwareEncoders.Count == 0
                    ? "\n  HW init probe : no hardware encoders usable (software-only)"
                    : $"\n  HW init probe : usable [{string.Join(separator: ", ", values: usableHardwareEncoders)}]"
            );
            logger.LogInformation(message: "{HardwareSummary}", args: summary.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(exception: ex, message: "Hardware detection failed — software-only fallback");
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
                .DetectAndPersistAsync(ct: ct)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (driverResult.IsFirstBoot)
            {
                logger.LogInformation(
                    message: "Driver fingerprint: first boot (hash {Hash}) — initial calibration deferred to benchmark service",
                    args: driverResult.CurrentHash
                );
            }
            else if (driverResult.Changed)
            {
                logger.LogWarning(
                    message: "GPU driver change detected (prev={Prev}, curr={Curr}) — queuing benchmark recalibration", args: [driverResult.PreviousHash, driverResult.CurrentHash]
                );
                benchmarkJobTracker.Start(codecs: Array.Empty<VideoCodecType>(), resolutions: Array.Empty<int>());
            }
            else
            {
                logger.LogInformation(
                    message: "Driver fingerprint unchanged (hash {Hash})",
                    args: driverResult.CurrentHash
                );
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                exception: ex,
                message: "Driver fingerprint check failed — continuing without recalibration trigger"
            );
        }

        phaseTracker?.MarkComplete(stage: BootStage.Hardware);
    }

    /// <summary>
    /// Runs the real hardware-encoder init probe once, against every
    /// compiled-in hardware encoder name ffmpeg advertises — not just the
    /// ones matching a physically detected GPU vendor. This is deliberate:
    /// the probe result IS the authority PlanStage uses for selection, so it
    /// must independently confirm or refute every candidate rather than only
    /// checking the ones vendor detection already believes are present. A
    /// probe failure degrades to "no hardware encoders usable" (software-only)
    /// instead of throwing — an init-probe outage must never block boot or
    /// crash the server, it only removes hardware acceleration for this run.
    ///
    /// The caller only reaches this method when at least one GPU was detected;
    /// a zero-GPU host is short-circuited before here so no ffmpeg process is
    /// spawned for encoders that have no device to run on.
    /// </summary>
    private async Task<IReadOnlySet<string>> ProbeUsableHardwareEncodersAsync(CancellationToken ct)
    {
        try
        {
            IReadOnlyList<string> candidates = ffmpegCapabilities
                .AvailableEncoders.Where(predicate: encoderName =>
                    GpuEncoderTokens.VendorForEncoderName(ffmpegEncoderName: encoderName) is not null
                )
                .ToList();

            if (candidates.Count == 0)
                return new HashSet<string>();

            return await hardwareEncoderProbe.ProbeAsync(candidateHardwareEncoders: candidates, ct: ct).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                exception: ex,
                message: "Hardware encoder init probe failed — no hardware encoders usable (software-only)"
            );
            return new HashSet<string>();
        }
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
                await ffmpegCapabilities.ProbeAsync(ct: ct).ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt == MaxProbeRetries)
                    throw;

                logger.LogWarning(
                    exception: ex,
                    message: "ffmpeg probe failed (attempt {Attempt}/{Max}), retrying in {DelayMs}ms", args: [attempt + 1, MaxProbeRetries, probeRetryDelayMs]
                );
                await Task.Delay(millisecondsDelay: probeRetryDelayMs, cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
                continue;
            }

            if (ffmpegCapabilities.AvailableEncoders.Count > 0)
                return;

            if (attempt == MaxProbeRetries)
            {
                logger.LogWarning(
                    message: "ffmpeg -encoders returned empty set after {Max} attempt(s) — proceeding with CPU-only capabilities",
                    args: MaxProbeRetries + 1
                );
                return;
            }

            logger.LogWarning(
                message: "ffmpeg -encoders returned empty set (attempt {Attempt}/{Max}), retrying in {DelayMs}ms", args: [attempt + 1, MaxProbeRetries, probeRetryDelayMs]
            );
            await Task.Delay(millisecondsDelay: probeRetryDelayMs, cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
