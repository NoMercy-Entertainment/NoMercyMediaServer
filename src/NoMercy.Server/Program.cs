using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NoMercy.Networking;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Server.Seeds;
using NoMercy.Setup;

namespace NoMercy.Server;

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

        _applicationShutdownCts = new();
        bool startedWithoutCert = !Certificate.HasValidCertificate();
        IWebHost app = CreateWebHostBuilder(options).Build();

        SetupState setupState = app.Services.GetRequiredService<SetupState>();
        TokenState tokenState = await SetupState.ValidateTokenFile();
        SetupPhase initialPhase = setupState.DetermineInitialPhase(tokenState);
        Logger.App($"Token validation: {tokenState} → setup phase: {initialPhase}");

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
        });

        if (startedWithoutCert)
        {
            Logger.App("Starting in HTTP mode — waiting for certificate acquisition...");
            await RunWithHttpsRestart(app, options);
        }
        else
        {
            await RunHost(app);
        }
    }

    private static void RegisterLifetimeEvents(IWebHost app, Stopwatch stopWatch)
    {
        app.Services.GetService<IHostApplicationLifetime>()?.ApplicationStarted.Register(() =>
        {
            Config.Started = true;
            stopWatch.Stop();

            Task.Run(async () =>
            {
                Task.Delay(300).Wait();

                Logger.App($"Internal Address: {Networking.Networking.InternalAddress}");
                Logger.App($"External Address: {Networking.Networking.ExternalAddress}");

                if (!IsRunningAsService && !Console.IsOutputRedirected)
                    await ConsoleMessages.ServerRunning();

                Logger.App($"Server started in {stopWatch.ElapsedMilliseconds}ms");

                await Dev.Run();
            });
        });

        app.Services.GetService<IHostApplicationLifetime>()?.ApplicationStopping.Register(() =>
        {
            Logger.App("Application is shutting down...");
        });
    }

    private static async Task RunWithHttpsRestart(IWebHost httpHost, StartupOptions options)
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
            await HandlePortInUse(Config.InternalServerPort, ex);
            return;
        }

        // Wait for either setup completion or shutdown
        Task shutdownTask = Task.Delay(Timeout.Infinite, _applicationShutdownCts!.Token);
        Task setupCompleteTask = setupState.WaitForSetupCompleteAsync(_applicationShutdownCts.Token);

        Task completedTask = await Task.WhenAny(setupCompleteTask, shutdownTask);

        if (completedTask == shutdownTask || _applicationShutdownCts.IsCancellationRequested)
        {
            await httpHost.StopAsync(TimeSpan.FromSeconds(10));
            httpHost.Dispose();
            return;
        }

        // Setup completed — certificate should now be available
        if (!Certificate.HasValidCertificate())
        {
            Logger.App("Setup completed but certificate not found — continuing on HTTP");
            await httpHost.WaitForShutdownAsync(_applicationShutdownCts.Token);
            httpHost.Dispose();
            return;
        }

        Logger.App("Certificate acquired — restarting with HTTPS...");

        // Gracefully stop the HTTP host
        Config.Started = false;
        await httpHost.StopAsync(TimeSpan.FromSeconds(10));
        httpHost.Dispose();

        // Build and start a new host with HTTPS
        _applicationShutdownCts = new();
        Stopwatch restartStopWatch = new();
        restartStopWatch.Start();

        IWebHost httpsHost = CreateWebHostBuilder(options).Build();
        RegisterLifetimeEvents(httpsHost, restartStopWatch);

        Logger.App("HTTPS server starting...");
        await RunHost(httpsHost);
    }

    private static async Task RunHost(IWebHost host)
    {
        try
        {
            if (IsRunningAsService && OperatingSystem.IsWindows())
            {
                host.RunAsService();
            }
            else
            {
                await host.RunAsync(_applicationShutdownCts!.Token);
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException
                                     || ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
        {
            await HandlePortInUse(Config.InternalServerPort, ex);
        }
        catch (OperationCanceledException)
        {
            Logger.App("Shutdown completed");
        }
    }

    private static async Task HandlePortInUse(int port, IOException ex)
    {
        Logger.Error($"Failed to start: port {port} is already in use.");

        string processInfo = await FindProcessOnPortAsync(port);

        if (!string.IsNullOrEmpty(processInfo))
            Logger.Error($"Process holding port {port}:\n{processInfo}");
        else
            Logger.Warning($"Could not identify the process using port {port}.");

        bool isInteractive = !IsRunningAsService && !Console.IsInputRedirected && !Console.IsOutputRedirected;

        if (!isInteractive)
        {
            Logger.Error("Running non-interactively — cannot prompt to kill the blocking process. "
                        + "Stop the other process manually and restart the server.");
            Environment.ExitCode = 1;
            return;
        }

        int blockingPid = ParsePidFromPortInfo(processInfo);

        if (blockingPid <= 0)
        {
            Logger.Error("Could not determine the PID of the blocking process. Please free the port manually.");
            Environment.ExitCode = 1;
            return;
        }

        Console.Write($"\nWould you like to kill process {blockingPid} and retry? [y/N] ");
        string? answer = Console.ReadLine();

        if (string.IsNullOrEmpty(answer) || !answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
        {
            Logger.App("User declined to kill the blocking process. Exiting.");
            Environment.ExitCode = 1;
            return;
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
            return;
        }

        // Brief pause so the OS releases the socket
        await Task.Delay(1000);
        Logger.App("Port freed — please restart the server.");
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

    private static IWebHostBuilder CreateWebHostBuilder(StartupOptions options)
    {
        List<IPAddress> localAddresses =
        [
            IPAddress.Any
        ];

        if(Software.IsWindows || Software.IsMac)
            localAddresses.Add(IPAddress.IPv6Any);

        IWebHostBuilder builder = WebHost.CreateDefaultBuilder([])
            .ConfigureServices(services =>
            {
                services.AddSingleton<StartupOptions>(options);
                services.AddSingleton<IApiVersionDescriptionProvider, DefaultApiVersionDescriptionProvider>();
                services.AddSingleton<ISunsetPolicyManager, DefaultSunsetPolicyManager>();
                services.AddSingleton(typeof(ILogger<>), typeof(CustomLogger<>));

                // Configure host options with reduced shutdown timeout
                services.Configure<HostOptions>(hostOptions =>
                {
                    hostOptions.ShutdownTimeout = TimeSpan.FromSeconds(10);
                });

                // Systemd integration — context-aware, no-ops when not running under systemd.
                // Registers SystemdLifetime (sd_notify) and configures console logging for journal.
                if (IsRunningAsService && Software.IsLinux)
                    services.AddSystemd();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            })
            .ConfigureKestrel(kestrelOptions =>
            {
                Certificate.KestrelConfig(kestrelOptions);

                // Main server endpoints — HTTPS when certificate is available
                foreach (IPAddress address in localAddresses)
                {
                    kestrelOptions.Listen(address, Config.InternalServerPort, listenOptions =>
                    {
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
            })
            .UseQuic()
            .UseSockets()
            .UseStartup<Startup>();

        // Set content root to executable directory when running as a service
        if (IsRunningAsService)
            builder.UseContentRoot(AppContext.BaseDirectory);

        return builder;
    }
}