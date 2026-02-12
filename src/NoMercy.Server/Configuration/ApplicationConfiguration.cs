using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using NoMercy.Api.Controllers.Socket;
using NoMercy.Api.Hubs;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.Helpers;
using NoMercy.Server.Configuration.Swagger;
using NoMercy.Queue.MediaServer.Jobs;
using NoMercy.Queue.Workers;
using NoMercy.Server.Extensions;

namespace NoMercy.Server.Configuration;

public static class ApplicationConfiguration
{
    public static void ConfigureApp(IApplicationBuilder app, IApiVersionDescriptionProvider provider)
    {
        HttpClientProvider.Initialize(app.ApplicationServices.GetRequiredService<IHttpClientFactory>());
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
        cronWorker.RegisterJobWithSchedule<CertificateRenewalJob>("certificate-renewal", app.ApplicationServices);
        
        cronWorker.RegisterJobWithSchedule<TvPaletteCronJob>("tv-palette-job", app.ApplicationServices);
        cronWorker.RegisterJobWithSchedule<SeasonPaletteCronJob>("season-palette-job", app.ApplicationServices);
        cronWorker.RegisterJobWithSchedule<EpisodePaletteCronJob>("episode-palette-job", app.ApplicationServices);
        cronWorker.RegisterJobWithSchedule<MoviePaletteCronJob>("movie-palette-job", app.ApplicationServices);
        cronWorker.RegisterJobWithSchedule<CollectionPaletteCronJob>("collection-palette-job", app.ApplicationServices);
        cronWorker.RegisterJobWithSchedule<PersonPaletteCronJob>("person-palette-job", app.ApplicationServices);
        //
        cronWorker.RegisterJobWithSchedule<ImagePaletteCronJob>("image-palette-job", app.ApplicationServices);
        cronWorker.RegisterJobWithSchedule<RecommendationPaletteCronJob>("recommendation-palette-job", app.ApplicationServices);
        cronWorker.RegisterJobWithSchedule<SimilarPaletteCronJob>("similar-palette-job", app.ApplicationServices);
        
        cronWorker.RegisterJobWithSchedule<FanartArtistImagesCronJob>("fanart-images-job", app.ApplicationServices);
        // cronWorker.RegisterJobWithSchedule<ArtistPaletteCronJob>("artist-palette-job", app.ApplicationServices);
        // cronWorker.RegisterJobWithSchedule<AlbumPaletteCronJob>("album-palette-job", app.ApplicationServices);
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

        app.UseHsts();
        app.UseHttpsRedirection();
        app.UseResponseCompression();
        app.UseResponseCaching();

        app.UseCors("AllowNoMercyOrigins");
        app.UseRouting();

        app.UseMiddleware<LocalizationMiddleware>();
        app.UseMiddleware<TokenParamAuthMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<AccessLogMiddleware>();
        app.UseMiddleware<DynamicStaticFilesMiddleware>();

        app.UseWebSockets();

        app.Use(async (context, next) =>
        {
            if (!Config.Swagger && (context.Request.Path.StartsWithSegments("/swagger") ||
                                    context.Request.Path.StartsWithSegments("/index.html")))
            {
                context.Response.StatusCode = StatusCodes.Status410Gone;
                await context.Response.WriteAsync("Swagger is disabled.");
                return;
            }

            await next();
        });
    }

    private static void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
            {
                // Map API controllers
                endpoints.MapControllers();
                
                // Map SignalR hubs
                endpoints.MapHub<VideoHub>("/videoHub", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                });

                endpoints.MapHub<MusicHub>("/musicHub", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                });

                endpoints.MapHub<CastHub>("/castHub", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                });

                endpoints.MapHub<DashboardHub>("/dashboardHub", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                });

                endpoints.MapHub<RipperHub>("/ripperHub", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                    options.CloseOnAuthenticationExpiration = true;
                });
            });
    }

    private static void ConfigureStaticFiles(IApplicationBuilder app)
    {
        // Folders.EmptyFolder(AppFiles.TranscodePath);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
            RequestPath = new("/transcodes"),
            ServeUnknownFileTypes = true,
            HttpsCompression = HttpsCompressionMode.Compress
        });
        
        app.UseDirectoryBrowser(new DirectoryBrowserOptions
        {
            FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
            RequestPath = new("/transcodes")
        });
    }

    private static void ConfigureDynamicStaticFiles(IApplicationBuilder app)
    {
        using MediaContext mediaContext = new();
        List<Folder> folderLibraries = mediaContext.Folders.ToList();
        foreach (Folder folder in folderLibraries.Where(folder => Directory.Exists(folder.Path)))
            DynamicStaticFilesMiddleware.AddPath(folder.Id, folder.Path);
    }
}