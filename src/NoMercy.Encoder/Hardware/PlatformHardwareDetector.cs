namespace NoMercy.Encoder.Hardware;

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Infrastructure;

public partial class PlatformHardwareDetector(
    IProcessRunner processRunner,
    IFfmpegCapabilities ffmpegCapabilities,
    ILogger<PlatformHardwareDetector> logger
) : IHardwareDetector
{
    private const int NvidiaConsumerMaxSessions = 12;
    private const int DefaultMaxSessions = 8;

    public async Task<IReadOnlyList<GpuDevice>> DetectGpusAsync(CancellationToken ct = default)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return await DetectWindowsGpusAsync(ct);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return await DetectLinuxGpusAsync(ct);

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
        ProcessResult result = await processRunner.RunAsync(
            "wmic",
            ["path", "Win32_VideoController", "get", "Name,AdapterRAM", "/format:csv"],
            null,
            ct
        );

        if (!result.IsSuccess)
        {
            logger.LogWarning("wmic failed (exit {Code}): {Err}", result.ExitCode, result.StdErr);
            return [];
        }

        List<GpuDevice> devices = [];

        foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (
                string.IsNullOrWhiteSpace(trimmed)
                || trimmed.StartsWith("Node", StringComparison.Ordinal)
            )
                continue;

            // CSV format: Node,AdapterRAM,Name
            string[] parts = trimmed.Split(',');
            if (parts.Length < 3)
                continue;

            string adapterRamStr = parts[1].Trim();
            string name = string.Join(",", parts[2..]).Trim();

            if (string.IsNullOrWhiteSpace(name))
                continue;

            _ = long.TryParse(adapterRamStr, out long adapterRamBytes);
            long vramMb = adapterRamBytes / (1024 * 1024);

            GpuDevice? device = BuildGpuDevice(name, vramMb);
            if (device is not null)
                devices.Add(device);
        }

        return devices;
    }

    private async Task<IReadOnlyList<GpuDevice>> DetectLinuxGpusAsync(CancellationToken ct)
    {
        // Check for render nodes first — no /dev/dri means no GPU acceleration
        bool hasDri = Directory.Exists("/dev/dri");
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

    private GpuDevice? BuildGpuDevice(string name, long vramMb)
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

        int maxSessions =
            vendor.Value == GpuVendor.Nvidia ? NvidiaConsumerMaxSessions : DefaultMaxSessions;

        logger.LogInformation(
            "Detected GPU: {Vendor} {Name} ({VramMb}MB, {CodecCount} codecs, max {Sessions} sessions)",
            vendor.Value,
            name,
            vramMb,
            supportedCodecs.Count,
            maxSessions
        );

        return new GpuDevice(vendor.Value, name, vramMb, maxSessions, supportedCodecs);
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
                (VideoCodecType.H264, ["h264_qsv"]),
                (VideoCodecType.H265, ["hevc_qsv"]),
                (VideoCodecType.Av1, ["av1_qsv"]),
                (VideoCodecType.Vp9, ["vp9_qsv"]),
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

        return null;
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
}
