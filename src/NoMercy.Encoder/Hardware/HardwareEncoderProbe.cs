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
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    public async Task<IReadOnlySet<string>> ProbeAsync(
        IEnumerable<string> candidateHardwareEncoders,
        CancellationToken ct = default
    )
    {
        HashSet<string> usable = new(StringComparer.OrdinalIgnoreCase);

        foreach (
            string encoderName in candidateHardwareEncoders.Distinct(
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            ct.ThrowIfCancellationRequested();

            if (await ProbeOneAsync(encoderName, ct).ConfigureAwait(false))
                usable.Add(encoderName);
        }

        return usable;
    }

    private async Task<bool> ProbeOneAsync(string encoderName, CancellationToken ct)
    {
        string[]? arguments = BuildProbeArguments(encoderName);
        if (arguments is null)
        {
            logger.LogWarning(
                "No init-probe invocation known for hardware encoder {Encoder} — treating as unusable",
                encoderName
            );
            return false;
        }

        using CancellationTokenSource timeoutCts = new(ProbeTimeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            timeoutCts.Token
        );

        try
        {
            ProcessResult result = await processRunner
                .RunAsync(AppFiles.FfmpegPath, arguments, null, linkedCts.Token)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                logger.LogInformation("Hardware encoder init probe: {Encoder} usable", encoderName);
                return true;
            }

            logger.LogInformation(
                "Hardware encoder init probe: {Encoder} unusable (exit {Code}): {Err}",
                encoderName,
                result.ExitCode,
                result.StdErr.Trim()
            );
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Only the probe's own timeout fired — the caller's token is still
            // live. A hang means the encoder cannot be trusted; never usable.
            logger.LogWarning(
                "Hardware encoder init probe timed out after {Timeout}: {Encoder} — treating as unusable",
                ProbeTimeout,
                encoderName
            );
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Hardware encoder init probe threw for {Encoder} — treating as unusable",
                encoderName
            );
            return false;
        }
    }

    /// <summary>
    /// Builds the minimal known-working init invocation per hardware encoder
    /// family. Returns null for a name outside the known families — callers
    /// treat that as unusable rather than guessing at an invocation.
    /// </summary>
    private static string[]? BuildProbeArguments(string encoderName)
    {
        if (GpuEncoderTokens.NvencNames.Contains(encoderName, StringComparer.OrdinalIgnoreCase))
            return DirectSoftwareFrameArgs(encoderName);

        if (GpuEncoderTokens.AmfNames.Contains(encoderName, StringComparer.OrdinalIgnoreCase))
            return DirectSoftwareFrameArgs(encoderName);

        if (
            GpuEncoderTokens.VideotoolboxNames.Contains(
                encoderName,
                StringComparer.OrdinalIgnoreCase
            )
        )
            return DirectSoftwareFrameArgs(encoderName);

        if (GpuEncoderTokens.QsvNames.Contains(encoderName, StringComparer.OrdinalIgnoreCase))
            return QsvArgs(encoderName);

        if (GpuEncoderTokens.VaapiNames.Contains(encoderName, StringComparer.OrdinalIgnoreCase))
            return VaapiArgs(encoderName);

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
