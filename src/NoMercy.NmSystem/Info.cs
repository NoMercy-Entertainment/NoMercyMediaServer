using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DeviceId;

namespace NoMercy.NmSystem;

public class Info
{
    public static string DeviceName { get; set; } = Environment.MachineName;
    public static readonly Guid DeviceId = GetDeviceId();
    public static readonly string Os = RuntimeInformation.OSDescription;
    public static readonly string Platform = GetPlatform();
    public static readonly string Architecture = RuntimeInformation.ProcessArchitecture.ToString();
    public static readonly string[] Cpu = GetCpuFullName();
    public static readonly string[] Gpu = GetGpuFullName();
    public static readonly string? Version = GetSystemVersion();
    public static readonly DateTime BootTime = GetBootTime();
    public static readonly DateTime StartTime = DateTime.UtcNow;
    public static readonly string ExecSuffix = Platform == "windows" ? ".exe" : "";

    private static Guid GetDeviceId()
    {
        string? generatedId = new DeviceIdBuilder()
            .OnWindows(windows => windows
                .AddMotherboardSerialNumber()
                .AddSystemDriveSerialNumber())
            .OnLinux(linux => linux
                .AddMotherboardSerialNumber()
                .AddSystemDriveSerialNumber())
            .OnMac(mac => mac
                .AddSystemDriveSerialNumber()
                .AddPlatformSerialNumber())
            .ToString();

        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(generatedId));

        return new(hash);
    }

    private static string GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "mac";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";

        throw new("Unknown platform");
    }

    private static string[] GetGpuFullName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ManagementObjectSearcher searcher = new("select Name from Win32_VideoController");
            List<string> gpus = [];
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                if (o is not ManagementObject item) continue;
                if (item["Name"] is not {} value) continue;
                if (value.ToString() is not {} valueString) continue;
                gpus.Add(valueString.Trim());
            }

            return gpus.ToArray();
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Use system_profiler to get GPU details on macOS
            // This might not be the most efficient way to get GPU info on macOS, but it works
            string systemProfilerOutput = ExecuteBashCommand("system_profiler SPDisplaysDataType");
            // Parse the output for GPU names; typically the “Chipset Model” field contains the GPU info
            var lines = systemProfilerOutput.Split('\n');
            List<string> gpus = [];
            foreach (var line in lines)
            {
                const string marker = "Chipset Model:";
                if (line.TrimStart().StartsWith(marker))
                {
                    // Extract the GPU name after the marker
                    string gpuName = line.Substring(line.IndexOf(marker) + marker.Length).Trim();
                    if (!string.IsNullOrEmpty(gpuName))
                    {
                        gpus.Add(gpuName);
                    }
                }
            }
            return gpus.ToArray();
        }

        string output = ExecuteBashCommand("lspci | grep 'VGA'");

        return output.Trim().Split(':').LastOrDefault()?.Trim()?.Split('\n') ?? [];
    }

    private static string[] GetCpuFullName()
    {
        List<string> cpus = new();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ManagementObjectSearcher searcher = new("select Name from Win32_Processor");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                if (item["Name"] is string cpuName)
                {
                    cpus.Add(cpuName.Trim());
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string output = ExecuteBashCommand("sysctl -n machdep.cpu.brand_string");
            cpus.Add(output.Trim());
        }
        else
        {
            string output = ExecuteBashCommand("lscpu | grep 'Model name:'");
            cpus.Add(output.Trim().Split(':')[1].Trim());
        }

        return cpus.ToArray();
    }

    private static string? GetSystemVersion()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ManagementObjectSearcher searcher = new("select Version from Win32_OperatingSystem");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                return item["Version"].ToString();
            }
        }
        else
        {
            string output = ExecuteBashCommand("uname -r");
            return output.Trim();
        }

        return "Unknown";
    }

    private static DateTime GetBootTime()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ManagementObjectSearcher searcher = new("select LastBootUpTime from Win32_OperatingSystem");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                return ManagementDateTimeConverter.ToDateTime(item["LastBootUpTime"].ToString());
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string output = ExecuteBashCommand("sysctl -n kern.boottime");
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(long.Parse(output.Split(' ').Last()));
        }
        else
        {
            string output = ExecuteBashCommand("uptime -s");
            return DateTime.Parse(output.Trim());
        }

        return DateTime.MinValue;
    }

    private static string ExecuteBashCommand(string command)
    {
        command = command.Replace("\"", "\\\"");
        Process process = new()
        {
            StartInfo = new()
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        string result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return result;
    }
}