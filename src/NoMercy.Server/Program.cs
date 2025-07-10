using System.Diagnostics;
using System.Net;
using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using Microsoft.AspNetCore;
using NoMercy.MediaSources.OpticalMedia;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Server.Seeds;
using NoMercy.Setup;

namespace NoMercy.Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // CultureInfo.DefaultThreadCurrentCulture = new("en-US");
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
            new(DatabaseSeeder.Run),
            new(Dev.Run)
        ];

        await Setup.Start.Init(startupTasks);

        IWebHost app = CreateWebHostBuilder(options).Build();

        app.Services.GetService<IHostApplicationLifetime>()?.ApplicationStarted.Register(() =>
        {
            Config.Started = true;
            stopWatch.Stop();

            Task.Run(() =>
            {
                Task.Delay(300).Wait();

                Logger.App($"Internal Address: {Networking.Networking.InternalAddress}");
                Logger.App($"External Address: {Networking.Networking.ExternalAddress}");

                if (!Console.IsOutputRedirected) ConsoleMessages.ServerRunning();

                Logger.App($"Server started in {stopWatch.ElapsedMilliseconds}ms");
            });
        });

        new Thread(() => app.RunAsync()).Start();

        await DriveMonitor.Start();

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

    private static IWebHostBuilder CreateWebHostBuilder(StartupOptions options)
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
                services.AddSingleton<StartupOptions>(options);
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
}