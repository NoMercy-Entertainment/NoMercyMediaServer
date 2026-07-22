// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Networking.Cast;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.Setup.Maintenance;
using NoMercy.Setup.Server;
using NoMercy.Setup.Ui;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercyQueue;
using Serilog.Events;
using AppFiles = NoMercy.NmSystem.Information.AppFiles;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.Setup.Boot;

public class Start
{
    public static INetworkDiscovery? NetworkDiscovery { get; set; }
    public static IChromeCastService? ChromeCast { get; set; }
    public static ICertificateService? Certificate { get; set; }

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
            new(Name: "CreateAppFolders", Action: AppFiles.CreateAppFolders, CanDefer: false, Phase: 1),
            new(
                Name: "UserSettings",
                Action: async () =>
                {
                    if (UserSettings.TryGetUserSettings(settings: out Dictionary<string, string> settings))
                        UserSettings.ApplySettings(settings: settings);
                },
                CanDefer: false,
                Phase: 1,
                DependsOn: ["CreateAppFolders"]
            ),
            // ── PHASE 2: BEST-EFFORT (network, with fallback) ──────────
            new(
                Name: "NetworkProbe",
                Action: async () =>
                {
                    hasNetwork = await NetworkProbe.CheckConnectivity();
                },
                CanDefer: false,
                Phase: 2,
                DependsOn: ["CreateAppFolders"]
            ),
            // Auth is now handled by AuthManager (DI) via BootOrchestrator — not here.
            new(
                Name: "Binaries",
                // LOCAL-ONLY: Start.cs is in NoMercy.Setup which cannot reference NoMercy.Providers (circular).
                Action: () =>
                {
                    IStorageDriver driver = new LocalStorageDriver();
                    IStorage storage = new LocalStorage(driver: driver, guard: new(allowedRoots: [], driver: driver));
                    return new Binaries(driver: driver, storage: storage).DownloadAll();
                },
                // CanDefer:true — a transient provisioning failure (GitHub rate limit,
                // momentarily-empty release feed, network blip) must not permanently wedge
                // BootStage.Binaries with no recovery path. DegradedModeRecovery retries
                // provisioning with backoff and marks the stage once ffmpeg is on disk.
                CanDefer: true,
                Phase: 2,
                DependsOn: ["NetworkProbe"]
            ),
            // ── PHASE 3: NETWORK-DEPENDENT (run if possible, degrade if not) ──
            new(
                Name: "Networking",
                Action: async () =>
                {
                    if (NetworkDiscovery is not null)
                        await NetworkDiscovery.DiscoverExternalIpAsync();
                },
                CanDefer: true,
                Phase: 3,
                DependsOn: ["NetworkProbe"]
            ),
            new(
                Name: "ChromeCast",
                Action: async () =>
                {
                    if (ChromeCast is not null)
                        await ChromeCast.Init();
                },
                CanDefer: true,
                Phase: 3,
                DependsOn: ["NetworkProbe"]
            ),
            new(
                Name: "DesktopIcon",
                Action: () =>
                    Task.Run(action: () =>
                        DesktopIconCreator.CreateDesktopIcon(
                            appName: AppFiles.ApplicationName,
                            appPath: AppFiles.ServerExePath,
                            iconPath: AppFiles.AppIcon
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

        List<StartupTask> phase1Tasks = _allTasks.Where(predicate: t => t.Phase == 1).ToList();
        StartupTaskRunner runner = new(tasks: phase1Tasks);

        await runner.RunAll();

        _phase1Completed = [.. runner.CompletedTasks];
    }

    public static async Task InitRemaining(
        IDegradedModeRecovery? recovery = null,
        string? accessToken = null
    )
    {
        List<StartupTask> remainingTasks = _allTasks.Where(predicate: t => t.Phase > 1).ToList();
        StartupTaskRunner runner = new(tasks: remainingTasks, alreadyCompleted: _phase1Completed);

        await runner.RunAll();

        // Translate task completions into boot-stage advances so queue workers
        // can gate on the specific stage they depend on. Binaries is the one the
        // encoder cares about — its absence races ffmpeg.exe replacement.
        IServerPhaseTracker? tracker = ServerPhaseTracker.Current;
        if (tracker is not null)
        {
            if (runner.CompletedTasks.Contains(item: "Binaries"))
                tracker.MarkComplete(stage: BootStage.Binaries);
            if (runner.CompletedTasks.Contains(item: "Networking"))
                tracker.MarkComplete(stage: BootStage.Network);
        }

        if (runner.DeferredTasks.Count > 0)
        {
            IsDegradedMode = true;
            Logger.Setup(
                message: "Some startup tasks were deferred — they will be retried in the background"
            );
            Logger.Setup(
                message: $"  Deferred tasks: {string.Join(separator: ", ", values: runner.DeferredTasks.Select(selector: t => t.Name))}"
            );

            DeferredTasks deferred = new()
            {
                ApiKeysLoaded = !string.IsNullOrEmpty(value: ApiKeyStore.Current.TmdbToken),
                // Auth is handled by AuthManager/BootOrchestrator — check AccessToken directly.
                Authenticated = !string.IsNullOrEmpty(value: accessToken),
                NetworkDiscovered = runner.CompletedTasks.Contains(item: "Networking"),
                SeedsRun = true,
                Registered = runner.CompletedTasks.Contains(item: "Register"),
                BinariesReady = runner.CompletedTasks.Contains(item: "Binaries"),
            };
            if (recovery is not null)
                _ = Task.Run(function: () => recovery.StartRecoveryLoop(tasks: deferred));
        }

        // Delay heavy initialization tasks to run in the background after server is ready
        _ = Task.Run(function: async () =>
        {
            // Wait a bit for the server to fully start and be responsive
            await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 3));

            // Hardware acceleration detection is handled by HardwareInitializationService
            // (registered via services.AddNoMercyEncoder() as IHostedService)
            Logger.Encoder(
                message: "Hardware acceleration detection handled by V3 encoder startup service",
                level: LogEventLevel.Debug
            );

            // Start queue workers after a short delay
            await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 2));
            if (QueueRunner.Current is not null)
            {
                await QueueRunner.Current.Initialize();
            }
            else
            {
                Logger.Setup(
                    message: "QueueRunner.Current is null — skipping Initialize from InitRemaining (will be initialized after host restart)",
                    level: LogEventLevel.Warning
                );
            }

            await TitleSortBackfill.RunAsync();
        });
    }
}
