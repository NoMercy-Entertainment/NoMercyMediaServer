using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using Microsoft.AspNetCore;
using NoMercy.Data.Jobs;
using NoMercy.Data.Logic;
using NoMercy.Database;
using NoMercy.Helpers.Monitoring;
using NoMercy.MediaProcessing.Files;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Queue;
using NoMercy.Server.app.Helper;
using AppFiles = NoMercy.NmSystem.AppFiles;

namespace NoMercy.Server;
public static class Program
{
    [DllImport("Kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
    [DllImport("User32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int cmdShow);
    
    public static int ConsoleVisible { get; set; } = 1;

    internal static void VsConsoleWindow(int i)
    {
        IntPtr hWnd = GetConsoleWindow();
        if (hWnd != IntPtr.Zero)
        {
            ConsoleVisible = i;
            ShowWindow(hWnd, i);
        }
    }
    
    private static bool ShouldSeedMarvel { get; set; }
    
    public static Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Exception exception = (Exception)eventArgs.ExceptionObject;
            Logger.App("UnhandledException " + exception);
        };

        Console.CancelKeyPress += (_, _) =>
        {
            Shutdown().Wait();
            Environment.Exit(0);
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Logger.App("SIGTERM received, shutting down.");
            Shutdown().Wait();
        };

        return Parser.Default.ParseArguments<StartupOptions>(args)
            .MapResult(Start, ErrorParsingArguments);
        
        static Task ErrorParsingArguments(IEnumerable<Error> errors)
        {
            Environment.ExitCode = 1;
            return Task.CompletedTask;
        }
    }

    private static async Task Start(StartupOptions options)
    {
        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
            Console.Title = "NoMercy Server";
        }

        options.ApplySettings(out bool shouldSeedMarvel);
        ShouldSeedMarvel = shouldSeedMarvel;

        Stopwatch stopWatch = new();
        stopWatch.Start();

        Databases.QueueContext = new();
        Databases.MediaContext = new();

        await Init();

        IWebHost app = CreateWebHostBuilder(new WebHostBuilder()).Build();

        app.Services.GetService<IHostApplicationLifetime>()?.ApplicationStarted.Register(() =>
        {
            Task.Run(() =>
            {
                stopWatch.Stop();

                Task.Delay(300).Wait();

                Logger.App($"Internal Address: {Networking.Networking.InternalAddress}");
                Logger.App($"External Address: {Networking.Networking.ExternalAddress}");

                if (!Console.IsOutputRedirected)
                {
                    ConsoleMessages.ServerRunning();
                }
                
                Logger.App($"Server started in {stopWatch.ElapsedMilliseconds}ms");
            });
        });

        new Thread(() => app.RunAsync()).Start();
        
        await Task.Delay(-1);
    }

    private static async Task Shutdown()
    {
        await Task.CompletedTask;
    }

    private static async Task Restart()
    {
        await Task.CompletedTask;
    }

    private static IWebHostBuilder CreateWebHostBuilder(this IWebHostBuilder _)
    {
        UriBuilder localhostIPv4Url = new()
        {
            Host = IPAddress.Any.ToString(),
            Port = Config.InternalServerPort,
            Scheme = Uri.UriSchemeHttps
        };

        List<string> urls = [localhostIPv4Url.ToString()];

        return WebHost.CreateDefaultBuilder([])
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddFilter("Microsoft", LogLevel.None);
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<IApiVersionDescriptionProvider, DefaultApiVersionDescriptionProvider>();
                services.AddSingleton<ISunsetPolicyManager, DefaultSunsetPolicyManager>();
            })
            .ConfigureKestrel(Certificate.KestrelConfig)
            .UseUrls(urls.ToArray())
            .UseKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Limits.MaxRequestBodySize = null;
                options.Limits.MaxRequestBufferSize = null;
                options.Limits.MaxConcurrentConnections = null;
                options.Limits.MaxConcurrentUpgradedConnections = null;
            })
            .UseQuic()
            .UseSockets()
            .UseStartup<Startup>();
    }

    private static async Task Init()
    {
        await ApiInfo.RequestInfo();

        if (UserSettings.TryGetUserSettings(out Dictionary<string, string>? settings))
        {
            UserSettings.ApplySettings(settings);
        }

        List<TaskDelegate> startupTasks =
        [
            new (ConsoleMessages.Logo),
            new (AppFiles.CreateAppFolders),
            new (Networking.Networking.Discover),
            new (Auth.Init),
            new (() => Seed.Init(ShouldSeedMarvel)),
            new (Register.Init),
            new (Binaries.DownloadAll),
            // new (AniDbBaseClient.Init),
            new (TrayIcon.Make),
            new (StorageMonitor.UpdateStorage),
        ];

        // AppDomain.CurrentDomain.ProcessExit += (_, _) => { AniDbBaseClient.Dispose(); };

        await RunStartup(startupTasks);

        Thread queues = new(new Task(() => QueueRunner.Initialize().Wait()).Start)
        {
            Name = "Queue workers",
            Priority = ThreadPriority.Lowest,
            IsBackground = true
        };
        queues.Start();

        Thread fileWatcher = new(new Task(() => _ = new LibraryFileWatcher()).Start)
        {
            Name = "Library File Watcher",
            Priority = ThreadPriority.Lowest,
            IsBackground = true
        };
        fileWatcher.Start();
        
        Thread storageMonitor = new(new Task(() =>
        {
            StorageJob storageJob = new(StorageMonitor.Storage);
            storageJob.Handle().Wait();
            // JobDispatcher.Dispatch(storageJob, "data", 1000);
        }).Start)
        {
            Name = "Storage Watcher",
            Priority = ThreadPriority.Lowest,
            IsBackground = true
        };
        storageMonitor.Start();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
        {
            Logger.App(
                "Your server is ready and we will hide the console window in 10 seconds\n you can show it again by right-clicking the tray icon");
            await Task.Delay(10000)
                .ContinueWith(_ => VsConsoleWindow(0));
        }
    }

    private static async Task RunStartup(List<TaskDelegate> startupTasks)
    {
        foreach (TaskDelegate task in startupTasks)
        {
            await task.Invoke();
        }
    }
}