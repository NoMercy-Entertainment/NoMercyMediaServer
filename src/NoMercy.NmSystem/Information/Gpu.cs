using System.Management;
using FFMpegCore;

namespace NoMercy.NmSystem.Information;

public class Gpu
{
    public static List<string> Vendors()
    {
        if (Software.IsWindows)
        {
            return GetGpuVendorsWindows();
        }

        if (Software.IsLinux)
        {
            return GetGpuVendorsLinux();
        }

        if (Software.IsMac)
        {
            return GetGpuVendorsMac();
        }
        
        return ["Unknown"];
    }

    private static List<string> GetGpuVendorsWindows()
    {
        List<string> vendors = [];
        try
        {
            using ManagementObjectSearcher searcher = new("SELECT * FROM Win32_VideoController");
            foreach (ManagementBaseObject? obj in searcher.Get())
            {
                string vendor = obj["AdapterCompatibility"]?.ToString() ?? "Unknown";
                if (!vendors.Contains(vendor))
                {
                    vendors.Add(vendor);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting GPUs on Windows: {ex.Message}");
        }
        return vendors;
    }

    private static List<string> GetGpuVendorsLinux()
    {
        string output = Helper.RunCommand("lspci | grep -i 'VGA' | awk -F ': ' '{print $2}'");
        return output.Split('\n').Where(v => !string.IsNullOrEmpty(v)).ToList();
    }
    private static List<string> GetGpuVendorsMac()
    {
        string output = Helper.RunCommand("system_profiler SPDisplaysDataType | grep 'Chipset Model' | awk -F ': ' '{print $2}'");
        return output.Split('\n').Where(v => !string.IsNullOrEmpty(v)).ToList();
    }

    
    public static List<string> Names()
    {
        if (Software.IsWindows)
        {
            return GetGpuNamesWindows();
        }

        if (Software.IsLinux)
        {
            return GetGpuNamesLinux();
        }

        if (Software.IsMac)
        {
            return GetGpuNamesMac();
        }
        
        return ["Unknown"];
    }
    private static List<string> GetGpuNamesWindows()
    {
        List<string> gpus = [];
        
        try
        {
            ManagementObjectSearcher searcher = new("select Name from Win32_VideoController");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                if (o is not ManagementObject item) continue;
                if (item["Name"] is not {} value) continue;
                if (value.ToString() is not {} valueString) continue;
                gpus.Add(valueString.Trim());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting GPUs on Windows: {ex.Message}");
        }
        
        return gpus;
    }
    private static List<string> GetGpuNamesLinux()
    {
        string output = Helper.RunCommand("lspci | grep 'VGA'");
        return output.Split('\n').Where(v => !string.IsNullOrEmpty(v)).ToList();
    }
    private static List<string> GetGpuNamesMac()
    {
        List<string> gpus = [];
        
        string systemProfilerOutput = Helper.RunCommand("system_profiler SPDisplaysDataType");
        
        string[] lines = systemProfilerOutput.Split('\n');
        foreach (string line in lines)
        {
            const string marker = "Chipset Model:";
            if (line.TrimStart().StartsWith(marker))
            {
                string gpuName = line.Substring(line.IndexOf(marker, StringComparison.Ordinal) + marker.Length).Trim();
                if (!string.IsNullOrEmpty(gpuName))
                {
                    gpus.Add(gpuName);
                }
            }
        }

        return gpus;
    }
    
}