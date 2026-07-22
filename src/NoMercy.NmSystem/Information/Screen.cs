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

namespace NoMercy.NmSystem.Information;

public static class Screen
{
    public static bool IsDocker =>
        !string.IsNullOrEmpty(value: Environment.GetEnvironmentVariable(variable: "DOTNET_RUNNING_IN_CONTAINER"));

    public static int ScreenWidth()
    {
        return RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows) ? ScreenWidthWindows() : 1666;
    }

    [DllImport(dllName: "user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private static int ScreenWidthWindows(int screenIndex = 0)
    {
        return GetSystemMetrics(nIndex: screenIndex);
    }

    public static bool IsDesktopEnvironment()
    {
        if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows)
            || RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX)
        )
            return true;

        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return false;

        if (IsDocker)
            return false;

        if (!string.IsNullOrEmpty(value: Environment.GetEnvironmentVariable(variable: "WSL_DISTRO_NAME")))
            return false;

        if (!string.IsNullOrEmpty(value: Environment.GetEnvironmentVariable(variable: "WAYLAND_DISPLAY")))
            return true;

        if (!string.IsNullOrEmpty(value: Environment.GetEnvironmentVariable(variable: "DISPLAY")))
            return true;

        string? sessionType = Environment.GetEnvironmentVariable(variable: "XDG_SESSION_TYPE");
        return sessionType is "x11" or "wayland";
    }
}
