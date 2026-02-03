using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NoMercy.NmSystem.Capabilities;

/// <summary>
/// GPU device information
/// </summary>
public class GpuDevice
{
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = "Unknown";
    public string DeviceType { get; set; } = "Unknown"; // NVIDIA, AMD, Intel, Apple
    public long MemoryMb { get; set; }
    public string? ComputeCapability { get; set; }
    public List<string> SupportedEncoders { get; set; } = [];
}

/// <summary>
/// GPU vendor enumeration
/// </summary>
public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel,
    Apple
}

/// <summary>
/// Detects GPU devices on the system
/// </summary>
public class GpuDeviceDetector
{
    /// <summary>
    /// Detect all GPU devices on the system
    /// </summary>
    public List<GpuDevice> DetectGpuDevices()
    {
        List<GpuDevice> devices = [];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            devices.AddRange(DetectWindowsGpus());
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            devices.AddRange(DetectLinuxGpus());
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            devices.AddRange(DetectMacGpus());
        }

        return devices;
    }

    private List<GpuDevice> DetectWindowsGpus()
    {
        List<GpuDevice> devices = [];

#pragma warning disable CA1416
        try
        {
            using System.Management.ManagementObjectSearcher searcher = new(
                "SELECT Name, AdapterRAM, AdapterCompatibility FROM Win32_VideoController");

            int id = 0;
            foreach (System.Management.ManagementBaseObject? obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "Unknown";
                string vendor = obj["AdapterCompatibility"]?.ToString() ?? "Unknown";
                object? ramObj = obj["AdapterRAM"];
                long memoryMb = 0;

                if (ramObj != null && long.TryParse(ramObj.ToString(), out long ram))
                {
                    memoryMb = ram / (1024 * 1024);
                }

                devices.Add(new GpuDevice
                {
                    DeviceId = id++,
                    DeviceName = name,
                    DeviceType = DetermineDeviceType(name, vendor),
                    MemoryMb = memoryMb,
                    SupportedEncoders = GetEncodersForDevice(name)
                });
            }
        }
        catch (Exception)
        {
            // Ignore errors
        }
#pragma warning restore CA1416

        return devices;
    }

    private List<GpuDevice> DetectLinuxGpus()
    {
        List<GpuDevice> devices = [];

        try
        {
            // Use lspci to detect GPU devices
            ProcessStartInfo psi = new("lspci")
            {
                Arguments = "-v",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return devices;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Parse VGA compatible controllers
            Regex vgaRegex = new(@"VGA compatible controller: (.+)", RegexOptions.Multiline);
            MatchCollection matches = vgaRegex.Matches(output);

            int id = 0;
            foreach (Match match in matches)
            {
                string name = match.Groups[1].Value.Trim();
                devices.Add(new GpuDevice
                {
                    DeviceId = id++,
                    DeviceName = name,
                    DeviceType = DetermineDeviceType(name, name),
                    SupportedEncoders = GetEncodersForDevice(name)
                });
            }

            // Try to get NVIDIA memory info
            EnrichNvidiaInfo(devices);
        }
        catch (Exception)
        {
            // Ignore errors
        }

        return devices;
    }

    private List<GpuDevice> DetectMacGpus()
    {
        List<GpuDevice> devices = [];

        try
        {
            ProcessStartInfo psi = new("system_profiler")
            {
                Arguments = "SPDisplaysDataType -json",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return devices;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Simple parsing for chipset model
            Regex chipsetRegex = new(@"""sppci_model""\s*:\s*""([^""]+)""");
            MatchCollection matches = chipsetRegex.Matches(output);

            int id = 0;
            foreach (Match match in matches)
            {
                string name = match.Groups[1].Value;
                devices.Add(new GpuDevice
                {
                    DeviceId = id++,
                    DeviceName = name,
                    DeviceType = "Apple",
                    SupportedEncoders = ["videotoolbox_h264", "videotoolbox_hevc"]
                });
            }
        }
        catch (Exception)
        {
            // Ignore errors
        }

        return devices;
    }

    private void EnrichNvidiaInfo(List<GpuDevice> devices)
    {
        try
        {
            ProcessStartInfo psi = new("nvidia-smi")
            {
                Arguments = "--query-gpu=memory.total --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process == null) return;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            int idx = 0;
            foreach (GpuDevice device in devices.Where(d => d.DeviceType == "NVIDIA"))
            {
                if (idx < lines.Length && long.TryParse(lines[idx].Trim(), out long memMb))
                {
                    device.MemoryMb = memMb;
                }
                idx++;
            }
        }
        catch (Exception)
        {
            // nvidia-smi not available
        }
    }

    private static string DetermineDeviceType(string name, string vendor)
    {
        string combined = $"{name} {vendor}".ToUpperInvariant();

        if (combined.Contains("NVIDIA") || combined.Contains("GEFORCE") || combined.Contains("QUADRO") || combined.Contains("RTX"))
            return "NVIDIA";

        if (combined.Contains("AMD") || combined.Contains("RADEON") || combined.Contains("ATI"))
            return "AMD";

        if (combined.Contains("INTEL") || combined.Contains("UHD") || combined.Contains("HD GRAPHICS"))
            return "Intel";

        if (combined.Contains("APPLE") || combined.Contains("M1") || combined.Contains("M2") || combined.Contains("M3"))
            return "Apple";

        return "Unknown";
    }

    private static List<string> GetEncodersForDevice(string name)
    {
        string upper = name.ToUpperInvariant();

        if (upper.Contains("NVIDIA") || upper.Contains("GEFORCE") || upper.Contains("QUADRO") || upper.Contains("RTX"))
        {
            return ["h264_nvenc", "hevc_nvenc", "av1_nvenc"];
        }

        if (upper.Contains("AMD") || upper.Contains("RADEON"))
        {
            return ["h264_amf", "hevc_amf", "av1_amf"];
        }

        if (upper.Contains("INTEL"))
        {
            return ["h264_qsv", "hevc_qsv", "av1_qsv"];
        }

        if (upper.Contains("APPLE") || upper.Contains("M1") || upper.Contains("M2") || upper.Contains("M3"))
        {
            return ["videotoolbox_h264", "videotoolbox_hevc"];
        }

        return [];
    }
}
