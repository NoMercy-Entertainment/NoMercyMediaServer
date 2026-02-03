using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NoMercy.NmSystem.Capabilities;

/// <summary>
/// Information about an available FFmpeg hardware accelerator
/// </summary>
public class AcceleratorInfo
{
    public string Accelerator { get; set; } = string.Empty;
    public GpuVendor Vendor { get; set; }
    public bool IsAvailable { get; set; }
    public List<string> SupportedEncoders { get; set; } = [];
    public List<string> SupportedDecoders { get; set; } = [];
}

/// <summary>
/// Detects available FFmpeg hardware acceleration methods
/// </summary>
public class FFmpegAccelerationDetector
{
    private readonly string _ffmpegPath;
    private List<AcceleratorInfo>? _accelerators;

    public FFmpegAccelerationDetector(string ffmpegPath)
    {
        _ffmpegPath = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath;
    }

    /// <summary>
    /// Get all available accelerators
    /// </summary>
    public IReadOnlyList<AcceleratorInfo> Accelerators
    {
        get
        {
            _accelerators ??= DetectAccelerators();
            return _accelerators;
        }
    }

    private List<AcceleratorInfo> DetectAccelerators()
    {
        List<AcceleratorInfo> accelerators = [];

        try
        {
            // Query FFmpeg for available hardware acceleration methods
            ProcessStartInfo psi = new(_ffmpegPath)
            {
                Arguments = "-hide_banner -hwaccels",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return accelerators;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Parse hwaccels output
            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Skip the "Hardware acceleration methods:" header line
            foreach (string line in lines.Skip(1))
            {
                string accel = line.Trim();
                if (string.IsNullOrEmpty(accel)) continue;

                AcceleratorInfo info = new()
                {
                    Accelerator = accel,
                    Vendor = DetermineVendor(accel),
                    IsAvailable = true
                };

                // Detect supported encoders/decoders for this accelerator
                DetectCodecsForAccelerator(info);

                accelerators.Add(info);
            }
        }
        catch (Exception)
        {
            // FFmpeg not available or error running
        }

        return accelerators;
    }

    private static GpuVendor DetermineVendor(string accelerator)
    {
        return accelerator.ToLowerInvariant() switch
        {
            "cuda" or "nvdec" or "cuvid" => GpuVendor.Nvidia,
            "vaapi" or "vdpau" => GpuVendor.Amd, // Can also be Intel, but typically AMD on Linux
            "qsv" => GpuVendor.Intel,
            "videotoolbox" => GpuVendor.Apple,
            "d3d11va" or "dxva2" => GpuVendor.Unknown, // Windows generic
            _ => GpuVendor.Unknown
        };
    }

    private void DetectCodecsForAccelerator(AcceleratorInfo info)
    {
        // Based on the accelerator type, populate known encoders/decoders
        switch (info.Accelerator.ToLowerInvariant())
        {
            case "cuda":
                info.SupportedDecoders = ["h264_cuvid", "hevc_cuvid", "vp9_cuvid", "av1_cuvid"];
                info.SupportedEncoders = CheckEncoders(["h264_nvenc", "hevc_nvenc", "av1_nvenc"]);
                info.Vendor = GpuVendor.Nvidia;
                break;

            case "qsv":
                info.SupportedDecoders = ["h264_qsv", "hevc_qsv", "vp9_qsv", "av1_qsv"];
                info.SupportedEncoders = CheckEncoders(["h264_qsv", "hevc_qsv", "av1_qsv"]);
                info.Vendor = GpuVendor.Intel;
                break;

            case "vaapi":
                info.SupportedDecoders = ["h264_vaapi", "hevc_vaapi", "vp9_vaapi"];
                info.SupportedEncoders = CheckEncoders(["h264_vaapi", "hevc_vaapi", "av1_vaapi"]);
                // VAAPI can be either Intel or AMD
                break;

            case "videotoolbox":
                info.SupportedDecoders = ["h264_videotoolbox", "hevc_videotoolbox"];
                info.SupportedEncoders = CheckEncoders(["h264_videotoolbox", "hevc_videotoolbox"]);
                info.Vendor = GpuVendor.Apple;
                break;
        }
    }

    private List<string> CheckEncoders(List<string> encoders)
    {
        List<string> available = [];

        foreach (string encoder in encoders)
        {
            if (IsEncoderAvailable(encoder))
            {
                available.Add(encoder);
            }
        }

        return available;
    }

    private bool IsEncoderAvailable(string encoder)
    {
        try
        {
            ProcessStartInfo psi = new(_ffmpegPath)
            {
                Arguments = $"-hide_banner -h encoder={encoder}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if a specific accelerator is available
    /// </summary>
    public bool IsAcceleratorAvailable(string name)
    {
        return Accelerators.Any(a => a.Accelerator.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get the best available accelerator for a vendor
    /// </summary>
    public AcceleratorInfo? GetBestForVendor(GpuVendor vendor)
    {
        return Accelerators.FirstOrDefault(a => a.Vendor == vendor && a.IsAvailable);
    }
}
