using System.Diagnostics;
using InfiniFrame;
using InfiniFrame.Js.MessageHandlers;
using InfiniFrame.WebServer;

namespace NoMercy.App;

internal class Program
{
    private static int WindowWidth { get; set; } = 1280;
    private static int WindowHeight { get; set; } = 720;
    private static int WindowRestoreWidth { get; set; } = 1280;
    private static int WindowRestoreHeight { get; set; } = 720;
    private static int Top { get; set; }
    private static int Left { get; set; }

    [STAThread]
    private static void Main(string[] args)
    {
        string windowTitle = "NoMercy TV";
        string iconPath = GetIconPath();

        // Set environment variable for URL before creating builder
        if (!Debugger.IsAttached)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:7625");
        }

        InfiniFrameWebApplicationBuilder builder = InfiniFrameWebApplication.CreateBuilder(args);

        IInfiniFrameWindowBuilder window = builder.Window
            .Center()
            .SetTitle(windowTitle)
            .SetMinSize(960 + 16, 540 + 39)
            .SetResizable(true)
            .SetIconFile(iconPath)
            .SetUseOsDefaultSize(false)
            .SetSmoothScrollingEnabled(true)
            .SetMediaAutoplayEnabled(true)
            .SetMediaStreamEnabled(true);

        // In debug mode, load from dev server; otherwise use local server
        window.SetStartUrl(Debugger.IsAttached ? "https://app-dev.nomercy.tv" : "https://app.nomercy.tv");

        window.RegisterFullScreenWebMessageHandler()
            .RegisterWindowManagementWebMessageHandler()
            .RegisterCustomSchemeHandler("nomercy",
                (object sender, string scheme, string url, out string? contentType) =>
                {
                    contentType = "text/javascript";
                    return new MemoryStream("""
                        (() =>{
                            window.setTimeout(() => {
                                alert(`NoMercy custom scheme handler loaded.`);
                            }, 1000);
                        })();
                    """u8.ToArray());
                })
            .RegisterWebMessageReceivedHandler((object? sender, string message) =>
            {
                if (sender is not IInfiniFrameWindow window) return;

                switch (message)
                {
                    case "enterFullscreen":
                        window.SetFullScreen(true);
                        return;
                    case "exitFullscreen":
                        window.SetFullScreen(false);
                        return;
                }
            })
            .RegisterWindowCreatedHandler((sender, _) =>
            {
                if (sender is not IInfiniFrameWindow window) return;

                InfiniMonitor primaryMonitor = window.MainMonitor;

                WindowWidth = primaryMonitor.WorkArea.Width / 2;
                WindowHeight = (int)(primaryMonitor.WorkArea.Width / 2 / 16 * 9.3);
                Top = window.Top;
                Left = window.Left;
                window.SetSize(WindowWidth, WindowHeight);
                window.Center();
            })
            .RegisterMaximizedHandler((sender, _) =>
            {
                if (sender is not IInfiniFrameWindow window) return;

                WindowRestoreWidth = WindowWidth;
                WindowRestoreHeight = WindowHeight;

                InfiniMonitor primaryMonitor = window.MainMonitor;
                WindowWidth = primaryMonitor.WorkArea.Width;
                WindowHeight = primaryMonitor.WorkArea.Height;
            })
            .RegisterRestoredHandler((sender, _) =>
            {
                WindowWidth = WindowRestoreWidth;
                WindowHeight = WindowRestoreHeight;
            })
            .RegisterLocationChangedHandler((sender, e) =>
            {
                if (e.IsEmpty || e.X == 0) return;
                Top = e.Y;
                Left = e.X;
            });

        InfiniFrameWebApplication application = builder.Build();

        application.UseAutoServerClose();

        application.WebApp.UseStaticFiles();

        application.Run();
    }

    private static string GetIconPath()
    {
        string iconPath;
        if (OperatingSystem.IsWindows())
            iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "AppIcon", "icon.ico");
        else if (OperatingSystem.IsLinux())
            iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "AppIcon", "icon.png");
        else if (OperatingSystem.IsMacOS())
            iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "AppIcon", "icon.icns");
        else
            throw new PlatformNotSupportedException("Unsupported OS platform");

        if (!File.Exists(iconPath)) throw new FileNotFoundException("Tray icon file not found", iconPath);

        return iconPath;
    }
}
