using System.Management;
using System.Text.RegularExpressions;

namespace NoMercy.NmSystem.Information;

/// <summary>
/// System memory information utilities
/// </summary>
public static class Memory
{
    /// <summary>
    /// Get total system memory in MB
    /// </summary>
    public static long GetTotalMemoryMb()
    {
        try
        {
            if (Software.IsWindows) return GetTotalMemoryMbWindows();
            if (Software.IsLinux) return GetTotalMemoryMbLinux();
            if (Software.IsMac) return GetTotalMemoryMbMac();
        }
        catch
        {
            // Ignore errors
        }

        return 0;
    }

    private static long GetTotalMemoryMbWindows()
    {
        try
        {
#pragma warning disable CA1416
            ManagementObjectSearcher searcher = new("select Capacity from Win32_PhysicalMemory");
            long total = 0;
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                if (item["Capacity"] is ulong capacity)
                {
                    total += (long)(capacity / (1024 * 1024));
                }
            }
#pragma warning restore CA1416

            return total;
        }
        catch
        {
            return 0;
        }
    }

    private static long GetTotalMemoryMbLinux()
    {
        try
        {
            string[] lines = File.ReadAllLines("/proc/meminfo");
            string? memTotal = lines.FirstOrDefault(l => l.StartsWith("MemTotal:"));
            if (memTotal != null)
            {
                Match match = Regex.Match(memTotal, @"(\d+)");
                if (match.Success && long.TryParse(match.Groups[1].Value, out long kb))
                {
                    return kb / 1024;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return 0;
    }

    private static long GetTotalMemoryMbMac()
    {
        try
        {
            string output = SystemCalls.Shell.ExecCommand("sysctl -n hw.memsize");
            if (long.TryParse(output.Trim(), out long bytes))
            {
                return bytes / (1024 * 1024);
            }
        }
        catch
        {
            // Ignore errors
        }

        return 0;
    }
}
