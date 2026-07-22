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
using System.Runtime.InteropServices;
using System.Text;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Setup.Ui;

public static class DesktopIconCreator
{
    // LOCAL-ONLY: DesktopIconCreator is in NoMercy.Setup which cannot reference NoMercy.Providers (circular).
    private static IStorageDriver _driver => new LocalStorageDriver();

    public static void CreateDesktopIcon(string appName, string appPath, string iconPath) =>
        CreateDesktopIcon(
            appName: appName,
            appPath: appPath,
            iconPath: iconPath,
            desktopPath: Environment.GetFolderPath(folder: Environment.SpecialFolder.Desktop)
        );

    /// <summary>
    /// Internal (not private): threading the desktop directory through as a parameter
    /// lets NoMercy.Tests.Setup exercise the real per-platform shortcut-writing branches
    /// against an isolated temp directory instead of either skipping this method entirely
    /// or — worse — actually writing a shortcut onto the real Desktop of whatever machine
    /// runs the test suite. The public overload above preserves the exact production
    /// default, so no caller behavior changes.
    /// </summary>
    internal static void CreateDesktopIcon(
        string appName,
        string appPath,
        string iconPath,
        string desktopPath
    )
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
                CreateWindowsShortcut(appName: appName, appPath: appPath, iconPath: iconPath, desktopPath: desktopPath);
            else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
                CreateMacShortcut(appName: appName, appPath: appPath, iconPath: iconPath, desktopPath: desktopPath);
            else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
                CreateLinuxShortcut(appName: appName, appPath: appPath, iconPath: iconPath, desktopPath: desktopPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine(value: $"Error creating desktop icon: {ex.Message}");
        }
    }

    private static void CreateWindowsShortcut(
        string appName,
        string appPath,
        string iconPath,
        string desktopPath
    )
    {
#pragma warning disable CA1416
        try
        {
            string shortcutPath = Path.Combine(path1: desktopPath, path2: $"{appName}.lnk");

            Type? id = Type.GetTypeFromProgID(progID: "WScript.Shell");
            if (id == null)
                return;

            dynamic shell = Activator.CreateInstance(type: id) ?? throw new InvalidOperationException();
            if (shell == null)
                return;

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = appPath;
            shortcut.IconLocation = iconPath;
            shortcut.Save();
        }
        catch (Exception ex)
        {
            Console.WriteLine(value: $"Error creating Windows shortcut: {ex.Message}");
        }
#pragma warning restore CA1416
    }

    private static void CreateMacShortcut(
        string appName,
        string appPath,
        string iconPath,
        string desktopPath
    )
    {
        try
        {
            string aliasPath = Path.Combine(path1: desktopPath, path2: appName);

            string script =
                $@"
            tell application ""Finder""
                set appAlias to make new alias file at POSIX file ""{desktopPath}"" to POSIX file ""{appPath}""
                set name of appAlias to ""{appName}""
            end tell";

            string scriptPath = "/tmp/CreateShortcut.scpt";
            using (Stream scriptStream = _driver.OpenWrite(path: scriptPath, overwrite: true))
            using (StreamWriter scriptWriter = new(stream: scriptStream, encoding: Encoding.UTF8, leaveOpen: true))
                scriptWriter.Write(value: script);
            using (Process? osascriptProc = Process.Start(fileName: "osascript", arguments: scriptPath))
                osascriptProc.WaitForExit();

            if (!string.IsNullOrEmpty(value: iconPath) && _driver.FileExists(path: iconPath))
            {
                string iconDest = Path.Combine(path1: aliasPath, path2: "Icon.icns");
                _driver.CopyFile(source: iconPath, destination: iconDest, overwrite: true);

                using (
                    Process? shProc = Process.Start(
                        fileName: "sh",
                        arguments: $"-c \"cp '{iconPath}' '{aliasPath}/Icon.icns' && /usr/bin/SetFile -a C '{aliasPath}'\""
                    )
                )
                    shProc.WaitForExit();

                using (Process? killProc = Process.Start(fileName: "killall", arguments: "Finder"))
                    killProc.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(value: $"Error creating Mac shortcut: {ex.Message}");
        }
    }

    private static void CreateLinuxShortcut(
        string appName,
        string appPath,
        string iconPath,
        string desktopPath
    )
    {
        try
        {
            string shortcutPath = Path.Combine(path1: desktopPath, path2: $"{appName}.desktop");

            string content =
                $@"
                [Desktop Entry]
                Name={appName}
                Exec={appPath}
                Icon={iconPath}
                Type=Application
                Terminal=false";

            using (Stream shortcutStream = _driver.OpenWrite(path: shortcutPath, overwrite: true))
            using (
                StreamWriter shortcutWriter = new(stream: shortcutStream, encoding: Encoding.UTF8, leaveOpen: true)
            )
                shortcutWriter.Write(value: content);
            using (Process? chmodProc = Process.Start(fileName: "chmod", arguments: $"+x \"{shortcutPath}\""))
                chmodProc.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine(value: $"Error creating Linux shortcut: {ex.Message}");
        }
    }
}
