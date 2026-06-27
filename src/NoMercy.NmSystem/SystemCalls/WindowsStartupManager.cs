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
using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.SystemCalls;

internal static class WindowsStartupManager
{
    [SupportedOSPlatform("windows")]
    public static bool IsWindowsStartupEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                false
            );
            return key?.GetValue("NoMercyMediaServer") is not null;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public static void RegisterWindowsStartup()
    {
        try
        {
            // Prefer the Launcher for desktop auto-start
            string? launcherPath = StartupManagerShared.ResolveLauncherPath();
            string targetPath = launcherPath ?? StartupManagerShared.GetExecutablePath();

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                true
            );
            if (key != null)
            {
                key.SetValue("NoMercyMediaServer", $"\"{targetPath}\"");
                Logger.App($"Windows startup registration successful: {targetPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to register Windows startup: {ex.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    public static void UnregisterWindowsStartup()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                true
            );
            if (key?.GetValue("NoMercyMediaServer") != null)
            {
                key.DeleteValue("NoMercyMediaServer");
                Logger.App("Windows startup unregistration successful.");
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to unregister Windows startup: {ex.Message}");
        }
    }
}
