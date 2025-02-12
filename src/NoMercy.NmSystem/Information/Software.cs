using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DeviceId;

namespace NoMercy.NmSystem.Information;

public class Software
{
    public static Version? Version { get; set; }
    
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    
    internal static string GetPlatform()
    {
        if (IsWindows)
            return "windows";
        if (IsMac)
            return "mac";
        if (IsLinux)
            return "linux";

        throw new("Unknown platform");
    }
    
    internal static Guid GetDeviceId()
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

    public static string? GetSystemVersion()
    {
        if (IsWindows)
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
            string output = Helper.RunCommand("uname -r");
            return output.Trim();
        }

        return "Unknown";
    }
    
    public static string GetReleaseVersion()
    {
        return $"{Version!.Major}.{Version.Minor}.{Version.Build}";
    }

    internal static DateTime GetBootTime()
    {
        if (IsWindows)
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
            string output = Helper.RunCommand("sysctl -n kern.boottime");
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(long.Parse(output.Split(' ').Last()));
        }
        else
        {
            string output = Helper.RunCommand("uptime -s");
            return DateTime.Parse(output.Trim());
        }

        return DateTime.MinValue;
    }

}