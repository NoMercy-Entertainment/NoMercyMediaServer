using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using H.NotifyIcon.Core;

namespace NoMercy.Server;

public class TrayIcon
{
    private static readonly string IconStream = Path.Combine(Directory.GetCurrentDirectory(), "Assets/icon.ico");

    private static readonly Icon Icon = new(IconStream);

    private readonly TrayIconWithContextMenu _trayIcon = new()
    {
        Icon = Icon.Handle,
        ToolTip = "NoMercy MediaServer C#",
    };

    [SupportedOSPlatform("windows10.0.18362")]
    private TrayIcon()
    {
        _trayIcon.ContextMenu = new PopupMenu
        {
            Items =
            {
                new PopupMenuItem("Show App", (_, _) => Show()),
                new PopupMenuSeparator(),
                new PopupMenuItem("Pause Server", (_, _) => Pause()),
                new PopupMenuItem("Restart Server", (_, _) => Restart()),
                new PopupMenuItem("Shutdown", (_, _) => Shutdown()),
            },
        };

        _trayIcon.Create();
    }

    private static void Pause()
    {
    }

    private static void Show()
    {
    }

    private static void Restart()
    {
    }

    private void Shutdown()
    {
        _trayIcon.Dispose();
        Environment.Exit(0);
    }

    public static Task Make()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
        {
            TrayIcon _ = new();
        }

        return Task.CompletedTask;
    }
}