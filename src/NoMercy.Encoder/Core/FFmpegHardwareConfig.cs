using System.Management;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder.Core;

public enum GpuVendor
{
    Nvidia,
    Amd,
    Intel,
    Qualcomm,
    Apple,
    Unknown
}

public static class FFmpegHardwareConfig
{
    private static List<GpuAccelerator>? _accelerators;
    private static List<GpuAccelerator>? _allDevices;
    private static bool _isInitialized;
    private static readonly object InitLock = new();
    private const int MaxDeviceProbe = 8;

    /// <summary>
    /// Accelerators selected for command building (one per vendor type + OpenCL).
    /// </summary>
    public static List<GpuAccelerator> Accelerators
    {
        get
        {
            EnsureInitialized();
            return _accelerators!;
        }
    }

    /// <summary>
    /// All discovered GPU devices across all vendors, including multiple devices of the same type.
    /// </summary>
    public static List<GpuAccelerator> AllDevices
    {
        get
        {
            EnsureInitialized();
            return _allDevices!;
        }
    }

    private static void EnsureInitialized()
    {
        if (_isInitialized) return;
        lock (InitLock)
        {
            if (_isInitialized) return;
            _accelerators = [];
            _allDevices = [];
            SetHardwareAccelerationFlags(Info.GpuVendors);
            _isInitialized = true;
        }
    }

    public static Task InitializeAsync()
    {
        return Task.Run(EnsureInitialized);
    }

    public static bool HasAccelerator(string accelerator)
    {
        EnsureInitialized();
        return _accelerators!.Any(a => a.Accelerator == accelerator);
    }

    private static bool CheckAccel(string arg)
    {
        try
        {
            // Per-check command not logged — results summarized after detection
            string result = Shell.ExecStdOutSync(AppFiles.FfmpegPath, $"-hide_banner {arg} -hwaccels 2>&1");

            return !result.Contains("Failed", StringComparison.InvariantCultureIgnoreCase) &&
                   !result.Contains("exception", StringComparison.InvariantCultureIgnoreCase);
        }
        catch (Exception e)
        {
            Logger.Encoder(e, LogEventLevel.Debug);
            return false;
        }
    }

    private static void OpenClCheck()
    {
        if (_accelerators!.Any(a => a.Vendor == GpuVendor.Unknown)) return;

        string arg = "-extra_hw_frames 3 -init_hw_device opencl=ocl";
        bool supported = CheckAccel(arg);
        if (supported)
            _accelerators!.Add(new(
                GpuVendor.Unknown,
                arg,
                "none"
            ));
    }

