using System.Runtime.Versioning;
using Microsoft.Win32;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.NmSystem;

public class AutoStartupManager
{
    public static void Initialize()
    {
        if (Screen.IsDocker)
        {
            Logger.App("Auto-start is managed by Docker restart policy; skipping registration.");
            return;
        }

        if (OperatingSystem.IsWindows())
            RegisterWindowsStartup();
        else if (OperatingSystem.IsMacOS())
            RegisterMacStartup();
        else if (OperatingSystem.IsLinux())
            RegisterLinuxStartup();
    }

    public static void Remove()
    {
        if (Screen.IsDocker)
            return;

        if (OperatingSystem.IsWindows())
            UnregisterWindowsStartup();
        else if (OperatingSystem.IsMacOS())
            UnregisterMacStartup();
        else if (OperatingSystem.IsLinux())
            UnregisterLinuxStartup();
    }

    public static bool IsEnabled()
    {
        try
        {
            if (Screen.IsDocker)
                return false;

            if (OperatingSystem.IsWindows())
                return IsWindowsStartupEnabled();
            if (OperatingSystem.IsMacOS())
                return IsMacStartupEnabled();
            if (OperatingSystem.IsLinux())
                return IsLinuxStartupEnabled();
        }
        catch
        {
            // Fall through to false on any unexpected error
        }

        return false;
    }

    // ── Launcher path resolution ────────────────────────────────────────

    /// <summary>
    /// Finds the Launcher binary. Checks the server's own directory first,
    /// then the standard download location in AppFiles.
    /// </summary>
    internal static string? ResolveLauncherPath()
    {
        // 1. Same directory as the running server process
        string? processDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(processDir))
        {
            string candidate = Path.Combine(processDir, "NoMercyLauncher" + Info.ExecSuffix);
            if (File.Exists(candidate))
                return candidate;
        }

        // 2. Standard download location
        if (File.Exists(AppFiles.LauncherExePath))
            return AppFiles.LauncherExePath;

