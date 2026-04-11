using NoMercy.Encoder.Core;
using NoMercy.Networking;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem;
using NoMercyQueue;
using Serilog.Events;
using AppFiles = NoMercy.NmSystem.Information.AppFiles;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Setup;

public class Start
{
    public static INetworkDiscovery? NetworkDiscovery { get; set; }

    public static bool IsDegradedMode { get; internal set; }

    private static List<StartupTask> _allTasks = [];
    private static HashSet<string> _phase1Completed = [];
    private static List<TaskDelegate> _callerTasks = [];

    internal static List<StartupTask> BuildStartupTasks(List<TaskDelegate> callerTasks)
    {
        bool hasNetwork = false;

        return
        [
            // ── PHASE 1: MUST SUCCEED (no network) ─────────────────────
            new(
                "UserSettings",
                async () =>
                {
                    if (UserSettings.TryGetUserSettings(out Dictionary<string, string> settings))
                        UserSettings.ApplySettings(settings);
                },
                CanDefer: false,
                Phase: 1
            ),
            new(
                "CreateAppFolders",
                AppFiles.CreateAppFolders,
                CanDefer: false,
                Phase: 1,
                DependsOn: ["UserSettings"]
            ),
            new(
                "ApiInfo",
                ApiInfo.RequestInfo,
                CanDefer: false,
                Phase: 1,
                DependsOn: ["CreateAppFolders"]
            ),
            // ── PHASE 2: BEST-EFFORT (network, with fallback) ──────────
            new(
                "NetworkProbe",
                async () =>
                {
                    hasNetwork = await NetworkProbe.CheckConnectivity();
                },
                CanDefer: false,
                Phase: 2,
                DependsOn: ["ApiInfo"]
            ),
            // Auth is now handled by AuthManager (DI) via BootOrchestrator — not here.
            new(
                "Binaries",
                Binaries.DownloadAll,
                CanDefer: false,
                Phase: 2,
                DependsOn: ["NetworkProbe"]
            ),
            // ── PHASE 3: NETWORK-DEPENDENT (run if possible, degrade if not) ──
            new(
                "Networking",
                async () =>
                {
                    if (NetworkDiscovery is not null)
                        await NetworkDiscovery.DiscoverExternalIpAsync();
                },
                CanDefer: true,
                Phase: 3,
                DependsOn: ["NetworkProbe"]
            ),
            new(
                "ChromeCast",
                ChromeCast.Init,
                CanDefer: true,
                Phase: 3,
                DependsOn: ["NetworkProbe"]
            ),
            new(
                "UpdateChecker",
                UpdateChecker.StartPeriodicUpdateCheck,
                CanDefer: true,
                Phase: 3,
                DependsOn: ["NetworkProbe"]
            ),
            new(
                "DesktopIcon",
                () =>
                    Task.Run(() =>
                        DesktopIconCreator.CreateDesktopIcon(
                            AppFiles.ApplicationName,
                            AppFiles.ServerExePath,
                            AppFiles.AppIcon
                        )
                    ),
                CanDefer: true,
                Phase: 3
            ),
            .. callerTasks.Select(
                (TaskDelegate taskDelegate, int i) =>
                    new StartupTask(
                        $"CallerTask_{i}",
                        taskDelegate.Invoke,
                        CanDefer: true,
                        Phase: 3,
                        DependsOn: ["NetworkProbe"]
                    )
            ),
            // ── PHASE 4: REGISTRATION (needs Networking) ────────────────
            // Auth is handled by AuthManager/BootOrchestrator before InitRemaining runs.
            new(
                "Register",
                () => Register.Init(),
                CanDefer: true,
                Phase: 4,
                DependsOn: ["Networking"]
            ),
        ];
    }

    public static async Task InitEssential(List<TaskDelegate> tasks)
    {
        _callerTasks = tasks;
        _allTasks = BuildStartupTasks(tasks);

        List<StartupTask> phase1Tasks = _allTasks.Where(t => t.Phase == 1).ToList();
        StartupTaskRunner runner = new(phase1Tasks);

        await runner.RunAll();

        _phase1Completed = [.. runner.CompletedTasks];
    }

    public static async Task InitRemaining()
    {
        List<StartupTask> remainingTasks = _allTasks.Where(t => t.Phase > 1).ToList();
        StartupTaskRunner runner = new(remainingTasks, _phase1Completed);

        await runner.RunAll();

        if (runner.DeferredTasks.Count > 0)
        {
            IsDegradedMode = true;
            Logger.Setup(
                "Some startup tasks were deferred — they will be retried in the background"
            );
            Logger.Setup(
                $"  Deferred tasks: {string.Join(", ", runner.DeferredTasks.Select(t => t.Name))}"
            );

            DeferredTasks deferred = new()
            {
                ApiKeysLoaded = _phase1Completed.Contains("ApiInfo"),
                // Auth is handled by AuthManager/BootOrchestrator — check AccessToken directly.
                Authenticated = !string.IsNullOrEmpty(Globals.Globals.AccessToken),
                NetworkDiscovered = runner.CompletedTasks.Contains("Networking"),
                SeedsRun = runner.DeferredTasks.All(t => !t.Name.StartsWith("CallerTask_")),
                Registered = runner.CompletedTasks.Contains("Register"),
                CallerTasks = _callerTasks,
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
            if (FFmpegHardwareConfig.Accelerators.Count > 0)
            {
                List<string> gpus = FFmpegHardwareConfig
                    .Accelerators.Select(a => $"{a.Vendor}/{a.Accelerator}")
                    .ToList();
                Logger.Encoder($"GPU acceleration: {string.Join(", ", gpus)}", LogEventLevel.Debug);
            }

            // Start queue workers after a short delay
            await Task.Delay(TimeSpan.FromSeconds(2));
            if (QueueRunner.Current is not null)
            {
                await QueueRunner.Current.Initialize();
            }
            else
            {
                Logger.Setup(
                    "QueueRunner.Current is null — skipping Initialize from InitRemaining (will be initialized after host restart)",
                    LogEventLevel.Warning
                );
            }
        });
    }
}
