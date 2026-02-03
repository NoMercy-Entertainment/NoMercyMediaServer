using System.Runtime.InteropServices;
using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Services;

/// <summary>
/// Detects available hardware acceleration options
/// </summary>
public sealed class HardwareAccelerationDetector : IHardwareAccelerationDetector
{
    private readonly IFFmpegExecutor _executor;
    private IReadOnlyList<HardwareAcceleration>? _cachedAccelerators;
    private readonly SemaphoreSlim _detectionLock = new(1, 1);

    public HardwareAccelerationDetector(IFFmpegExecutor executor)
    {
        _executor = executor;
    }

    public async Task<IReadOnlyList<HardwareAcceleration>> GetAvailableAcceleratorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedAccelerators != null)
        {
            return _cachedAccelerators;
        }

        await _detectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedAccelerators != null)
            {
                return _cachedAccelerators;
            }

            List<HardwareAcceleration> available = [];

            // Check each acceleration type
            HardwareAcceleration[] accelerationsToCheck = GetPlatformAccelerations();

            foreach (HardwareAcceleration accel in accelerationsToCheck)
            {
                if (await CheckAccelerationAsync(accel, cancellationToken))
                {
                    available.Add(accel);
                }
            }

            _cachedAccelerators = available;
            return available;
        }
        finally
        {
            _detectionLock.Release();
        }
    }

    public async Task<bool> IsAvailableAsync(HardwareAcceleration acceleration, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HardwareAcceleration> available = await GetAvailableAcceleratorsAsync(cancellationToken);
        return available.Contains(acceleration);
    }

    public async Task<HardwareAcceleration?> GetRecommendedAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HardwareAcceleration> available = await GetAvailableAcceleratorsAsync(cancellationToken);

        if (available.Count == 0)
        {
            return null;
        }

        // Priority order based on performance and compatibility
        HardwareAcceleration[] priorityOrder;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            priorityOrder = [HardwareAcceleration.Nvenc, HardwareAcceleration.Qsv, HardwareAcceleration.Amf, HardwareAcceleration.Dxva2];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            priorityOrder = [HardwareAcceleration.Nvenc, HardwareAcceleration.Vaapi, HardwareAcceleration.Qsv];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            priorityOrder = [HardwareAcceleration.VideoToolbox];
        }
        else
        {
            priorityOrder = [HardwareAcceleration.Cuda, HardwareAcceleration.Nvenc];
        }

        foreach (HardwareAcceleration accel in priorityOrder)
        {
            if (available.Contains(accel))
            {
                return accel;
            }
        }

        return available.FirstOrDefault();
    }

    private static HardwareAcceleration[] GetPlatformAccelerations()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return [HardwareAcceleration.Cuda, HardwareAcceleration.Nvenc, HardwareAcceleration.Qsv, HardwareAcceleration.Amf, HardwareAcceleration.Dxva2];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return [HardwareAcceleration.Cuda, HardwareAcceleration.Nvenc, HardwareAcceleration.Vaapi, HardwareAcceleration.Qsv];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return [HardwareAcceleration.VideoToolbox];
        }

        return [];
    }

    private async Task<bool> CheckAccelerationAsync(HardwareAcceleration acceleration, CancellationToken cancellationToken)
    {
        string? hwaccel = GetHwaccelName(acceleration);
        if (hwaccel == null) return false;

        try
        {
            // Use FFmpeg to check if the hardware acceleration is available
            string arguments = $"-hide_banner -hwaccels";
            FFmpegResult result = await _executor.ExecuteSilentAsync(arguments, cancellationToken);

            if (!result.Success)
            {
                return false;
            }

            string output = result.StandardOutput + result.StandardError;
            return output.Contains(hwaccel, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetHwaccelName(HardwareAcceleration acceleration)
    {
        return acceleration switch
        {
            HardwareAcceleration.Cuda => "cuda",
            HardwareAcceleration.Nvenc => "cuda", // NVENC uses CUDA hwaccel
            HardwareAcceleration.Qsv => "qsv",
            HardwareAcceleration.Vaapi => "vaapi",
            HardwareAcceleration.VideoToolbox => "videotoolbox",
            HardwareAcceleration.Amf => "amf",
            HardwareAcceleration.Dxva2 => "dxva2",
            _ => null
        };
    }

    /// <summary>
    /// Gets the FFmpeg encoder name for a codec with hardware acceleration
    /// </summary>
    public static string? GetHardwareEncoder(string baseCodec, HardwareAcceleration acceleration)
    {
        return (baseCodec.ToLowerInvariant(), acceleration) switch
        {
            ("h264" or "libx264", HardwareAcceleration.Nvenc) => "h264_nvenc",
            ("h264" or "libx264", HardwareAcceleration.Qsv) => "h264_qsv",
            ("h264" or "libx264", HardwareAcceleration.Vaapi) => "h264_vaapi",
            ("h264" or "libx264", HardwareAcceleration.VideoToolbox) => "h264_videotoolbox",
            ("h264" or "libx264", HardwareAcceleration.Amf) => "h264_amf",

            ("h265" or "hevc" or "libx265", HardwareAcceleration.Nvenc) => "hevc_nvenc",
            ("h265" or "hevc" or "libx265", HardwareAcceleration.Qsv) => "hevc_qsv",
            ("h265" or "hevc" or "libx265", HardwareAcceleration.Vaapi) => "hevc_vaapi",
            ("h265" or "hevc" or "libx265", HardwareAcceleration.VideoToolbox) => "hevc_videotoolbox",
            ("h265" or "hevc" or "libx265", HardwareAcceleration.Amf) => "hevc_amf",

            ("av1" or "libaom-av1" or "libsvtav1", HardwareAcceleration.Nvenc) => "av1_nvenc",
            ("av1" or "libaom-av1" or "libsvtav1", HardwareAcceleration.Qsv) => "av1_qsv",
            ("av1" or "libaom-av1" or "libsvtav1", HardwareAcceleration.Vaapi) => "av1_vaapi",

            _ => null
        };
    }

    /// <summary>
    /// Gets the FFmpeg hardware acceleration input arguments
    /// </summary>
    public static IReadOnlyList<string> GetHwaccelInputArgs(HardwareAcceleration acceleration)
    {
        return acceleration switch
        {
            HardwareAcceleration.Cuda or HardwareAcceleration.Nvenc =>
                ["-hwaccel", "cuda", "-hwaccel_output_format", "cuda", "-extra_hw_frames", "8"],
            HardwareAcceleration.Qsv =>
                ["-hwaccel", "qsv", "-hwaccel_output_format", "qsv"],
            HardwareAcceleration.Vaapi =>
                ["-hwaccel", "vaapi", "-hwaccel_device", "/dev/dri/renderD128", "-hwaccel_output_format", "vaapi"],
            HardwareAcceleration.VideoToolbox =>
                ["-hwaccel", "videotoolbox"],
            HardwareAcceleration.Dxva2 =>
                ["-hwaccel", "dxva2"],
            HardwareAcceleration.Amf =>
                ["-hwaccel", "amf"],
            _ => []
        };
    }
}
