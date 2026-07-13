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

using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DeviceId;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;

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
        bool inContainer =
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") is "true" or "1";

        string? generatedId = new DeviceIdBuilder()
            .OnWindows(windows => windows.AddMotherboardSerialNumber().AddSystemDriveSerialNumber())
            .OnLinux(linux =>
            {
                linux.AddMotherboardSerialNumber().AddSystemDriveSerialNumber();

                // Inside a container the motherboard/drive serials live in root-only
                // DMI/sysfs files, so the non-root app user reads them back empty and
                // every recreated container collapses onto the same degenerate id —
                // recreating the container then registers a brand-new server each time.
                // The host machine-id (bind-mounted read-only by the shipped compose)
                // is a stable, unique-per-host token the non-root process CAN read, so a
                // recreated container keeps its identity. Gated to containers only: the
                // bare-metal fingerprint is unchanged, so already-registered hosts keep
                // the exact id their DNS subdomain and certificate were issued for.
                if (inContainer)
                    linux.AddMachineId();
            })
            .OnMac(mac => mac.AddSystemDriveVolumeUUID().AddPlatformSerialNumber())
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
            string output = Shell.ExecCommand("uname -r");
            return output.Trim();
        }

        return "Unknown";
    }

    public static string GetReleaseVersion()
    {
        return $"{Version!.Major}.{Version.Minor}.{Version.Build}";
    }

    public static string? GetFileVersion(IStorageDriver driver, string exePath)
    {
        try
        {
            if (!driver.FileExists(exePath))
                return null;

            FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(exePath);
            if (
                fileInfo.FileMajorPart == 0
                && fileInfo.FileMinorPart == 0
                && fileInfo.FileBuildPart == 0
            )
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
            ManagementObjectSearcher searcher = new(
                "select LastBootUpTime from Win32_OperatingSystem"
            );
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                return ManagementDateTimeConverter.ToDateTime(item["LastBootUpTime"].ToString());
#pragma warning restore CA1416
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string output = Shell.ExecCommand("sysctl -n kern.boottime");
            // Tolerate unexpected sysctl output rather than crashing the
            // diagnostic call; fall through to DateTime.UtcNow as a sentinel.
            return long.TryParse(output.Split(' ').Last(), out long bootSec)
                ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(bootSec)
                : DateTime.UtcNow;
        }
        else
        {
            string output = Shell.ExecCommand("uptime -s");
            // "uptime -s" can return sentinels (e.g. "Unknown") that
            // culture-sensitive DateTime.Parse throws on — tolerate them the
            // same way the macOS branch above does.
            return DateTime.TryParse(
                output.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime bootTime
            )
                ? bootTime
                : DateTime.UtcNow;
        }

        return DateTime.MinValue;
    }
}
