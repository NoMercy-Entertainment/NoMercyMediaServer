using System.Diagnostics;
using System.Linq;
using Avalonia;

namespace NoMercy.Tray;

public static class Program
{
    public static bool ShowOnStartup { get; private set; }
    public static bool IsDev { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        IsDev = Debugger.IsAttached
                || args.Contains("--dev")
                || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") is not null
                || Environment.ProcessPath?.Contains("bin\\Debug") == true
                || Environment.ProcessPath?.Contains("bin/Debug") == true;

        ShowOnStartup = args.Contains("--show") || IsDev;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
