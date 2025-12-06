using System.Management;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.Capabilities;

/// <summary>
/// Centralized FFmpeg GPU hardware acceleration detection and verification system.
/// This service detects available GPU accelerators and validates their support via FFmpeg.
/// </summary>
public class FFmpegAccelerationDetector
{
    /// <summary>
    /// List of detected and verified GPU accelerators
    /// </summary>
    public List<GpuAccelerator> Accelerators { get; private set; } = [];

    /// <summary>
    /// Initialize the detector and discover available GPU accelerators
    /// </summary>
    public FFmpegAccelerationDetector(string ffmpegPath)
    {
        FfmpegPath = ffmpegPath ?? throw new ArgumentNullException(nameof(ffmpegPath));
        DetectAccelerators(Info.GpuVendors);
    }

    private string FfmpegPath { get; }

    /// <summary>
    /// Check if a specific accelerator is available
    /// </summary>
    public bool HasAccelerator(string accelerator) => Accelerators.Any(a => a.Accelerator == accelerator);

    /// <summary>
    /// Verify FFmpeg hardware acceleration support via command execution
    /// </summary>
    private bool VerifyAccelerationSupport(string ffmpegArgs)
    {
        try
        {
            string command = $"-hide_banner {ffmpegArgs} -hwaccels";
            Logger.App($"Testing acceleration support with: ffmpeg {command}", LogEventLevel.Debug);
            string result = Shell.ExecStdOutSync(FfmpegPath, $"{command} 2>&1");

            bool isSupported = !result.Contains("Failed", StringComparison.InvariantCultureIgnoreCase) &&
                   !result.Contains("exception", StringComparison.InvariantCultureIgnoreCase);

            return isSupported;
        }
        catch (Exception e)
        {
            Logger.App($"GPU acceleration verification failed: {e.Message}", LogEventLevel.Debug);
            return false;
        }
    }

    /// <summary>
    /// Attempt OpenCL as fallback acceleration method
    /// </summary>
    private void TryOpenClFallback()
    {
        const string openClArgs = "-extra_hw_frames 3 -init_hw_device opencl=ocl";
        if (VerifyAccelerationSupport(openClArgs))
        {
            Accelerators.Add(new(
                GpuVendor.Unknown,
                openClArgs,
                "opencl"
            ));

            Logger.App("OpenCL acceleration fallback detected", LogEventLevel.Information);
        }
    }

    /// <summary>
    /// Detect all available GPU accelerators for the system
    /// </summary>
    private void DetectAccelerators(List<string> gpuVendors)
    {
        if (!IsDedicatedGpuAvailable())
        {
            Logger.App("No dedicated GPU detected, skipping hardware acceleration.", LogEventLevel.Warning);
            return;
        }

        Logger.App($"Detecting GPU accelerators for {gpuVendors.Count} vendor(s)...", LogEventLevel.Information);

        Dictionary<GpuVendor, int> gpuCounts = new()
        {
            { GpuVendor.Nvidia, 0 },
            { GpuVendor.Amd, 0 },
            { GpuVendor.Intel, 0 },
            { GpuVendor.Qualcomm, 0 },
            { GpuVendor.Apple, 0 }
        };

        foreach (string vendor in gpuVendors.Select(v => v.ToLower()))
        {
            if (vendor.Contains("nvidia"))
            {
                DetectNvidiaAccelerator(gpuCounts[GpuVendor.Nvidia]++);
            }
            else if (vendor.Contains("amd") || vendor.Contains("advanced micro devices"))
            {
                DetectAmdAccelerator(gpuCounts[GpuVendor.Amd]++);
            }
            else if (vendor.Contains("intel"))
            {
                DetectIntelAccelerator(gpuCounts[GpuVendor.Intel]++);
            }
            else if (vendor.Contains("apple"))
            {
                DetectAppleAccelerator(gpuCounts[GpuVendor.Apple]++);
            }
            else
            {
                TryOpenClFallback();
            }
        }

        Logger.App($"GPU acceleration detection complete. Found {Accelerators.Count} accelerator(s).", 
            LogEventLevel.Information);
    }

    /// <summary>
    /// Detect NVIDIA CUDA acceleration
    /// </summary>
    private void DetectNvidiaAccelerator(int index)
    {
        const string cudaArgs = " -init_hw_device cuda=cu:{index} -filter_hw_device cu -extra_hw_frames 8 -hwaccel_output_format cuda";
        string formattedArgs = cudaArgs.Replace("{index}", index.ToString());

        if (VerifyAccelerationSupport(formattedArgs))
        {
            Accelerators.Add(new(
                GpuVendor.Nvidia,
                formattedArgs,
                "cuda"
            ));

            Logger.App($"NVIDIA CUDA acceleration detected (device {index})", LogEventLevel.Information);
        }
        else
        {
            Logger.App($"NVIDIA CUDA acceleration not supported (device {index}), trying OpenCL fallback", 
                LogEventLevel.Warning);
            TryOpenClFallback();
        }
    }

