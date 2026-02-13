using System.Runtime.Versioning;
using Microsoft.Win32;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.NmSystem;

public class AutoStartupManager
{
    public static void Initialize()
    {
        if (OperatingSystem.IsWindows())
            RegisterWindowsStartup();
        else if (OperatingSystem.IsMacOS())
            RegisterMacStartup();
        else if (OperatingSystem.IsLinux())
            RegisterLinuxStartup();
    }

    public static void Remove()
    {
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

    [SupportedOSPlatform("linux")]
    private static bool IsLinuxStartupEnabled()
    {
        try
        {
            return File.Exists(GetSystemdUnitPath());
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates a systemd user service unit file for Linux.
    /// Returns the generated unit file content and the path where it would be installed.
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
    /// Generates a macOS LaunchAgent plist file.
    /// Returns the generated plist content and the path where it would be installed.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static (string Content, string Path) GenerateLaunchdPlist()
    {
        string appPath = GetExecutablePath();
        string plistPath = GetLaunchdPlistPath();
        string logPath = AppFiles.LogPath;

        string plistContent = $"""
                               <?xml version="1.0" encoding="UTF-8"?>
                               <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                               <plist version="1.0">
                               <dict>
                                   <key>Label</key>
                                   <string>tv.nomercy.mediaserver</string>
                                   <key>ProgramArguments</key>
                                   <array>
                                       <string>{appPath}</string>
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
                                   <string>{Path.GetDirectoryName(appPath)}</string>
                               </dict>
                               </plist>
                               """;

        return (plistContent, plistPath);
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindowsStartup()
    {
        try
        {
            using RegistryKey? key =
                Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                key.SetValue("NoMercyMediaServer", $"\"{System.Reflection.Assembly.GetExecutingAssembly().Location}\"");
                Logger.App("Windows startup registration successful.");
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

    [SupportedOSPlatform("linux")]
    private static void RegisterLinuxStartup()
    {
        try
        {
            // Install systemd user service unit
            (string unitContent, string unitPath) = GenerateSystemdUnit();

            string? directory = Path.GetDirectoryName(unitPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(unitPath, unitContent);
            Logger.App($"systemd user service unit written to {unitPath}");
            Logger.App("To enable: systemctl --user enable --now nomercy-mediaserver.service");
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to register Linux systemd service: {ex.Message}");
        }
    }

    [SupportedOSPlatform("linux")]
    private static void UnregisterLinuxStartup()
    {
        try
        {
            string unitPath = GetSystemdUnitPath();

            if (File.Exists(unitPath))
            {
                File.Delete(unitPath);
                Logger.App("Linux systemd service unregistration successful.");
            }

            // Also remove legacy desktop entry if it exists
            string desktopFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/autostart/nomercymediaserver.desktop");

            if (File.Exists(desktopFilePath))
            {
                File.Delete(desktopFilePath);
                Logger.App("Legacy Linux desktop autostart entry removed.");
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to unregister Linux startup: {ex.Message}");
        }
    }

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

    [SupportedOSPlatform("macos")]
    internal static string GetLaunchdPlistPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library/LaunchAgents/tv.nomercy.mediaserver.plist");
    }
}
