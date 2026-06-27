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

using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using NoMercy.Api.Hubs;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Helpers.Extensions;
using NoMercy.Authorization;
using NoMercy.MediaProcessing.Jobs.ChangesJobs;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.CoverArt.Client;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.NoMercy.Client;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Queue.MediaServer.Jobs;
using NoMercy.Service.Configuration.Swagger;
using NoMercy.Service.Extensions;
using NoMercy.Storage;
using NoMercyQueue.Workers;

namespace NoMercy.Service.Configuration;

public static class ApplicationConfiguration
{
    public static void ConfigureApp(
        IApplicationBuilder app,
        IApiVersionDescriptionProvider provider
    )
    {
        IWebHostEnvironment env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

        HttpClientProvider.Initialize(
            app.ApplicationServices.GetRequiredService<IHttpClientFactory>()
        );
        IStorage storage = app.ApplicationServices.GetRequiredService<IStorage>();
        CacheController.Initialize(storage);
        TmdbImageClient.Initialize(storage);
        NoMercyImageClient.Initialize(storage);
        FanArtImageClient.Initialize(storage);
        CoverArtCoverArtClient.Initialize(storage);

        // Skip eager singleton resolution in the test environment — event handlers use
        // IClientMessenger which captures ConnectedClients at construction time.  During
        // integration tests the ConnectedClients instance may be replaced by the test
        // harness after service collection construction; eagerly resolving here would
        // pin the original instance and prevent test-supplied instances from being used.
        if (!env.IsEnvironment("Testing"))
            app.ApplicationServices.InitializeSignalREventHandlers();

        ConfigureLocalization(app);
        ConfigureMiddleware(app);
        ConfigureStaticFiles(app);
        ConfigureDynamicStaticFiles(app);
        ConfigureEndpoints(app);
        SwaggerConfiguration.UseSwaggerUi(app, provider);
        ConfigureCronJobs(app);
    }

    private static void ConfigureCronJobs(IApplicationBuilder app)
    {
        CronWorker cronWorker = app.ApplicationServices.GetRequiredService<CronWorker>();
        cronWorker.RegisterJobWithSchedule<CertificateRenewalCronJob>(
            "certificate-renewal",
            app.ApplicationServices
        );
        cronWorker.RegisterJobWithSchedule<ActivityLogRetentionCronJob>(
            "activity-log-retention",
            app.ApplicationServices
        );
        cronWorker.RegisterJobWithSchedule<TmdbChangesCronJob>(
            "tmdb-changes-sync",
            app.ApplicationServices
        );

        cronWorker.RegisterJobWithSchedule<DeviceDropRuleCronJob>(
            "device-drop-rule-job",
            app.ApplicationServices
        );
    }

    private static void ConfigureLocalization(IApplicationBuilder app)
    {
        string[] supportedCultures = ["en-US", "nl-NL"]; // Add other supported locales
        RequestLocalizationOptions localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture(supportedCultures[0])
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);

        localizationOptions.FallBackToParentCultures = true;
        localizationOptions.FallBackToParentUICultures = true;

