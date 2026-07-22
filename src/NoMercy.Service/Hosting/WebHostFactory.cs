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

using System.Net;
using System.Net.Sockets;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Service.Configuration;
using NoMercy.Setup.Server;

namespace NoMercy.Service.Hosting;

public static class WebHostFactory
{
    public static WebApplication Create(StartupOptions options, bool forceHttp = false)
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

        builder.Services.Configure<ServerConfiguration>(config: builder.Configuration.GetSection(key: "Server"));
        builder.Services.AddSingleton<IServerConfiguration, ServerConfigurationWrapper>();

        builder.Services.AddSingleton(implementationInstance: options);
        builder.Services.AddSingleton<
            IApiVersionDescriptionProvider,
            DefaultApiVersionDescriptionProvider
        >();
        builder.Services.AddSingleton<ISunsetPolicyManager, DefaultSunsetPolicyManager>();
        builder.Services.AddSingleton<NmSystem.Logging.NoMercyLoggerOptions>(implementationFactory: _ =>
            new()
            {
                MinimumLevel = LogLevel.Information,
                LogDirectory = AppFiles.LogPath,
                MaxRunFiles = 10,
                BridgeLegacyLogger = true,
                WidthProvider = static () =>
                {
                    try
                    {
                        if (!Console.IsOutputRedirected)
                        {
                            int w = Console.WindowWidth;
                            if (w > 0)
                                return w;
                        }
                    }
                    catch
                    {
                        // No attached console (piped under the launcher or run
                        // as a service): fall through to a sensible default.
                    }

                    // Redirected/headless: assume a standard width so long
                    // lines still wrap and hang under the gutter instead of the
                    // consumer terminal hard-wrapping them flush-left.
                    return
                        int.TryParse(s: Environment.GetEnvironmentVariable(variable: "COLUMNS"), result: out int cols)
                        && cols > 0
                        ? cols
                        : 120;
                },
            }
        );
        builder.Services.AddSingleton<NmSystem.Logging.NoMercyLoggerProvider>();
        builder.Services.AddSingleton(serviceType: typeof(ILogger<>), implementationType: typeof(CustomLogger<>));

        // Configure host options with reduced shutdown timeout
        builder.Services.Configure<HostOptions>(configureOptions: hostOptions =>
        {
            hostOptions.ShutdownTimeout = TimeSpan.FromSeconds(seconds: 10);
        });

        // Service integration — context-aware lifetime management
        if (options.RunAsService)
        {
            if (Software.IsWindows)
                builder.Services.AddWindowsService();
            else if (Software.IsLinux)
                builder.Services.AddSystemd();
        }

        builder.Logging.ClearProviders();

        // ClearProviders leaves the logger factory with nothing to write to, which
        // only worked because ILogger<T> is swapped for CustomLogger<T> above and
        // reaches the provider directly, never touching the factory. Everything
        // that asks the factory for a logger instead — every job base class does,
        // via LoggerFactory.CreateLogger(GetType()) — was handed a logger with no
        // providers and had its entries dropped without a trace. That silence hid
        // real encode failures: a job's Log.LogWarning in a catch went nowhere.
        // Register the provider so both paths land in the same sink.
        builder.Logging.Services.AddSingleton<ILoggerProvider>(implementationFactory: serviceProvider =>
            serviceProvider.GetRequiredService<NmSystem.Logging.NoMercyLoggerProvider>()
        );

        // The framework's own categories were silenced wholesale by ClearProviders.
        // Now that the factory can write again, keep their routine chatter out while
        // letting genuine problems (Kestrel, hosting) through.
        builder.Logging.AddFilter(category: "Microsoft", level: LogLevel.Warning);
        builder.Logging.AddFilter(category: "System", level: LogLevel.Warning);

        builder.WebHost.ConfigureKestrel(options: kestrelOptions =>
        {
            ICertificateService certificateService =
                kestrelOptions.ApplicationServices.GetRequiredService<ICertificateService>();
            certificateService.KestrelConfig(options: kestrelOptions);

            // Main server endpoints.
            // forceHttp = true during setup/auth, so we never need HTTPS to handle the
            // OAuth callback and setup UI, even when a stale cert file is present.
            foreach (IPAddress address in localAddresses)
            {
                kestrelOptions.Listen(
                    address: address,
                    port: RuntimeServerSettings.Current.InternalServerPort,
                    configure: listenOptions =>
                    {
                        if (forceHttp)
                        {
                            listenOptions.Protocols = HttpProtocols.Http1 | HttpProtocols.Http3;
                        }
                        else
                        {
                            // HTTP/1.1 + HTTP/3, no HTTP/2 — deliberate. With h2 in the ALPN
                            // set Kestrel advertises Extended CONNECT (RFC 8441,
                            // SETTINGS_ENABLE_CONNECT_PROTOCOL), so Firefox tunnels SignalR
                            // WebSockets over the single shared h2 connection; when one such
                            // stream stalls it poisons the whole connection and every hub's
                            // negotiate riding it fails with status(null) — an endless
                            // reconnect storm. Chrome never does WS-over-h2, which masked it.
                            // .NET exposes no switch to keep h2 while disabling Extended
                            // CONNECT, and h3/QUIC already supersedes h2's multiplexing, so we
                            // drop h2 and let WebSockets use the plain HTTP/1.1 upgrade. Do not
                            // re-add Http2 here without first solving Extended CONNECT.
                            listenOptions.Protocols = HttpProtocols.Http1 | HttpProtocols.Http3;
                            certificateService.ConfigureHttpsListener(listenOptions: listenOptions);
                        }
                    }
                );
            }

            // Health check endpoint — HTTP only, localhost only (for Docker HEALTHCHECK).
            // No TLS is configured here, so Http3 can never negotiate — requesting it
            // just makes Kestrel log a "HTTP/3 is not enabled" warning every startup.
            kestrelOptions.Listen(
                address: IPAddress.Loopback,
                port: RuntimeServerSettings.Current.InternalServerPort + 1,
                configure: listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1;
                }
            );

            // IPC transport — named pipe (Windows) or Unix socket (Linux/macOS).
            // Neither transport supports QUIC, so Http3 is unreachable here too.
            if (Software.IsWindows)
            {
                kestrelOptions.ListenNamedPipe(
                    pipeName: Config.ManagementPipeName,
                    configure: listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1;
                    }
                );

                Logger.App(message: $"IPC listening on named pipe: {Config.ManagementPipeName}");
            }
            else
            {
                string socketPath = Config.ManagementSocketPath;

                // Remove stale socket file from previous run
                if (File.Exists(path: socketPath))
                    File.Delete(path: socketPath);

                kestrelOptions.ListenUnixSocket(
                    socketPath: socketPath,
                    configure: listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1;
                    }
                );

                Logger.App(message: $"IPC listening on Unix socket: {socketPath}");
            }
        });

        builder.WebHost.UseQuic();
        builder.WebHost.UseSockets();

        // Set content root to executable directory when running as a service
        if (options.RunAsService)
            builder.WebHost.UseContentRoot(contentRoot: AppContext.BaseDirectory);

        // Register services from Startup.ConfigureServices
        ServiceConfiguration.ConfigureServices(services: builder.Services, configuration: builder.Configuration);
        builder.Services.AddSingleton(implementationInstance: options);

        WebApplication app = builder.Build();

        // Configure middleware from Startup.Configure
        IApiVersionDescriptionProvider provider =
            app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        ApplicationConfiguration.ConfigureApp(app: app, provider: provider);

        return app;
    }
}
