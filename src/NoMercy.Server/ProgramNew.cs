using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using I18N.DotNet;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NoMercy.Api.Constraints;
using NoMercy.Api.Controllers.V1.Media;
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

namespace NoMercy.Server;
public static class ProgramNew
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
        IWebHostBuilder builder = WebHost.CreateDefaultBuilder();

        builder.ConfigureKestrel(Certificate.KestrelConfig)
            .UseKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Limits.MaxRequestBodySize = null;
                options.Limits.MaxRequestBufferSize = null;
                options.Limits.MaxConcurrentConnections = null;
                options.Limits.MaxConcurrentUpgradedConnections = null;
            });

        builder.UseUrls(new UriBuilder
            {
                Host = IPAddress.Any.ToString(),
                Port = Config.InternalServerPort,
                Scheme = Uri.UriSchemeHttps
            }.ToString()
        );

        builder.UseQuic();

        builder.UseSockets();

        // TODO split ConfigureServices into a proper builder pattern
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IApiVersionDescriptionProvider, DefaultApiVersionDescriptionProvider>();
            services.AddSingleton<ISunsetPolicyManager, DefaultSunsetPolicyManager>();
            
            // Add Memory Cache
            services.AddMemoryCache();

            // Add Singleton Services
            services.AddSingleton<JobQueue>();
            services.AddSingleton<ResourceMonitor>();
            services.AddSingleton<Networking.Networking>();
            services.AddSingleton<StorageMonitor>();

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
            // services.Configure<RequestLocalizationOptions>(options =>
            // {
            //     var supportedCultures = new[] {  "en-US", "nl-NL"  };
            //     options.SetDefaultCulture(supportedCultures[0])
            //         .AddSupportedCultures(supportedCultures)
            //         .AddSupportedUICultures(supportedCultures);
            // });

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
            services.AddLogging(bld =>
            {
                bld.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
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
                    config.DefaultApiVersion = new ApiVersion(1, 0);
                    config.UnsupportedApiVersionStatusCode = 418;
                })
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "VV";
                    options.SubstituteApiVersionInUrl = true;
                    options.DefaultApiVersion = new ApiVersion(1, 0);
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

            services.AddCors(options =>
            {
                options.AddPolicy("AllowNoMercyOrigins",
                    builder =>
                    {
                        builder
                            .WithOrigins("https://nomercy.tv")
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

            services.AddResponseCompression(options => { options.EnableForHttps = true; });

            services.AddTransient<DynamicStaticFilesMiddleware>();
        });
        
        // -----------------------------------------------------------------------------------------------------------------
        // Application
        // -----------------------------------------------------------------------------------------------------------------
        IApplicationBuilder app = builder.Build();
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

    private static Task Restart() => Task.CompletedTask;
}
