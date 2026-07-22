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

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NoMercy.NmSystem.SystemCalls;

internal static class WindowsStartupManager
{
    [SupportedOSPlatform(platformName: "windows")]
    public static bool IsWindowsStartupEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                name: @"Software\Microsoft\Windows\CurrentVersion\Run",
                writable: false
            );
            return key?.GetValue(name: "NoMercyMediaServer") is not null;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform(platformName: "windows")]
    public static void RegisterWindowsStartup()
    {
        try
        {
            // Prefer the Launcher for desktop auto-start
            string? launcherPath = StartupManagerShared.ResolveLauncherPath();
            string targetPath = launcherPath ?? StartupManagerShared.GetExecutablePath();

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                name: @"Software\Microsoft\Windows\CurrentVersion\Run",
                writable: true
            );
            if (key != null)
            {
                key.SetValue(name: "NoMercyMediaServer", value: $"\"{targetPath}\"");
                Logger.App(message: $"Windows startup registration successful: {targetPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.App(message: $"Failed to register Windows startup: {ex.Message}");
        }
    }

    [SupportedOSPlatform(platformName: "windows")]
    public static void UnregisterWindowsStartup()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                name: @"Software\Microsoft\Windows\CurrentVersion\Run",
                writable: true
            );
            if (key?.GetValue(name: "NoMercyMediaServer") != null)
            {
                key.DeleteValue(name: "NoMercyMediaServer");
                Logger.App(message: "Windows startup unregistration successful.");
            }
        }
        catch (Exception ex)
        {
            Logger.App(message: $"Failed to unregister Windows startup: {ex.Message}");
        }
    }
}