    private static void SetHardwareAccelerationFlags(List<string> gpuVendors)
    {
        if (!IsDedicatedGpuAvailable())
        {
            Logger.Encoder("No dedicated GPU detected, skipping hardware acceleration.", LogEventLevel.Warning);
            return;
        }

        // Classify which vendor types are present (dedup by GpuVendor)
        bool hasNvidia = gpuVendors.Any(v => v.Contains("nvidia", StringComparison.OrdinalIgnoreCase));
        bool hasAmd = gpuVendors.Any(v =>
            v.Contains("amd", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("advanced micro devices", StringComparison.OrdinalIgnoreCase));
        bool hasIntel = gpuVendors.Any(v => v.Contains("intel", StringComparison.OrdinalIgnoreCase));
        bool hasApple = gpuVendors.Any(v => v.Contains("apple", StringComparison.OrdinalIgnoreCase));

        // Probe each vendor type for working devices
        if (hasNvidia) ProbeNvidiaDevices();
        if (hasAmd) ProbeAmdDevices();
        if (hasIntel) ProbeIntelDevices();
        if (hasApple) ProbeAppleDevices();

        // OpenCL as general-purpose fallback if no vendor-specific acceleration found
        if (!_accelerators!.Any(a => a.Vendor != GpuVendor.Unknown))
            OpenClCheck();

        Logger.Encoder($"Hardware acceleration: {_allDevices!.Count} device(s) discovered, {_accelerators!.Count} selected for encoding.");
    }

    private static void ProbeNvidiaDevices()
    {
        List<GpuAccelerator> allDevices = _allDevices!;
        List<GpuAccelerator> accelerators = _accelerators!;

        for (int index = 0; index < MaxDeviceProbe; index++)
        {
            string arg =
                $" -init_hw_device cuda=cu:{index} -filter_hw_device cu -extra_hw_frames 8 -hwaccel_output_format cuda";
            if (CheckAccel(arg))
            {
                GpuAccelerator device = new(GpuVendor.Nvidia, arg, "cuda", deviceIndex: index);
                allDevices.Add(device);
                Logger.Encoder($"CUDA device cu:{index} available.");

                if (!accelerators.Any(a => a.Vendor == GpuVendor.Nvidia))
                    accelerators.Add(device);
            }
            else
            {
                break; // CUDA device indices are contiguous
            }
        }

        if (!accelerators.Any(a => a.Vendor == GpuVendor.Nvidia))
            OpenClCheck();
    }

    private static void ProbeAmdDevices()
    {
        List<GpuAccelerator> allDevices = _allDevices!;
        List<GpuAccelerator> accelerators = _accelerators!;

        for (int index = 0; index < MaxDeviceProbe; index++)
        {
            string arg = OperatingSystem.IsLinux()
                ? $" -init_hw_device vaapi=hw{index}:/dev/dri/renderD128 -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format vaapi"
                : $" -init_hw_device dxva2=hw{index} -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format dxva2";

            if (CheckAccel(arg))
            {
                GpuAccelerator device = new(
                    GpuVendor.Amd,
                    arg,
                    filter: OperatingSystem.IsLinux() ? "hwupload" : "",
                    accelerator: OperatingSystem.IsLinux() ? "vaapi" : "dxva2",
                    deviceIndex: index
                );
                allDevices.Add(device);
                Logger.Encoder($"AMD device hw{index} available.");

                if (!accelerators.Any(a => a.Vendor == GpuVendor.Amd))
                    accelerators.Add(device);
            }
            else
            {
                break;
            }
        }

        if (!accelerators.Any(a => a.Vendor == GpuVendor.Amd))
            OpenClCheck();
    }

    private static void ProbeIntelDevices()
    {
        List<GpuAccelerator> allDevices = _allDevices!;
        List<GpuAccelerator> accelerators = _accelerators!;

        for (int index = 0; index < MaxDeviceProbe; index++)
        {
            string arg = OperatingSystem.IsLinux()
                ? $" -init_hw_device vaapi=hw{index}:/dev/dri/renderD128 -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format vaapi"
                : $" -init_hw_device dxva2=hw{index} -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format dxva2";

            if (CheckAccel(arg))
            {
                GpuAccelerator device = new(
                    GpuVendor.Intel,
                    arg,
                    filter: OperatingSystem.IsLinux() ? "hwupload" : "",
                    accelerator: OperatingSystem.IsLinux() ? "vaapi" : "dxva2",
                    deviceIndex: index
                );
                allDevices.Add(device);
                Logger.Encoder($"Intel device hw{index} available.");

                if (!accelerators.Any(a => a.Vendor == GpuVendor.Intel))
                    accelerators.Add(device);
            }
            else
            {
                break;
            }
        }

        if (!accelerators.Any(a => a.Vendor == GpuVendor.Intel))
            OpenClCheck();
    }

    private static void ProbeAppleDevices()
    {
        List<GpuAccelerator> allDevices = _allDevices!;
        List<GpuAccelerator> accelerators = _accelerators!;

        for (int index = 0; index < MaxDeviceProbe; index++)
        {
            string arg =
                $" -init_hw_device videotoolbox=hw{index} -filter_hw_device hw -extra_hw_frames 8 -hwaccel_output_format videotoolbox";
            if (CheckAccel(arg))
            {
                GpuAccelerator device = new(GpuVendor.Apple, arg, "videotoolbox", deviceIndex: index);
                allDevices.Add(device);
                Logger.Encoder($"VideoToolbox device hw{index} available.");

                if (!accelerators.Any(a => a.Vendor == GpuVendor.Apple))
                    accelerators.Add(device);
            }
            else
            {
                break;
            }
        }

        if (!accelerators.Any(a => a.Vendor == GpuVendor.Apple))
            OpenClCheck();
    }

    private static bool IsKnownGpuVendor(string value)
    {
        return value.Contains("NVIDIA", StringComparison.InvariantCultureIgnoreCase) ||
               value.Contains("AMD", StringComparison.InvariantCultureIgnoreCase) ||
               value.Contains("Advanced Micro Devices", StringComparison.InvariantCultureIgnoreCase) ||
               value.Contains("Radeon", StringComparison.InvariantCultureIgnoreCase) ||
               value.Contains("ATI", StringComparison.InvariantCultureIgnoreCase) ||
               value.Contains("Intel", StringComparison.InvariantCultureIgnoreCase);
    }

    private static bool IsDedicatedGpuAvailable()
    {
        if (OperatingSystem.IsWindows())
            try
            {
                ManagementObjectSearcher searcher = new("SELECT * FROM Win32_VideoController");
                ManagementObjectCollection? results = searcher.Get();

                foreach (ManagementBaseObject? managementBaseObject in results)
                {
                    ManagementObject? obj = (ManagementObject)managementBaseObject;
                    string caption = obj["Caption"]?.ToString().OrEmpty();
                    string adapterCompatibility = obj["AdapterCompatibility"]?.ToString().OrEmpty();

                    // Check both Caption and AdapterCompatibility for vendor detection.
                    // Caption may say "Radeon RX 580 Series" without "AMD" prefix,
                    // while AdapterCompatibility reliably returns "Advanced Micro Devices, Inc."
                    if (IsKnownGpuVendor(caption) || IsKnownGpuVendor(adapterCompatibility))
                        return true;
                }
            }
            catch (Exception e)
            {
                Logger.Encoder($"Error checking for dedicated GPU on Windows: {e.Message}", LogEventLevel.Error);
            }
        else if (OperatingSystem.IsLinux())
            try
            {
                string result = Shell.ExecStdOutSync("lspci", "| grep -i 'vga'");
                return result.Contains("VGA", StringComparison.InvariantCultureIgnoreCase);
            }
            catch (Exception e)
            {
                Logger.Encoder($"Error checking for dedicated GPU: {e.Message}", LogEventLevel.Error);
            }
        else if (OperatingSystem.IsMacOS())
            try
            {
                string result = Shell.ExecStdOutSync("system_profiler", "SPDisplaysDataType");

                if (IsKnownGpuVendor(result))
                    return true;
            }
            catch (Exception e)
            {
                Logger.Encoder($"Error checking for dedicated GPU on macOS: {e.Message}", LogEventLevel.Error);
            }

        return false;
    }
}

public class GpuAccelerator
{
    public GpuVendor Vendor { get; }
    public string FfmpegArgs { get; }
    public string Filter { get; }
    public string Accelerator { get; }
    public int DeviceIndex { get; }

    public GpuAccelerator(GpuVendor vendor, string ffmpegArgs, string accelerator, string filter = "", int deviceIndex = -1)
    {
        Vendor = vendor;
        FfmpegArgs = ffmpegArgs;
        Filter = filter;
        Accelerator = accelerator;
        DeviceIndex = deviceIndex;
    }
}