using System.Runtime.InteropServices;
using NoMercy.Encoder.Core;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Queue;
using Serilog.Events;
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
    public static bool IsDegradedMode { get; internal set; }

    public static void VsConsoleWindow(int i)
    {
        IntPtr hWnd = GetConsoleWindow();
        if (hWnd == IntPtr.Zero) return;
        ConsoleVisible = i;
        ShowWindow(hWnd, i);
    }

    internal static List<StartupTask> BuildStartupTasks(List<TaskDelegate> callerTasks)
    {
        bool hasNetwork = false;

        return
        [
            // ── PHASE 1: MUST SUCCEED (no network) ─────────────────────
            new("UserSettings", async () =>
            {
                if (UserSettings.TryGetUserSettings(out Dictionary<string, string> settings))
                    UserSettings.ApplySettings(settings);
            }, CanDefer: false, Phase: 1),

            new("CreateAppFolders", AppFiles.CreateAppFolders,
                CanDefer: false, Phase: 1, DependsOn: ["UserSettings"]),

            new("ApiInfo", ApiInfo.RequestInfo,
                CanDefer: false, Phase: 1, DependsOn: ["CreateAppFolders"]),

            // ── PHASE 2: BEST-EFFORT (network, with fallback) ──────────
            new("NetworkProbe", async () =>
            {
                hasNetwork = await NetworkProbe.CheckConnectivity();
            }, CanDefer: false, Phase: 2, DependsOn: ["ApiInfo"]),

            new("Auth", async () =>
            {
                if (hasNetwork)
                {
                    try
                    {
                        await Auth.Init();
                        if (Globals.Globals.AccessToken is null)
                            throw new InvalidOperationException("No access token after auth");
                    }
                    catch (Exception e)
                    {
                        Logger.Setup($"Auth failed: {e.Message}. Trying cached tokens.",
                            LogEventLevel.Warning);
                        bool result = await Auth.InitWithFallback();
                        if (!result)
                            throw new InvalidOperationException("Auth failed and no cached tokens available");
                    }
                }
                else
                {
                    bool result = await Auth.InitWithFallback();
                    if (!result)
                        throw new InvalidOperationException("No network and no cached tokens available");
                }
            }, CanDefer: true, Phase: 2, DependsOn: ["NetworkProbe"]),

            new("Binaries", Binaries.DownloadAll,
                CanDefer: true, Phase: 2, DependsOn: ["NetworkProbe"]),

            // ── PHASE 3: NETWORK-DEPENDENT (run if possible, degrade if not) ──
            new("Networking", Networking.Networking.Discover,
                CanDefer: true, Phase: 3, DependsOn: ["NetworkProbe"]),

            new("ChromeCast", ChromeCast.Init,
                CanDefer: true, Phase: 3, DependsOn: ["NetworkProbe"]),

            new("UpdateChecker", UpdateChecker.StartPeriodicUpdateCheck,
                CanDefer: true, Phase: 3, DependsOn: ["NetworkProbe"]),

            new("TrayIcon", () =>
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362)
                        ? TrayIcon.Make()
                        : Task.CompletedTask,
                CanDefer: true, Phase: 3),

            new("DesktopIcon", () => Task.Run(() =>
                DesktopIconCreator.CreateDesktopIcon(
                    AppFiles.ApplicationName, AppFiles.ServerExePath, AppFiles.AppIcon)),
                CanDefer: true, Phase: 3),

            .. callerTasks.Select((TaskDelegate taskDelegate, int i) =>
                new StartupTask($"CallerTask_{i}", taskDelegate.Invoke,
                    CanDefer: true, Phase: 3, DependsOn: ["Auth"])),

            // ── PHASE 4: REGISTRATION (needs Auth + Networking) ────────
            new("Register", () => Register.Init(),
                CanDefer: true, Phase: 4, DependsOn: ["Auth", "Networking"]),
        ];
    }

    public static async Task Init(List<TaskDelegate> tasks)
    {
        List<StartupTask> startupTasks = BuildStartupTasks(tasks);
        StartupTaskRunner runner = new(startupTasks);

        await runner.RunAll();

        if (runner.DeferredTasks.Count > 0)
        {
            IsDegradedMode = true;
            Logger.Setup("Starting in DEGRADED MODE — some features unavailable",
                LogEventLevel.Warning);
            Logger.Setup(
                $"  Deferred tasks: {string.Join(", ", runner.DeferredTasks.Select(t => t.Name))}",
                LogEventLevel.Warning);

            DeferredTasks deferred = new()
            {
                ApiKeysLoaded = runner.CompletedTasks.Contains("ApiInfo"),
                Authenticated = runner.CompletedTasks.Contains("Auth"),
                NetworkDiscovered = runner.CompletedTasks.Contains("Networking"),
                SeedsRun = runner.DeferredTasks.All(t => !t.Name.StartsWith("CallerTask_")),
                Registered = runner.CompletedTasks.Contains("Register"),
                CallerTasks = tasks
            };
            _ = Task.Run(() => DegradedModeRecovery.StartRecoveryLoop(deferred));
        }

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
