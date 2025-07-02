using Asp.Versioning.ApiExplorer;
using AspNetCore.Swagger.Themes;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using NoMercy.Api.Controllers.Socket;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Information;
using Config = NoMercy.NmSystem.Config;

namespace NoMercy.Server.AppConfig;

public static class ApplicationConfiguration
{
    public static void ConfigureApp(IApplicationBuilder app, IApiVersionDescriptionProvider provider)
    {
        ConfigureLocalization(app);
        ConfigureMiddleware(app);
        ConfigureSwaggerUi(app, provider);
        ConfigureWebSockets(app);
        ConfigureStaticFiles(app);
        ConfigureDynamicStaticFiles(app);

        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
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
        app.UseRouting();
        app.UseCors("AllowNoMercyOrigins");

        app.UseHsts();
        app.UseHttpsRedirection();
        app.UseResponseCompression();
        app.UseRequestLocalization();

        app.UseMiddleware<LocalizationMiddleware>();
        app.UseMiddleware<TokenParamAuthMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<AccessLogMiddleware>();
        app.UseMiddleware<DynamicStaticFilesMiddleware>();

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
        app.UseDeveloperExceptionPage();
    }

    private static void ConfigureSwaggerUi(IApplicationBuilder app, IApiVersionDescriptionProvider provider)
    {
        app.UseSwagger();
        app.UseSwaggerUI(ModernStyle.Dark, options =>
        {
            options.RoutePrefix = string.Empty;
            options.DocumentTitle = "NoMercy MediaServer API";
            options.OAuthClientId(Config.TokenClientId);
            options.OAuthScopes("openid");
            options.EnablePersistAuthorization();
            options.EnableTryItOutByDefault();

            IReadOnlyList<ApiVersionDescription> descriptions = provider.ApiVersionDescriptions;
            foreach (ApiVersionDescription description in descriptions)
            {
                string url = $"/swagger/{description.GroupName}/swagger.json";
                string name = description.GroupName.ToUpperInvariant();
                options.SwaggerEndpoint(url, name);
            }
        });

        app.UseMvcWithDefaultRoute();
    }

    private static void ConfigureWebSockets(IApplicationBuilder app)
    {
        app.UseWebSockets()
            .UseEndpoints(endpoints =>
            {
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
        Folders.EmptyFolder(AppFiles.TranscodePath);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
            RequestPath = new("/transcode"),
            ServeUnknownFileTypes = true,
            HttpsCompression = HttpsCompressionMode.Compress
        });

        app.UseDirectoryBrowser(new DirectoryBrowserOptions
        {
            FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
            RequestPath = new("/transcode")
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