using System.Diagnostics;
using Photino.NET;
using Photino.NET.Server;
using Monitor = Photino.NET.Monitor;
using PlatformNotSupportedException = System.PlatformNotSupportedException;

namespace NoMercy.App;

internal class Program
{
    private static PhotinoWindow Window { get; set; } = null!;
    private static int WindowWidth { get; set; } = 1280;
    private static int WindowHeight { get; set; } = 720;
    private static int WindowRestoreWidth { get; set; } = 1280;
    private static int WindowRestoreHeight { get; set; } = 720;
    private static int Top { get; set; }
    private static int Left { get; set; }

    [STAThread]
    private static void Main(string[] args)
    {
        PhotinoServer
            .CreateStaticFileServer(args, 7625, 100, "", out string baseUrl)
            .RunAsync();

        string appUrl = Debugger.IsAttached ? "https://app-dev.nomercy.tv" : baseUrl;

        string windowTitle = "NoMercy TV";

        string iconPath;
        if (PhotinoWindow.IsWindowsPlatform)
            iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "AppIcon", "icon.ico");
        else if (PhotinoWindow.IsLinuxPlatform)
            iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "AppIcon", "icon.png");
        else if (PhotinoWindow.IsMacOsPlatform)
            iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "AppIcon", "icon.icns");
        else
            throw new PlatformNotSupportedException("Unsupported OS platform");

        if (!File.Exists(iconPath)) throw new FileNotFoundException("Tray icon file not found", iconPath);

        Window = new PhotinoWindow
            {
                Centered = true,
                Title = windowTitle,
                MinHeight = 540 + 39,
                MinWidth = 960 + 16,
                Resizable = true,
                IconFile = iconPath,
                UseOsDefaultSize = false,
                SmoothScrollingEnabled = true,
                MediaAutoplayEnabled = true,
                MediaStreamEnabled = true
            }
            .RegisterCustomSchemeHandler("nomercy",
                (object sender, string scheme, string url, out string contentType) =>
                {
                    contentType = "text/javascript";
                    return new MemoryStream("""
                        (() =>{
                            window.setTimeout(() => {
                                alert(`🎉 Dynamically inserted JavaScript.`);
                            }, 1000);
                        })();
                    """u8.ToArray());
                })
            .RegisterWebMessageReceivedHandler((object? sender, string message) =>
            {
                PhotinoWindow window = (PhotinoWindow)sender!;

                switch (message)
                {
                    case "enterFullscreen":
                        EnterFullScreen(window);
                        return;
                    case "exitFullscreen":
                        ExitFullScreen(window);
                        return;
                }
            })
            .Load(appUrl);

        Window.WindowCreated += (_, _) =>
        {
            Monitor? primaryMonitor = Window.Monitors.FirstOrDefault();

            if (primaryMonitor != null)
            {
                WindowWidth = primaryMonitor.Value.WorkArea.Width / 2;
                WindowHeight = (int)(primaryMonitor.Value.WorkArea.Width / 2 / 16 * 9.3);
                Top = Window.Top;
                Left = Window.Left;
                Window.SetSize(WindowWidth, WindowHeight);
                Window.Center();
            }

            Window.WindowMaximizedHandler += (_, _) =>
            {
                WindowRestoreWidth = WindowWidth;
                WindowRestoreHeight = WindowHeight;

                if (primaryMonitor == null) return;
                WindowWidth = primaryMonitor.Value.WorkArea.Width;
                WindowHeight = primaryMonitor.Value.WorkArea.Width;
            };

            Window.WindowRestoredHandler += (_, _) =>
            {
                if (primaryMonitor == null) return;
                WindowWidth = WindowRestoreWidth;
                WindowHeight = WindowRestoreHeight;
            };

            Window.WindowLocationChanged += (_, e) =>
            {
                if (e.IsEmpty || e.X == 0) return;
                Top = e.Y;
                Left = e.X;
            };
        };

        Window.WaitForClose();
    }

    private static void EnterFullScreen(PhotinoWindow window)
    {
        Monitor? primaryMonitor = window.MainMonitor;

        window.SetFullScreen(true);
        window.SetSize(primaryMonitor.Value.MonitorArea.Width - 1, primaryMonitor.Value.MonitorArea.Height - 1);
        window.SetTop(0);
        window.SetLeft(0);
        window.SetSize(primaryMonitor.Value.MonitorArea.Width, primaryMonitor.Value.MonitorArea.Height);
        window.SetTopMost(true);
    }

    private static void ExitFullScreen(PhotinoWindow window)
    {
        window.SetFullScreen(false);
        window.SetTopMost(false);
        window.SetSize(WindowWidth, WindowHeight);
        window.SetTop(Top);
        window.SetLeft(Left);
    }
}