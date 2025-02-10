using System.Runtime.InteropServices;

namespace NoMercy.NmSystem;

public static class StorageHelper
{
    public static long GetFreeSpace(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsFreeSpace(path);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetUnixFreeSpace(path);
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    private static long GetWindowsFreeSpace(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new ArgumentException($"Path does not exist: {path}");
        }

        if (GetDiskFreeSpaceEx(path, out ulong freeBytesAvailable, out _, out _))
        {
            return (long)freeBytesAvailable;
        }

        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

    [StructLayout(LayoutKind.Sequential)]
    public struct Statvfs
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
        if (!Directory.Exists(path))
        {
            throw new ArgumentException($"Path does not exist: {path}");
        }

        if (statvfs(path, out Statvfs stat) == 0)
        {
            return (long)(stat.f_bavail * stat.f_frsize);
        }

        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    
    public static long GetUsedSpace(string path)
    {
        long totalSpace = GetTotalSpace(path);
        long freeSpace = GetFreeSpace(path);
        return totalSpace - freeSpace;
    }

    private static long GetTotalSpace(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (GetDiskFreeSpaceEx(path, out _, out ulong totalBytes, out _))
            {
                return (long)totalBytes;
            }
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (statvfs(path, out Statvfs stat) == 0)
            {
                return (long)(stat.f_blocks * stat.f_frsize);
            }
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }
}