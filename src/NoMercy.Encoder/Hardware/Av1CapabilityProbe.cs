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

using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Infrastructure;

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Vendor-specific AV1 encoder capability detection. ffmpeg ships every
/// hardware AV1 encoder (av1_nvenc / av1_amf / av1_qsv / av1_vaapi) against
/// any driver that knows about the codec name, but the physical encoder block
/// only exists on specific GPU generations:
///
///   Nvidia: Ada Lovelace+         (RTX 4000+, L4/L40, RTX 6000 Ada)
///   AMD:    RDNA 3+               (RX 7000, RX 9000, W7700+)
///   Intel:  Arc Alchemist+        (Arc A/B series, Xe-LPG/Xe2 iGPU)
///
/// Probing on cards without the silicon fails at codec init and the benchmark
/// gets nothing, so we gate up-front. Each vendor uses its own detection
/// signal (nvidia-smi compute_cap for Nvidia, GPU name pattern for AMD/Intel).
/// Unknown / future SKUs default to "allowed" so the probe-and-log fallback
/// covers any pattern miss.
/// </summary>
internal sealed class Av1CapabilityProbe(IProcessRunner processRunner, ILogger logger)
{
    public Task<bool> SupportsAsync(GpuVendor vendor, string gpuName) =>
        vendor switch
        {
            GpuVendor.Nvidia => NvidiaSupportsAv1NvencAsync(),
            GpuVendor.Amd => Task.FromResult(AmdSupports(gpuName)),
            GpuVendor.Intel => Task.FromResult(IntelSupports(gpuName)),
            _ => Task.FromResult(true),
        };

    /// <summary>
    /// Returns true when the AMD GPU name matches an RDNA 3 or later SKU.
    /// AV1 AMF first appeared on RDNA 3 (Navi 31/32/33 — RX 7000-series and
    /// W7700+ workstation cards). RDNA 1 (RX 5000) and RDNA 2 (RX 6000,
    /// console APUs) lack the silicon.
    ///
    /// Pattern coverage:
    ///   - "RX 7..."       → RX 7600 / 7700 / 7800 / 7900 (XT/XTX) and mobile M/S/XT variants
    ///   - "RX 9..."       → RX 9000-series (RDNA 4)
    ///   - "Pro W7..."     → W7700 / W7800 / W7900 workstation cards
    ///   - "Radeon 8..."   → Strix Halo / Strix Point integrated (Radeon 8050S–8060S)
    ///
    /// Anything else defaults to true so an unrecognised model still gets
    /// the probe attempt; a real failure shows up in the Information-level
    /// benchmark log.
    /// </summary>
    private bool AmdSupports(string gpuName)
    {
        string upper = gpuName.ToUpperInvariant();

        // Confirmed pre-AV1 generations. Block these explicitly so the
        // benchmark doesn't waste time probing a known-failing encoder.
        if (
            upper.Contains("RX 5")
            || upper.Contains("RX 6")
            || upper.Contains("PRO W5")
            || upper.Contains("PRO W6")
            || upper.Contains("VEGA")
            || upper.Contains("POLARIS")
        )
        {
            logger.LogInformation(
                "AV1 AMF unavailable on {Name} — RDNA 1/2 / Vega / Polaris silicon predates the AV1 encoder block.",
                gpuName
            );
            return false;
        }

        // Unknown SKU — default allow so the probe-and-log fallback handles
        // any future card we haven't pattern-matched yet.
        return true;
    }

    /// <summary>
    /// Returns true when the Intel GPU name matches an Arc Alchemist /
    /// Battlemage discrete card or an Xe-LPG / Xe2 integrated GPU. AV1 QSV /
    /// VAAPI first appeared on Arc Alchemist (DG2). Iris Xe Graphics, UHD
    /// Graphics 6xx/7xx, and earlier iGPUs lack the encoder block.
    ///
    /// Pattern coverage:
    ///   - "ARC A"           → Arc Alchemist desktop (A310/380/580/750/770) and mobile
    ///   - "ARC B"           → Arc Battlemage (B570/B580)
    ///   - "ARC GRAPHICS"    → Meteor Lake (Xe-LPG) and Lunar Lake (Xe2) integrated
    ///   - "ARC PRO"         → Arc Pro workstation variants
    ///
    /// Iris Xe / UHD Graphics on the same machine are blocked explicitly.
    /// Unknown SKUs default to true so a future Intel discrete GPU isn't
    /// silently denied.
    /// </summary>
    private bool IntelSupports(string gpuName)
    {
        string upper = gpuName.ToUpperInvariant();

        // Pre-Arc iGPUs — known to lack AV1 encode silicon.
        if (
            upper.Contains("IRIS XE")
            || upper.Contains("UHD GRAPHICS")
            || upper.Contains("HD GRAPHICS")
        )
        {
            logger.LogInformation(
                "AV1 QSV/VAAPI unavailable on {Name} — pre-Arc Intel iGPUs lack the AV1 encoder block.",
                gpuName
            );
            return false;
        }

        // Arc family — discrete cards and modern Core Ultra integrated GPUs
        // both ship the encoder block.
        if (upper.Contains("ARC"))
            return true;

        // Anything else — default allow, let the probe decide.
        return true;
    }

    /// <summary>
    /// Returns true when at least one Nvidia GPU on the host has compute
    /// capability ≥ 8.9 (Ada Lovelace), which is the minimum architecture
    /// that includes the AV1 NVENC encoder block.
    ///
    /// Queries <c>nvidia-smi</c> — ships with every Nvidia driver, so it's
    /// available wherever the encoders themselves are. If nvidia-smi is
    /// missing or the query fails, defaults to <c>true</c> so users with
    /// non-standard installs (e.g. Linux drivers without nvidia-smi in
    /// PATH) still get an attempt at the encoder rather than silently
    /// being denied; a failed probe at benchmark time will then drop the
    /// codec from the speed index with a clear "encoder unavailable"
    /// message.
    /// </summary>
    private async Task<bool> NvidiaSupportsAv1NvencAsync()
    {
        try
        {
            ProcessResult result = await processRunner
                .RunAsync(
                    "nvidia-smi",
                    ["--query-gpu=compute_cap", "--format=csv,noheader,nounits"],
                    null,
                    CancellationToken.None
                )
                .ConfigureAwait(false);

            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StdOut))
            {
                logger.LogDebug(
                    "nvidia-smi compute_cap query failed (exit {Code}) — assuming AV1 NVENC available",
                    result.ExitCode
                );
                return true;
            }

            foreach (
                string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            )
            {
                if (
                    double.TryParse(
                        line.Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double cap
                    )
                    && cap >= 8.9
                )
                    return true;
            }

            logger.LogInformation(
                "AV1 NVENC unavailable — every Nvidia GPU on the host is below compute capability 8.9 (Ada Lovelace). The encoder block does not exist on Turing / Ampere silicon."
            );
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(
                ex,
                "nvidia-smi compute_cap query threw — assuming AV1 NVENC available"
            );
            return true;
        }
    }
}
