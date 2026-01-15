using System.Management;
using System.Runtime.InteropServices;
using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem.Information;

public class Storage
{
    #region Storage Device Information

    public static List<StorageDevice> GetStorageDevices()
    {
        if (Software.IsWindows) return GetWindowsStorageDevices();

        if (Software.IsLinux || Software.IsMac) return GetUnixStorageDevices();

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    private static List<StorageDevice> GetWindowsStorageDevices()
    {
        List<StorageDevice> devices = [];

#pragma warning disable CA1416
        ManagementObjectSearcher searcher = new("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item["DeviceID"] is string deviceId)
                devices.Add(new()
                {
                    Name = deviceId,
                    TotalSpace = (long)(ulong)item["Size"],
                    FreeSpace = (long)(ulong)item["FreeSpace"]
                });
        }
#pragma warning restore CA1416

        return devices;
    }

    private static List<StorageDevice> GetUnixStorageDevices()
    {
        List<StorageDevice> devices = [];

        string output = SystemCalls.Shell.ExecCommand("df -k");
        string[] lines = output.Split('\n');
        foreach (string line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6) continue;

            devices.Add(new()
            {
                Name = parts[0],
                TotalSpace = long.Parse(parts[1]) * 1024,
                FreeSpace = long.Parse(parts[3]) * 1024
            });
        }

        return devices;
    }

    #endregion

    #region Space Information

    public static long GetUsedSpace(string path)
    {
        long totalSpace = GetTotalSpace(path);
        long freeSpace = GetFreeSpace(path);
        return totalSpace - freeSpace;
    }

    private static long GetFreeSpace(string path)
    {
        if (Software.IsWindows) return GetWindowsFreeSpace(path);

        if (Software.IsLinux || Software.IsMac) return GetUnixFreeSpace(path);

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    private static long GetWindowsFreeSpace(string path)
    {
        if (!Directory.Exists(path)) throw new ArgumentException($"Path does not exist: {path}");

        if (GetDiskFreeSpaceEx(path, out ulong freeBytesAvailable, out _, out _)) return (long)freeBytesAvailable;

        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct Statvfs
    {
        public ulong f_bsize;
        public ulong f_frsize;
        public ulong f_blocks;
        public ulong f_bfree;
        public ulong f_bavail;
        public ulong f_files;
        public ulong f_ffree;
        public ulong f_favail;
        public ulong f_fsid;
        public ulong f_flag;
        public ulong f_namemax;
    }

    [DllImport("libc.so.6", EntryPoint = "statvfs", SetLastError = true)]
    private static extern int statvfs(string path, out Statvfs buf);

    private static long GetUnixFreeSpace(string path)
    {
        if (!Directory.Exists(path)) throw new ArgumentException($"Path does not exist: {path}");

        if (statvfs(path, out Statvfs stat) == 0) return (long)(stat.f_bavail * stat.f_frsize);

        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private static long GetTotalSpace(string path)
    {
        if (Software.IsWindows)
        {
            if (GetDiskFreeSpaceEx(path, out _, out ulong totalBytes, out _)) return (long)totalBytes;
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        if (Software.IsLinux || Software.IsMac)
        {
            if (statvfs(path, out Statvfs stat) == 0) return (long)(stat.f_blocks * stat.f_frsize);
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    #endregion

    #region File System Information

    public static string GetFileSystemType(string path)
    {
        if (Software.IsWindows) return GetWindowsFileSystemType(path);

        if (Software.IsLinux || Software.IsMac) return GetUnixFileSystemType(path);

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    private static string GetWindowsFileSystemType(string path)
    {
        if (!Directory.Exists(path)) throw new ArgumentException($"Path does not exist: {path}");

#pragma warning disable CA1416
        ManagementObjectSearcher searcher = new($"SELECT FileSystem FROM Win32_LogicalDisk WHERE DeviceID='{path}'");
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item["FileSystem"] is string fileSystem) return fileSystem;
        }
#pragma warning restore CA1416

        throw new("File system type not found.");
    }

    private static string GetUnixFileSystemType(string path)
    {
        if (!Directory.Exists(path)) throw new ArgumentException($"Path does not exist: {path}");

        string output = SystemCalls.Shell.ExecCommand($"df -T {path} | awk 'NR==2 {{print $2}}'");
        return output.Trim();
    }

    #endregion

    #region Disk Usage by Directory

    public static Dictionary<string, long> GetDiskUsageByDirectory(string path)
    {
        if (!Directory.Exists(path)) throw new ArgumentException($"Path does not exist: {path}");

        Dictionary<string, long> directorySizes = new();
        foreach (string dir in Directory.GetDirectories(path))
        {
            long size = GetDirectorySize(dir);
            directorySizes.Add(dir, size);
        }

        return directorySizes;
    }

    private static long GetDirectorySize(string path)
    {
        long size = 0;
        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            size += new FileInfo(file).Length;

        return size;
    }

    #endregion
}