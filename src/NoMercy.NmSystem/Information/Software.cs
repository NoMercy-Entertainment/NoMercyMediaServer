using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DeviceId;

namespace NoMercy.NmSystem.Information;

public static class Software
{
    public static Version? Version { get; set; } = new(0, 1, 0, 0);

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
#pragma warning disable CA1416
            ManagementObjectSearcher searcher = new("select Version from Win32_OperatingSystem");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                return item["Version"].ToString();
            }
#pragma warning restore CA1416
        }
        else
        {
            string output = SystemCalls.Shell.ExecCommand("uname -r");
            return output.Trim();
        }

        return "Unknown";
    }

    public static string GetReleaseVersion()
    {
        return $"{Version!.Major}.{Version.Minor}.{Version.Build}";
    }

    public static string? GetFileVersion(string exePath)
    {
        try
        {
            if (!File.Exists(exePath))
                return null;

            FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(exePath);
            if (fileInfo.FileMajorPart == 0 && fileInfo.FileMinorPart == 0 && fileInfo.FileBuildPart == 0)
                return null;

            return $"{fileInfo.FileMajorPart}.{fileInfo.FileMinorPart}.{fileInfo.FileBuildPart}";
        }
        catch
        {
            return null;
        }
    }

    internal static DateTime GetBootTime()
    {
        if (IsWindows)
        {
#pragma warning disable CA1416
            ManagementObjectSearcher searcher = new("select LastBootUpTime from Win32_OperatingSystem");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                return ManagementDateTimeConverter.ToDateTime(item["LastBootUpTime"].ToString());
#pragma warning restore CA1416
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string output = SystemCalls.Shell.ExecCommand("sysctl -n kern.boottime");
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(long.Parse(output.Split(' ').Last()));
        }
        else
        {
            string output = SystemCalls.Shell.ExecCommand("uptime -s");
            return DateTime.Parse(output.Trim());
        }

        return DateTime.MinValue;
    }
}