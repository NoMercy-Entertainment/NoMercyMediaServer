using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NoMercy.Storage;
using NoMercy.Storage.Local;

namespace NoMercy.Setup;

public static class DesktopIconCreator
{
    // LOCAL-ONLY: DesktopIconCreator is in NoMercy.Setup which cannot reference NoMercy.Providers (circular).
    private static IStorageBackend _backend => new SystemIoStorageBackend();

    public static void CreateDesktopIcon(string appName, string appPath, string iconPath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                CreateWindowsShortcut(appName, appPath, iconPath);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                CreateMacShortcut(appName, appPath, iconPath);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                CreateLinuxShortcut(appName, appPath, iconPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating desktop icon: {ex.Message}");
        }
    }

    private static void CreateWindowsShortcut(string appName, string appPath, string iconPath)
    {
#pragma warning disable CA1416
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktopPath, $"{appName}.lnk");

            Type? id = Type.GetTypeFromProgID("WScript.Shell");
            if (id == null)
                return;

            dynamic shell = Activator.CreateInstance(id) ?? throw new InvalidOperationException();
            if (shell == null)
                return;

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = appPath;
            shortcut.IconLocation = iconPath;
            shortcut.Save();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating Windows shortcut: {ex.Message}");
        }
#pragma warning restore CA1416
    }

    private static void CreateMacShortcut(string appName, string appPath, string iconPath)
    {
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string aliasPath = Path.Combine(desktopPath, appName);

            string script =
                $@"
            tell application ""Finder""
                set appAlias to make new alias file at POSIX file ""{desktopPath}"" to POSIX file ""{appPath}""
                set name of appAlias to ""{appName}""
            end tell";

            string scriptPath = "/tmp/CreateShortcut.scpt";
            using (Stream scriptStream = _backend.OpenWrite(scriptPath, overwrite: true))
            using (StreamWriter scriptWriter = new(scriptStream, Encoding.UTF8, leaveOpen: true))
                scriptWriter.Write(script);
            using (Process? osascriptProc = Process.Start("osascript", scriptPath))
                osascriptProc?.WaitForExit();

            if (!string.IsNullOrEmpty(iconPath) && _backend.FileExists(iconPath))
            {
                string iconDest = Path.Combine(aliasPath, "Icon.icns");
                _backend.CopyFile(iconPath, iconDest, overwrite: true);

                using (
                    Process? shProc = Process.Start(
                        "sh",
                        $"-c \"cp '{iconPath}' '{aliasPath}/Icon.icns' && /usr/bin/SetFile -a C '{aliasPath}'\""
                    )
                )
                    shProc?.WaitForExit();

                using (Process? killProc = Process.Start("killall", "Finder"))
                    killProc?.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating Mac shortcut: {ex.Message}");
        }
    }

    private static void CreateLinuxShortcut(string appName, string appPath, string iconPath)
    {
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktopPath, $"{appName}.desktop");

            string content =
                $@"
                [Desktop Entry]
                Name={appName}
                Exec={appPath}
                Icon={iconPath}
                Type=Application
                Terminal=false";

            using (Stream shortcutStream = _backend.OpenWrite(shortcutPath, overwrite: true))
            using (
                StreamWriter shortcutWriter = new(shortcutStream, Encoding.UTF8, leaveOpen: true)
            )
                shortcutWriter.Write(content);
            using (Process? chmodProc = Process.Start("chmod", $"+x \"{shortcutPath}\""))
                chmodProc?.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating Linux shortcut: {ex.Message}");
        }
    }
}
