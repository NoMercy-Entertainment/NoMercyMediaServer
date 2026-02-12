using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting.WindowsServices;
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

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

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

        await Setup.Start.Init(startupTasks);

        _applicationShutdownCts = new CancellationTokenSource();
        IWebHost app = CreateWebHostBuilder(options).Build();

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
                // await DriveMonitor.Start();
                // LibraryFileWatcher.Start();

                if (!IsRunningAsService &&
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                    OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
                {
                    Logger.App(
                        "Your server is ready and we will hide the console window in 10 seconds\n you can show it again by right-clicking the tray icon");
                    await Task.Delay(10000)
                        .ContinueWith(_ => Setup.Start.VsConsoleWindow(0));
                }

            });
        });

        app.Services.GetService<IHostApplicationLifetime>()?.ApplicationStopping.Register(() =>
        {
            Logger.App("Application is shutting down...");
        });

        try
        {
            if (IsRunningAsService && OperatingSystem.IsWindows())
            {
                // RunAsService blocks until the Windows SCM stops the service
                app.RunAsService();
            }
            else
            {
                // On Linux (systemd) and macOS (launchd), RunAsync handles the
                // service lifecycle correctly — systemd sends SIGTERM for shutdown,
                // launchd uses SIGTERM as well. The UseSystemd() call in
                // CreateWebHostBuilder hooks into systemd's notification protocol.
                await app.RunAsync(_applicationShutdownCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.App("Shutdown completed");
        }
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
        UriBuilder localhostIPv4Url = new()
        {
            Host = IPAddress.Any.ToString(),
            Port = Config.InternalServerPort,
            Scheme = Uri.UriSchemeHttps
        };
        UriBuilder localhostIPv6Url = new()
        {
            Host = IPAddress.IPv6Any.ToString(),
            Port = Config.InternalServerPort,
            Scheme = Uri.UriSchemeHttps
        };

        List<string> urls = [
            localhostIPv4Url.ToString(),
        ];

        if(Software.IsWindows || Software.IsMac)
            urls.Add(localhostIPv6Url.ToString());

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

                // Management API — plain HTTP on separate port
                foreach (IPAddress localAddress in localAddresses)
                {
                    kestrelOptions.Listen(localAddress, Config.ManagementPort, listenOptions =>
                    {
                        listenOptions.Protocols =
                            Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
                    });
                }

                Logger.App(
                    $"Management API listening on http://0.0.0.0:{Config.ManagementPort}");

                // IPC transport — named pipe (Windows) or Unix socket (Linux/macOS)
                if (Software.IsWindows)
                {
                    kestrelOptions.ListenNamedPipe(Config.ManagementPipeName, listenOptions =>
                    {
                        listenOptions.Protocols =
                            Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
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
                            Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
                    });

                    Logger.App(
                        $"IPC listening on Unix socket: {socketPath}");
                }
            })
            .UseUrls(urls.ToArray())
            .UseQuic()
            .UseSockets()
            .UseStartup<Startup>();

        // Set content root to executable directory when running as a service
        if (IsRunningAsService)
            builder.UseContentRoot(AppContext.BaseDirectory);

        return builder;
    }
}