        return null;
    }

    // ── Windows ─────────────────────────────────────────────────────────

    [SupportedOSPlatform("windows")]
    private static bool IsWindowsStartupEnabled()
    {
        try
        {
            using RegistryKey? key =
                Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue("NoMercyMediaServer") is not null;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindowsStartup()
    {
        try
        {
            // Prefer the Launcher for desktop auto-start
            string? launcherPath = ResolveLauncherPath();
            string targetPath = launcherPath ?? GetExecutablePath();

            using RegistryKey? key =
                Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
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
    private static void UnregisterWindowsStartup()
    {
        try
        {
            using RegistryKey? key =
                Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
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

    // ── macOS ───────────────────────────────────────────────────────────

    [SupportedOSPlatform("macos")]
    private static bool IsMacStartupEnabled()
    {
        try
        {
            return File.Exists(GetLaunchdPlistPath());
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
    [SupportedOSPlatform("macos")]
    public static (string Content, string Path) GenerateLaunchdPlist()
    {
        string? launcherPath = ResolveLauncherPath();
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
                                       <string>{System.IO.Path.GetDirectoryName(launcherPath)}</string>
                                   </dict>
                                   </plist>
                                   """;
            return (plistContent, plistPath);
        }

        // Fallback: start server directly with --service
        string serverPath = GetExecutablePath();
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
                                      <string>{System.IO.Path.GetDirectoryName(serverPath)}</string>
                                  </dict>
                                  </plist>
                                  """;
        return (fallbackContent, plistPath);
    }

    [SupportedOSPlatform("macos")]
    private static void RegisterMacStartup()
    {
        try
        {
            (string plistContent, string plistPath) = GenerateLaunchdPlist();

            string? directory = Path.GetDirectoryName(plistPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(plistPath, plistContent);
            Logger.App("macOS LaunchAgent registration successful.");
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to register macOS LaunchAgent: {ex.Message}");
        }
    }

    [SupportedOSPlatform("macos")]
    private static void UnregisterMacStartup()
    {
        try
        {
            string plistPath = GetLaunchdPlistPath();

            if (File.Exists(plistPath))
            {
                File.Delete(plistPath);
                Logger.App("macOS LaunchAgent unregistration successful.");
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to unregister macOS LaunchAgent: {ex.Message}");
        }
    }

    // ── Linux ───────────────────────────────────────────────────────────

    [SupportedOSPlatform("linux")]
    private static bool IsLinuxStartupEnabled()
    {
        try
        {
            // Check both mechanisms: XDG autostart (desktop) and systemd (headless)
            return File.Exists(GetXdgAutostartPath()) || File.Exists(GetSystemdUnitPath());
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
    [SupportedOSPlatform("linux")]
    public static (string Content, string Path) GenerateSystemdUnit()
    {
        string appPath = GetExecutablePath();
        string unitPath = GetSystemdUnitPath();

        string unitContent = $"""
                              [Unit]
                              Description=NoMercy MediaServer
                              After=network-online.target
                              Wants=network-online.target

                              [Service]
                              Type=notify
                              ExecStart={appPath} --service
                              WorkingDirectory={Path.GetDirectoryName(appPath)}
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
    [SupportedOSPlatform("linux")]
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

    [SupportedOSPlatform("linux")]
    private static void RegisterLinuxStartup()
    {
        try
        {
            if (Screen.IsDesktopEnvironment())
            {
                // Desktop: use XDG autostart for the Launcher
                string? launcherPath = ResolveLauncherPath();
                if (launcherPath is not null)
                {
                    (string desktopContent, string desktopPath) = GenerateXdgAutostart(launcherPath);

                    string? directory = Path.GetDirectoryName(desktopPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    File.WriteAllText(desktopPath, desktopContent);
                    Logger.App($"XDG autostart entry written to {desktopPath}");

                    // Clean up headless systemd unit if it exists
                    string unitPath = GetSystemdUnitPath();
                    if (File.Exists(unitPath))
                    {
                        File.Delete(unitPath);
                        Logger.App("Removed stale systemd unit (switched to desktop mode).");
                    }

                    return;
                }

                Logger.App("Launcher binary not found; falling back to systemd service for server.");
            }

            // Headless (or Launcher not found): systemd user service for the server
            (string unitContent, string unitPath2) = GenerateSystemdUnit();

            string? unitDir = Path.GetDirectoryName(unitPath2);
            if (!string.IsNullOrEmpty(unitDir))
                Directory.CreateDirectory(unitDir);

            File.WriteAllText(unitPath2, unitContent);
            Logger.App($"systemd user service unit written to {unitPath2}");
            Logger.App("To enable: systemctl --user enable --now nomercy-mediaserver.service");

            // Clean up desktop autostart entry if it exists
            string xdgPath = GetXdgAutostartPath();
            if (File.Exists(xdgPath))
            {
                File.Delete(xdgPath);
                Logger.App("Removed stale XDG autostart entry (switched to headless mode).");
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to register Linux startup: {ex.Message}");
        }
    }

    [SupportedOSPlatform("linux")]
    private static void UnregisterLinuxStartup()
    {
        try
        {
            // Remove systemd unit
            string unitPath = GetSystemdUnitPath();
            if (File.Exists(unitPath))
            {
                File.Delete(unitPath);
                Logger.App("Linux systemd service unregistration successful.");
            }

            // Remove XDG autostart entry
            string xdgPath = GetXdgAutostartPath();
            if (File.Exists(xdgPath))
            {
                File.Delete(xdgPath);
                Logger.App("Linux XDG autostart unregistration successful.");
            }

            // Also remove legacy desktop entry if it exists
            string legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/autostart/nomercymediaserver.desktop");

            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
                Logger.App("Legacy Linux desktop autostart entry removed.");
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to unregister Linux startup: {ex.Message}");
        }
    }

    // ── Path helpers ────────────────────────────────────────────────────

    internal static string GetExecutablePath()
    {
        // For single-file published apps, Assembly.Location returns empty string.
        // Use Process.MainModule or Environment.ProcessPath instead.
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
            return processPath;

        return System.Reflection.Assembly.GetExecutingAssembly().Location;
    }

    [SupportedOSPlatform("linux")]
    internal static string GetSystemdUnitPath()
    {
        string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                            ?? Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                ".config");
        return Path.Combine(configHome, "systemd/user/nomercy-mediaserver.service");
    }

    [SupportedOSPlatform("linux")]
    internal static string GetXdgAutostartPath()
    {
        string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                            ?? Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                ".config");
        return Path.Combine(configHome, "autostart/nomercy-launcher.desktop");
    }

    [SupportedOSPlatform("macos")]
    internal static string GetLaunchdPlistPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library/LaunchAgents/tv.nomercy.mediaserver.plist");
    }
}
