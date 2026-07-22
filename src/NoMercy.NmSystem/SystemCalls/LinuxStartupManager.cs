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

internal static class LinuxStartupManager
{
    [SupportedOSPlatform(platformName: "linux")]
    public static bool IsLinuxStartupEnabled()
    {
        try
        {
            // Check both mechanisms: XDG autostart (desktop) and systemd (headless)
            return File.Exists(path: GetXdgAutostartPath()) || File.Exists(path: GetSystemdUnitPath());
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates a systemd user service unit file for headless Linux.
    /// Starts the server directly with --service.
    /// </summary>
    [SupportedOSPlatform(platformName: "linux")]
    public static (string Content, string Path) GenerateSystemdUnit()
    {
        string appPath = StartupManagerShared.GetExecutablePath();
        string unitPath = GetSystemdUnitPath();

        string unitContent = $"""
            [Unit]
            Description=NoMercy MediaServer
            After=network-online.target
            Wants=network-online.target

            [Service]
            Type=notify
            ExecStart={appPath} --service
            WorkingDirectory={Path.GetDirectoryName(path: appPath)}
            Restart=on-failure
            RestartSec=10
            StandardOutput=journal
            StandardError=journal
            SyslogIdentifier=nomercy-mediaserver
            Environment=DOTNET_ROOT=/usr/share/dotnet

            [Install]
            WantedBy=default.target
            """;

        return (unitContent, unitPath);
    }

    /// <summary>
    /// Generates an XDG autostart .desktop file for the Launcher on desktop Linux.
    /// </summary>
    [SupportedOSPlatform(platformName: "linux")]
    public static (string Content, string Path) GenerateXdgAutostart(string launcherPath)
    {
        string desktopPath = GetXdgAutostartPath();

        string desktopContent = $"""
            [Desktop Entry]
            Type=Application
            Name=NoMercy Launcher
            Comment=Launcher for NoMercy MediaServer
            Exec={launcherPath}
            Icon=NoMercy-MediaServer
            Terminal=false
            StartupNotify=true
            X-GNOME-Autostart-enabled=true
            Categories=AudioVideo;Video;Player;Network;
            """;

        return (desktopContent, desktopPath);
    }

    [SupportedOSPlatform(platformName: "linux")]
    public static void RegisterLinuxStartup()
    {
        try
        {
            if (Screen.IsDesktopEnvironment())
            {
                // Desktop: use XDG autostart for the Launcher
                string? launcherPath = StartupManagerShared.ResolveLauncherPath();
                if (launcherPath is not null)
                {
                    (string desktopContent, string desktopPath) = GenerateXdgAutostart(
                        launcherPath: launcherPath
                    );

                    string? directory = Path.GetDirectoryName(path: desktopPath);
                    if (!string.IsNullOrEmpty(value: directory))
                        Directory.CreateDirectory(path: directory);

                    File.WriteAllText(path: desktopPath, contents: desktopContent);
                    Logger.App(message: $"XDG autostart entry written to {desktopPath}");

                    // Clean up headless systemd unit if it exists
                    string unitPath = GetSystemdUnitPath();
                    if (File.Exists(path: unitPath))
                    {
                        File.Delete(path: unitPath);
                        Logger.App(message: "Removed stale systemd unit (switched to desktop mode).");
                    }

                    return;
                }

                Logger.App(
                    message: "Launcher binary not found; falling back to systemd service for server."
                );
            }

            // Headless (or Launcher not found): systemd user service for the server
            (string unitContent, string unitPath2) = GenerateSystemdUnit();

            string? unitDir = Path.GetDirectoryName(path: unitPath2);
            if (!string.IsNullOrEmpty(value: unitDir))
                Directory.CreateDirectory(path: unitDir);

            File.WriteAllText(path: unitPath2, contents: unitContent);
            Logger.App(message: $"systemd user service unit written to {unitPath2}");
            Logger.App(message: "To enable: systemctl --user enable --now nomercy-mediaserver.service");

            // Clean up desktop autostart entry if it exists
            string xdgPath = GetXdgAutostartPath();
            if (File.Exists(path: xdgPath))
            {
                File.Delete(path: xdgPath);
                Logger.App(message: "Removed stale XDG autostart entry (switched to headless mode).");
            }
        }
        catch (Exception ex)
        {
            Logger.App(message: $"Failed to register Linux startup: {ex.Message}");
        }
    }

    [SupportedOSPlatform(platformName: "linux")]
    public static void UnregisterLinuxStartup()
    {
        try
        {
            // Remove systemd unit
            string unitPath = GetSystemdUnitPath();
            if (File.Exists(path: unitPath))
            {
                File.Delete(path: unitPath);
                Logger.App(message: "Linux systemd service unregistration successful.");
            }

            // Remove XDG autostart entry
            string xdgPath = GetXdgAutostartPath();
            if (File.Exists(path: xdgPath))
            {
                File.Delete(path: xdgPath);
                Logger.App(message: "Linux XDG autostart unregistration successful.");
            }

            // Also remove legacy desktop entry if it exists
            string legacyPath = Path.Combine(
                path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.UserProfile),
                path2: ".config/autostart/nomercymediaserver.desktop"
            );

            if (File.Exists(path: legacyPath))
            {
                File.Delete(path: legacyPath);
                Logger.App(message: "Legacy Linux desktop autostart entry removed.");
            }
        }
        catch (Exception ex)
        {
            Logger.App(message: $"Failed to unregister Linux startup: {ex.Message}");
        }
    }

    [SupportedOSPlatform(platformName: "linux")]
    public static string GetSystemdUnitPath()
    {
        string configHome =
            Environment.GetEnvironmentVariable(variable: "XDG_CONFIG_HOME")
            ?? Path.Combine(
                path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.UserProfile),
                path2: ".config"
            );
        return Path.Combine(path1: configHome, path2: "systemd/user/nomercy-mediaserver.service");
    }

    [SupportedOSPlatform(platformName: "linux")]
    public static string GetXdgAutostartPath()
    {
        string configHome =
            Environment.GetEnvironmentVariable(variable: "XDG_CONFIG_HOME")
            ?? Path.Combine(
                path1: Environment.GetFolderPath(folder: Environment.SpecialFolder.UserProfile),
                path2: ".config"
            );
        return Path.Combine(path1: configHome, path2: "autostart/nomercy-launcher.desktop");
    }
}
