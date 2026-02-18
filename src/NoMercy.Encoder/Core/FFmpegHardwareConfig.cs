using System.Management;
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
    private static bool _isInitialized;
    private static readonly object InitLock = new();

    public static List<GpuAccelerator> Accelerators
    {
        get
        {
            EnsureInitialized();
            return _accelerators!;
        }
    }

    private static void EnsureInitialized()
    {
        if (_isInitialized) return;
        lock (InitLock)
        {
            if (_isInitialized) return;
            _accelerators = [];
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

        Dictionary<GpuVendor, int> gpuCounts = new()
        {
            { GpuVendor.Nvidia, 0 },
            { GpuVendor.Amd, 0 },
            { GpuVendor.Intel, 0 },
            { GpuVendor.Qualcomm, 0 },
            { GpuVendor.Apple, 0 }
        };

        foreach (string vendor in gpuVendors.Select(v => v.ToLower()))
            if (vendor.Contains("nvidia"))
            {
                int index = gpuCounts[GpuVendor.Nvidia];
                string arg =
                    $" -init_hw_device cuda=cu:{index} -filter_hw_device cu -extra_hw_frames 8 -hwaccel_output_format cuda";
                bool supported = CheckAccel(arg);
                if (supported)
                    _accelerators!.Add(new(
                        GpuVendor.Nvidia,
                        arg,
                        "cuda"
                    ));
                else
                    OpenClCheck();
                gpuCounts[GpuVendor.Nvidia]++;
            }
            else if (vendor.Contains("amd") || vendor.Contains("advanced micro devices"))
            {
                int index = gpuCounts[GpuVendor.Amd];
                string arg = OperatingSystem.IsLinux()
                    ? $" -init_hw_device vaapi=hw{index}:/dev/dri/renderD128 -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format vaapi"
                    : $" -init_hw_device dxva2=hw{index} -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format dxva2";

                bool supported = CheckAccel(arg);
                if (supported)
                    _accelerators!.Add(new(
                        GpuVendor.Amd,
                        arg,
                        filter: OperatingSystem.IsLinux() ? "hwupload" : "",
                        accelerator: OperatingSystem.IsLinux() ? "vaapi" : "dxva2"
                    ));
                else
                    OpenClCheck();
                gpuCounts[GpuVendor.Amd]++;
            }
            else if (vendor.Contains("intel"))
            {
                int index = gpuCounts[GpuVendor.Intel];
                string arg = OperatingSystem.IsLinux()
                    ? $" -init_hw_device vaapi=hw{index}:/dev/dri/renderD128 -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format vaapi"
                    : $" -init_hw_device dxva2=hw{index} -filter_hw_device hw{index} -extra_hw_frames 8 -hwaccel_output_format dxva2";

                bool supported = CheckAccel(arg);
                if (supported)
                    _accelerators!.Add(new(
                        GpuVendor.Intel,
                        arg,
                        filter: OperatingSystem.IsLinux() ? "hwupload" : "",
                        accelerator: OperatingSystem.IsLinux() ? "vaapi" : "dxva2"
                    ));
                else
                    OpenClCheck();
                gpuCounts[GpuVendor.Intel]++;
            }
            else if (vendor.Contains("apple"))
            {
                int index = gpuCounts[GpuVendor.Apple];
                string arg =
                    $" -init_hw_device videotoolbox=hw{index} -filter_hw_device hw -extra_hw_frames 8 -hwaccel_output_format videotoolbox";
                bool supported = CheckAccel(arg);
                if (supported)
                    _accelerators!.Add(new(
                        GpuVendor.Apple,
                        arg,
                        "videotoolbox"
                    ));
                else
                    OpenClCheck();
                gpuCounts[GpuVendor.Apple]++;
            }
            else
            {
                OpenClCheck();
            }
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
                    object? adapterRam = obj["AdapterRAM"];
                    if (adapterRam == null || (uint)adapterRam <= 0) continue;

                    string caption = obj["Caption"]?.ToString() ?? string.Empty;
                    if (caption.Contains("NVIDIA", StringComparison.InvariantCultureIgnoreCase) ||
                        caption.Contains("AMD", StringComparison.InvariantCultureIgnoreCase) ||
                        caption.Contains("Intel",
                            StringComparison
                                .InvariantCultureIgnoreCase))
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

                return result.Contains("NVIDIA", StringComparison.InvariantCultureIgnoreCase) ||
                       result.Contains("AMD", StringComparison.InvariantCultureIgnoreCase) ||
                       result.Contains("Intel", StringComparison.InvariantCultureIgnoreCase);
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

    public GpuAccelerator(GpuVendor vendor, string ffmpegArgs, string accelerator, string filter = "")
    {
        Vendor = vendor;
        FfmpegArgs = ffmpegArgs;
        Filter = filter;
        Accelerator = accelerator;
    }
}