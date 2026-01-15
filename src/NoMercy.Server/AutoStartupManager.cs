using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NoMercy.Server;

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
                Console.WriteLine("Windows startup registration successful.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register Windows startup: {ex.Message}");
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
                Console.WriteLine("Windows startup unregistration successful.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to unregister Windows startup: {ex.Message}");
        }
    }

    [SupportedOSPlatform("macos")]
    private static void RegisterMacStartup()
    {
        try
        {
            string plistPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/LaunchAgents/nomercymediaserver.startup.plist");
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            string plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>nomercymediaserver.startup</string>
    <key>ProgramArguments</key>
    <array>
        <string>{appPath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";

            File.WriteAllText(plistPath, plistContent);
            Console.WriteLine("macOS startup registration successful.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register macOS startup: {ex.Message}");
        }
    }

    [SupportedOSPlatform("macos")]
    private static void UnregisterMacStartup()
    {
        try
        {
            string plistPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/LaunchAgents/nomercymediaserver.startup.plist");

            if (File.Exists(plistPath))
            {
                File.Delete(plistPath);
                Console.WriteLine("macOS startup unregistration successful.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to unregister macOS startup: {ex.Message}");
        }
    }

    [SupportedOSPlatform("linux")]
    private static void RegisterLinuxStartup()
    {
        try
        {
            string desktopFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/autostart/nomercymediaserver.desktop");
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            string desktopFileContent = $@"[Desktop Entry]
Type=Application
Exec={appPath}
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
Name=NoMercyMediaServer
Comment=Start NoMercyMediaServer at login";

            Directory.CreateDirectory(Path.GetDirectoryName(desktopFilePath) ?? string.Empty);
            File.WriteAllText(desktopFilePath, desktopFileContent);
            Console.WriteLine("Linux startup registration successful.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register Linux startup: {ex.Message}");
        }
    }

    [SupportedOSPlatform("linux")]
    private static void UnregisterLinuxStartup()
    {
        try
        {
            string desktopFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/autostart/nomercymediaserver.desktop");

            if (File.Exists(desktopFilePath))
            {
                File.Delete(desktopFilePath);
                Console.WriteLine("Linux startup unregistration successful.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to unregister Linux startup: {ex.Message}");
        }
    }
}