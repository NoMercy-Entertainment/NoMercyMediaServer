using System.Diagnostics;
using System.Reflection;
using InfiniFrame;
using InfiniFrame.Js.MessageHandlers;
using InfiniFrame.WebServer;
using NoMercy.App.EmbeddedStaticAssets;

namespace NoMercy.App;

internal class Program
{
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
                if (sender is not IInfiniFrameWindow infiniWindow) return;

                string response = $"Received message: \"{message}\"";
                infiniWindow.SendWebMessage(response);
            });

        // In debug mode, load from dev server; otherwise use local server
        if (Debugger.IsAttached)
            window.SetStartUrl("https://app-dev.nomercy.tv");

        InfiniFrameWebApplication application = builder.Build();

        application.UseAutoServerClose();

        // Use custom embedded static assets middleware with optimizations
        // (compression, caching, ETags) - replaces MapStaticAssets for embedded resources
        // Also injects the InfiniFrame.js script tag into HTML files at runtime
        application.WebApp.UseEmbeddedStaticAssets(options =>
        {
            // Inject InfiniFrame script before </body> - required for InfiniFrame communication
            options.InjectScripts.Add("/_content/InfiniLore.InfiniFrame.Js/InfiniFrame.js");
        }, typeof(Program).Assembly, "wwwroot");

        application.Run();
    }

    private static string GetIconPath()
    {
        string iconName;
        if (OperatingSystem.IsWindows())
            iconName = "icon.ico";
        else if (OperatingSystem.IsLinux())
            iconName = "icon.png";
        else if (OperatingSystem.IsMacOS())
            iconName = "icon.icns";
        else
            throw new PlatformNotSupportedException("Unsupported OS platform");

        // Extract embedded icon to temp directory (InfiniFrame requires a file path)
        string tempDir = Path.Combine(Path.GetTempPath(), "NoMercyApp");
        Directory.CreateDirectory(tempDir);
        string iconPath = Path.Combine(tempDir, iconName);

        if (!File.Exists(iconPath))
        {
            Assembly assembly = typeof(Program).Assembly;
            string resourceName = $"NoMercy.App.Resources.AppIcon.{iconName}";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new FileNotFoundException($"Embedded icon resource not found: {resourceName}");

            using FileStream fileStream = File.Create(iconPath);
            stream.CopyTo(fileStream);
        }

        return iconPath;
    }
}
