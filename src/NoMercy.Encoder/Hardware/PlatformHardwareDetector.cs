namespace NoMercy.Encoder.Hardware;

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Storage;

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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return await DetectWindowsGpusAsync(ct);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return await DetectLinuxGpusAsync(ct);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return await DetectMacGpusAsync(ct);

            logger.LogWarning(
                "GPU detection not supported on {OS}",
                RuntimeInformation.OSDescription
            );
            return [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "GPU detection failed");
            return [];
        }
    }

    public Task<int> DetectCpuCoreCountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Environment.ProcessorCount);
    }

    private async Task<IReadOnlyList<GpuDevice>> DetectWindowsGpusAsync(CancellationToken ct)
    {
        // Try wmic first (faster, works on every Windows 10/11 build through
        // 22H2). wmic is deprecated and removed by default on Windows 11
        // 24H2+ — when wmic.exe is missing the runner returns an exit code
        // and we fall back to PowerShell Get-CimInstance, which is always
        // available. Without the fallback users on modern Windows see zero
        // detected GPUs and the hardware benchmark only ever tests the CPU.
        ProcessResult result = await processRunner.RunAsync(
            "wmic",
            [
                "path",
                "Win32_VideoController",
                "get",
                "Name,AdapterRAM,DriverVersion",
                "/format:csv",
            ],
            null,
            ct
        );

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StdOut))
        {
            logger.LogInformation(
                "wmic GPU detection unavailable (exit {Code}) — falling back to PowerShell Get-CimInstance",
                result.ExitCode
            );
            IReadOnlyList<GpuDevice> psDevices = await DetectWindowsGpusViaPowerShellAsync(ct);
            if (psDevices.Count > 0)
                return psDevices;
            logger.LogWarning(
                "Both wmic and PowerShell GPU detection returned no devices on Windows. Hardware benchmark will run CPU-only."
            );
            return [];
        }

        List<GpuDevice> devices = [];

        // Track header column positions from first non-empty header line
        int nameIndex = -1;
        int ramIndex = -1;
        int driverIndex = -1;
        bool headerParsed = false;

        foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            string[] parts = trimmed.Split(',');

            if (!headerParsed)
            {
                // Header row: Node,AdapterRAM,DriverVersion,Name (order varies by wmic version)
                for (int i = 0; i < parts.Length; i++)
                {
                    string col = parts[i].Trim();
                    if (col.Equals("Name", StringComparison.OrdinalIgnoreCase))
                        nameIndex = i;
                    else if (col.Equals("AdapterRAM", StringComparison.OrdinalIgnoreCase))
                        ramIndex = i;
                    else if (col.Equals("DriverVersion", StringComparison.OrdinalIgnoreCase))
                        driverIndex = i;
                }

                if (nameIndex >= 0 && ramIndex >= 0)
                {
                    headerParsed = true;
                }
                else if (trimmed.StartsWith("Node", StringComparison.Ordinal))
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

            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (string.IsNullOrWhiteSpace(driverVersion))
                driverVersion = null;

            _ = long.TryParse(adapterRamStr, out long adapterRamBytes);
            long vramMb = adapterRamBytes / (1024 * 1024);

            GpuDevice? device = BuildGpuDevice(name, vramMb, driverVersion);
            if (device is not null)
                devices.Add(device);
        }

        // Some wmic builds on Windows 11 return an empty stdout instead of an
        // error code. Treat empty as "wmic broken" and try PowerShell too.
        if (devices.Count == 0)
        {
            logger.LogInformation(
                "wmic returned 0 GPUs — falling back to PowerShell Get-CimInstance"
            );
            IReadOnlyList<GpuDevice> psDevices = await DetectWindowsGpusViaPowerShellAsync(ct);
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
            "powershell",
            ["-NoProfile", "-NonInteractive", "-Command", Script],
            null,
            ct
        );

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StdOut))
        {
            logger.LogWarning(
                "PowerShell Get-CimInstance failed (exit {Code}): {Err}",
                result.ExitCode,
                result.StdErr
            );
            return [];
        }

        List<GpuDevice> devices = [];
        foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Trim().Split('|');
            if (parts.Length < 2)
                continue;

            string name = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            _ = long.TryParse(parts[1].Trim(), out long adapterRamBytes);
            long vramMb = adapterRamBytes / (1024 * 1024);
            string? driverVersion =
                parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : null;

            GpuDevice? device = BuildGpuDevice(name, vramMb, driverVersion);
            if (device is not null)
                devices.Add(device);
        }
        return devices;
    }

    private async Task<IReadOnlyList<GpuDevice>> DetectLinuxGpusAsync(CancellationToken ct)
    {
        // Check for render nodes first — no /dev/dri means no GPU acceleration
        bool hasDri = storage.Exists("/dev/dri");
        if (!hasDri)
        {
            logger.LogInformation("No /dev/dri found — no GPU acceleration available");
            return [];
        }

        ProcessResult result = await processRunner.RunAsync("lspci", ["-nn"], null, ct);

        if (!result.IsSuccess)
        {
            logger.LogWarning("lspci failed (exit {Code}): {Err}", result.ExitCode, result.StdErr);
            return [];
        }

        List<GpuDevice> devices = [];

        foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Match VGA compatible controller or 3D controller lines
            Match match = VgaControllerPattern().Match(line);
            if (!match.Success)
                continue;

            string name = match.Groups["name"].Value.Trim();

            // Linux lspci doesn't report VRAM — estimate from name or use 0
            long vramMb = EstimateVramFromName(name);

            GpuDevice? device = BuildGpuDevice(name, vramMb);
            if (device is not null)
                devices.Add(device);
        }

        return devices;
    }

    private async Task<IReadOnlyList<GpuDevice>> DetectMacGpusAsync(CancellationToken ct)
    {
        ProcessResult result = await processRunner.RunAsync(
            "system_profiler",
            ["SPDisplaysDataType"],
            null,
            ct
        );

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "system_profiler failed (exit {Code}): {Err}",
                result.ExitCode,
                result.StdErr
            );
            return [];
        }

        List<GpuDevice> devices = [];
        string? currentChipset = null;
        long currentVramMb = 0;

        foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();

            // Chipset line: "Chipset Model: Apple M2 Pro" or "Chipset Model: AMD Radeon Pro 5500M"
            Match chipsetMatch = MacChipsetPattern().Match(trimmed);
            if (chipsetMatch.Success)
            {
                // Flush previous GPU if any
                if (currentChipset is not null)
                {
                    GpuDevice? prev = BuildGpuDevice(currentChipset, currentVramMb);
                    if (prev is not null)
                        devices.Add(prev);
                }

                currentChipset = chipsetMatch.Groups["name"].Value.Trim();
                currentVramMb = 0;
                continue;
            }

            // VRAM line: "VRAM (Total): 16 GB" or "VRAM (Dynamic, Max): 21845 MB"
            Match vramMatch = MacVramPattern().Match(trimmed);
            if (vramMatch.Success && currentChipset is not null)
            {
                if (long.TryParse(vramMatch.Groups["size"].Value, out long size))
                {
                    string unit = vramMatch.Groups["unit"].Value.ToUpperInvariant();
                    currentVramMb = unit == "GB" ? size * 1024 : size;
                }
            }
        }

        // Flush last GPU
        if (currentChipset is not null)
        {
            GpuDevice? last = BuildGpuDevice(currentChipset, currentVramMb);
            if (last is not null)
                devices.Add(last);
        }

        return devices;
    }

    private GpuDevice? BuildGpuDevice(string name, long vramMb, string? driverVersion = null)
    {
        GpuVendor? vendor = ClassifyVendor(name);
        if (vendor is null)
        {
            logger.LogDebug("Skipping non-GPU adapter: {Name}", name);
            return null;
        }

        List<VideoCodecType> supportedCodecs = DetectSupportedCodecs(vendor.Value);
        if (supportedCodecs.Count == 0)
        {
            logger.LogDebug(
                "GPU {Name} detected but no hardware encoders available in FFmpeg",
                name
            );
            return null;
        }

        int maxSessions = ResolveMaxSessions(vendor.Value, name);

        logger.LogInformation(
            "Detected GPU: {Vendor} {Name} ({VramMb}MB, {CodecCount} codecs, max {Sessions} sessions, driver {Driver})",
            vendor.Value,
            name,
            vramMb,
            supportedCodecs.Count,
            maxSessions,
            driverVersion ?? "unknown"
        );

        return new(vendor.Value, name, vramMb, maxSessions, supportedCodecs, driverVersion);
    }

    private List<VideoCodecType> DetectSupportedCodecs(GpuVendor vendor)
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

        foreach ((VideoCodecType codec, string[] encoderNames) in mappings)
        {
            foreach (string encoderName in encoderNames)
            {
                if (ffmpegCapabilities.HasEncoder(encoderName))
                {
                    codecs.Add(codec);
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
            upper.Contains("NVIDIA")
            || upper.Contains("GEFORCE")
            || upper.Contains("QUADRO")
            || upper.Contains("TESLA")
        )
            return GpuVendor.Nvidia;

        if (upper.Contains("AMD") || upper.Contains("RADEON") || upper.Contains("NAVI"))
            return GpuVendor.Amd;

        if (
            upper.Contains("INTEL")
            && (
                upper.Contains("ARC")
                || upper.Contains("UHD")
                || upper.Contains("IRIS")
                || upper.Contains("HD GRAPHICS")
            )
        )
            return GpuVendor.Intel;

        if (upper.Contains("APPLE") && AppleSiliconPattern().IsMatch(upper))
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
            upper.Contains("QUADRO")
            || upper.Contains("TESLA")
            || upper.Contains("RTX A")
            || upper.Contains("RTX L")
            || upper.Contains("A100")
            || upper.Contains("A40")
            || upper.Contains("A30")
            || upper.Contains("A16")
            || upper.Contains("A10")
            || upper.Contains("L40")
            || upper.Contains("H100")
            || upper.Contains("H200");

        if (isProfessional)
            return int.MaxValue;

        // Consumer GeForce/RTX — 8 concurrent sessions (driver-enforced)
        return 8;
    }

    private static long EstimateVramFromName(string name)
    {
        // Try to extract VRAM from name patterns like "16GB", "8 GB"
        Match match = VramPattern().Match(name);
        if (match.Success && int.TryParse(match.Groups["size"].Value, out int sizeGb))
            return sizeGb * 1024L;

        return 0;
    }

    [GeneratedRegex(
        @"(?:VGA compatible|3D) controller.*?:\s*(?<name>.+?)(?:\s+\[[\da-f]{4}:[\da-f]{4}\])?$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex VgaControllerPattern();

    [GeneratedRegex(@"(?<size>\d+)\s*GB", RegexOptions.IgnoreCase)]
    private static partial Regex VramPattern();

    // Matches "M1", "M2 Pro", "M3 Max", "M4 Ultra", "M17" etc — any Apple Silicon generation
    [GeneratedRegex(@"\bM\d+", RegexOptions.IgnoreCase)]
    private static partial Regex AppleSiliconPattern();

    [GeneratedRegex(@"^Chipset Model:\s*(?<name>.+)$")]
    private static partial Regex MacChipsetPattern();

    [GeneratedRegex(@"^VRAM\s*\([^)]*\):\s*(?<size>\d+)\s*(?<unit>MB|GB)", RegexOptions.IgnoreCase)]
    private static partial Regex MacVramPattern();
}
