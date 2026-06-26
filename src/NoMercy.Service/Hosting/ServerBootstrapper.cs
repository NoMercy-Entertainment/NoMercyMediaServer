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

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Plugins.Abstractions;
using NoMercy.Service.Configuration;
using NoMercy.Service.Hosting;
using NoMercy.Service.Seeds;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Server;
using NoMercy.Setup.Ui;
using NoMercy.Storage;
using NoMercyQueue;


namespace NoMercy.Service.Hosting;

public sealed class ServerBootstrapper
{
    public async Task RunAsync(StartupOptions options)
    {

        switch (options.RunAsService)
        {
            case true:
            {
                // When running as a service, the working directory may not be the executable's directory.
                // Windows services start in system32; systemd services start in /.
                // Set it to the executable's directory so config and data paths resolve correctly.
                string exeDir = AppContext.BaseDirectory;
                Directory.SetCurrentDirectory(exeDir);

                string platform =
                    Software.IsWindows ? "Windows service"
                    : Software.IsLinux ? "systemd service"
                    : Software.IsMac ? "launchd service"
                    : "service";
                Logger.App($"Running as {platform}, content root: {exeDir}");
                break;
            }
            case false when !Console.IsOutputRedirected:
                Console.Clear();
                Console.Title = AppFiles.ApplicationName;
                break;
        }

        if (!options.RunAsService)
            ConsoleMessages.Logo();

        options.ApplySettings();

        Version version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        Software.Version = version;
        Logger.App(
            $"NoMercy MediaServer version: v{version.Major}.{version.Minor}.{version.Build}"
        );

        Stopwatch stopWatch = new();
        stopWatch.Start();

        // Phase 1 only (UserSettings, CreateAppFolders, ApiInfo) — fast, no network
        await Setup.Boot.Start.InitEssential();

        // Route storage-facade temp + transcode writes inside the NoMercy data
        // directory instead of the OS temp folder. StoragePaths default to
        // Path.GetTempPath(); the orchestrator + remote storage stage files
        // there. Encoder gets its own 'cache/encoder' subdir so transcodes
        // don't share scratch space with the rest of the lease churn.
        StoragePaths.TempRoot = AppFiles.TempPath;
        StoragePaths.TranscodeRoot = AppFiles.EncoderCachePath;

        // Pre-DI storage pair — used for seed calls that run before the DI
        // container is built. Same pattern as Start.cs Binaries task.
        (IStorage preBootStorage, IStorageDriver preBootBackend) = BootstrapStorageFactory.Create();

        // Create a database schema before anything else can query it.
        // This does NOT require auth — only migrations + EnsureCreated.
        await DatabaseSeeder.InitSchema(preBootStorage);

        // Seed offline data (config, languages, encoder profiles, etc.)
        // immediately so the UI has data before auth completes.
        await DatabaseSeeder.SeedOfflineData(preBootStorage, preBootBackend);

        // Use certificate presence for the initial forceHttp decision — this is a
        // filesystem check that doesn't require DI. BootOrchestrator (resolved below)
        // will own the real needsSetupMode determination via token validation.
        bool hasCert = Certificate.HasValidCertificate();

        WebApplication app = WebHostFactory.Create(options, forceHttp: !hasCert);

        IShutdownCoordinator shutdownCoordinator = app.Services.GetRequiredService<IShutdownCoordinator>();
        IPortManager portManager = app.Services.GetRequiredService<IPortManager>();

        IApiKeyLoader apiKeyLoader = app.Services.GetRequiredService<IApiKeyLoader>();
        await apiKeyLoader.LoadKeys(shutdownCoordinator.Token);

        // Proactively resolve port conflicts before proceeding.
        // This avoids the costly build→fail→kill→rebuild cycle and prevents
        // CronWorker "Failed to start database job workers" errors.
        await portManager.EnsurePortAvailable(Config.InternalServerPort);

        // Hand the phase tracker to the static accessor so boot helpers in
        // NoMercy.Setup (Start.cs, Binaries.cs) can advance stages without DI
        // plumbing. Phase 1 (essentials) already completed pre-DI — mark it now.
        NmSystem.Lifecycle.ServerPhaseTracker.RegisterCurrent(
            app.Services.GetRequiredService<NmSystem.Lifecycle.IServerPhaseTracker>()
        );
        NmSystem.Lifecycle.ServerPhaseTracker.Current?.MarkComplete(
            NmSystem.Lifecycle.BootStage.Essential
        );

        // From this point on, use the DI-registered storage singletons.
        IStorage diStorage = app.Services.GetRequiredService<IStorage>();
        IStorageDriver dIStorageDriver = app.Services.GetRequiredService<IStorageDriver>();

        // API keys are available without auth, so seed TMDB/MusicBrainz data
        // (genres, languages, etc.) now — before any import jobs can run.
        // Must run AFTER CreateWebApplication so HttpClientProvider is bound
        // to the real IHttpClientFactory (otherwise seed HTTP calls fall back
        // to a bare HttpClient with no registered headers, and MusicBrainz
        // returns 403 for anonymous UAs).
        await DatabaseSeeder.Run(diStorage, dIStorageDriver);

        // Rename on-disk bundle directories when a built-in preset slug changed.
        await DatabaseSeeder.RunBundleSlugRenamePassAsync(
            app.Services.GetRequiredService<IStorageFactory>()
        );

        // BootOrchestrator owns Phase 2 (auth) and Phase 3 (registration).
        // It returns true when interactive auth is required (setup mode).
        BootOrchestrator orchestrator = app.Services.GetRequiredService<BootOrchestrator>();
        bool needsSetupMode = await orchestrator.RunAsync(
            app.Services,
            shutdownCoordinator.Token
        );

        // The initial forceHttp decision used cert presence as a proxy for "auth done".
        // It's wrong when a cert exists but tokens are missing/unreadable (DataProtection
        // key rotation, manual wipe, schema migration) — Kestrel binds HTTPS-only but the
        // setup browser URL is plain HTTP, so the browser gets ERR_EMPTY_RESPONSE. Rebuild
        // the host as HTTP-only for setup flow, then let BootOrchestrator handle the real needsSetupMode determination.
        if (needsSetupMode && hasCert)
        {
            Logger.App(
                "Setup required but host is HTTPS-bound — rebuilding as HTTP-only for setup flow"
            );
            await app.DisposeAsync();
            app = WebHostFactory.Create(options, forceHttp: true);
            diStorage = app.Services.GetRequiredService<IStorage>();
            orchestrator = app.Services.GetRequiredService<BootOrchestrator>();
        }

        // Load SSL cert from database now that TokenStore is initialized by BootOrchestrator
        Certificate.LoadFromDb();

        // Auth completed — seed auth-dependent data (users, library assignment, claims)
        if (!needsSetupMode)
            await DatabaseSeeder.SeedAuthData(diStorage);

        // Force QueueRunner singleton creation and initialize workers immediately —
        // don't wait for InitRemaining() which can be blocked by rate-limited HTTP calls.
        QueueRunner queueRunner = app.Services.GetRequiredService<QueueRunner>();
        await queueRunner.Initialize();

        // Scan and load plugins from the plugins directory. Missing directory is safe —
        // returns empty list and logs INFO. One plugin's failure never blocks others.
        IPluginLoader pluginLoader = app.Services.GetRequiredService<IPluginLoader>();
        IReadOnlyList<PluginLoadResult> loadedPlugins = await pluginLoader.LoadPlugins(
            shutdownCoordinator.Token
        );

        HostLifecycleHooks.Register(app, stopWatch);

        // Log addresses and run dev tasks after the host is live.
        // InitRemaining is now owned by BootOrchestrator — only host-level
        // post-startup concerns belong here.
        _ = Task.Run(async () =>
        {
            INetworkDiscovery? networkDiscovery = app.Services.GetService<INetworkDiscovery>();
            if (networkDiscovery is not null)
            {
                Logger.App($"Internal Address: {networkDiscovery.InternalAddress}");
                if (
                    !string.IsNullOrEmpty(networkDiscovery.ExternalIp)
                    && networkDiscovery.ExternalIp != "0.0.0.0"
                )
                    Logger.App($"External Address: {networkDiscovery.ExternalAddress}");
                if (networkDiscovery.ExternalAddressV6 is not null)
                    Logger.App($"External IPv6 Address: {networkDiscovery.ExternalAddressV6}");
            }

            if (!options.RunAsService && !Console.IsOutputRedirected)
                await ConsoleMessages.ServerRunning();

            Logger.App($"Server started in {stopWatch.ElapsedMilliseconds}ms");

            await Dev.Run();
        });

        IServerRunner serverRunner = app.Services.GetRequiredService<IServerRunner>();

        bool shouldRetry;
        if (needsSetupMode)
        {
            Logger.App("Starting in HTTP mode — waiting for setup completion...");
            shouldRetry = await serverRunner.RunWithHttpsRestart(app, options, orchestrator);
        }
        else
        {
            shouldRetry = await serverRunner.RunHost(app);
        }

        if (shouldRetry)
        {
            Logger.App("Rebuilding server after port conflict resolution...");
            shutdownCoordinator.RequestShutdown(); // Reset existing (if any)

            Stopwatch retryStopWatch = new();
            retryStopWatch.Start();

            WebApplication retryHost = WebHostFactory.Create(options, forceHttp: needsSetupMode);
            HostLifecycleHooks.Register(retryHost, retryStopWatch);

            IServerRunner retryServerRunner = retryHost.Services.GetRequiredService<IServerRunner>();

            // Force the DI container to instantiate QueueRunner (it's a lazy singleton).
            QueueRunner retryQueueRunner = retryHost.Services.GetRequiredService<QueueRunner>();

            // Initialize queue workers so they can process jobs on the retry host.
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    Logger.App("Initializing QueueRunner for retry host...");
                    await retryQueueRunner.Initialize();
                    Logger.App("QueueRunner initialized for retry host");
                }
                catch (Exception ex)
                {
                    Logger.App($"Failed to initialize QueueRunner for retry host: {ex}");
                }
            });

            if (needsSetupMode)
            {
                // Re-enter the setup/certificate flow so the server can complete
                // first-boot setup rather than just running without HTTPS support.
                // Resolve a fresh orchestrator from the retry host's DI container.
                BootOrchestrator retryOrchestrator =
                    retryHost.Services.GetRequiredService<BootOrchestrator>();
                await retryServerRunner.RunWithHttpsRestart(retryHost, options, retryOrchestrator);
            }
            else
            {
                await retryServerRunner.RunHost(retryHost);
            }
        }
    }
}
