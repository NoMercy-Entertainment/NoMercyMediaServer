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

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Storage;

namespace NoMercy.Encoder.Hardware;

public partial class PlatformHardwareDetector(
    IProcessRunner processRunner,
    IFfmpegCapabilities ffmpegCapabilities,
    ILogger<PlatformHardwareDetector> logger,
    IStorage storage
) : IHardwareDetector
{
    private const int DefaultMaxSessions = 8;

    public async Task<IReadOnlyList<GpuDevice>> DetectGpusAsync(CancellationToken ct = default)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
                return await DetectWindowsGpusAsync(ct: ct);

            if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
                return await DetectLinuxGpusAsync(ct: ct);

            if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
                return await DetectMacGpusAsync(ct: ct);

            logger.LogWarning(
                message: "GPU detection not supported on {OS}",
                args: RuntimeInformation.OSDescription
            );
            return [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(exception: ex, message: "GPU detection failed");
            return [];
        }
    }

    public Task<int> DetectCpuCoreCountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(result: Environment.ProcessorCount);
    }

    private async Task<IReadOnlyList<GpuDevice>> DetectWindowsGpusAsync(CancellationToken ct)
    {
        // Try wmic first (faster, works on every Windows 10/11 build through
        // 22H2). wmic is deprecated and removed by default on Windows 11
        // 24H2+ — when wmic.exe is missing, Process.Start throws a
        // Win32Exception (the runner never gets to return a ProcessResult),
        // so the launch itself must be guarded. Either way we fall back to
        // PowerShell Get-CimInstance, which is always available. Without the
        // fallback users on modern Windows see zero detected GPUs and the
        // hardware benchmark only ever tests the CPU.
        ProcessResult result;
        try
        {
            result = await processRunner.RunAsync(
                executable: "wmic",
                arguments:
                [
                    "path",
                    "Win32_VideoController",
                    "get",
                    "Name,AdapterRAM,DriverVersion",
                    "/format:csv",
                ],
                workingDirectory: null,
                cancellationToken: ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogInformation(
                exception: ex,
                message: "wmic could not be launched (removed on Windows 11 24H2+) — falling back to PowerShell Get-CimInstance"
            );
            IReadOnlyList<GpuDevice> psDevicesOnLaunchFailure =
                await DetectWindowsGpusViaPowerShellAsync(ct: ct);
            if (psDevicesOnLaunchFailure.Count > 0)
                return psDevicesOnLaunchFailure;
            logger.LogWarning(
                message: "Both wmic and PowerShell GPU detection returned no devices on Windows. Hardware benchmark will run CPU-only."
            );
            return [];
        }

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(value: result.StdOut))
        {
            logger.LogInformation(
                message: "wmic GPU detection unavailable (exit {Code}) — falling back to PowerShell Get-CimInstance",
                args: result.ExitCode
            );
            IReadOnlyList<GpuDevice> psDevices = await DetectWindowsGpusViaPowerShellAsync(ct: ct);
            if (psDevices.Count > 0)
                return psDevices;
            logger.LogWarning(
                message: "Both wmic and PowerShell GPU detection returned no devices on Windows. Hardware benchmark will run CPU-only."
            );
            return [];
        }

        List<GpuDevice> devices = [];

        // Track header column positions from first non-empty header line
        int nameIndex = -1;
        int ramIndex = -1;
        int driverIndex = -1;
        bool headerParsed = false;

        foreach (string line in result.StdOut.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(value: trimmed))
                continue;

            string[] parts = trimmed.Split(separator: ',');

            if (!headerParsed)
            {
                // Header row: Node,AdapterRAM,DriverVersion,Name (order varies by wmic version)
                for (int i = 0; i < parts.Length; i++)
                {
                    string col = parts[i].Trim();
                    if (col.Equals(value: "Name", comparisonType: StringComparison.OrdinalIgnoreCase))
                        nameIndex = i;
                    else if (col.Equals(value: "AdapterRAM", comparisonType: StringComparison.OrdinalIgnoreCase))
                        ramIndex = i;
                    else if (col.Equals(value: "DriverVersion", comparisonType: StringComparison.OrdinalIgnoreCase))
                        driverIndex = i;
                }

                if (nameIndex >= 0 && ramIndex >= 0)
                {
                    headerParsed = true;
                }
                else if (trimmed.StartsWith(value: "Node", comparisonType: StringComparison.Ordinal))
                {
                    // Old-style header without column discovery — fall back to positional
                    // CSV format: Node,AdapterRAM,DriverVersion,Name
                    ramIndex = 1;
                    driverIndex = 2;
                    nameIndex = 3;
                    headerParsed = true;
                }

                continue;
            }

            if (parts.Length <= nameIndex || parts.Length <= ramIndex)
                continue;

            string adapterRamStr = parts[ramIndex].Trim();
            string name = parts[nameIndex].Trim();
            string? driverVersion =
                driverIndex >= 0 && driverIndex < parts.Length ? parts[driverIndex].Trim() : null;

            if (string.IsNullOrWhiteSpace(value: name))
                continue;

            if (string.IsNullOrWhiteSpace(value: driverVersion))
                driverVersion = null;

            _ = long.TryParse(s: adapterRamStr, result: out long adapterRamBytes);
            long vramMb = adapterRamBytes / (1024 * 1024);

            GpuDevice? device = await BuildGpuDeviceAsync(name: name, vramMb: vramMb, driverVersion: driverVersion)
                .ConfigureAwait(continueOnCapturedContext: false);
            if (device is not null)
                devices.Add(item: device);
        }

        // Some wmic builds on Windows 11 return an empty stdout instead of an
        // error code. Treat empty as "wmic broken" and try PowerShell too.
        if (devices.Count == 0)
        {
            logger.LogInformation(
                message: "wmic returned 0 GPUs — falling back to PowerShell Get-CimInstance"
            );
            IReadOnlyList<GpuDevice> psDevices = await DetectWindowsGpusViaPowerShellAsync(ct: ct);
            if (psDevices.Count > 0)
                return psDevices;
        }

        return devices;
    }

    /// <summary>
    /// PowerShell <c>Get-CimInstance Win32_VideoController</c> fallback for
    /// Windows hosts where wmic is missing or broken (Windows 11 24H2+ removed
    /// wmic.exe by default). Emits one CSV line per GPU so we can reuse the
    /// same parser shape: <c>Name|AdapterRAM|DriverVersion</c>.
    /// </summary>
    private async Task<IReadOnlyList<GpuDevice>> DetectWindowsGpusViaPowerShellAsync(
        CancellationToken ct
    )
    {
        // -NoProfile avoids the user's PowerShell profile. ConvertTo-Csv keeps
        // the output trivially parseable. We pick a pipe delimiter so commas
        // in driver-version strings (rare but possible) don't break the split.
        const string Script =
            "Get-CimInstance Win32_VideoController | "
            + "Select-Object Name,AdapterRAM,DriverVersion | "
            + "ForEach-Object { \"$($_.Name)|$($_.AdapterRAM)|$($_.DriverVersion)\" }";

        ProcessResult result = await processRunner.RunAsync(
            executable: "powershell",
            arguments: ["-NoProfile", "-NonInteractive", "-Command", Script],
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(value: result.StdOut))
        {
            logger.LogWarning(
                message: "PowerShell Get-CimInstance failed (exit {Code}): {Err}", args: [result.ExitCode, result.StdErr]
            );
            return [];
        }

        List<GpuDevice> devices = [];
        foreach (string line in result.StdOut.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Trim().Split(separator: '|');
            if (parts.Length < 2)
                continue;

            string name = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(value: name))
                continue;

            _ = long.TryParse(s: parts[1].Trim(), result: out long adapterRamBytes);
            long vramMb = adapterRamBytes / (1024 * 1024);
            string? driverVersion =
                parts.Length >= 3 && !string.IsNullOrWhiteSpace(value: parts[2]) ? parts[2].Trim() : null;

            GpuDevice? device = await BuildGpuDeviceAsync(name: name, vramMb: vramMb, driverVersion: driverVersion)
                .ConfigureAwait(continueOnCapturedContext: false);
            if (device is not null)
                devices.Add(item: device);
        }
        return devices;
    }

    /// <summary>
    /// Internal (not private) so <c>NoMercy.Tests.Encoder</c> can exercise
    /// the Linux detection path directly on any host OS via
    /// <c>InternalsVisibleTo</c>, instead of being gated behind the
    /// <see cref="RuntimeInformation"/> check in <see cref="DetectGpusAsync"/>.
    /// </summary>
    internal async Task<IReadOnlyList<GpuDevice>> DetectLinuxGpusAsync(CancellationToken ct)
    {
        // NVENC does not require a DRM render node — only the VAAPI/QSV path
        // used by Intel/AMD genuinely needs /dev/dri. Probe NVIDIA via
        // nvidia-smi independently of /dev/dri so the common Docker case is
        // detected: nvidia-container-toolkit's `--gpus all` mounts
        // /dev/nvidia0, /dev/nvidiactl, /dev/nvidia-uvm but never /dev/dri.
        // The old dri-gated early return silently dropped NVENC there and
        // fell back to CPU-only encoding.
        List<GpuDevice> nvidiaDevices = await DetectNvidiaGpusAsync(ct: ct).ConfigureAwait(continueOnCapturedContext: false);

        bool hasDri = storage.Exists(path: "/dev/dri");

        if (!hasDri)
        {
            if (nvidiaDevices.Count > 0)
                logger.LogInformation(
                    message: "NVIDIA GPU detected via nvidia-smi without /dev/dri — NVENC available (nvidia-container-toolkit Docker case)"
                );
            else
                logger.LogInformation(
                    message: "No /dev/dri and no NVIDIA GPU found via nvidia-smi — no GPU acceleration available"
                );

            return nvidiaDevices;
        }

        ProcessResult result = await processRunner.RunAsync(executable: "lspci", arguments: ["-nn"], workingDirectory: null, cancellationToken: ct);

        if (!result.IsSuccess)
        {
            logger.LogWarning(message: "lspci failed (exit {Code}): {Err}", args: [result.ExitCode, result.StdErr]);
            return nvidiaDevices;
        }

        List<GpuDevice> devices = [.. nvidiaDevices];

        foreach (string line in result.StdOut.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries))
        {
            // Match VGA compatible controller or 3D controller lines
            Match match = VgaControllerPattern().Match(input: line);
            if (!match.Success)
                continue;

            string name = match.Groups[groupname: "name"].Value.Trim();

            // Already captured this card via nvidia-smi (which gives real
            // VRAM + driver version) — don't double-count it from lspci.
            if (nvidiaDevices.Count > 0 && ClassifyVendor(name: name) == GpuVendor.Nvidia)
                continue;

            // Linux lspci doesn't report VRAM — estimate from name or use 0
            long vramMb = EstimateVramFromName(name: name);

            GpuDevice? device = await BuildGpuDeviceAsync(name: name, vramMb: vramMb).ConfigureAwait(continueOnCapturedContext: false);
            if (device is not null)
                devices.Add(item: device);
        }

        return devices;
    }

    /// <summary>
    /// Probes NVIDIA GPUs via <c>nvidia-smi</c> — the correct NVIDIA signal
    /// on Linux, since NVENC does not require a DRM render node the way
    /// VAAPI does. <c>Av1CapabilityProbe</c> already shells nvidia-smi for
    /// compute_cap, so nvidia-smi availability is the established NVIDIA
    /// detection signal in this codebase.
    /// </summary>
    private async Task<List<GpuDevice>> DetectNvidiaGpusAsync(CancellationToken ct)
    {
        ProcessResult result;
        try
        {
            result = await processRunner
                .RunAsync(
                    executable: "nvidia-smi",
                    arguments:
                    [
                        "--query-gpu=name,memory.total,driver_version",
                        "--format=csv,noheader,nounits",
                    ],
                    workingDirectory: null,
                    cancellationToken: ct
                )
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(exception: ex, message: "nvidia-smi not available — skipping NVIDIA NVENC probe");
            return [];
        }

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(value: result.StdOut))
        {
            // Secondary hint: NVIDIA device nodes exist but nvidia-smi failed
            // (driver mismatch, missing binary in a minimal container, etc).
            // Log it — this is the "GPU is there but we can't see it" case.
            if (storage.Exists(path: "/dev/nvidia0") || storage.Exists(path: "/dev/nvidiactl"))
                logger.LogWarning(
                    message: "NVIDIA device nodes present (/dev/nvidia0 or /dev/nvidiactl) but nvidia-smi query failed (exit {Code}) — NVENC detection skipped. Verify the NVIDIA driver / nvidia-smi is available inside the container.",
                    args: result.ExitCode
                );

            return [];
        }

        List<GpuDevice> devices = [];

        foreach (string line in result.StdOut.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split(separator: ',');
            string name = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(value: name))
                continue;

            long vramMb = parts.Length >= 2 && long.TryParse(s: parts[1].Trim(), result: out long mb) ? mb : 0;
            string? driverVersion =
                parts.Length >= 3 && !string.IsNullOrWhiteSpace(value: parts[2]) ? parts[2].Trim() : null;

            GpuDevice? device = await BuildGpuDeviceAsync(name: name, vramMb: vramMb, driverVersion: driverVersion)
                .ConfigureAwait(continueOnCapturedContext: false);
            if (device is not null)
                devices.Add(item: device);
        }

        return devices;
    }

    private async Task<IReadOnlyList<GpuDevice>> DetectMacGpusAsync(CancellationToken ct)
    {
        ProcessResult result = await processRunner.RunAsync(
            executable: "system_profiler",
            arguments: ["SPDisplaysDataType"],
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                message: "system_profiler failed (exit {Code}): {Err}", args: [result.ExitCode, result.StdErr]
            );
            return [];
        }

        List<GpuDevice> devices = [];
        string? currentChipset = null;
        long currentVramMb = 0;

        foreach (string line in result.StdOut.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();

            // Chipset line: "Chipset Model: Apple M2 Pro" or "Chipset Model: AMD Radeon Pro 5500M"
            Match chipsetMatch = MacChipsetPattern().Match(input: trimmed);
            if (chipsetMatch.Success)
            {
                // Flush previous GPU if any
                if (currentChipset is not null)
                {
                    GpuDevice? prev = await BuildGpuDeviceAsync(name: currentChipset, vramMb: currentVramMb)
                        .ConfigureAwait(continueOnCapturedContext: false);
                    if (prev is not null)
                        devices.Add(item: prev);
                }

                currentChipset = chipsetMatch.Groups[groupname: "name"].Value.Trim();
                currentVramMb = 0;
                continue;
            }

            // VRAM line: "VRAM (Total): 16 GB" or "VRAM (Dynamic, Max): 21845 MB"
            Match vramMatch = MacVramPattern().Match(input: trimmed);
            if (vramMatch.Success && currentChipset is not null)
            {
                if (long.TryParse(s: vramMatch.Groups[groupname: "size"].Value, result: out long size))
                {
                    string unit = vramMatch.Groups[groupname: "unit"].Value.ToUpperInvariant();
                    currentVramMb = unit == "GB" ? size * 1024 : size;
                }
            }
        }

        // Flush last GPU
        if (currentChipset is not null)
        {
            GpuDevice? last = await BuildGpuDeviceAsync(name: currentChipset, vramMb: currentVramMb)
                .ConfigureAwait(continueOnCapturedContext: false);
            if (last is not null)
                devices.Add(item: last);
        }

        return devices;
    }

    private async Task<GpuDevice?> BuildGpuDeviceAsync(
        string name,
        long vramMb,
        string? driverVersion = null
    )
    {
        GpuVendor? vendor = ClassifyVendor(name: name);
        if (vendor is null)
        {
            logger.LogDebug(message: "Skipping non-GPU adapter: {Name}", args: name);
            return null;
        }

        List<VideoCodecType> supportedCodecs = await DetectSupportedCodecsAsync(vendor: vendor.Value, gpuName: name)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (supportedCodecs.Count == 0)
        {
            logger.LogDebug(
                message: "GPU {Name} detected but no hardware encoders available in FFmpeg",
                args: name
            );
            return null;
        }

        int maxSessions = ResolveMaxSessions(vendor: vendor.Value, name: name);

        return new(Vendor: vendor.Value, Name: name, VramMb: vramMb, MaxEncoderSessions: maxSessions, SupportedCodecs: supportedCodecs, DriverVersion: driverVersion);
    }

    private async Task<List<VideoCodecType>> DetectSupportedCodecsAsync(
        GpuVendor vendor,
        string gpuName
    )
    {
        List<VideoCodecType> codecs = [];

        (VideoCodecType codec, string[] encoderNames)[] mappings = vendor switch
        {
            GpuVendor.Nvidia =>
            [
                (VideoCodecType.H264, ["h264_nvenc"]),
                (VideoCodecType.H265, ["hevc_nvenc"]),
                (VideoCodecType.Av1, ["av1_nvenc"]),
            ],
            GpuVendor.Amd =>
            [
                (VideoCodecType.H264, ["h264_amf"]),
                (VideoCodecType.H265, ["hevc_amf"]),
                (VideoCodecType.Av1, ["av1_amf"]),
            ],
            GpuVendor.Intel =>
            [
                (VideoCodecType.H264, ["h264_qsv", "h264_vaapi"]),
                (VideoCodecType.H265, ["hevc_qsv", "hevc_vaapi"]),
                (VideoCodecType.Av1, ["av1_qsv", "av1_vaapi"]),
                (VideoCodecType.Vp9, ["vp9_qsv", "vp9_vaapi"]),
            ],
            GpuVendor.Apple =>
            [
                (VideoCodecType.H264, ["h264_videotoolbox"]),
                (VideoCodecType.H265, ["hevc_videotoolbox"]),
            ],
            _ => [],
        };

        // Gate AV1 hardware encoding per vendor. ffmpeg ships every encoder
        // (av1_nvenc / av1_amf / av1_qsv / av1_vaapi) against any driver that
        // knows about the codec name, but the physical encoder block only
        // exists on specific GPU generations:
        //
        //   Nvidia: Ada Lovelace+         (RTX 4000+, L4/L40, RTX 6000 Ada)
        //   AMD:    RDNA 3+               (RX 7000, RX 9000, W7700+)
        //   Intel:  Arc Alchemist+        (Arc A/B series, Xe-LPG/Xe2 iGPU)
        //
        // Probing on cards without the silicon fails at codec init, the
        // benchmark gets nothing, and the dashboard shows a confusing
        // missing-row gap. Each vendor uses its own detection signal:
        //   Nvidia → nvidia-smi compute_cap (clean numeric query)
        //   AMD/Intel → GPU name pattern (no equivalent CLI)
        // For unknown / future GPU SKUs we default to "allowed" so the
        // probe-and-log fallback covers any pattern miss — a real card
        // without AV1 support emits a visible Information-level log.
        bool gpuSupportsAv1 = await new Av1CapabilityProbe(processRunner: processRunner, logger: logger)
            .SupportsAsync(vendor: vendor, gpuName: gpuName)
            .ConfigureAwait(continueOnCapturedContext: false);

        foreach ((VideoCodecType codec, string[] encoderNames) in mappings)
        {
            if (codec == VideoCodecType.Av1 && !gpuSupportsAv1)
                continue;

            foreach (string encoderName in encoderNames)
            {
                if (ffmpegCapabilities.HasEncoder(name: encoderName))
                {
                    codecs.Add(item: codec);
                    break;
                }
            }
        }

        return codecs;
    }

    private static GpuVendor? ClassifyVendor(string name)
    {
        string upper = name.ToUpperInvariant();

        if (
            upper.Contains(value: "NVIDIA")
            || upper.Contains(value: "GEFORCE")
            || upper.Contains(value: "QUADRO")
            || upper.Contains(value: "TESLA")
        )
            return GpuVendor.Nvidia;

        if (upper.Contains(value: "AMD") || upper.Contains(value: "RADEON") || upper.Contains(value: "NAVI"))
            return GpuVendor.Amd;

        if (
            upper.Contains(value: "INTEL")
            && (
                upper.Contains(value: "ARC")
                || upper.Contains(value: "UHD")
                || upper.Contains(value: "IRIS")
                || upper.Contains(value: "HD GRAPHICS")
            )
        )
            return GpuVendor.Intel;

        if (upper.Contains(value: "APPLE") && AppleSiliconPattern().IsMatch(input: upper))
            return GpuVendor.Apple;

        return null;
    }

    /// <summary>
    /// NVENC session limits are driver-enforced and not queryable via standard APIs.
    /// Professional cards (Quadro, Tesla, A-series, RTX A/L-series) have no limit.
    /// Consumer cards: 8 sessions (post-2024 driver update, up from 5).
    /// </summary>
    private static int ResolveMaxSessions(GpuVendor vendor, string name)
    {
        if (vendor != GpuVendor.Nvidia)
            return DefaultMaxSessions;

        string upper = name.ToUpperInvariant();

        // Professional/datacenter cards — unlimited sessions
        bool isProfessional =
            upper.Contains(value: "QUADRO")
            || upper.Contains(value: "TESLA")
            || upper.Contains(value: "RTX A")
            || upper.Contains(value: "RTX L")
            || upper.Contains(value: "A100")
            || upper.Contains(value: "A40")
            || upper.Contains(value: "A30")
            || upper.Contains(value: "A16")
            || upper.Contains(value: "A10")
            || upper.Contains(value: "L40")
            || upper.Contains(value: "H100")
            || upper.Contains(value: "H200");

        if (isProfessional)
            return int.MaxValue;

        // Consumer GeForce/RTX — 8 concurrent sessions (driver-enforced)
        return 8;
    }

    private static long EstimateVramFromName(string name)
    {
        // Try to extract VRAM from name patterns like "16GB", "8 GB"
        Match match = VramPattern().Match(input: name);
        if (match.Success && int.TryParse(s: match.Groups[groupname: "size"].Value, result: out int sizeGb))
            return sizeGb * 1024L;

        return 0;
    }

    [GeneratedRegex(
        pattern: @"(?:VGA compatible|3D) controller.*?:\s*(?<name>.+?)(?:\s+\[[\da-f]{4}:[\da-f]{4}\])?$",
        options: RegexOptions.IgnoreCase
    )]
    private static partial Regex VgaControllerPattern();

    [GeneratedRegex(pattern: @"(?<size>\d+)\s*GB", options: RegexOptions.IgnoreCase)]
    private static partial Regex VramPattern();

    // Matches "M1", "M2 Pro", "M3 Max", "M4 Ultra", "M17" etc — any Apple Silicon generation
    [GeneratedRegex(pattern: @"\bM\d+", options: RegexOptions.IgnoreCase)]
    private static partial Regex AppleSiliconPattern();

    [GeneratedRegex(pattern: @"^Chipset Model:\s*(?<name>.+)$")]
    private static partial Regex MacChipsetPattern();

    [GeneratedRegex(pattern: @"^VRAM\s*\([^)]*\):\s*(?<size>\d+)\s*(?<unit>MB|GB)", options: RegexOptions.IgnoreCase)]
    private static partial Regex MacVramPattern();
}
