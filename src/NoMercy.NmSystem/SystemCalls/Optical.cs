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

using System.Runtime.InteropServices;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.NmSystem.SystemCalls;

public static class Optical
{
    public static Dictionary<string, string?> GetOpticalDrives()
    {
        if (Software.IsWindows)
            return GetWindowsOpticalDrives();
        if (Software.IsLinux)
            return GetLinuxOpticalDrives();
        if (Software.IsMac)
            return GetMacOpticalDrives();

        throw new PlatformNotSupportedException(message: "Unsupported OS.");
    }

    private static Dictionary<string, string?> GetWindowsOpticalDrives()
    {
        Dictionary<string, string?> drives = new();
        foreach (DriveInfo drive in DriveInfo.GetDrives())
            if (drive is { DriveType: DriveType.CDRom, IsReady: true })
                drives[key: drive.Name] = drive.VolumeLabel.Length > 0 ? drive.VolumeLabel : null;
            else if (drive.DriveType == DriveType.CDRom)
                drives[key: drive.Name] = null;

        return drives;
    }

    private static Dictionary<string, string?> GetLinuxOpticalDrives()
    {
        Dictionary<string, string?> drives = new();
        List<string> output = RunShellCommand(command: "lsblk -o NAME,MOUNTPOINT,LABEL -n | grep sr");

        foreach (string line in output)
        {
            string[] parts = line.Split(separator: [' '], options: StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            string path = $"/dev/{parts[0]}";
            drives[key: path] = parts.Length > 2 ? parts[2] : null;
        }

        return drives;
    }

    private static Dictionary<string, string?> GetMacOpticalDrives()
    {
        Dictionary<string, string?> drives = new();
        List<string> output = RunShellCommand(command: "diskutil list | grep -i 'CD/DVD'");

        foreach (string line in output)
        {
            string[] parts = line.Split(separator: [' '], options: StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 0)
                continue;

            string path = parts[^1]; // Last item is typically the disk identifier (e.g., /dev/disk2)
            drives[key: path] = RunShellCommand(command: $"diskutil info {path} | grep 'Volume Name'")
                .FirstOrDefault()
                ?.Split(separator: ": ")[1];
        }

        return drives;
    }

    private static List<string> RunShellCommand(string command)
    {
        List<string> outputLines = [];

        try
        {
            string result = Shell.ExecCommand(command: command);
            outputLines.AddRange(
                collection: result.Split(separator: [Environment.NewLine], options: StringSplitOptions.RemoveEmptyEntries)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(message: $"[ERROR] Failed to run command '{command}': {ex.Message}");
        }

        return outputLines;
    }

    public static bool OpenDrive(string drivePath)
    {
        if (Software.IsWindows)
            return OpenWindowsOpticalDrives(drivePath: drivePath);
        if (Software.IsLinux)
            return OpenLinuxOpticalDrives(drivePath: drivePath);
        if (Software.IsMac)
            return OpenMacOpticalDrives(drivePath: drivePath);

        throw new PlatformNotSupportedException(message: "Unsupported OS.");
    }

    public static bool CloseDrive(string drivePath)
    {
        if (Software.IsWindows)
            return CloseWindowsOpticalDrives(drivePath: drivePath);
        if (Software.IsLinux)
            return CloseLinuxOpticalDrives(drivePath: drivePath);
        if (Software.IsMac)
            return CloseMacOpticalDrives(drivePath: drivePath);

        throw new PlatformNotSupportedException(message: "Unsupported OS.");
    }

    #region Windows Optical Drive Control

    [DllImport(dllName: "winmm.dll", EntryPoint = "mciSendString")]
    public static extern int mciSendString(
        string lpstrCommand,
        string lpstrReturnString,
        int uReturnLength,
        int hwndCallback
    );

    private static bool OpenWindowsOpticalDrives(string drivePath)
    {
        if (!IsOpticalDrive(drivePath: drivePath))
            return false; // Early check

        try
        {
            int locked = mciSendString(
                lpstrCommand: $"open {drivePath[index: 0]}: type CDAudio alias drive{drivePath[index: 0]}",
                lpstrReturnString: string.Empty,
                uReturnLength: 0,
                hwndCallback: 0
            );
            if (locked != 0)
                return false; // Check if open was successful

            int result = mciSendString(lpstrCommand: $"set drive{drivePath[index: 0]} door open", lpstrReturnString: string.Empty, uReturnLength: 0, hwndCallback: 0);
            int released = mciSendString(lpstrCommand: $"close drive{drivePath[index: 0]}", lpstrReturnString: string.Empty, uReturnLength: 0, hwndCallback: 0);

            return result == 0;
        }
        catch (Exception ex)
        {
            Logger.Error(
                message: $"[ERROR] Failed to open Windows optical drive '{drivePath}': {ex.Message}"
            );
            return false;
        }
    }

    private static bool CloseWindowsOpticalDrives(string drivePath)
    {
        if (!IsOpticalDrive(drivePath: drivePath))
            return false; //Early check

        try
        {
            int locked = mciSendString(
                lpstrCommand: $"open {drivePath[index: 0]}: type CDAudio alias drive{drivePath[index: 0]}",
                lpstrReturnString: string.Empty,
                uReturnLength: 0,
                hwndCallback: 0
            );
            if (locked != 0)
                return false; // check if open was successful

            int result = mciSendString(lpstrCommand: $"set drive{drivePath[index: 0]} door closed", lpstrReturnString: string.Empty, uReturnLength: 0, hwndCallback: 0);
            int released = mciSendString(lpstrCommand: $"close drive{drivePath[index: 0]}", lpstrReturnString: string.Empty, uReturnLength: 0, hwndCallback: 0);

            return result == 0;
        }
        catch (Exception ex)
        {
            Logger.Error(
                message: $"[ERROR] Failed to close Windows optical drive '{drivePath}': {ex.Message}"
            );
            return false;
        }
    }

    private static bool IsOpticalDrive(string drivePath)
    {
        DriveInfo driveInfo = new(driveName: drivePath);
        return driveInfo.DriveType == DriveType.CDRom;
    }

    #endregion

    #region Linux Optical Drive Control

    private static bool OpenLinuxOpticalDrives(string drivePath)
    {
        try
        {
            Shell.ExecSync(executable: "eject", arguments: [drivePath]);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(
                message: $"[ERROR] Failed to open Linux optical drive '{drivePath}': {ex.Message}"
            );
            return false;
        }
    }

    private static bool CloseLinuxOpticalDrives(string drivePath)
    {
        try
        {
            Shell.ExecSync(executable: "eject", arguments: ["-t", drivePath]);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(
                message: $"[ERROR] Failed to close Linux optical drive '{drivePath}': {ex.Message}"
            );
            return false;
        }
    }

    #endregion

    #region macOS Optical Drive Control

    private static bool OpenMacOpticalDrives(string drivePath)
    {
        try
        {
            Shell.ExecSync(executable: "drutil", arguments: ["eject", drivePath]);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(
                message: $"[ERROR] Failed to open macOS optical drive '{drivePath}': {ex.Message}"
            );
            return false;
        }
    }

    private static bool CloseMacOpticalDrives(string drivePath)
    {
        try
        {
            Shell.ExecSync(executable: "drutil", arguments: ["tray", "close", drivePath]);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(
                message: $"[ERROR] Failed to close macOS optical drive '{drivePath}': {ex.Message}"
            );
            return false;
        }
    }

    #endregion

    public static OpticalDiscType GetDiscType(string drivePath)
    {
        // LOCAL-ONLY: Optical is a static class in NmSystem; no reference to NoMercy.Providers.
        IStorageDriver driver = new LocalStorageDriver();

        if (!driver.DirectoryExists(path: drivePath))
            return OpticalDiscType.None;

        // Check for Blu-ray
        if (driver.DirectoryExists(path: Path.Combine(path1: drivePath, path2: "BDMV")))
            return OpticalDiscType.BluRay;

        // Check for DVD
        if (driver.DirectoryExists(path: Path.Combine(path1: drivePath, path2: "VIDEO_TS")))
            return OpticalDiscType.Dvd;

        // Check for CD (Audio CD or Data CD)
        try
        {
            DriveInfo drive = new(driveName: drivePath);
            if (drive is { DriveType: DriveType.CDRom, IsReady: true })
                // If we get here and it's not BD or DVD, it's some form of CD
                return OpticalDiscType.Cd;
        }
        catch
        {
            return OpticalDiscType.None;
        }

        return OpticalDiscType.None;
    }
}
