using Asp.Versioning.ApiExplorer;
using AspNetCore.Swagger.Themes;
using I18N.DotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NoMercy.Api.Constraints;
using NoMercy.Api.Controllers.Socket;
using NoMercy.Api.Controllers.V1.Media;
using NoMercy.Api.Middleware;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;
using NoMercy.Database;
using NoMercy.MediaProcessing.Collections;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaProcessing.Movies;
using NoMercy.MediaProcessing.People;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
using NoMercy.NmSystem;
using NoMercy.Queue;
using NoMercy.Server.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Security.Claims;
using System.Text.Json.Serialization;
using NoMercy.Helpers.Monitoring;
using NoMercy.MediaProcessing.Files;
using NoMercy.Networking;
using CollectionRepository = NoMercy.Data.Repositories.CollectionRepository;
using LibraryRepository = NoMercy.Data.Repositories.LibraryRepository;
using MovieRepository = NoMercy.Data.Repositories.MovieRepository;

namespace NoMercy.Server;

public class Startup(IApiVersionDescriptionProvider provider)
{
    private static readonly MediaContext MediaContext = new();
    private static readonly List<Folder> FolderLibraries = MediaContext.Folders.ToList();
    
    public void ConfigureServices(IServiceCollection services)
    {
        // Add Memory Cache
        services.AddMemoryCache();

        // Add Singleton Services
        services.AddSingleton<JobQueue>();
        services.AddSingleton<Helpers.Monitoring.ResourceMonitor>();
        services.AddSingleton<Networking.Networking>();
        services.AddSingleton<StorageMonitor>();
        services.AddSingleton<ChromeCast>();

        // Add DbContexts
        services.AddDbContext<QueueContext>(optionsAction =>
        {
            optionsAction.UseSqlite($"Data Source={AppFiles.QueueDatabase}");
        });
        services.AddTransient<QueueContext>();

        services.AddDbContext<MediaContext>(optionsAction =>
        {
            optionsAction.UseSqlite($"Data Source={AppFiles.MediaDatabase} Pooling=True",
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });
        services.AddTransient<MediaContext>();

        // Add Repositories
        services.AddScoped<EncoderRepository>();
        services.AddScoped<LibraryRepository>();
        services.AddScoped<DeviceRepository>();
        services.AddScoped<FolderRepository>();
        services.AddScoped<LanguageRepository>();
        services.AddScoped<CollectionRepository>();
        services.AddScoped<GenreRepository>();
        services.AddScoped<MovieRepository>();
        services.AddScoped<TvShowRepository>();
        services.AddScoped<SpecialRepository>();

        // Add Managers
        // services.AddScoped<EncoderManager>();
        services.AddScoped<LibraryManager>();
        services.AddScoped<MovieManager>();
        services.AddScoped<CollectionManager>();
        services.AddScoped<ShowManager>();
        services.AddScoped<SeasonManager>();
        services.AddScoped<EpisodeManager>();
        services.AddScoped<PersonManager>();

        services.AddScoped<HomeController>();

        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddScoped<ILocalizer, Localizer>();

        // Add Controllers and JSON Options
        services.AddControllers()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.Configure<RouteOptions>(options =>
        {
            options.ConstraintMap.Add("ulid", typeof(UlidRouteConstraint));
        });

        // Configure Logging
        services.AddLogging(builder =>
        {
            builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });

        // Configure Authorization
        services.AddAuthorizationBuilder()
            .AddPolicy("api", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme);
                policy.RequireClaim("scope", "openid", "profile");
                policy.AddRequirements(new AssertionRequirement(context =>
                {
                    using MediaContext mediaContext = new();
                    User? user = mediaContext.Users
                        .FirstOrDefault(user =>
                            user.Id == Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                                  string.Empty));
                    return user is not null;
                }));
            });

        // Configure Authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = Config.AuthBaseUrl;
                options.RequireHttpsMetadata = true;
                options.Audience = "nomercy-ui";
                options.Audience = Config.TokenClientId;
                options.Events = new()
                {
                    OnMessageReceived = context =>
                    {
                        StringValues accessToken = context.Request.Query["access_token"];
                        string[] result = accessToken.ToString().Split('&');

                        if (result.Length > 0 && !string.IsNullOrEmpty(result[0])) context.Token = result[0];

                        return Task.CompletedTask;
                    }
                };
            });

        // Add Other Services
        services.AddCors();
        services.AddDirectoryBrowser();
        // services.AddResponseCaching();
        services.AddMvc(option => option.EnableEndpointRouting = false);
        services.AddEndpointsApiExplorer();

        // Add API versioning
        services.AddApiVersioning(config =>
            {
                config.ReportApiVersions = true;
                config.AssumeDefaultVersionWhenUnspecified = true;
                config.DefaultApiVersion = new(1, 0);
                config.UnsupportedApiVersionStatusCode = 418;
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "VV";
                options.SubstituteApiVersionInUrl = true;
                options.DefaultApiVersion = new(1, 0);
            });

        // Add Swagger
        services.AddSwaggerGen(options => { options.OperationFilter<SwaggerDefaultValues>(); });
        services.AddSwaggerGenNewtonsoftSupport();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        services.AddHttpContextAccessor();
        services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = true;
                o.MaximumReceiveMessageSize = 1024 * 1000 * 100;
            })
            .AddNewtonsoftJsonProtocol(options => { options.PayloadSerializerSettings = JsonHelper.Settings; });

        // Configure CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowNoMercyOrigins",
                builder =>
                {
                    builder
                        .WithOrigins("https://nomercy.tv")
                        .WithOrigins("https://*.nomercy.tv")
                        .WithOrigins("https://cast.nomercy.tv")
                        .WithOrigins("https://hlsjs.video-dev.org")
                        .WithOrigins("http://192.168.2.201:5501")
                        .WithOrigins("http://192.168.2.201:5502")
                        .WithOrigins("http://localhost")
                        .WithOrigins("https://localhost")
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .WithHeaders("Access-Control-Allow-Private-Network", "true")
                        .WithHeaders("Access-Control-Allow-Headers", "*")
                        .AllowAnyHeader();
                });
        });

        services.AddResponseCompression(options => { options.EnableForHttps = true; });

        services.AddTransient<DynamicStaticFilesMiddleware>();

        // services.AddSingleton(LibraryFileWatcher.Instance);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        string[] supportedCultures = ["en-US", "nl-NL"]; // Add other supported locales
        RequestLocalizationOptions? localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture(supportedCultures[0])
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);

        localizationOptions.FallBackToParentCultures = true;
        localizationOptions.FallBackToParentUICultures = true;

        app.UseRequestLocalization(localizationOptions);

        app.UseRouting();
        app.UseCors("AllowNoMercyOrigins");

        // Security Middleware
        app.UseHsts();
        app.UseHttpsRedirection();

        // Performance Middleware
        app.UseResponseCompression();
        app.UseRequestLocalization();
        // app.UseResponseCaching();

        // Custom Middleware
        app.UseMiddleware<LocalizationMiddleware>();
        app.UseMiddleware<TokenParamAuthMiddleware>();

        // Authentication and Authorization
        app.UseAuthentication();
        app.UseAuthorization();

        // Logging Middleware
        app.UseMiddleware<AccessLogMiddleware>();

        // Static Files Middleware
        app.UseMiddleware<DynamicStaticFilesMiddleware>();
        
        // Swagger Middleware
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

        // MVC
        app.UseMvcWithDefaultRoute();

        // WebSockets
        app.UseWebSockets()
            .UseEndpoints(endpoints =>
            {
                endpoints.MapHub<VideoHub>("/socket", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.CloseOnAuthenticationExpiration = true;
                });

                endpoints.MapHub<DashboardHub>("/dashboardHub", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.CloseOnAuthenticationExpiration = true;
                });

                endpoints.MapHub<CastHub>("/castHub", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.CloseOnAuthenticationExpiration = true;
                });
            });

        // Static Files
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

        foreach (Folder folder in FolderLibraries.Where(folder => Directory.Exists(folder.Path)))
            DynamicStaticFilesMiddleware.AddPath(folder.Id, folder.Path);

    }
}
