using NoMercy.Networking;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;
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

    internal static List<StartupTask> BuildStartupTasks()
    {
        bool hasNetwork = false;

        return
        [
            // ── PHASE 1: MUST SUCCEED (no network) ─────────────────────
            // CreateAppFolders runs first so the DataProtection keyring directory
            // exists (with restrictive perms on Unix) before TokenStore lazy-bootstraps
            // during the Configuration table read in UserSettings.
            new("CreateAppFolders", AppFiles.CreateAppFolders, CanDefer: false, Phase: 1),
            new(
                "UserSettings",
                async () =>
                {
                    if (UserSettings.TryGetUserSettings(out Dictionary<string, string> settings))
                        UserSettings.ApplySettings(settings);
                },
                CanDefer: false,
                Phase: 1,
                DependsOn: ["CreateAppFolders"]
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
                // LOCAL-ONLY: Start.cs is in NoMercy.Setup which cannot reference NoMercy.Providers (circular).
                () =>
                {
                    IStorageDriver driver = new LocalStorageDriver();
                    IStorage storage = new LocalStorage(driver, new([], driver));
                    return new Binaries(driver, storage).DownloadAll();
                },
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
            // Registration removed — BootOrchestrator handles it in Phase 3.
            // Having it here caused double registration + 5-minute cert retry loops.
        ];
    }

    public static async Task InitEssential()
    {
        _allTasks = BuildStartupTasks();

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

        // Translate task completions into boot-stage advances so queue workers
        // can gate on the specific stage they depend on. Binaries is the one the
        // encoder cares about — its absence races ffmpeg.exe replacement.
        IServerPhaseTracker? tracker = ServerPhaseTracker.Current;
        if (tracker is not null)
        {
            if (runner.CompletedTasks.Contains("Binaries"))
                tracker.MarkComplete(BootStage.Binaries);
            if (runner.CompletedTasks.Contains("Networking"))
                tracker.MarkComplete(BootStage.Network);
        }

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
                SeedsRun = true,
                Registered = runner.CompletedTasks.Contains("Register"),
            };
            _ = Task.Run(() => DegradedModeRecovery.StartRecoveryLoop(deferred));
        }

        // Delay heavy initialization tasks to run in the background after server is ready
        _ = Task.Run(async () =>
        {
            // Wait a bit for the server to fully start and be responsive
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Hardware acceleration detection is handled by HardwareInitializationService
            // (registered via services.AddNoMercyEncoder() as IHostedService)
            Logger.Encoder(
                "Hardware acceleration detection handled by V3 encoder startup service",
                LogEventLevel.Debug
            );

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