        app.UseRequestLocalization(localizationOptions);
    }

    private static void ConfigureMiddleware(IApplicationBuilder app)
    {
        if (Config.IsDev)
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        app.UseMiddleware<EncoderRuntimeExceptionMiddleware>();

        if (Certificate.HasValidCertificate())
        {
            app.UseHsts();
            app.UseWhen(
                context =>
                    !context.Request.Path.StartsWithSegments("/manage")
                    && context.Connection.LocalPort
                        != RuntimeServerSettings.Current.InternalServerPort + 1,
                branch => branch.UseHttpsRedirection()
            );
        }
        app.UseResponseCompression();
        app.UseResponseCaching();

        app.UseCors("AllowNoMercyOrigins");
        app.UseRouting();

        // Serve Keycloak silent SSO check page — must be available before auth middleware.
        // The web app's Keycloak adapter loads this in a hidden iframe for token refresh.
        app.Use(
            async (context, next) =>
            {
                if (
                    context.Request.Path.Value?.EndsWith(
                        "/silent-check-sso.html",
                        StringComparison.OrdinalIgnoreCase
                    ) == true
                )
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.Headers.CacheControl = "no-store";
                    await context.Response.WriteAsync(
                        "<html><body><script>parent.postMessage(location.href, location.origin)</script></body></html>"
                    );
                    return;
                }

                await next();
            }
        );

        app.UseMiddleware<SetupModeMiddleware>();
        app.UseMiddleware<LocalizationMiddleware>();
        app.UseMiddleware<TokenParamAuthMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<HmacValidationMiddleware>();
        app.UseMiddleware<AccessLogMiddleware>();
        app.UseMiddleware<DynamicStaticFilesMiddleware>();

        app.UseWebSockets();

        app.Use(
            async (context, next) =>
            {
                if (
                    !RuntimeServerSettings.Current.Swagger
                    && (
                        context.Request.Path.StartsWithSegments("/swagger")
                        || context.Request.Path.StartsWithSegments("/index.html")
                    )
                )
                {
                    context.Response.StatusCode = StatusCodes.Status410Gone;
                    await context.Response.WriteAsync("Swagger is disabled.");
                    return;
                }

                await next();
            }
        );
    }

    private static void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            // Map API controllers
            endpoints.MapControllers();

            // Map SignalR hubs
            endpoints.MapHub<VideoHub>(
                "/videoHub",
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                }
            );

            endpoints.MapHub<MusicHub>(
                "/musicHub",
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                }
            );

            endpoints.MapHub<CastHub>(
                "/castHub",
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                }
            );

            endpoints.MapHub<DashboardHub>(
                "/dashboardHub",
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                }
            );

            endpoints.MapHub<RipperHub>(
                "/ripperHub",
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                }
            );

            endpoints.MapHub<DrivesHub>(
                "/drivesHub",
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                }
            );

            endpoints.MapHub<DeviceHub>(
                "/deviceHub",
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                }
            );

            endpoints.MapHub<LiveTranscodeHub>(
                "/liveTranscodeHub",
                options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                }
            );
        });
    }

    private static void ConfigureStaticFiles(IApplicationBuilder app)
    {
        // Folders.EmptyFolder(AppFiles.TranscodePath);

        app.UseStaticFiles(
            new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
                RequestPath = new("/transcodes"),
                ServeUnknownFileTypes = true,
                HttpsCompression = HttpsCompressionMode.Compress,
            }
        );

        app.UseDirectoryBrowser(
            new DirectoryBrowserOptions
            {
                FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
                RequestPath = new("/transcodes"),
            }
        );
    }

    private static void ConfigureDynamicStaticFiles(IApplicationBuilder app)
    {
        try
        {
            IStorageDriver storageDriver =
                app.ApplicationServices.GetRequiredService<IStorageDriver>();
            using MediaContext mediaContext = new();
            // Folders now reference a driver instance + sub-path; the
            // middleware resolves the actual backend per-request through
            // IStorageFactory, so we register every folder unconditionally
            // (DirectoryExists check would require materialising IStorage
            // here, which we don't have access to in the sync startup path).
            List<Folder> folderLibraries = mediaContext.Folders.ToList();
            foreach (Folder folder in folderLibraries)
                DynamicStaticFilesMiddleware.AddFolder(folder.Id, folder.DriverId, folder.Path);

            // Refresh the cached folder IDs so AccessLogMiddleware allows
            // requests through before the background seeder finishes.
            // Sync boundary: ConfigureDynamicStaticFiles is called from the synchronous
            // ASP.NET Core middleware pipeline (IApplicationBuilder.Use*) and cannot be
            // made async without refactoring the entire startup chain. This is startup-only,
            // before any requests are served, so blocking here is safe.
            ClaimsPrincipalExtensions
                .RefreshFolderIdsAsync(mediaContext)
                .GetAwaiter()
                .GetResult();
        }
        catch (SqliteException)
        {
            // Database not yet initialized (fresh install) — folders will be
            // registered when libraries are created after seeding completes.
        }
    }
}
