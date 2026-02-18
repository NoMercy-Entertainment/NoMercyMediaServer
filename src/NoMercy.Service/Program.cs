using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NoMercy.Networking;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue;
using NoMercy.Service.Configuration;
using NoMercy.Service.Seeds;
using NoMercy.Setup;

namespace NoMercy.Service;

public static class Program
{
    private static int _shutdownAttempts;
    private static readonly object ShutdownLock = new();
    private static CancellationTokenSource? _applicationShutdownCts;

    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Exception exception = (Exception)eventArgs.ExceptionObject;
            Logger.App("UnhandledException " + exception);
        };

        Console.CancelKeyPress += (_, e) =>
        {
            lock (ShutdownLock)
            {
                _shutdownAttempts++;

                if (_shutdownAttempts == 1)
                {
                    e.Cancel = true; // Prevent immediate termination
                    Logger.App("Graceful shutdown initiated... (Press Ctrl+C again to force shutdown)");
                    _applicationShutdownCts?.Cancel();
                }
                else if (_shutdownAttempts >= 2)
                {
                    e.Cancel = false; // Allow immediate termination
                    Logger.App("Force shutdown requested!");
                    Environment.Exit(1);
                }
            }
        };

        await Parser.Default.ParseArguments<StartupOptions>(args)
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

        if (IsRunningAsService)
        {
            // When running as a service, the working directory may not be the executable's directory.
            // Windows services start in system32; systemd services start in /.
            // Set it to the executable's directory so config and data paths resolve correctly.
            string exeDir = AppContext.BaseDirectory;
            Directory.SetCurrentDirectory(exeDir);

            string platform = Software.IsWindows ? "Windows service" :
                              Software.IsLinux ? "systemd service" :
                              Software.IsMac ? "launchd service" : "service";
            Logger.App($"Running as {platform}, content root: {exeDir}");
        }

        if (!IsRunningAsService && !Console.IsOutputRedirected)
        {
            Console.Clear();
            Console.Title = AppFiles.ApplicationName;
        }

        if (!IsRunningAsService)
            ConsoleMessages.Logo();

        options.ApplySettings();

        if (Config.Sentry)
            SentrySdk.Init(config =>
            {
                config.Dsn = Config.SentryDsn;
                config.TracesSampleRate = 1.0;
            });

        Version version = Assembly.GetExecutingAssembly().GetName().Version!;
        Software.Version = version;
        Logger.App($"NoMercy MediaServer version: v{version.Major}.{version.Minor}.{version.Build}");

        Stopwatch stopWatch = new();
        stopWatch.Start();

        List<TaskDelegate> startupTasks =
        [
            DatabaseSeeder.Run,
            // new(Dev.Run)
        ];

        // Phase 1 only (UserSettings, CreateAppFolders, ApiInfo) — fast, no network
        await Setup.Start.InitEssential(startupTasks);

        // Create database schema before anything else can query it.
        // This does NOT require auth — only migrations + EnsureCreated.
        await DatabaseSeeder.InitSchema();

        // Seed offline data (config, languages, encoder profiles, etc.)
        // immediately so the UI has data before auth completes.
        await DatabaseSeeder.SeedOfflineData();

        // Proactively resolve port conflicts before building the host.
        // This avoids the costly build→fail→kill→rebuild cycle and prevents
        // CronWorker "Failed to start database job workers" errors.
        await EnsurePortAvailable(Config.InternalServerPort);

        _applicationShutdownCts = new();
        bool startedWithoutCert = !Certificate.HasValidCertificate();
        WebApplication app = CreateWebApplication(options);

        SetupState setupState = app.Services.GetRequiredService<SetupState>();
        TokenState tokenState = await SetupState.ValidateTokenFile();
        SetupPhase initialPhase = setupState.DetermineInitialPhase(tokenState);
        Logger.App($"Token validation: {tokenState} → setup phase: {initialPhase}");

        // Force QueueRunner singleton creation so QueueRunner.Current is set
        // before InitRemaining's background task tries to call Initialize().
        app.Services.GetRequiredService<QueueRunner>();

        RegisterLifetimeEvents(app, stopWatch);

        // Run Phase 2-4 in background (auth, networking, registration) while host is starting
        _ = Task.Run(async () =>
        {
            try
            {
                await Setup.Start.InitRemaining();
            }
            catch (Exception ex)
            {
                Logger.App($"Background startup tasks failed: {ex.Message}");
            }

            // Show addresses and server box after all startup tasks complete
            NoMercy.Networking.Discovery.INetworkDiscovery? networkDiscovery = app.Services.GetService<NoMercy.Networking.Discovery.INetworkDiscovery>();
            if (networkDiscovery is not null)
            {
                Logger.App($"Internal Address: {networkDiscovery.InternalAddress}");
                if (!string.IsNullOrEmpty(networkDiscovery.ExternalIp) && networkDiscovery.ExternalIp != "0.0.0.0")
                    Logger.App($"External Address: {networkDiscovery.ExternalAddress}");
                if (networkDiscovery.ExternalAddressV6 is not null)
                    Logger.App($"External IPv6 Address: {networkDiscovery.ExternalAddressV6}");
            }

            if (!IsRunningAsService && !Console.IsOutputRedirected)
                await ConsoleMessages.ServerRunning();

            Logger.App($"Server started in {stopWatch.ElapsedMilliseconds}ms");

            // Auto-open setup URL in browser if in setup mode and desktop environment
            SetupState? setupState = app.Services.GetService<SetupState>();
            if (setupState?.IsSetupRequired == true && !IsRunningAsService && Auth.IsDesktopEnvironment())
            {
                try
                {
                    string internalIp = networkDiscovery?.InternalIp ?? "127.0.0.1";
                    string setupUrl = $"http://{internalIp}:{Config.InternalServerPort}/setup";
                    Logger.App($"Opening setup page in browser: {setupUrl}");
                    Auth.OpenBrowser(setupUrl);
                }
                catch (Exception ex)
                {
                    Logger.App($"Could not open browser automatically: {ex.Message}");
                    string internalIp2 = networkDiscovery?.InternalIp ?? "127.0.0.1";
                    Logger.App($"Please open your browser and navigate to: http://{internalIp2}:{Config.InternalServerPort}/setup");
                }
            }

            await Dev.Run();
        });

        bool shouldRetry;
        if (startedWithoutCert)
        {
            Logger.App("Starting in HTTP mode — waiting for certificate acquisition...");
            shouldRetry = await RunWithHttpsRestart(app, options);
        }
        else
        {
            shouldRetry = await RunHost(app);
        }

        if (shouldRetry)
        {
            Logger.App("Rebuilding server after port conflict resolution...");
            _applicationShutdownCts = new();
            Stopwatch retryStopWatch = new();
            retryStopWatch.Start();

            WebApplication retryHost = CreateWebApplication(options);
            RegisterLifetimeEvents(retryHost, retryStopWatch);

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

            if (startedWithoutCert)
            {
                // Re-enter the setup/certificate flow so the server can complete
                // first-boot setup rather than just running without HTTPS support.
                await RunWithHttpsRestart(retryHost, options);
            }
            else
            {
                await RunHost(retryHost);
            }
        }
    }

    private static void RegisterLifetimeEvents(WebApplication app, Stopwatch stopWatch)
    {
        app.Services.GetService<IHostApplicationLifetime>()?.ApplicationStarted.Register(() =>
        {
            Config.Started = true;
            stopWatch.Stop();
        });

        app.Services.GetService<IHostApplicationLifetime>()?.ApplicationStopping.Register(() =>
        {
            Logger.App("Application is shutting down...");
        });
    }

    private static async Task<bool> RunWithHttpsRestart(WebApplication httpHost, StartupOptions options)
    {
        SetupState setupState = httpHost.Services.GetRequiredService<SetupState>();

        // Start the HTTP host
        try
        {
            await httpHost.StartAsync(_applicationShutdownCts!.Token);
        }
        catch (IOException ex) when (ex.InnerException is SocketException
                                     || ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
        {
            bool shouldRetry = await HandlePortInUse(Config.InternalServerPort, ex);
            await httpHost.DisposeAsync();
            return shouldRetry;
        }

        // Wait for either setup completion or shutdown
        Task shutdownTask = Task.Delay(Timeout.Infinite, _applicationShutdownCts!.Token);
        Task setupCompleteTask = setupState.WaitForSetupCompleteAsync(_applicationShutdownCts.Token);

        Task completedTask = await Task.WhenAny(setupCompleteTask, shutdownTask);

        if (completedTask == shutdownTask || _applicationShutdownCts.IsCancellationRequested)
        {
            await httpHost.StopAsync(TimeSpan.FromSeconds(10));
            await httpHost.DisposeAsync();
            return false;
        }

        // Setup completed — certificate should now be available
        if (!Certificate.HasValidCertificate())
        {
            Logger.App("Setup completed but certificate not found — continuing on HTTP");
            await httpHost.WaitForShutdownAsync(_applicationShutdownCts.Token);
            await httpHost.DisposeAsync();
            return false;
        }

        Logger.App("Certificate acquired — restarting with HTTPS...");

        // Give the SSO callback page time to deliver its response to the browser
        await Task.Delay(3000);

        // Gracefully stop the HTTP host
        Config.Started = false;
        await httpHost.StopAsync(TimeSpan.FromSeconds(10));
        await httpHost.DisposeAsync();

        // Build and start a new host with HTTPS
        _applicationShutdownCts = new();
        Stopwatch restartStopWatch = new();
        restartStopWatch.Start();

        WebApplication httpsHost = CreateWebApplication(options);

        // Re-evaluate token state so the new host's SetupState reflects
        // that auth already completed — prevents reopening the setup page.
        SetupState httpsSetupState = httpsHost.Services.GetRequiredService<SetupState>();
        TokenState httpsTokenState = await SetupState.ValidateTokenFile();
        httpsSetupState.DetermineInitialPhase(httpsTokenState);

        // Force the DI container to instantiate QueueRunner (it's a lazy singleton).
        // The constructor sets QueueRunner.Current = this.
        QueueRunner httpsQueueRunner = httpsHost.Services.GetRequiredService<QueueRunner>();

        // Initialize queue workers so they can process jobs on the HTTPS host.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                Logger.App("Initializing QueueRunner for HTTPS host...");
                await httpsQueueRunner.Initialize();
                Logger.App("QueueRunner initialized for HTTPS host");
            }
            catch (Exception ex)
            {
                Logger.App($"Failed to initialize QueueRunner for HTTPS host: {ex}");
            }
        });

        RegisterLifetimeEvents(httpsHost, restartStopWatch);

        Logger.App("HTTPS server starting...");
        return await RunHost(httpsHost);
    }

    private static async Task<bool> RunHost(WebApplication host)
    {
        try
        {
            await host.RunAsync(_applicationShutdownCts!.Token);
        }
        catch (IOException ex) when (ex.InnerException is SocketException
                                     || ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
        {
            bool shouldRetry = await HandlePortInUse(Config.InternalServerPort, ex);
            await host.DisposeAsync();
            return shouldRetry;
        }
        catch (OperationCanceledException)
        {
            Logger.App("Shutdown completed");
        }

        return false;
    }

    private static async Task EnsurePortAvailable(int port)
    {
        if (IsPortAvailable(port))
            return;

        Logger.App($"Port {port} is in use — checking for stale instances...");
        string processInfo = await FindProcessOnPortAsync(port);

        if (!string.IsNullOrEmpty(processInfo))
            Logger.App($"Process holding port {port}:\n{processInfo}");

        int blockingPid = ParsePidFromPortInfo(processInfo);

        if (blockingPid <= 0)
        {
            Logger.Error($"Port {port} is in use but cannot identify the process. Please free it manually.");
            Environment.ExitCode = 1;
            Environment.Exit(1);
            return;
        }

        bool isStaleInstance = false;
        try
        {
            Process blockingProcess = Process.GetProcessById(blockingPid);
            isStaleInstance = blockingProcess.ProcessName == "NoMercyMediaServer";
        }
        catch
        {
            // Process may have exited between detection and lookup
        }

        if (isStaleInstance)
        {
            Logger.App($"Stale NoMercyMediaServer instance detected (PID {blockingPid}). Auto-killing...");
        }
        else
        {
            bool isInteractive = !IsRunningAsService && !Console.IsInputRedirected && !Console.IsOutputRedirected;
            if (!isInteractive)
            {
                Logger.Error($"Port {port} is in use by PID {blockingPid}. Stop it manually and restart.");
                Environment.ExitCode = 1;
                Environment.Exit(1);
                return;
            }

            Console.Write($"\nPort {port} is in use by process {blockingPid}. Kill it and retry? [y/N] ");
            string? answer = Console.ReadLine();
            if (string.IsNullOrEmpty(answer) || !answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                Logger.App("User declined. Exiting.");
                Environment.ExitCode = 1;
                Environment.Exit(1);
                return;
            }
        }

        try
        {
            Process blockingProcess = Process.GetProcessById(blockingPid);
            Logger.App($"Killing process {blockingPid} ({blockingProcess.ProcessName})...");
            blockingProcess.Kill();
            blockingProcess.WaitForExit(5000);
            Logger.App($"Process {blockingPid} terminated.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to kill process {blockingPid}: {ex.Message}");
            Environment.ExitCode = 1;
            Environment.Exit(1);
            return;
        }

        // Retry port check — the OS may not release the socket immediately
        bool portFreed = false;
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            await Task.Delay(500);
            if (IsPortAvailable(port))
            {
                portFreed = true;
                break;
            }
            Logger.App($"Port {port} still in use, retrying ({attempt}/5)...");
        }

        if (!portFreed)
        {
            Logger.Error($"Port {port} still not available after killing process. Exiting.");
            Environment.ExitCode = 1;
            Environment.Exit(1);
            return;
        }

        Logger.App("Port freed — continuing startup...");
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            // Check on IPAddress.Any (0.0.0.0) to match how Kestrel binds.
            // Checking only on Loopback can miss processes bound to 0.0.0.0.
            using TcpListener listener = new(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task<bool> HandlePortInUse(int port, IOException ex)
    {
        Logger.Error($"Failed to start: port {port} is already in use.");

        string processInfo = await FindProcessOnPortAsync(port);

        if (!string.IsNullOrEmpty(processInfo))
            Logger.Error($"Process holding port {port}:\n{processInfo}");
        else
            Logger.Warning($"Could not identify the process using port {port}.");

        int blockingPid = ParsePidFromPortInfo(processInfo);

        if (blockingPid <= 0)
        {
            Logger.Error("Could not determine the PID of the blocking process. Please free the port manually.");
            Environment.ExitCode = 1;
            return false;
        }

        // Check if the blocking process is a stale instance of ourselves
        bool isStaleInstance = false;
        try
        {
            Process blockingProcess = Process.GetProcessById(blockingPid);
            isStaleInstance = blockingProcess.ProcessName == "NoMercyMediaServer";
        }
        catch
        {
            // Process may have exited between detection and lookup
        }

        if (isStaleInstance)
        {
            Logger.App($"Stale NoMercyMediaServer instance detected (PID {blockingPid}). Auto-killing...");
        }
        else
        {
            bool isInteractive = !IsRunningAsService && !Console.IsInputRedirected && !Console.IsOutputRedirected;

            if (!isInteractive)
            {
                Logger.Error("Running non-interactively — cannot prompt to kill the blocking process. "
                            + "Stop the other process manually and restart the server.");
                Environment.ExitCode = 1;
                return false;
            }

            Console.Write($"\nWould you like to kill process {blockingPid} and retry? [y/N] ");
            string? answer = Console.ReadLine();

            if (string.IsNullOrEmpty(answer) || !answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                Logger.App("User declined to kill the blocking process. Exiting.");
                Environment.ExitCode = 1;
                return false;
            }
        }

        try
        {
            Process blockingProcess = Process.GetProcessById(blockingPid);
            Logger.App($"Killing process {blockingPid} ({blockingProcess.ProcessName})...");
            blockingProcess.Kill();
            blockingProcess.WaitForExit(5000);
            Logger.App($"Process {blockingPid} terminated.");
        }
        catch (Exception killEx)
        {
            Logger.Error($"Failed to kill process {blockingPid}: {killEx.Message}");
            Environment.ExitCode = 1;
            return false;
        }

        // Retry port check — the OS may not release the socket immediately
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            await Task.Delay(500);
            if (IsPortAvailable(port))
            {
                Logger.App("Port freed — retrying...");
                return true;
            }
            Logger.App($"Port {port} still in use, retrying ({attempt}/5)...");
        }

        Logger.Error($"Port {port} still not available after killing process.");
        Environment.ExitCode = 1;
        return false;
    }

    private static async Task<string> FindProcessOnPortAsync(int port)
    {
        try
        {
            if (Software.IsWindows)
            {
                Shell.ExecResult result = await Shell.ExecAsync("netstat", "-ano");
                if (!result.Success)
                    return string.Empty;

                // Filter lines containing the port in LISTENING state
                string[] lines = result.StandardOutput.Split('\n');
                List<string> matches = [];
                foreach (string line in lines)
                {
                    if (line.Contains($":{port} ") && line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                        matches.Add(line.Trim());
                }

                if (matches.Count == 0)
                    return string.Empty;

                // Extract PIDs and get process names
                List<string> output = [];
                foreach (string match in matches)
                {
                    string[] parts = match.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string pidStr = parts[^1];
                    if (int.TryParse(pidStr, out int pid))
                    {
                        try
                        {
                            Process proc = Process.GetProcessById(pid);
                            output.Add($"  PID {pid} ({proc.ProcessName}) — {match}");
                        }
                        catch
                        {
                            output.Add($"  PID {pidStr} — {match}");
                        }
                    }
                    else
                    {
                        output.Add($"  {match}");
                    }
                }
                return string.Join("\n", output);
            }
            else
            {
                // Linux / macOS — try ss first, fall back to lsof
                Shell.ExecResult result = await Shell.ExecAsync("ss", $"-tlnp sport = :{port}");
                if (result.Success && result.StandardOutput.Contains($":{port}"))
                    return result.StandardOutput;

                result = await Shell.ExecAsync("lsof", $"-i :{port} -sTCP:LISTEN -P -n");
                if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
                    return result.StandardOutput;

                return string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int ParsePidFromPortInfo(string processInfo)
    {
        if (string.IsNullOrEmpty(processInfo))
            return -1;

        if (Software.IsWindows)
        {
            // Look for "PID <number>" in our formatted output
            Match match = Regex.Match(processInfo, @"PID\s+(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int pid))
                return pid;
        }
        else
        {
            // ss output: pid=<number>
            Match match = Regex.Match(processInfo, @"pid=(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int pid))
                return pid;

            // lsof output: second column is PID (skip header)
            string[] lines = processInfo.Split('\n');
            if (lines.Length > 1)
            {
                string[] parts = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && int.TryParse(parts[1], out pid))
                    return pid;
            }
        }

        return -1;
    }

    private static async Task Shutdown()
    {
        await Task.CompletedTask;
    }

    private static async Task Restart()
    {
        await Task.CompletedTask;
    }

    private static WebApplication CreateWebApplication(StartupOptions options)
    {
        List<IPAddress> localAddresses =
        [
            IPAddress.Any
        ];

        if(Software.IsWindows || Software.IsMac)
            localAddresses.Add(IPAddress.IPv6Any);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IApiVersionDescriptionProvider, DefaultApiVersionDescriptionProvider>();
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

            // Main server endpoints — HTTPS when certificate is available, with HTTP/3 (QUIC) support
            foreach (IPAddress address in localAddresses)
            {
                kestrelOptions.Listen(address, Config.InternalServerPort, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                    Certificate.ConfigureHttpsListener(listenOptions);
                });
            }

            // IPC transport — named pipe (Windows) or Unix socket (Linux/macOS)
            if (Software.IsWindows)
            {
                kestrelOptions.ListenNamedPipe(Config.ManagementPipeName, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1;
                });

                Logger.App(
                    $"IPC listening on named pipe: {Config.ManagementPipeName}");
            }
            else
            {
                string socketPath = Config.ManagementSocketPath;

                // Remove stale socket file from previous run
                if (File.Exists(socketPath))
                    File.Delete(socketPath);

                kestrelOptions.ListenUnixSocket(socketPath, listenOptions =>
                {
                    listenOptions.Protocols =
                        HttpProtocols.Http1;
                });

                Logger.App(
                    $"IPC listening on Unix socket: {socketPath}");
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
        IApiVersionDescriptionProvider provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        ApplicationConfiguration.ConfigureApp(app, provider);

        return app;
    }
}
