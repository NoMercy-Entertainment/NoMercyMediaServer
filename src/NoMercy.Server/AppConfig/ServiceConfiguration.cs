using System.Security.Claims;
using System.Text.Json.Serialization;
using I18N.DotNet;
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
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Helpers.Monitoring;
using NoMercy.MediaProcessing.Collections;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaProcessing.Movies;
using NoMercy.MediaProcessing.People;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
using NoMercy.MediaSources.OpticalMedia;
using NoMercy.Networking;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue;
using NoMercy.Server.services;
using NoMercy.Server.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using CollectionRepository = NoMercy.Data.Repositories.CollectionRepository;
using LibraryRepository = NoMercy.Data.Repositories.LibraryRepository;
using MovieRepository = NoMercy.Data.Repositories.MovieRepository;

namespace NoMercy.Server.AppConfig;

public static class ServiceConfiguration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        ConfigureKestrel(services);
        ConfigureCoreServices(services);
        ConfigureLogging(services);
        ConfigureAuth(services);
        ConfigureApi(services);
        ConfigureCors(services);
    }

    private static void ConfigureKestrel(IServiceCollection services)
    {
    }

    private static void ConfigureCoreServices(IServiceCollection services)
    {
        // Add Memory Cache
        services.AddMemoryCache();

        // Add Singleton Services
        // services.AddSingleton<JobQueue>();
        services.AddScoped<JobQueue>();
        services.AddSingleton<ResourceMonitor>();
        services.AddSingleton<Networking.Networking>();
        services.AddSingleton<StorageMonitor>();
        services.AddSingleton<ChromeCast>();
        services.AddSingleton<DriveMonitor>();

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
        services.AddScoped<FileRepository>();
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

        services.AddScoped<HomeService>();
        services.AddScoped<HomeController>();

        services.AddScoped<JobDispatcher>();

        services.AddMusicHubServices();

        services.AddHostedService<ServerRegistrationService>(_ =>
        {
            ServerRegistrationService service = new();
            return service;
        });

        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddScoped<ILocalizer, Localizer>();
    }

    private static void ConfigureLogging(IServiceCollection services)
    {
        // Configure Logging
        services.AddLogging(options =>
        {
            options.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });
    }

    private static void ConfigureAuth(IServiceCollection services)
    {
        // Configure Authorization
        services.AddAuthorizationBuilder()
            .AddPolicy("api", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme);
                policy.RequireClaim("scope", "openid", "profile");
                policy.AddRequirements(new AssertionRequirement(context =>
                {
                    User? user = ClaimsPrincipleExtensions.Users
                        .FirstOrDefault(user =>
                            user.Id == Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                                  string.Empty));

                    Logger.App($"User: {user?.Name ?? "Unknown"}");
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
    }

    private static void ConfigureApi(IServiceCollection services)
    {
        // Add Controllers and JSON Options
        services.AddControllers(options =>
            {
                options.EnableEndpointRouting = true; // This is the default, but explicit for clarity
            })
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

        // Add Other Services
        services.AddDirectoryBrowser();
        // services.AddResponseCaching();
        services.AddMvc(option => option.EnableEndpointRouting = false);
        services.AddEndpointsApiExplorer();

        services.AddHttpContextAccessor();
        services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = true;
                o.MaximumReceiveMessageSize = 1024 * 1000 * 100;

                o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                o.KeepAliveInterval = TimeSpan.FromSeconds(15);
            })
            .AddNewtonsoftJsonProtocol(options => { options.PayloadSerializerSettings = JsonHelper.Settings; });

        services.AddResponseCompression(options => { options.EnableForHttps = true; });

        // services.AddTransient<DynamicStaticFilesMiddleware>();
        // services.AddSingleton(LibraryFileWatcher.Instance);
        
        ConfigureApiVersioning(services);
        ConfigureSwagger(services);
    }

    private static void ConfigureSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(options => options.OperationFilter<SwaggerDefaultValues>());
        services.AddSwaggerGenNewtonsoftSupport();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    }
    
    private static void ConfigureApiVersioning(IServiceCollection services)
    {
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
    }

    
    private static void ConfigureCors(IServiceCollection services)
    {
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
                        .WithOrigins("http://192.168.2.201:5503")
                        .WithOrigins("http://localhost")
                        .WithOrigins("http://localhost:7625")
                        .WithOrigins("https://localhost")
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .WithHeaders("Access-Control-Allow-Private-Network", "true")
                        .WithHeaders("Access-Control-Allow-Headers", "*")
                        .AllowAnyHeader();
                });
        });
    }
}