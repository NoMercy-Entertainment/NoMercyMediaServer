using System.Runtime.InteropServices;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;

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

        throw new PlatformNotSupportedException("Unsupported OS.");
    }

    private static Dictionary<string, string?> GetWindowsOpticalDrives()
    {
        Dictionary<string, string?> drives = new();
        foreach (DriveInfo drive in DriveInfo.GetDrives())
            if (drive is { DriveType: DriveType.CDRom, IsReady: true })
                drives[drive.Name] = drive.VolumeLabel.Length > 0 ? drive.VolumeLabel : null;
            else if (drive.DriveType == DriveType.CDRom) drives[drive.Name] = null;

        return drives;
    }

    private static Dictionary<string, string?> GetLinuxOpticalDrives()
    {
        Dictionary<string, string?> drives = new();
        List<string> output = RunShellCommand("lsblk -o NAME,MOUNTPOINT,LABEL -n | grep sr");

        foreach (string line in output)
        {
            string[] parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            string path = $"/dev/{parts[0]}";
            drives[path] = parts.Length > 2 ? parts[2] : null;
        }

        return drives;
    }

    private static Dictionary<string, string?> GetMacOpticalDrives()
    {
        Dictionary<string, string?> drives = new();
        List<string> output = RunShellCommand("diskutil list | grep -i 'CD/DVD'");

        foreach (string line in output)
        {
            string[] parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 0) continue;

            string path = parts[^1]; // Last item is typically the disk identifier (e.g., /dev/disk2)
            drives[path] =
                RunShellCommand($"diskutil info {path} | grep 'Volume Name'").FirstOrDefault()?.Split(": ")[1];
        }

        return drives;
    }

    private static List<string> RunShellCommand(string command)
    {
        List<string> outputLines = [];

        try
        {
            string result = Shell.ExecCommand(command);
            outputLines.AddRange(result.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to run command '{command}': {ex.Message}");
        }

        return outputLines;
    }

    public static bool OpenDrive(string drivePath)
    {
        if (Software.IsWindows)
            return OpenWindowsOpticalDrives(drivePath);
        if (Software.IsLinux)
            return OpenLinuxOpticalDrives(drivePath);
        if (Software.IsMac)
            return OpenMacOpticalDrives(drivePath);

        throw new PlatformNotSupportedException("Unsupported OS.");
    }

    public static bool CloseDrive(string drivePath)
    {
        if (Software.IsWindows)
            return CloseWindowsOpticalDrives(drivePath);
        if (Software.IsLinux)
            return CloseLinuxOpticalDrives(drivePath);
        if (Software.IsMac)
            return CloseMacOpticalDrives(drivePath);

        throw new PlatformNotSupportedException("Unsupported OS.");
    }

    #region Windows Optical Drive Control

    [DllImport("winmm.dll", EntryPoint = "mciSendString")]
    public static extern int mciSendString(string lpstrCommand, string lpstrReturnString, int uReturnLength,
        int hwndCallback);

    private static bool OpenWindowsOpticalDrives(string drivePath)
    {
        if (!IsOpticalDrive(drivePath)) return false; // Early check

        try
        {
            int locked = mciSendString($"open {drivePath[0]}: type CDAudio alias drive{drivePath[0]}", string.Empty, 0,
                0);
            if (locked != 0) return false; // Check if open was successful

            int result = mciSendString($"set drive{drivePath[0]} door open", string.Empty, 0, 0);
            int released = mciSendString($"close drive{drivePath[0]}", string.Empty, 0, 0);

            return result == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to open Windows optical drive '{drivePath}': {ex.Message}");
            return false;
        }
    }

    private static bool CloseWindowsOpticalDrives(string drivePath)
    {
        if (!IsOpticalDrive(drivePath)) return false; //Early check

        try
        {
            int locked = mciSendString($"open {drivePath[0]}: type CDAudio alias drive{drivePath[0]}", string.Empty, 0,
                0);
            if (locked != 0) return false; // check if open was successful

            int result = mciSendString($"set drive{drivePath[0]} door closed", string.Empty, 0, 0);
            int released = mciSendString($"close drive{drivePath[0]}", string.Empty, 0, 0);

            return result == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to close Windows optical drive '{drivePath}': {ex.Message}");
            return false;
        }
    }

    private static bool IsOpticalDrive(string drivePath)
    {
        DriveInfo driveInfo = new(drivePath);
        return driveInfo.DriveType == DriveType.CDRom;
    }

    #endregion

    #region Linux Optical Drive Control

    private static bool OpenLinuxOpticalDrives(string drivePath)
    {
        try
        {
            RunShellCommand($"eject {drivePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to open Linux optical drive '{drivePath}': {ex.Message}");
            return false;
        }
    }

    private static bool CloseLinuxOpticalDrives(string drivePath)
    {
        try
        {
            RunShellCommand($"eject -t {drivePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to close Linux optical drive '{drivePath}': {ex.Message}");
            return false;
        }
    }

    #endregion

    #region macOS Optical Drive Control

    private static bool OpenMacOpticalDrives(string drivePath)
    {
        try
        {
            RunShellCommand($"drutil eject {drivePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to open macOS optical drive '{drivePath}': {ex.Message}");
            return false;
        }
    }

    private static bool CloseMacOpticalDrives(string drivePath)
    {
        try
        {
            RunShellCommand($"drutil tray close {drivePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to close macOS optical drive '{drivePath}': {ex.Message}");
            return false;
        }
    }

    #endregion

    public static OpticalDiscType GetDiscType(string drivePath)
    {
        if (!Directory.Exists(drivePath))
            return OpticalDiscType.None;

        // Check for Blu-ray
        if (Directory.Exists(Path.Combine(drivePath, "BDMV")))
            return OpticalDiscType.BluRay;

        // Check for DVD
        if (Directory.Exists(Path.Combine(drivePath, "VIDEO_TS")))
            return OpticalDiscType.Dvd;

        // Check for CD (Audio CD or Data CD)
        try
        {
            DriveInfo drive = new(drivePath);
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