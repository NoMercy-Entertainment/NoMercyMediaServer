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

using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.SystemCalls;

public class StartupManager : IStartupManager
{
    public static IStartupManager Current { get; } = new StartupManager();

    public void Initialize()
    {
        if (Screen.IsDocker)
        {
            Logger.App(message: "Auto-start is managed by Docker restart policy; skipping registration.");
            return;
        }

        if (OperatingSystem.IsWindows())
            WindowsStartupManager.RegisterWindowsStartup();
        else if (OperatingSystem.IsMacOS())
            MacStartupManager.RegisterMacStartup();
        else if (OperatingSystem.IsLinux())
            LinuxStartupManager.RegisterLinuxStartup();
    }

    public void Remove()
    {
        if (Screen.IsDocker)
            return;

        if (OperatingSystem.IsWindows())
            WindowsStartupManager.UnregisterWindowsStartup();
        else if (OperatingSystem.IsMacOS())
            MacStartupManager.UnregisterMacStartup();
        else if (OperatingSystem.IsLinux())
            LinuxStartupManager.UnregisterLinuxStartup();
    }

    public bool IsEnabled()
    {
        try
        {
            if (Config.IsTest)
                return false;

            if (Screen.IsDocker)
                return false;

            if (OperatingSystem.IsWindows())
                return WindowsStartupManager.IsWindowsStartupEnabled();
            if (OperatingSystem.IsMacOS())
                return MacStartupManager.IsMacStartupEnabled();
            if (OperatingSystem.IsLinux())
                return LinuxStartupManager.IsLinuxStartupEnabled();
        }
        catch
        {
            // Fall through to false on any unexpected error
        }

        return false;
    }
}
