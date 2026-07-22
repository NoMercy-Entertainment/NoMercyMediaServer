// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;

namespace NoMercy.NmSystem.Information;

public class Storage
{
    #region Storage Device Information

    public static List<StorageDevice> GetStorageDevices()
    {
        if (Software.IsWindows)
            return GetWindowsStorageDevices();

        if (Software.IsLinux || Software.IsMac)
            return GetUnixStorageDevices();

        throw new PlatformNotSupportedException(message: "Unsupported operating system.");
    }

    private static List<StorageDevice> GetWindowsStorageDevices()
    {
        List<StorageDevice> devices = [];

#pragma warning disable CA1416
        ManagementObjectSearcher searcher = new(
            queryString: "SELECT * FROM Win32_LogicalDisk WHERE DriveType=3"
        );
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item[propertyName: "DeviceID"] is string deviceId)
                devices.Add(
                    item: new()
                    {
                        Name = deviceId,
                        TotalSpace = (long)(ulong)item[propertyName: "Size"],
                        FreeSpace = (long)(ulong)item[propertyName: "FreeSpace"],
                    }
                );
        }
#pragma warning restore CA1416

        return devices;
    }

    private static List<StorageDevice> GetUnixStorageDevices()
    {
        List<StorageDevice> devices = [];

        string output = Shell.ExecCommand(command: "df -k");
        string[] lines = output.Split(separator: '\n');
        foreach (string line in lines.Skip(count: 1))
        {
            if (string.IsNullOrWhiteSpace(value: line))
                continue;
            string[] parts = line.Split(separator: [' '], options: StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6)
                continue;

            // `df -k` rows for tmpfs/devfs/none can carry hyphens or non-numeric
            // sentinels in size columns. TryParse so a single oddball line
            // doesn't crash the whole device enumeration.
            if (
                !long.TryParse(s: parts[1], result: out long totalKb)
                || !long.TryParse(s: parts[3], result: out long freeKb)
            )
                continue;

            devices.Add(
                item: new()
                {
                    Name = parts[0],
                    TotalSpace = totalKb * 1024,
                    FreeSpace = freeKb * 1024,
                }
            );
        }

        return devices;
    }

    #endregion

    #region Space Information

    public static long GetUsedSpace(IStorageDriver driver, string path)
    {
        long totalSpace = GetTotalSpace(path: path);
        long freeSpace = GetFreeSpace(driver: driver, path: path);
        return totalSpace - freeSpace;
    }

    private static long GetFreeSpace(IStorageDriver driver, string path)
    {
        if (Software.IsWindows)
            return GetWindowsFreeSpace(driver: driver, path: path);

        if (Software.IsLinux || Software.IsMac)
            return GetUnixFreeSpace(driver: driver, path: path);

        throw new PlatformNotSupportedException(message: "Unsupported operating system.");
    }

    private static long GetWindowsFreeSpace(IStorageDriver driver, string path)
    {
        if (!driver.DirectoryExists(path: path))
            throw new ArgumentException(message: $"Path does not exist: {path}");

        if (GetDiskFreeSpaceEx(lpDirectoryName: path, lpFreeBytesAvailable: out ulong freeBytesAvailable, lpTotalNumberOfBytes: out _, lpTotalNumberOfFreeBytes: out _))
            return (long)freeBytesAvailable;

        throw new Win32Exception(error: Marshal.GetLastWin32Error());
    }

    [DllImport(dllName: "kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetDiskFreeSpaceEx(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes
    );

    [StructLayout(layoutKind: LayoutKind.Sequential)]
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

    [DllImport(dllName: "libc.so.6", EntryPoint = "statvfs", SetLastError = true)]
    private static extern int statvfs(string path, out Statvfs buf);

    private static long GetUnixFreeSpace(IStorageDriver driver, string path)
    {
        if (!driver.DirectoryExists(path: path))
            throw new ArgumentException(message: $"Path does not exist: {path}");

        if (statvfs(path: path, buf: out Statvfs stat) == 0)
            return (long)(stat.f_bavail * stat.f_frsize);

        throw new Win32Exception(error: Marshal.GetLastWin32Error());
    }

    private static long GetTotalSpace(string path)
    {
        if (Software.IsWindows)
        {
            if (GetDiskFreeSpaceEx(lpDirectoryName: path, lpFreeBytesAvailable: out _, lpTotalNumberOfBytes: out ulong totalBytes, lpTotalNumberOfFreeBytes: out _))
                return (long)totalBytes;
            throw new Win32Exception(error: Marshal.GetLastWin32Error());
        }

        if (Software.IsLinux || Software.IsMac)
        {
            if (statvfs(path: path, buf: out Statvfs stat) == 0)
                return (long)(stat.f_blocks * stat.f_frsize);
            throw new Win32Exception(error: Marshal.GetLastWin32Error());
        }

        throw new PlatformNotSupportedException(message: "Unsupported operating system.");
    }

    #endregion

    #region File System Information

    public static string GetFileSystemType(IStorageDriver driver, string path)
    {
        if (Software.IsWindows)
            return GetWindowsFileSystemType(driver: driver, path: path);

        if (Software.IsLinux || Software.IsMac)
            return GetUnixFileSystemType(driver: driver, path: path);

        throw new PlatformNotSupportedException(message: "Unsupported operating system.");
    }

    private static string GetWindowsFileSystemType(IStorageDriver driver, string path)
    {
        if (!driver.DirectoryExists(path: path))
            throw new ArgumentException(message: $"Path does not exist: {path}");

#pragma warning disable CA1416
        ManagementObjectSearcher searcher = new(
            queryString: $"SELECT FileSystem FROM Win32_LogicalDisk WHERE DeviceID='{path}'"
        );
        foreach (ManagementBaseObject? o in searcher.Get())
        {
            ManagementObject? item = (ManagementObject)o;
            if (item[propertyName: "FileSystem"] is string fileSystem)
                return fileSystem;
        }
#pragma warning restore CA1416

        throw new(message: "File system type not found.");
    }

    private static string GetUnixFileSystemType(IStorageDriver driver, string path)
    {
        if (!driver.DirectoryExists(path: path))
            throw new ArgumentException(message: $"Path does not exist: {path}");

        string output = Shell.ExecCommand(
            command: $"df -T {Shell.EscapeShellArgument(value: path)} | awk 'NR==2 {{print $2}}'"
        );
        return output.Trim();
    }

    #endregion

    #region Disk Usage by Directory

    public static Dictionary<string, long> GetDiskUsageByDirectory(
        IStorageDriver driver,
        string path,
        CancellationToken ct = default
    )
    {
        if (!driver.DirectoryExists(path: path))
            throw new ArgumentException(message: $"Path does not exist: {path}");

        Dictionary<string, long> directorySizes = new();
        foreach (
            string dir in driver
                .EnumerateFileSystemEntries(directory: path, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
                .Where(predicate: e => driver.DirectoryExists(path: e))
        )
        {
            ct.ThrowIfCancellationRequested();
            long size = GetDirectorySize(driver: driver, path: dir, ct: ct);
            directorySizes.Add(key: dir, value: size);
        }

        return directorySizes;
    }

    private static long GetDirectorySize(IStorageDriver driver, string path, CancellationToken ct)
    {
        long size = 0;
        foreach (
            string entry in driver.EnumerateFileSystemEntries(
                directory: path,
                searchPattern: "*",
                option: SearchOption.AllDirectories
            )
        )
        {
            ct.ThrowIfCancellationRequested();
            if (driver.FileExists(path: entry))
                size += driver.GetFileSize(path: entry);
        }

        return size;
    }

    #endregion
}
