using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NoMercy.NmSystem.Information;

/// <summary>
/// System memory information utilities
/// </summary>
public static class Memory
{
    /// <summary>
    /// Get total system memory in megabytes
    /// </summary>
    public static long GetTotalMemoryMb()
    {
        if (Software.IsWindows) return GetTotalMemoryWindows();
        if (Software.IsLinux) return GetTotalMemoryLinux();
        if (Software.IsMac) return GetTotalMemoryMac();
        return 0;
    }

    /// <summary>
    /// Get available system memory in megabytes
    /// </summary>
    public static long GetAvailableMemoryMb()
    {
        if (Software.IsWindows) return GetAvailableMemoryWindows();
        if (Software.IsLinux) return GetAvailableMemoryLinux();
        if (Software.IsMac) return GetAvailableMemoryMac();
        return 0;
    }

    private static long GetTotalMemoryWindows()
    {
#pragma warning disable CA1416
        try
        {
            using System.Management.ManagementObjectSearcher searcher = new(
                "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");

            foreach (System.Management.ManagementBaseObject? obj in searcher.Get())
            {
                if (obj["TotalVisibleMemorySize"] is ulong totalKb)
                {
                    return (long)(totalKb / 1024);
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors
        }
#pragma warning restore CA1416

        // Fallback: use GC memory info
        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
    }

    private static long GetTotalMemoryLinux()
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
        catch (Exception)
        {
            // Ignore errors
        }

        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
    }

    private static long GetTotalMemoryMac()
    {
        try
        {
            ProcessStartInfo psi = new("sysctl")
            {
                Arguments = "-n hw.memsize",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                if (long.TryParse(output, out long bytes))
                {
                    return bytes / (1024 * 1024);
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors
        }

        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
    }

    private static long GetAvailableMemoryWindows()
    {
#pragma warning disable CA1416
        try
        {
            using System.Management.ManagementObjectSearcher searcher = new(
                "SELECT FreePhysicalMemory FROM Win32_OperatingSystem");

            foreach (System.Management.ManagementBaseObject? obj in searcher.Get())
            {
                if (obj["FreePhysicalMemory"] is ulong freeKb)
                {
                    return (long)(freeKb / 1024);
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors
        }
#pragma warning restore CA1416

        return 0;
    }

    private static long GetAvailableMemoryLinux()
    {
        try
        {
            string[] lines = File.ReadAllLines("/proc/meminfo");
            string? memAvailable = lines.FirstOrDefault(l => l.StartsWith("MemAvailable:"));
            if (memAvailable != null)
            {
                Match match = Regex.Match(memAvailable, @"(\d+)");
                if (match.Success && long.TryParse(match.Groups[1].Value, out long kb))
                {
                    return kb / 1024;
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors
        }

        return 0;
    }

    private static long GetAvailableMemoryMac()
    {
        try
        {
            // vm_stat provides page statistics; we need to calculate free memory
            ProcessStartInfo psi = new("vm_stat")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd();

                // Parse page size and free pages
                Match pageSizeMatch = Regex.Match(output, @"page size of (\d+) bytes");
                Match freePagesMatch = Regex.Match(output, @"Pages free:\s+(\d+)");

                if (pageSizeMatch.Success && freePagesMatch.Success &&
                    long.TryParse(pageSizeMatch.Groups[1].Value, out long pageSize) &&
                    long.TryParse(freePagesMatch.Groups[1].Value, out long freePages))
                {
                    return (freePages * pageSize) / (1024 * 1024);
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors
        }

        return 0;
    }
}
