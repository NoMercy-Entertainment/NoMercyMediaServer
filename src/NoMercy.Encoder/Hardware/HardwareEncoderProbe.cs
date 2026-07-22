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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Information;

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Runs a minimal one-frame encode per candidate hardware encoder through the
/// real ffmpeg binary and records which ones actually initialize. This is the
/// authoritative signal for "usable on this host" — a real codec-init failure
/// (missing silicon, missing driver, wrong GPU vendor) is unambiguous, unlike
/// GPU-vendor detection or ffmpeg's compiled-in encoder list, both of which
/// only say an encoder *might* work.
/// </summary>
public sealed class HardwareEncoderProbe(
    IProcessRunner processRunner,
    ILogger<HardwareEncoderProbe> logger
) : IHardwareEncoderProbe
{
    // A hung or unresponsive driver stack must not stall boot indefinitely —
    // every family below completes in well under a second on a working
    // device, so this is generous headroom, not a target.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(seconds: 10);

    public async Task<IReadOnlySet<string>> ProbeAsync(
        IEnumerable<string> candidateHardwareEncoders,
        CancellationToken ct = default
    )
    {
        HashSet<string> usable = new(comparer: StringComparer.OrdinalIgnoreCase);

        foreach (
            string encoderName in candidateHardwareEncoders.Distinct(
                comparer: StringComparer.OrdinalIgnoreCase
            )
        )
        {
            ct.ThrowIfCancellationRequested();

            if (await ProbeOneAsync(encoderName: encoderName, ct: ct).ConfigureAwait(continueOnCapturedContext: false))
                usable.Add(item: encoderName);
        }

        return usable;
    }

    private async Task<bool> ProbeOneAsync(string encoderName, CancellationToken ct)
    {
        string[]? arguments = BuildProbeArguments(encoderName: encoderName);
        if (arguments is null)
        {
            logger.LogWarning(
                message: "No init-probe invocation known for hardware encoder {Encoder} — treating as unusable",
                args: encoderName
            );
            return false;
        }

        using CancellationTokenSource timeoutCts = new(delay: ProbeTimeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            token1: ct,
            token2: timeoutCts.Token
        );

        try
        {
            ProcessResult result = await processRunner
                .RunAsync(executable: AppFiles.FfmpegPath, arguments: arguments, workingDirectory: null, cancellationToken: linkedCts.Token)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (result.IsSuccess)
            {
                logger.LogInformation(message: "Hardware encoder init probe: {Encoder} usable", args: encoderName);
                return true;
            }

            // An encoder that fails to initialize is the expected outcome on any
            // host lacking that vendor's silicon/driver — it is diagnostic
            // detail, not an operational event, so it stays at Debug. Only the
            // first stderr line is logged: ffmpeg emits a multi-line cascade but
            // the first line carries the actual cause (e.g. "Cannot load
            // libcuda.so.1", "No VA display found"); the rest is downstream noise.
            logger.LogDebug(
                message: "Hardware encoder init probe: {Encoder} unusable (exit {Code}): {Err}", args: [encoderName, result.ExitCode, FirstMeaningfulLine(stdErr: result.StdErr)]
            );
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Only the probe's own timeout fired — the caller's token is still
            // live. A hang means the encoder cannot be trusted; never usable.
            logger.LogWarning(
                message: "Hardware encoder init probe timed out after {Timeout}: {Encoder} — treating as unusable", args: [ProbeTimeout, encoderName]
            );
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                exception: ex,
                message: "Hardware encoder init probe threw for {Encoder} — treating as unusable",
                args: encoderName
            );
            return false;
        }
    }

    /// <summary>
    /// Returns the first non-empty line of an ffmpeg error dump — the line
    /// carrying the actual cause — so a failed probe logs one readable reason
    /// instead of the full multi-line codec-init cascade.
    /// </summary>
    private static string FirstMeaningfulLine(string stdErr)
    {
        foreach (string line in stdErr.Split(separator: '\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0)
                return trimmed;
        }

        return string.Empty;
    }

    /// <summary>
    /// Builds the minimal known-working init invocation per hardware encoder
    /// family. Returns null for a name outside the known families — callers
    /// treat that as unusable rather than guessing at an invocation.
    /// </summary>
    private static string[]? BuildProbeArguments(string encoderName)
    {
        if (GpuEncoderTokens.NvencNames.Contains(value: encoderName, comparer: StringComparer.OrdinalIgnoreCase))
            return DirectSoftwareFrameArgs(encoderName: encoderName);

        if (GpuEncoderTokens.AmfNames.Contains(value: encoderName, comparer: StringComparer.OrdinalIgnoreCase))
            return DirectSoftwareFrameArgs(encoderName: encoderName);

        if (
            GpuEncoderTokens.VideotoolboxNames.Contains(
                value: encoderName,
                comparer: StringComparer.OrdinalIgnoreCase
            )
        )
            return DirectSoftwareFrameArgs(encoderName: encoderName);

        if (GpuEncoderTokens.QsvNames.Contains(value: encoderName, comparer: StringComparer.OrdinalIgnoreCase))
            return QsvArgs(encoderName: encoderName);

        if (GpuEncoderTokens.VaapiNames.Contains(value: encoderName, comparer: StringComparer.OrdinalIgnoreCase))
            return VaapiArgs(encoderName: encoderName);

        return null;
    }

    /// <summary>
    /// NVENC, AMF, and VideoToolbox encoders accept an ordinary software
    /// frame directly — the encoder itself performs the upload to the device
    /// internally, so no <c>-init_hw_device</c> / <c>hwupload</c> filter is
    /// required for a minimal init probe. A device or driver mismatch (e.g.
    /// h264_amf on an NVIDIA-only host) fails at codec-open time with a
    /// nonzero exit, which is exactly the signal this probe needs.
    /// </summary>
    private static string[] DirectSoftwareFrameArgs(string encoderName) =>
        [
            "-v",
            "error",
            "-f",
            "lavfi",
            "-i",
            "nullsrc=s=256x256",
            "-frames:v",
            "1",
            "-c:v",
            encoderName,
            "-f",
            "null",
            "-",
        ];

    /// <summary>
    /// QSV requires an explicit hardware device plus a software-to-hardware
    /// upload filter chain — without it ffmpeg fails at codec init with "No
    /// device available" before ever touching the actual encoder silicon.
    /// </summary>
    private static string[] QsvArgs(string encoderName) =>
        [
            "-v",
            "error",
            "-init_hw_device",
            "qsv=hw",
            "-f",
            "lavfi",
            "-i",
            "nullsrc=s=256x256",
            "-frames:v",
            "1",
            "-vf",
            "format=nv12,hwupload=extra_hw_frames=64",
            "-c:v",
            encoderName,
            "-f",
            "null",
            "-",
        ];

    /// <summary>
    /// VAAPI requires an explicit render-node device plus a software-to-
    /// hardware upload filter chain. <c>/dev/dri/renderD128</c> is the
    /// conventional first GPU render node on Linux; a host without one
    /// (Windows, or a headless box with no DRM node) fails to open the
    /// device and the encoder is correctly marked unusable rather than
    /// silently accepted.
    /// </summary>
    private static string[] VaapiArgs(string encoderName) =>
        [
            "-v",
            "error",
            "-vaapi_device",
            "/dev/dri/renderD128",
            "-f",
            "lavfi",
            "-i",
            "nullsrc=s=256x256",
            "-frames:v",
            "1",
            "-vf",
            "format=nv12,hwupload",
            "-c:v",
            encoderName,
            "-f",
            "null",
            "-",
        ];
}
