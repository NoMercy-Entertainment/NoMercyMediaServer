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

namespace NoMercy.NmSystem.SystemCalls;

// Static facade over the OS-specific startup managers. Prefer injecting
// IStartupManager where a DI scope is available; the per-OS strategy logic
// lives in WindowsStartupManager / MacStartupManager / LinuxStartupManager.
public class AutoStartupManager
{
    public static void Initialize() => StartupManager.Current.Initialize();

    public static void Remove() => StartupManager.Current.Remove();

    public static bool IsEnabled() => StartupManager.Current.IsEnabled();

    internal static string? ResolveLauncherPath() => StartupManagerShared.ResolveLauncherPath();

    internal static string GetExecutablePath() => StartupManagerShared.GetExecutablePath();

    [SupportedOSPlatform("macos")]
    public static (string Content, string Path) GenerateLaunchdPlist() =>
        MacStartupManager.GenerateLaunchdPlist();

    [SupportedOSPlatform("macos")]
    internal static string GetLaunchdPlistPath() => MacStartupManager.GetLaunchdPlistPath();

    [SupportedOSPlatform("linux")]
    public static (string Content, string Path) GenerateSystemdUnit() =>
        LinuxStartupManager.GenerateSystemdUnit();

    [SupportedOSPlatform("linux")]
    public static (string Content, string Path) GenerateXdgAutostart(string launcherPath) =>
        LinuxStartupManager.GenerateXdgAutostart(launcherPath);

    [SupportedOSPlatform("linux")]
    internal static string GetSystemdUnitPath() => LinuxStartupManager.GetSystemdUnitPath();

    [SupportedOSPlatform("linux")]
    internal static string GetXdgAutostartPath() => LinuxStartupManager.GetXdgAutostartPath();
}
