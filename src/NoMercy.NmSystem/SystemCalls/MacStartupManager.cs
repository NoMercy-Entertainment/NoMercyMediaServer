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
using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.SystemCalls;

internal static class MacStartupManager
{
    [SupportedOSPlatform(platformName: "macos")]
    public static bool IsMacStartupEnabled()
    {
        try
        {
            return File.Exists(path: GetLaunchdPlistPath());
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates a macOS LaunchAgent plist file.
    /// On desktop, starts the Launcher (GUI app, no --service flag).
    /// Falls back to the server with --service if Launcher is not found.
    /// </summary>
    [SupportedOSPlatform(platformName: "macos")]
    public static (string Content, string Path) GenerateLaunchdPlist()
    {
        string? launcherPath = StartupManagerShared.ResolveLauncherPath();
        string plistPath = GetLaunchdPlistPath();
        string logPath = AppFiles.LogPath;

        // Prefer Launcher (GUI app) — no --service flag needed
        if (launcherPath is not null)
        {
            string plistContent = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>tv.nomercy.mediaserver</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{launcherPath}</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>KeepAlive</key>
                    <false/>
                    <key>StandardOutPath</key>
                    <string>{logPath}/nomercy-launcher-stdout.log</string>
                    <key>StandardErrorPath</key>
                    <string>{logPath}/nomercy-launcher-stderr.log</string>
                    <key>WorkingDirectory</key>
                    <string>{Path.GetDirectoryName(path: launcherPath)}</string>
                </dict>
                </plist>
                """;
            return (plistContent, plistPath);
        }

        // Fallback: start server directly with --service
        string serverPath = StartupManagerShared.GetExecutablePath();
        string fallbackContent = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>tv.nomercy.mediaserver</string>
                <key>ProgramArguments</key>
                <array>
                    <string>{serverPath}</string>
                    <string>--service</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
                <key>KeepAlive</key>
                <true/>
                <key>StandardOutPath</key>
                <string>{logPath}/nomercy-stdout.log</string>
                <key>StandardErrorPath</key>
                <string>{logPath}/nomercy-stderr.log</string>
                <key>WorkingDirectory</key>
                <string>{Path.GetDirectoryName(path: serverPath)}</string>
            </dict>
            </plist>
            """;
        return (fallbackContent, plistPath);
    }

    [SupportedOSPlatform(platformName: "macos")]
    public static void RegisterMacStartup()
    {
        try
        {
            (string plistContent, string plistPath) = GenerateLaunchdPlist();

            string? directory = Path.GetDirectoryName(path: plistPath);
            if (!string.IsNullOrEmpty(value: directory))
                Directory.CreateDirectory(path: directory);

            File.WriteAllText(path: plistPath, contents: plistContent);
            Logger.App(message: "macOS LaunchAgent registration successful.");
        }
        catch (Exception ex)
        {
            Logger.App(message: $"Failed to register macOS LaunchAgent: {ex.Message}");
        }
    }

    [SupportedOSPlatform(platformName: "macos")]
    public static void UnregisterMacStartup()
    {
        try
        {
            string plistPath = GetLaunchdPlistPath();

            if (File.Exists(path: plistPath))
            {
                File.Delete(path: plistPath);
                Logger.App(message: "macOS LaunchAgent unregistration successful.");
            }
        }
        catch (Exception ex)
        {
            Logger.App(message: $"Failed to unregister macOS LaunchAgent: {ex.Message}");
        }
    }

    [SupportedOSPlatform(platformName: "macos")]
    public static string GetLaunchdPlistPath()
    {
        return Path.Combine(
            path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.UserProfile),
            path2: "Library/LaunchAgents/tv.nomercy.mediaserver.plist"
        );
    }
}
