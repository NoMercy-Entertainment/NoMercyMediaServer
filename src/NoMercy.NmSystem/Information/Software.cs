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
    public static Version? Version { get; set; } = new(major: 0, minor: 1, build: 0, revision: 0);

    public static bool IsWindows => RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows);
    public static bool IsMac => RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux);

    internal static string GetPlatform()
    {
        if (IsWindows)
            return "windows";
        if (IsMac)
            return "mac";
        if (IsLinux)
            return "linux";

        throw new(message: "Unknown platform");
    }

    internal static Guid GetDeviceId()
    {
        bool inContainer =
            Environment.GetEnvironmentVariable(variable: "DOTNET_RUNNING_IN_CONTAINER") is "true" or "1";

        string? generatedId = new DeviceIdBuilder()
            .OnWindows(windowsBuilderConfiguration: windows => windows.AddMotherboardSerialNumber().AddSystemDriveSerialNumber())
            .OnLinux(linuxBuilderConfiguration: linux =>
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
            .OnMac(macBuilderConfiguration: mac => mac.AddSystemDriveVolumeUUID().AddPlatformSerialNumber())
            .ToString();

        byte[] hash = MD5.HashData(source: Encoding.UTF8.GetBytes(s: generatedId));

        return new(b: hash);
    }

    public static string? GetSystemVersion()
    {
        if (IsWindows)
        {
#pragma warning disable CA1416
            ManagementObjectSearcher searcher = new(queryString: "select Version from Win32_OperatingSystem");
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                return item[propertyName: "Version"].ToString();
            }
#pragma warning restore CA1416
        }
        else
        {
            string output = Shell.ExecCommand(command: "uname -r");
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
            if (!driver.FileExists(path: exePath))
                return null;

            FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(fileName: exePath);
            if (
                fileInfo is { FileMajorPart: 0, FileMinorPart: 0, FileBuildPart: 0 }
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
                queryString: "select LastBootUpTime from Win32_OperatingSystem"
            );
            foreach (ManagementBaseObject? o in searcher.Get())
            {
                ManagementObject? item = (ManagementObject)o;
                return ManagementDateTimeConverter.ToDateTime(dmtfDate: item[propertyName: "LastBootUpTime"].ToString());
#pragma warning restore CA1416
            }
        }
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
        {
            string output = Shell.ExecCommand(command: "sysctl -n kern.boottime");
            // Tolerate unexpected sysctl output rather than crashing the
            // diagnostic call; fall through to DateTime.UtcNow as a sentinel.
            return long.TryParse(s: output.Split(separator: ' ').Last(), result: out long bootSec)
                ? new DateTime(year: 1970, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc).AddSeconds(value: bootSec)
                : DateTime.UtcNow;
        }
        else
        {
            string output = Shell.ExecCommand(command: "uptime -s");
            // "uptime -s" can return sentinels (e.g. "Unknown") that
            // culture-sensitive DateTime.Parse throws on — tolerate them the
            // same way the macOS branch above does.
            return DateTime.TryParse(
                s: output.Trim(),
                provider: CultureInfo.InvariantCulture,
                styles: DateTimeStyles.None,
                result: out DateTime bootTime
            )
                ? bootTime
                : DateTime.UtcNow;
        }

        return DateTime.MinValue;
    }
}
