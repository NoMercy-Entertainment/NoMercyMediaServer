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
            new("CreateAppFolders", AppFiles.CreateAppFolders, false, 1),
            new(
                "UserSettings",
                async () =>
                {
                    if (UserSettings.TryGetUserSettings(out Dictionary<string, string> settings))
                        UserSettings.ApplySettings(settings);
                },
                false,
                1,
                ["CreateAppFolders"]
            ),
            // ── PHASE 2: BEST-EFFORT (network, with fallback) ──────────
            new(
                "NetworkProbe",
                async () =>
                {
                    hasNetwork = await NetworkProbe.CheckConnectivity();
                },
                false,
                2,
                ["CreateAppFolders"]
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
                // CanDefer:true — a transient provisioning failure (GitHub rate limit,
                // momentarily-empty release feed, network blip) must not permanently wedge
                // BootStage.Binaries with no recovery path. DegradedModeRecovery retries
                // provisioning with backoff and marks the stage once ffmpeg is on disk.
                true,
                2,
                ["NetworkProbe"]
            ),
            // ── PHASE 3: NETWORK-DEPENDENT (run if possible, degrade if not) ──
            new(
                "Networking",
                async () =>
                {
                    if (NetworkDiscovery is not null)
                        await NetworkDiscovery.DiscoverExternalIpAsync();
                },
                true,
                3,
                ["NetworkProbe"]
            ),
            new(
                "ChromeCast",
                async () =>
                {
                    if (ChromeCast is not null)
                        await ChromeCast.Init();
                },
                true,
                3,
                ["NetworkProbe"]
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
                true,
                3
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

    public static async Task InitRemaining(
        IDegradedModeRecovery? recovery = null,
        string? accessToken = null
    )
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
                ApiKeysLoaded = !string.IsNullOrEmpty(ApiKeyStore.Current.TmdbToken),
                // Auth is handled by AuthManager/BootOrchestrator — check AccessToken directly.
                Authenticated = !string.IsNullOrEmpty(accessToken),
                NetworkDiscovered = runner.CompletedTasks.Contains("Networking"),
                SeedsRun = true,
                Registered = runner.CompletedTasks.Contains("Register"),
                BinariesReady = runner.CompletedTasks.Contains("Binaries"),
            };
            if (recovery is not null)
                _ = Task.Run(() => recovery.StartRecoveryLoop(deferred));
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

            await TitleSortBackfill.RunAsync();
        });
    }
}