    /// <summary>
    /// Detect AMD GPU acceleration (VAAPI on Linux, DXVA2 on Windows)
    /// </summary>
    private void DetectAmdAccelerator(int index)
    {
        string amdArgs = OperatingSystem.IsLinux()
            ? $" -init_hw_device vaapi=hw{index}:/dev/dri/renderD128 -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format vaapi"
            : $" -init_hw_device dxva2=hw{index} -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format dxva2";

        if (VerifyAccelerationSupport(amdArgs))
        {
            string accelerator = OperatingSystem.IsLinux() ? "vaapi" : "dxva2";
            string filter = OperatingSystem.IsLinux() ? "hwupload" : "";

            Accelerators.Add(new(
                GpuVendor.Amd,
                amdArgs,
                accelerator: accelerator,
                filter: filter
            ));

            Logger.App($"AMD {accelerator} acceleration detected (device {index})", LogEventLevel.Information);
        }
        else
        {
            Logger.App($"AMD acceleration not supported (device {index}), trying OpenCL fallback", 
                LogEventLevel.Warning);
            TryOpenClFallback();
        }
    }

    /// <summary>
    /// Detect Intel GPU acceleration (VAAPI on Linux, DXVA2 on Windows)
    /// </summary>
    private void DetectIntelAccelerator(int index)
    {
        string intelArgs = OperatingSystem.IsLinux()
            ? $" -init_hw_device vaapi=hw{index}:/dev/dri/renderD128 -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format vaapi"
            : $" -init_hw_device dxva2=hw{index} -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format dxva2";

        if (VerifyAccelerationSupport(intelArgs))
        {
            string accelerator = OperatingSystem.IsLinux() ? "vaapi" : "dxva2";
            string filter = OperatingSystem.IsLinux() ? "hwupload" : "";

            Accelerators.Add(new(
                GpuVendor.Intel,
                intelArgs,
                accelerator: accelerator,
                filter: filter
            ));

            Logger.App($"Intel {accelerator} acceleration detected (device {index})", LogEventLevel.Information);
        }
        else
        {
            Logger.App($"Intel acceleration not supported (device {index}), trying OpenCL fallback", 
                LogEventLevel.Warning);
            TryOpenClFallback();
        }
    }

    /// <summary>
    /// Detect Apple VideoToolbox acceleration (macOS only)
    /// </summary>
    private void DetectAppleAccelerator(int index)
    {
        const string appleArgs = " -init_hw_device videotoolbox=hw{index} -filter_hw_device hw -extra_hw_frames 8 -hwaccel_output_format videotoolbox";
        string formattedArgs = appleArgs.Replace("{index}", index.ToString());

        if (VerifyAccelerationSupport(formattedArgs))
        {
            Accelerators.Add(new(
                GpuVendor.Apple,
                formattedArgs,
                "videotoolbox"
            ));

            Logger.App($"Apple VideoToolbox acceleration detected (device {index})", LogEventLevel.Information);
        }
        else
        {
            Logger.App($"Apple VideoToolbox acceleration not supported (device {index}), trying OpenCL fallback", 
                LogEventLevel.Warning);
            TryOpenClFallback();
        }
    }

    /// <summary>
    /// Check if a dedicated GPU is available on the system
    /// </summary>
    private static bool IsDedicatedGpuAvailable()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using ManagementObjectSearcher searcher = new("SELECT * FROM Win32_VideoController");
                using ManagementObjectCollection results = searcher.Get();

                foreach (ManagementBaseObject? managementBaseObject in results)
                {
                    ManagementObject? obj = (ManagementObject)managementBaseObject;
                    object? adapterRam = obj["AdapterRAM"];
                    if (adapterRam == null || (uint)adapterRam <= 0) continue;

                    string caption = obj["Caption"]?.ToString() ?? string.Empty;
                    if (caption.Contains("NVIDIA", StringComparison.InvariantCultureIgnoreCase) ||
                        caption.Contains("AMD", StringComparison.InvariantCultureIgnoreCase) ||
                        caption.Contains("Intel", StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
            }
            catch (Exception e)
            {
                Logger.App($"Error checking for dedicated GPU on Windows: {e.Message}", LogEventLevel.Error);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                string result = Shell.ExecStdOutSync("lspci", "| grep -i 'vga'");
                return result.Contains("VGA", StringComparison.InvariantCultureIgnoreCase);
            }
            catch (Exception e)
            {
                Logger.App($"Error checking for dedicated GPU on Linux: {e.Message}", LogEventLevel.Error);
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            try
            {
                string result = Shell.ExecStdOutSync("system_profiler", "SPDisplaysDataType");

                return result.Contains("NVIDIA", StringComparison.InvariantCultureIgnoreCase) ||
                       result.Contains("AMD", StringComparison.InvariantCultureIgnoreCase) ||
                       result.Contains("Intel", StringComparison.InvariantCultureIgnoreCase);
            }
            catch (Exception e)
            {
                Logger.App($"Error checking for dedicated GPU on macOS: {e.Message}", LogEventLevel.Error);
            }
        }

        return false;
    }
}
