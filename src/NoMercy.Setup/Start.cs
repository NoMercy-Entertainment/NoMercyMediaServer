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
        if (hWnd == IntPtr.Zero) return;
        ConsoleVisible = i;
        ShowWindow(hWnd, i);
    }

    public static async Task Init(List<TaskDelegate> tasks)
    {
        if (UserSettings.TryGetUserSettings(out Dictionary<string, string> settings))
            UserSettings.ApplySettings(settings);

        await ApiInfo.RequestInfo();

        List<TaskDelegate> startupTasks =
        [
            // new (ApiInfo.RequestInfo),
            AppFiles.CreateAppFolders,
            Auth.Init,
            Networking.Networking.Discover,
            ..tasks,
            Register.Init,
            Binaries.DownloadAll,
            ChromeCast.Init,
            UpdateChecker.StartPeriodicUpdateCheck,

            delegate
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
                    return TrayIcon.Make();
                return Task.CompletedTask;
            },
            delegate
            {
                DesktopIconCreator.CreateDesktopIcon(AppFiles.ApplicationName, AppFiles.ServerExePath,
                    AppFiles.AppIcon);
                return Task.CompletedTask;
            }
        ];

        await RunStartup(startupTasks);

        // Delay heavy initialization tasks to run in the background after server is ready
        _ = Task.Run(async () =>
        {
            // Wait a bit for the server to fully start and be responsive
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Initialize hardware acceleration detection in background
            await FFmpegHardwareConfig.InitializeAsync();
            foreach (GpuAccelerator accelerator in FFmpegHardwareConfig.Accelerators)
                Logger.Encoder(
                    $"Found a dedicated GPU. Vendor: {accelerator.Vendor}, Accelerator: {accelerator.Accelerator}");

            // Start queue workers after a short delay
            await Task.Delay(TimeSpan.FromSeconds(2));
            await QueueRunner.Initialize();
        });
    }

    private static async Task RunStartup(List<TaskDelegate> startupTasks)
    {
        foreach (TaskDelegate task in startupTasks) await task.Invoke();
    }
}