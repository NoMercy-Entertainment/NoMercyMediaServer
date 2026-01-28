using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using Microsoft.AspNetCore;
using NoMercy.MediaProcessing.Files;
using NoMercy.Networking;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Server.Seeds;
using NoMercy.Setup;

namespace NoMercy.Server;

public static class Program
{
    public static async Task Main(string[] args)
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

        await Parser.Default.ParseArguments<StartupOptions>(args)
            .MapResult(Start, ErrorParsingArguments);

        static Task ErrorParsingArguments(IEnumerable<Error> errors)
        {
            Environment.ExitCode = 1;
            return Task.CompletedTask;
        }
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static async Task Start(StartupOptions options)
    {
        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
            Console.Title = AppFiles.ApplicationName;
        }

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

                if (!Console.IsOutputRedirected) await ConsoleMessages.ServerRunning();

                Logger.App($"Server started in {stopWatch.ElapsedMilliseconds}ms");

                await Dev.Run();
                // await DriveMonitor.Start();
                _ = LibraryFileWatcher.Instance;
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                    OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
                {
                    Logger.App(
                        "Your server is ready and we will hide the console window in 10 seconds\n you can show it again by right-clicking the tray icon");
                    await Task.Delay(10000)
                        .ContinueWith(_ => Setup.Start.VsConsoleWindow(0));
                }
                
            });
        });

        await app.RunAsync();
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
            localhostIPv4Url.ToString()
        ];
        
        if(Software.IsWindows || Software.IsMac)
            urls.Add(localhostIPv6Url.ToString());

        return WebHost.CreateDefaultBuilder([])
            .ConfigureServices(services =>
            {
                services.AddSingleton<StartupOptions>(options);
                services.AddSingleton<IApiVersionDescriptionProvider, DefaultApiVersionDescriptionProvider>();
                services.AddSingleton<ISunsetPolicyManager, DefaultSunsetPolicyManager>();
                services.AddSingleton(typeof(ILogger<>), typeof(CustomLogger<>));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            })
            .ConfigureKestrel(Certificate.KestrelConfig)
            .UseUrls(urls.ToArray())
            .UseQuic()
            .UseSockets()
            .UseStartup<Startup>();
    }
}