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

    public static async Task Init(List<TaskDelegate> tasks)
    {
        if (UserSettings.TryGetUserSettings(out Dictionary<string, string> settings))
            UserSettings.ApplySettings(settings);

        // ── PHASE 1: MUST SUCCEED (no network) ─────────────────────
        await AppFiles.CreateAppFolders();
        await ApiInfo.RequestInfo();

        // ── PHASE 2: BEST-EFFORT (network, with fallback) ──────────
        bool hasNetwork = await NetworkProbe.CheckConnectivity();
        bool hasAuth;

        Task binariesTask = Binaries.DownloadAll();

        if (hasNetwork)
        {
            try
            {
                await Auth.Init();
                hasAuth = Globals.Globals.AccessToken is not null;
            }
            catch (Exception e)
            {
                Logger.Setup($"Auth failed: {e.Message}. Trying cached tokens.",
                    LogEventLevel.Warning);
                hasAuth = await Auth.InitWithFallback();
            }
        }
        else
        {
            hasAuth = await Auth.InitWithFallback();
        }

        // ── PHASE 3: NETWORK-DEPENDENT (run if possible, degrade if not) ──
        Task networkingTask = Networking.Networking.Discover();

        List<Task> independentTasks =
        [
            ChromeCast.Init(),
            UpdateChecker.StartPeriodicUpdateCheck(),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362)
                    ? TrayIcon.Make()
                    : Task.CompletedTask,
            Task.Run(() => DesktopIconCreator.CreateDesktopIcon(
                AppFiles.ApplicationName, AppFiles.ServerExePath, AppFiles.AppIcon))
        ];

        if (hasNetwork && hasAuth)
        {
            // Full mode — run caller tasks (DatabaseSeeder etc.) normally
            independentTasks.AddRange(tasks.Select(t => t.Invoke()));
            await Task.WhenAll(independentTasks);

            await networkingTask;

            // Phase 4: Register needs both Auth (AccessToken) and Networking (InternalIp)
            try
            {
                await Register.Init();
            }
            catch (Exception e)
            {
                Logger.Setup($"Registration failed: {e.Message}. Server will operate in local-only mode.",
                    LogEventLevel.Warning);
                IsDegradedMode = true;
            }
        }
        else
        {
            // Degraded mode — skip network-dependent tasks, schedule background recovery
            IsDegradedMode = true;
            Logger.Setup("Starting in DEGRADED MODE — some features unavailable",
                LogEventLevel.Warning);
            Logger.Setup("  Network-dependent tasks will retry in background",
                LogEventLevel.Warning);

            // Still run caller tasks — seeds that already have data will return early,
            // seeds that need network will fail but are wrapped in try/catch
            foreach (TaskDelegate callerTask in tasks)
            {
                try
                {
                    await callerTask.Invoke();
                }
                catch (Exception e)
                {
                    Logger.Setup($"Startup task failed (degraded mode): {e.Message}",
                        LogEventLevel.Warning);
                }
            }

            await Task.WhenAll(independentTasks);
            await networkingTask;

            // Start background recovery
            DeferredTasks deferred = new()
            {
                ApiKeysLoaded = ApiInfo.KeysLoaded,
                Authenticated = hasAuth,
                NetworkDiscovered = true,
                SeedsRun = false,
                Registered = false,
                CallerTasks = tasks
            };
            _ = Task.Run(() => DegradedModeRecovery.StartRecoveryLoop(deferred));
        }

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
