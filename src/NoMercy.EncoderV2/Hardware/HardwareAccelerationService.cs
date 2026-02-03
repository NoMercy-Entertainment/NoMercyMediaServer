using NoMercy.NmSystem.Capabilities;

namespace NoMercy.EncoderV2.Hardware;

/// <summary>
/// Service for detecting and managing hardware acceleration
/// Wraps GPU detection with dependency injection
/// </summary>
public class HardwareAccelerationService : IHardwareAccelerationService
{
    private readonly List<GpuAccelerator> _cachedAccelerators;
    private static bool _detectorInitialized = false;
    private static readonly object _initLock = new();

    public HardwareAccelerationService()
    {
        // Initialize FFmpegAccelerationDetector if not already done
        if (!_detectorInitialized)
        {
            lock (_initLock)
            {
                if (!_detectorInitialized)
                {
                    string ffmpegPath = "ffmpeg"; // Will use ffmpeg from PATH
                    _ = new FFmpegAccelerationDetector(ffmpegPath);
                    _detectorInitialized = true;
                }
            }
        }

        _cachedAccelerators = FFmpegAccelerationDetector.Accelerators;
    }

    public List<GpuAccelerator> GetAvailableAccelerators()
    {
        return _cachedAccelerators;
    }

    public GpuAccelerator? GetBestAcceleratorForCodec(string codec)
    {
        string normalizedCodec = codec.ToLower();

        return _cachedAccelerators
            .Where(acc => SupportsCodec(acc, normalizedCodec))
            .OrderByDescending(acc => GetAcceleratorPriority(acc))
            .FirstOrDefault();
    }

    public bool IsHardwareAccelerationAvailable()
    {
        return _cachedAccelerators.Count > 0;
    }

    public string GetRecommendedVideoCodec(string requestedCodec)
    {
        string normalizedCodec = requestedCodec.ToLower();

        GpuAccelerator? accelerator = GetBestAcceleratorForCodec(normalizedCodec);

        if (accelerator == null)
        {
            return GetSoftwareCodec(normalizedCodec);
        }

        return GetHardwareCodec(normalizedCodec, accelerator);
    }

    private bool SupportsCodec(GpuAccelerator accelerator, string codec)
    {
        return codec switch
        {
            "h264" or "libx264" => accelerator.Vendor != GpuVendor.Unknown,
            "h265" or "hevc" or "libx265" => accelerator.Vendor != GpuVendor.Unknown,
            "vp9" or "libvpx-vp9" => accelerator.Vendor == GpuVendor.Nvidia,
            "av1" or "libaom-av1" => accelerator.Vendor == GpuVendor.Nvidia || accelerator.Vendor == GpuVendor.Intel,
            _ => false
        };
    }

    private int GetAcceleratorPriority(GpuAccelerator accelerator)
    {
        return accelerator.Vendor switch
        {
            GpuVendor.Nvidia => 3,
            GpuVendor.Intel => 2,
            GpuVendor.Amd => 1,
            _ => 0
        };
    }

    private string GetSoftwareCodec(string codec)
    {
        return codec switch
        {
            "h264" or "h264_nvenc" or "h264_qsv" or "h264_amf" or "h264_videotoolbox" => "libx264",
            "h265" or "hevc" or "hevc_nvenc" or "hevc_qsv" or "hevc_amf" or "hevc_videotoolbox" => "libx265",
            "vp9" or "vp9_nvenc" => "libvpx-vp9",
            "av1" or "av1_nvenc" => "libaom-av1",
            _ => "libx264"
        };
    }

    private string GetHardwareCodec(string codec, GpuAccelerator accelerator)
    {
        string baseCodec = codec.Contains("265") || codec.Contains("hevc") ? "hevc" : "h264";

        return accelerator.Vendor switch
        {
            GpuVendor.Nvidia => $"{baseCodec}_nvenc",
            GpuVendor.Intel => $"{baseCodec}_qsv",
            GpuVendor.Amd => $"{baseCodec}_amf",
            GpuVendor.Apple => $"{baseCodec}_videotoolbox",
            _ => GetSoftwareCodec(codec)
        };
    }
}
