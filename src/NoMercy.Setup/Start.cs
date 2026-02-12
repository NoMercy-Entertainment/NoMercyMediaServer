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

        // Phase 1: Create app folders (all other tasks depend on this)
        await AppFiles.CreateAppFolders();

        // Phase 2: Auth and binary downloads are independent — run in parallel
        // Binaries.DownloadAll only hits public GitHub APIs (no auth needed)
        Task binariesTask = Binaries.DownloadAll();
        await Auth.Init();

        // Phase 3: After auth, these tasks can all run in parallel:
        // - Networking.Discover needs AccessToken for GetExternalIp
        // - Caller tasks (DatabaseSeeder) need AccessToken for UsersSeed
        // - ChromeCast.Init needs AccessToken
        // - UpdateChecker, TrayIcon, DesktopIcon are fully independent
        Task networkingTask = Networking.Networking.Discover();

        List<Task> parallelTasks =
        [
            ..tasks.Select(t => t.Invoke()),
            ChromeCast.Init(),
            UpdateChecker.StartPeriodicUpdateCheck(),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362)
                    ? TrayIcon.Make()
                    : Task.CompletedTask,
            Task.Run(() => DesktopIconCreator.CreateDesktopIcon(
                AppFiles.ApplicationName, AppFiles.ServerExePath, AppFiles.AppIcon))
        ];

        await Task.WhenAll(parallelTasks);

        // Phase 4: Register needs both Auth (AccessToken) and Networking (InternalIp)
        await networkingTask;
        await Register.Init();

        // Wait for binary downloads to finish before server is considered ready
        await binariesTask;

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
            await QueueRunner.Current!.Initialize();
        });
    }
}