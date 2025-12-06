using System.Management;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.Information;

/// <summary>
/// Centralized hardware capability detection system
/// Consolidates GPU acceleration and system capability detection
/// Replaces scattered CheckAccel() and IsDedicatedGpuAvailable() calls from FFmpegHardwareConfig
/// </summary>
public static class Hardware
{
    private static ServerHardwareCapabilities? _cachedCapabilities;
    private static bool? _cachedHasGpu;

    /// <summary>
    /// Get complete server hardware capabilities including GPU acceleration support
    /// </summary>
    public static ServerHardwareCapabilities GetCapabilities()
    {
        return _cachedCapabilities ??= new()
        {
            CpuCores = Environment.ProcessorCount,
            GpuCount = Gpu.Names().Count,
            TotalMemoryGb = GetTotalMemoryGb(),
            SupportsHwAccel = HasDedicatedGpu(),
            GpuModel = Gpu.Names().FirstOrDefault()
        };
    }

    /// <summary>
    /// Check if system has a dedicated (non-integrated) GPU
    /// Replaces IsDedicatedGpuAvailable() from FFmpegHardwareConfig
    /// </summary>
    public static bool HasDedicatedGpu()
    {
        _cachedHasGpu ??= DetectDedicatedGpu();
        return _cachedHasGpu.Value;
    }

    /// <summary>
    /// Test if a specific FFmpeg hardware acceleration argument works
    /// Used by FFmpegHardwareConfig.CheckAccel() - replaces that scattered logic
    /// </summary>
    public static bool TestFfmpegAcceleration(string ffmpegArgs)
    {
        try
        {
            Logger.Encoder($"Checking Acceleration: -hide_banner {ffmpegArgs} -hwaccels 2>&1", LogEventLevel.Debug);
            string result = Shell.ExecStdOutSync(AppFiles.FfmpegPath, $"-hide_banner {ffmpegArgs} -hwaccels 2>&1");

            return !result.Contains("Failed", StringComparison.InvariantCultureIgnoreCase) &&
                   !result.Contains("exception", StringComparison.InvariantCultureIgnoreCase);
        }
        catch (Exception e)
        {
            Logger.Encoder(e, LogEventLevel.Debug);
            return false;
        }
    }

    private static bool DetectDedicatedGpu()
    {
        if (OperatingSystem.IsWindows())
            return DetectDedicatedGpuWindows();

        if (OperatingSystem.IsLinux())
            return DetectDedicatedGpuLinux();

        if (OperatingSystem.IsMacOS())
            return DetectDedicatedGpuMac();

        return false;
    }

    private static bool DetectDedicatedGpuWindows()
    {
        try
        {
            using ManagementObjectSearcher searcher = new("SELECT * FROM Win32_VideoController");
            ManagementObjectCollection? results = searcher.Get();

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
            Logger.Encoder($"Error checking for dedicated GPU on Windows: {e.Message}", LogEventLevel.Error);
        }

        return false;
    }

    private static bool DetectDedicatedGpuLinux()
    {
        try
        {
            string result = Shell.ExecStdOutSync("lspci", "| grep -i 'vga'");
            return result.Contains("VGA", StringComparison.InvariantCultureIgnoreCase);
        }
        catch (Exception e)
        {
            Logger.Encoder($"Error checking for dedicated GPU: {e.Message}", LogEventLevel.Error);
        }

        return false;
    }

    private static bool DetectDedicatedGpuMac()
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
            Logger.Encoder($"Error checking for dedicated GPU on macOS: {e.Message}", LogEventLevel.Error);
        }

        return false;
    }

    private static long GetTotalMemoryGb()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416
                ManagementObjectSearcher searcher = new("select TotalVisibleMemorySize from Win32_OperatingSystem");
                foreach (ManagementBaseObject? obj in searcher.Get())
                {
                    object? totalMemory = ((ManagementObject)obj)["TotalVisibleMemorySize"];
                    if (totalMemory != null && ulong.TryParse(totalMemory.ToString(), out ulong totalMemoryKb))
                        return (long)(totalMemoryKb / (1024 * 1024)); // Convert KB to GB
                }
#pragma warning restore CA1416
            }
            else
            {
                // For Linux/Mac, try to get from /proc/meminfo or similar
                string result = Shell.ExecStdOutSync("free", "-g | grep Mem");
                string[] parts = result.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && long.TryParse(parts[1], out long totalMem))
                    return totalMem;
            }
        }
        catch (Exception ex)
        {
            Logger.Encoder($"Error getting total memory: {ex.Message}", LogEventLevel.Debug);
        }

        return 0;
    }
}