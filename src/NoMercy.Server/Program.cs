using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using AspNetCore.Swagger.Themes;
using I18N.DotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NoMercy.Api.Constraints;
using NoMercy.Api.Controllers.Socket;
using NoMercy.Api.Middleware;
using NoMercy.Data.Jobs;
using NoMercy.Data.Logic;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers.Monitoring;
using NoMercy.MediaProcessing.Collections;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaProcessing.Movies;
using NoMercy.MediaProcessing.People;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Queue;
using NoMercy.Server.app.Helper;
using NoMercy.Server.Startup;
using NoMercy.Server.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using CollectionRepository=NoMercy.Data.Repositories.CollectionRepository;
using LibraryRepository=NoMercy.Data.Repositories.LibraryRepository;
using MovieRepository=NoMercy.Data.Repositories.MovieRepository;
using ResourceMonitor=NoMercy.Helpers.Monitoring.ResourceMonitor;

namespace NoMercy.Server;
public static class Program
{
    public static async Task Main(string[] args)
    {
        // -----------------------------------------------------------------------------------------------------------------
        // Initialization
        // -----------------------------------------------------------------------------------------------------------------
        SetExitHandling();
        StartupOptions cliOptions = new StartupOptionsParser(args).ParseAndApply();
        
        Console.Clear();
        Console.Title = "NoMercy Server";
        
        var stopWatch = Stopwatch.StartNew();
        
        Databases.QueueContext = new QueueContext();
        Databases.MediaContext = new MediaContext();
        
        await ApiInfo.RequestInfo();

        if (UserSettings.TryGetUserSettings(out Dictionary<string, string>? settings))
        {
            UserSettings.ApplySettings(settings);
        }

        // startup tasks
        #region Spawn worker Tasks
        await Task.WhenAll([
            ConsoleMessages.Logo(),
            AppFiles.CreateAppFolders(),
            Networking.Networking.Discover(),
            Auth.Init(),
            Task.Run(() => Seed.Init(cliOptions.ShouldSeedMarvel)),
            Register.Init(),
            Binaries.DownloadAll(),
            // new (AniDbBaseClient.Init),
            TrayIcon.Make(),
            StorageMonitor.UpdateStorage()
        ]);
        #endregion
        #region Spawn worker threads
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
        #endregion
        
        // -----------------------------------------------------------------------------------------------------------------
        // Builder
        // -----------------------------------------------------------------------------------------------------------------
        WebApplicationBuilder builder = WebApplication.CreateBuilder() ;

        #region WebHost
        builder.WebHost.ConfigureKestrel(Certificate.KestrelConfig)
            .UseKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Limits.MaxRequestBodySize = null;
                options.Limits.MaxRequestBufferSize = null;
                options.Limits.MaxConcurrentConnections = null;
                options.Limits.MaxConcurrentUpgradedConnections = null;
            });

        builder.WebHost.UseUrls(new UriBuilder
            {
                Host = IPAddress.Any.ToString(),
                Port = Config.InternalServerPort,
                Scheme = Uri.UriSchemeHttps
            }.ToString()
        );

        builder.WebHost.UseQuic();

        builder.WebHost.UseSockets();
        #endregion
        #region Database & Caching 
        builder.Services.AddMemoryCache();
        
        // TODO look into DbContextFactories ?
        builder.Services.AddDbContext<QueueContext>(options =>
        {
            options.UseSqlite($"Data Source={AppFiles.QueueDatabase}");
        });
        builder.Services.AddTransient<QueueContext>();

        builder.Services.AddDbContext<MediaContext>(options =>
        {
            options.UseSqlite($"Data Source={AppFiles.MediaDatabase} Pooling=True",
                contextOptions => contextOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });
        builder.Services.AddTransient<MediaContext>();
        #endregion
        #region Repositories
        builder.Services.AddScoped<EncoderRepository>();
        builder.Services.AddScoped<LibraryRepository>();
        builder.Services.AddScoped<DeviceRepository>();
        builder.Services.AddScoped<FolderRepository>();
        builder.Services.AddScoped<LanguageRepository>();
        builder.Services.AddScoped<CollectionRepository>();
        builder.Services.AddScoped<GenreRepository>();
        builder.Services.AddScoped<MovieRepository>();
        builder.Services.AddScoped<TvShowRepository>();
        builder.Services.AddScoped<SpecialRepository>();
        #endregion
        #region Managers
        // builder.Services.AddScoped<EncoderManager>();
        builder.Services.AddScoped<LibraryManager>();
        builder.Services.AddScoped<MovieManager>();
        builder.Services.AddScoped<CollectionManager>();
        builder.Services.AddScoped<ShowManager>();
        builder.Services.AddScoped<SeasonManager>();
        builder.Services.AddScoped<EpisodeManager>();
        builder.Services.AddScoped<PersonManager>();
        #endregion
        #region Localization
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.AddScoped<ILocalizer, Localizer>();
        // services.Configure<RequestLocalizationOptions>(options =>
        // {
        //     var supportedCultures = new[] {  "en-US", "nl-NL"  };
        //     options.SetDefaultCulture(supportedCultures[0])
        //         .AddSupportedCultures(supportedCultures)
        //         .AddSupportedUICultures(supportedCultures);
        // });
        #endregion
        #region Controllers
        builder.Services.AddControllers()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.Configure<RouteOptions>(options =>
        {
            options.ConstraintMap.Add("ulid", typeof(UlidRouteConstraint));
        });
        #endregion
        #region Auth
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("api", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new AssertionRequirement(context =>
                    {
                        using var mediaContext = new MediaContext();
                        User? user = mediaContext.Users.FirstOrDefault(u =>
                            u.Id == Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty));
                        return user is not null;
                    }));
                });
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = Config.AuthBaseUrl;
                options.RequireHttpsMetadata = true;
                options.Audience = "nomercy-ui";
                options.Audience = Config.TokenClientId;
                options.Events = new JwtBearerEvents
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
        #endregion
        #region Swagger
        builder.Services.AddApiVersioning(config =>
            {
                config.ReportApiVersions = true;
                config.AssumeDefaultVersionWhenUnspecified = true;
                config.DefaultApiVersion = new ApiVersion(1, 0);
                config.UnsupportedApiVersionStatusCode = 418; // Example HTTP Status Code
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "VV";
                options.SubstituteApiVersionInUrl = true;
            });

        builder.Services.AddSwaggerGen(options => options.OperationFilter<SwaggerDefaultValues>());
        builder.Services.AddSwaggerGenNewtonsoftSupport();
        builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        #endregion
        #region Cors
        builder.Services.AddCors(cors =>
        {
            cors.AddPolicy("AllowNoMercyOrigins", policy =>
            {
                policy.WithOrigins("https://nomercy.tv")
                    .WithOrigins("https://*.nomercy.tv")
                    .WithOrigins("https://hlsjs.video-dev.org")
                    .WithOrigins("http://192.168.2.201:5501")
                    .WithOrigins("http://localhost")
                    .WithOrigins("https://localhost")
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .WithHeaders("Access-Control-Allow-Private-Network", "true")
                    .AllowAnyHeader();
            });
        });
        #endregion
        #region ResponseCompression
        builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
        #endregion
        #region logging
        builder.Services.AddLogging(config =>
        {
            config.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });
        #endregion
        
        builder.Services.AddDirectoryBrowser();
        // builder.Services.AddResponseCaching();
        builder.Services.AddMvc(option => option.EnableEndpointRouting = false);
        builder.Services.AddEndpointsApiExplorer();
        
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = true;
                o.MaximumReceiveMessageSize = 1024 * 1000 * 100;
            })
            .AddNewtonsoftJsonProtocol(options => { options.PayloadSerializerSettings = JsonHelper.Settings; });

        
        builder.Services.AddSingleton<JobQueue>();
        builder.Services.AddSingleton<ResourceMonitor>();
        builder.Services.AddSingleton<Networking.Networking>();
        builder.Services.AddSingleton<StorageMonitor>();
        builder.Services.AddTransient<DynamicStaticFilesMiddleware>();
        
        // -----------------------------------------------------------------------------------------------------------------
        // Application
        // -----------------------------------------------------------------------------------------------------------------
        WebApplication app = builder.Build();
        
        string[] supportedCultures = ["en-US", "nl-NL"]; // Add other supported locales
        RequestLocalizationOptions localizationOptions = new RequestLocalizationOptions()
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

        // Development Tools
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

            // TODO how to get IApiVersionDescriptionProvider provider here?
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
            });

        // Static Files
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
            RequestPath = new PathString("/transcode"),
            ServeUnknownFileTypes = true,
            HttpsCompression = HttpsCompressionMode.Compress
        });

        app.UseDirectoryBrowser(new DirectoryBrowserOptions
        {
            FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
            RequestPath = new PathString("/transcode")
        });

        // Initialize Dynamic Static Files Middleware
        MediaContext mediaContext = new();
        List<Folder> folderLibraries = mediaContext.Folders.ToList();

        foreach (Folder folder in folderLibraries.Where(folder => Directory.Exists(folder.Path)))
            DynamicStaticFilesMiddleware.AddPath(folder.Id, folder.Path);

        // -----------------------------------------------------------------------------------------------------------------
        // Post Initialization
        // -----------------------------------------------------------------------------------------------------------------
        if (app.Services.GetService<IHostApplicationLifetime>() is { } applicationLifetime)
        {
            applicationLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(() =>
                {
                    stopWatch.Stop();

                    Task.Delay(300).Wait();

                    Logger.App($"Internal Address: {Networking.Networking.InternalAddress}");
                    Logger.App($"External Address: {Networking.Networking.ExternalAddress}");

                    ConsoleMessages.ServerRunning();

                    Logger.App($"Server started in {stopWatch.ElapsedMilliseconds}ms");
                });
            });
        }

        new Thread(() => app.RunAsync()).Start();
        new Thread(Dev.Run).Start();
    }

    /// <summary>
    /// Configures exit handling for the application by setting up event handlers for unhandled exceptions,
    /// console cancellation, and process termination signals (e.g., SIGTERM).
    /// </summary>
    private static void SetExitHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            var exception = (Exception)eventArgs.ExceptionObject;
            Logger.App("UnhandledException " + exception);
        };

        Console.CancelKeyPress += async (_, _) =>
        {
            await Shutdown();
        };

        AppDomain.CurrentDomain.ProcessExit += async (_, _) =>
        {
            Logger.App("SIGTERM received, shutting down.");
            await Shutdown();
        };
    }

    private static Task Shutdown()
    {
        Environment.Exit(0);
        return Task.CompletedTask;
    }

    // ReSharper disable once UnusedMember.Local
    private static Task Restart() => Task.CompletedTask;
}
