using System.Runtime.InteropServices;
using NoMercy.Encoder.Core;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Queue;
using AppFiles = NoMercy.NmSystem.Information.AppFiles;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Setup;

public class Start
{
    [DllImport("Kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
    [DllImport("User32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

    public static int AppProcessStarted { get; set; }
    public static int ConsoleVisible { get; set; } = 1;

    public static void VsConsoleWindow(int i)
    {
        IntPtr hWnd = GetConsoleWindow();
        if (hWnd != IntPtr.Zero)
        {
            ConsoleVisible = i;
            ShowWindow(hWnd, i);
        }
    }
    public static async Task Init(List<TaskDelegate> tasks)
    {
        await ApiInfo.RequestInfo();

        if (UserSettings.TryGetUserSettings(out Dictionary<string, string>? settings))
        {
            UserSettings.ApplySettings(settings);
        }

        List<TaskDelegate> startupTasks =
        [
            new (ConsoleMessages.Logo),
            new (AppFiles.CreateAppFolders),
            new (Networking.Networking.Discover),
            new (Auth.Init),
            ..tasks,
            new (Register.Init),
            new (Binaries.DownloadAll),
            new (ChromeCast.Init),
            new (UpdateChecker.StartPeriodicUpdateCheck),

            new (delegate
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
                    return TrayIcon.Make();
                return Task.CompletedTask;
            }),
            new (delegate
            {
                DesktopIconCreator.CreateDesktopIcon(AppFiles.ApplicationName, AppFiles.ServerExePath, AppFiles.AppIcon);
                return Task.CompletedTask;
            })
        ];

        await RunStartup(startupTasks);

        Thread queues = new(new Task(() => QueueRunner.Initialize().Wait()).Start)
        {
            Name = "Queue workers",
            Priority = ThreadPriority.Lowest,
            IsBackground = true
        };
        queues.Start();

        // Thread fileWatcher = new(new Task(() => _ = new LibraryFileWatcher()).Start)
        // {
        //     Name = "Library File Watcher",
        //     Priority = ThreadPriority.Lowest,
        //     IsBackground = true
        // };
        // fileWatcher.Start();
        
        foreach (GpuAccelerator accelerator in FFmpegHardwareConfig.Accelerators)
        {
            Logger.Encoder($"Found a dedicated GPU. Vendor: {accelerator.Vendor}, Accelerator: {accelerator.Accelerator}");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
        {
            Logger.App(
                "Your server is ready and we will hide the console window in 10 seconds\n you can show it again by right-clicking the tray icon");
            Task.Delay(10000)
                .ContinueWith(_ => VsConsoleWindow(0));
        }
    }
    
    private static async Task RunStartup(List<TaskDelegate> startupTasks)
    {
        foreach (TaskDelegate task in startupTasks)
        {
            await task.Invoke();
        }
    }

}