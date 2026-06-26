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

namespace NoMercy.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Resolve renamed OpenSSL DLL for installer deployments where
        // libcrypto-3-x64.dll is renamed to nmossl-3-x64.dll to avoid
        // file locks from other applications on the system.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) =>
            {
                if (!name.Contains("libcrypto", StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                string renamed = Path.Combine(AppContext.BaseDirectory, "nmossl-3-x64.dll");
                if (File.Exists(renamed) && NativeLibrary.TryLoad(renamed, out IntPtr handle))
                    return handle;

                return IntPtr.Zero;
            };
        }

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Exception exception = (Exception)eventArgs.ExceptionObject;
            Logger.App("UnhandledException " + exception);
        };

        // Tasks that lose their last exception observer (fire-and-forget
        // patterns, GC'd before await) raise here. Marking them observed
        // keeps them from escalating to UnhandledException. async-void
        // chains aren't covered by this — those are handled defensively
        // at the source (see ChromeCast.NeutralizeTimer).
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.App("UnobservedTaskException " + e.Exception);
            e.SetObserved();
        };

        await Parser
            .Default.ParseArguments<StartupOptions>(args)
            .MapResult(Start, ErrorParsingArguments);

        static Task ErrorParsingArguments(IEnumerable<Error> errors)
        {
            Environment.ExitCode = 1;
            return Task.CompletedTask;
        }
    }

    internal static bool IsRunningAsService { get; private set; }

    private static async Task Start(StartupOptions options)
    {
        IsRunningAsService = options.RunAsService;

        switch (IsRunningAsService)
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

        if (!IsRunningAsService)
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

        WebApplication app = CreateWebApplication(options, forceHttp: !hasCert);

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
            app = CreateWebApplication(options, forceHttp: true);
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

        RegisterLifetimeEvents(app, stopWatch);

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

            if (!IsRunningAsService && !Console.IsOutputRedirected)
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

            WebApplication retryHost = CreateWebApplication(options, forceHttp: needsSetupMode);
            RegisterLifetimeEvents(retryHost, retryStopWatch);

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

    internal static void RegisterLifetimeEvents(WebApplication app, Stopwatch stopWatch)
    {
        app.Services.GetService<IHostApplicationLifetime>()
            ?.ApplicationStarted.Register(() =>
            {
                Config.Started = true;
                stopWatch.Stop();
            });

        app.Services.GetService<IHostApplicationLifetime>()
            ?.ApplicationStopping.Register(() =>
            {
                Logger.App("Application is shutting down...");
            });
    }
    
    internal static WebApplication CreateWebApplication(
        StartupOptions options,
        bool forceHttp = false
    )
    {
        List<IPAddress> localAddresses = [IPAddress.Any];

        // if (Software.IsWindows || Software.IsMac)
        //     localAddresses.Add(IPAddress.IPv6Any);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton<IPortManager, PortManager>();
        builder.Services.AddSingleton<IShutdownCoordinator, ShutdownCoordinator>();
        builder.Services.AddSingleton<IServerRunner, ServerRunner>();
        builder.Services.AddSingleton<IPluginLoader, PluginLoader>();
        builder.Services.AddSingleton<IApiKeyStore, ApiKeyStore>();
        builder.Services.AddSingleton<IApiKeyLoader, ApiKeyLoader>();

        builder.Services.Configure<ServerConfiguration>(builder.Configuration.GetSection("Server"));
        builder.Services.AddSingleton<IServerConfiguration, ServerConfigurationWrapper>();

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<
            IApiVersionDescriptionProvider,
            DefaultApiVersionDescriptionProvider
        >();
        builder.Services.AddSingleton<ISunsetPolicyManager, DefaultSunsetPolicyManager>();
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(CustomLogger<>));

        // Configure host options with reduced shutdown timeout
        builder.Services.Configure<HostOptions>(hostOptions =>
        {
            hostOptions.ShutdownTimeout = TimeSpan.FromSeconds(10);
        });

        // Service integration — context-aware lifetime management
        if (IsRunningAsService)
        {
            if (Software.IsWindows)
                builder.Services.AddWindowsService();
            else if (Software.IsLinux)
                builder.Services.AddSystemd();
        }

        builder.Logging.ClearProviders();

        builder.WebHost.ConfigureKestrel(kestrelOptions =>
        {
            Certificate.KestrelConfig(kestrelOptions);

            // Main server endpoints.
            // forceHttp = true during setup/auth, so we never need HTTPS to handle the
            // OAuth callback and setup UI, even when a stale cert file is present.
            foreach (IPAddress address in localAddresses)
            {
                kestrelOptions.Listen(
                    address,
                    Config.InternalServerPort,
                    listenOptions =>
                    {
                        if (forceHttp)
                        {
                            listenOptions.Protocols = HttpProtocols.Http1;
                        }
                        else
                        {
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                            Certificate.ConfigureHttpsListener(listenOptions);
                        }
                    }
                );
            }

            // Health check endpoint — HTTP only, localhost only (for Docker HEALTHCHECK)
            kestrelOptions.Listen(
                IPAddress.Loopback,
                Config.InternalServerPort + 1,
                listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1;
                }
            );

            // IPC transport — named pipe (Windows) or Unix socket (Linux/macOS)
            if (Software.IsWindows)
            {
                kestrelOptions.ListenNamedPipe(
                    Config.ManagementPipeName,
                    listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1;
                    }
                );

                Logger.App($"IPC listening on named pipe: {Config.ManagementPipeName}");
            }
            else
            {
                string socketPath = Config.ManagementSocketPath;

                // Remove stale socket file from previous run
                if (File.Exists(socketPath))
                    File.Delete(socketPath);

                kestrelOptions.ListenUnixSocket(
                    socketPath,
                    listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1;
                    }
                );

                Logger.App($"IPC listening on Unix socket: {socketPath}");
            }
        });

        builder.WebHost.UseQuic();
        builder.WebHost.UseSockets();

        // Set content root to executable directory when running as a service
        if (IsRunningAsService)
            builder.WebHost.UseContentRoot(AppContext.BaseDirectory);

        // Register services from Startup.ConfigureServices
        ServiceConfiguration.ConfigureServices(builder.Services);
        builder.Services.AddSingleton(options);

        WebApplication app = builder.Build();

        // Configure middleware from Startup.Configure
        IApiVersionDescriptionProvider provider =
            app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        ApplicationConfiguration.ConfigureApp(app, provider);

        return app;
    }
}
