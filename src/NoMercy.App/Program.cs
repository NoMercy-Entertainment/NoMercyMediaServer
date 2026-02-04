using System.Diagnostics;
using InfiniFrame;
using InfiniFrame.Js.MessageHandlers;
using InfiniFrame.WebServer;

namespace NoMercy.App;

internal class Program
{
    // For single-file deployments, get the directory where the exe is located (not the extraction folder)
    private static readonly string ExeDirectory = Path.GetDirectoryName(Environment.ProcessPath)
                                                  ?? AppContext.BaseDirectory;

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
            .SetMinSize(1280 + 16, 720 + 39)
            .SetResizable(true)
            .SetIconFile(iconPath)
            .SetUseOsDefaultSize(false)
            .SetMediaAutoplayEnabled(true)
            .SetMediaStreamEnabled(true)
            .SetBrowserControlInitParameters("--remote-debugging-port=9222")
            .RegisterFullScreenWebMessageHandler()
            .RegisterOpenExternalTargetWebMessageHandler()
            .RegisterTitleChangedWebMessageHandler()
            .RegisterWindowManagementWebMessageHandler()
            .RegisterWebMessageReceivedHandler((sender, message) =>
            {
                if (sender is not IInfiniFrameWindow window) return;

                string response = $"Received message: \"{message}\"";
                window.SendWebMessage(response);
            });

        // In debug mode, load from dev server; otherwise use local server
        if(Debugger.IsAttached)
            window.SetStartUrl("https://app-dev.nomercy.tv");

        InfiniFrameWebApplication application = builder.Build();

        application.UseAutoServerClose();

        application.WebApp.UseStaticFiles();

        // For single-file deployments, the manifest is next to the exe, not in the extraction folder
        string manifestPath = Path.Combine(ExeDirectory, "NoMercyApp.staticwebassets.endpoints.json");
        if (File.Exists(manifestPath))
            application.WebApp.MapStaticAssets(manifestPath);
        else
            application.WebApp.MapStaticAssets();

        application.Run();
    }

    private static string GetIconPath()
    {
        string iconPath;
        if (OperatingSystem.IsWindows())
            iconPath = Path.Combine(ExeDirectory, "Resources", "AppIcon", "icon.ico");
        else if (OperatingSystem.IsLinux())
            iconPath = Path.Combine(ExeDirectory, "Resources", "AppIcon", "icon.png");
        else if (OperatingSystem.IsMacOS())
            iconPath = Path.Combine(ExeDirectory, "Resources", "AppIcon", "icon.icns");
        else
            throw new PlatformNotSupportedException("Unsupported OS platform");

        if (!File.Exists(iconPath)) throw new FileNotFoundException("Tray icon file not found", iconPath);

        return iconPath;
    }
}
