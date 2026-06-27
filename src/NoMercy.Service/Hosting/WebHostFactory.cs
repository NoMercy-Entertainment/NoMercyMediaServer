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
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Configuration;
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
        if (options.RunAsService)
        {
            if (Software.IsWindows)
                builder.Services.AddWindowsService();
            else if (Software.IsLinux)
                builder.Services.AddSystemd();
        }

        builder.Logging.ClearProviders();

        builder.WebHost.ConfigureKestrel(kestrelOptions =>
        {
            ICertificateService certificateService =
                kestrelOptions.ApplicationServices.GetRequiredService<ICertificateService>();
            certificateService.KestrelConfig(kestrelOptions);

            // Main server endpoints.
            // forceHttp = true during setup/auth, so we never need HTTPS to handle the
            // OAuth callback and setup UI, even when a stale cert file is present.
            foreach (IPAddress address in localAddresses)
            {
                kestrelOptions.Listen(
                    address,
                    RuntimeServerSettings.Current.InternalServerPort,
                    listenOptions =>
                    {
                        if (forceHttp)
                        {
                            listenOptions.Protocols = HttpProtocols.Http1;
                        }
                        else
                        {
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                            certificateService.ConfigureHttpsListener(listenOptions);
                        }
                    }
                );
            }

            // Health check endpoint — HTTP only, localhost only (for Docker HEALTHCHECK)
            kestrelOptions.Listen(
                IPAddress.Loopback,
                RuntimeServerSettings.Current.InternalServerPort + 1,
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
        if (options.RunAsService)
            builder.WebHost.UseContentRoot(AppContext.BaseDirectory);

        // Register services from Startup.ConfigureServices
        ServiceConfiguration.ConfigureServices(builder.Services, builder.Configuration);
        builder.Services.AddSingleton(options);

        WebApplication app = builder.Build();

        // Configure middleware from Startup.Configure
        IApiVersionDescriptionProvider provider =
            app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        ApplicationConfiguration.ConfigureApp(app, provider);

        return app;
    }
}
