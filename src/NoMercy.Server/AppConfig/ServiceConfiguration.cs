using System.Security.Claims;
using System.Text.Json.Serialization;
using I18N.DotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NoMercy.Api.Constraints;
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
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
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
using NoMercy.Providers.Helpers;
using NoMercy.Queue;
using NoMercy.Queue.Extensions;
using NoMercy.Queue.Jobs;
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
        ConfigureHttpClients(services);
        ConfigureCoreServices(services);
        ConfigureLogging(services);
        ConfigureAuth(services);
        ConfigureApi(services);
        ConfigureCors(services);
        ConfigureCronJobs(services);
    }

    private static void ConfigureKestrel(IServiceCollection services)
    {
    }

    private static void ConfigureHttpClients(IServiceCollection services)
    {
        TimeSpan defaultTimeout = TimeSpan.FromMinutes(5);

        services.AddHttpClient(HttpClientNames.Tmdb, client =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.TmdbImage, client =>
        {
            client.BaseAddress = new Uri("https://image.tmdb.org/t/p/");
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "image/*");
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.Tvdb, client =>
        {
            client.BaseAddress = new Uri("https://api4.thetvdb.com/v4/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.TvdbLogin, client =>
        {
            client.BaseAddress = new Uri("https://api4.thetvdb.com/v4/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        });

        services.AddHttpClient(HttpClientNames.MusicBrainz, client =>
        {
            client.BaseAddress = new Uri("https://musicbrainz.org/ws/2/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
        });

        services.AddHttpClient(HttpClientNames.AcoustId, client =>
        {
            client.BaseAddress = new Uri("https://api.acoustid.org/v2/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        });

        services.AddHttpClient(HttpClientNames.OpenSubtitles, client =>
        {
            client.BaseAddress = new Uri("https://api.opensubtitles.org/xml-rpc");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("text/xml"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.FanArt, client =>
        {
            client.BaseAddress = new Uri("http://webservice.fanart.tv/v3/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        });

        services.AddHttpClient(HttpClientNames.FanArtImage, client =>
        {
            client.BaseAddress = new Uri("https://assets.fanart.tv");
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "image/*");
        });

        services.AddHttpClient(HttpClientNames.CoverArt, client =>
        {
            client.BaseAddress = new Uri("https://coverartarchive.org/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        });

        services.AddHttpClient(HttpClientNames.CoverArtImage, client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "image/*");
        });

        services.AddHttpClient(HttpClientNames.Lrclib, client =>
        {
            client.BaseAddress = new Uri("https://lrclib.net/api/get");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
        });

        services.AddHttpClient(HttpClientNames.MusixMatch, client =>
        {
            client.BaseAddress = new Uri("https://apic-desktop.musixmatch.com/ws/1.1/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.DefaultRequestHeaders.Add("authority", "apic-desktop.musixmatch.com");
            client.DefaultRequestHeaders.Add("cookie", "x-mxm-token-guid=");
        });

        services.AddHttpClient(HttpClientNames.Tadb, client =>
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.NoMercyImage, client =>
        {
            client.BaseAddress = new Uri("https://image.nomercy.tv/");
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "image/*");
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.KitsuIo, client =>
        {
            client.BaseAddress = new Uri("https://kitsu.io/api/edge/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);
        });

        services.AddHttpClient(HttpClientNames.General, client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.Timeout = defaultTimeout;
        });
    }

    private static void ConfigureCronJobs(IServiceCollection services)
    {
        services.AddScoped<CertificateRenewalJob>();
        services.RegisterCronJob<CertificateRenewalJob>("certificate-renewal");
        
        services.AddScoped<TvPaletteCronJob>();
        services.RegisterCronJob<TvPaletteCronJob>("tv-palette-job");
        services.AddScoped<SeasonPaletteCronJob>();
        services.RegisterCronJob<SeasonPaletteCronJob>("season-palette-job");
        services.AddScoped<EpisodePaletteCronJob>();
        services.RegisterCronJob<EpisodePaletteCronJob>("episode-palette-job");
        services.AddScoped<MoviePaletteCronJob>();
        services.RegisterCronJob<MoviePaletteCronJob>("movie-palette-job");
        services.AddScoped<CollectionPaletteCronJob>();
        services.RegisterCronJob<CollectionPaletteCronJob>("collection-palette-job");
        services.AddScoped<PersonPaletteCronJob>();
        services.RegisterCronJob<PersonPaletteCronJob>("person-palette-job");
        
        services.AddScoped<ImagePaletteCronJob>();
        services.RegisterCronJob<ImagePaletteCronJob>("image-palette-job");
        services.AddScoped<RecommendationPaletteCronJob>();
        services.RegisterCronJob<RecommendationPaletteCronJob>("recommendation-palette-job");
        services.AddScoped<SimilarPaletteCronJob>();
        services.RegisterCronJob<SimilarPaletteCronJob>("similar-palette-job");
        
        services.AddScoped<FanartArtistImagesCronJob>();
        services.RegisterCronJob<FanartArtistImagesCronJob>("fanart-images-job");
        // services.AddScoped<ArtistPaletteCronJob>();
        // services.RegisterCronJob<ArtistPaletteCronJob>("artist-palette-job");
        // services.AddScoped<AlbumPaletteCronJob>();
        // services.RegisterCronJob<AlbumPaletteCronJob>("album-palette-job");
    }
    

    private static void ConfigureCoreServices(IServiceCollection services)
    {
        // Add Memory Cache
        services.AddMemoryCache();
        services.AddCronWorker();

        // Add Singleton Services
        services.AddScoped<JobQueue>();
        
        services.AddSingleton<ResourceMonitor>();
        services.AddSingleton<Networking.Networking>();
        services.AddSingleton<StorageMonitor>();
        services.AddSingleton<ChromeCast>();
        services.AddSingleton<DriveMonitor>();

        // Add DbContexts
        services.AddDbContext<QueueContext>(optionsAction =>
        {
            optionsAction.UseSqlite($"Data Source={AppFiles.QueueDatabase} Pooling=True");
        });

        services.AddDbContext<MediaContext>(optionsAction =>
        {
            optionsAction.UseSqlite($"Data Source={AppFiles.MediaDatabase}; Pooling=True; Cache=Shared; Foreign Keys=True;",
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });

        services.AddDbContextFactory<MediaContext>(optionsAction =>
        {
            optionsAction.UseSqlite($"Data Source={AppFiles.MediaDatabase}; Pooling=True; Cache=Shared; Foreign Keys=True;",
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        }, ServiceLifetime.Scoped);

        // Add Repositories
        services.AddScoped<HomeRepository>();
        services.AddScoped<MusicRepository>();
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
        services.AddScoped<SetupService>();

        services.AddSingleton<JobQueue>();
        services.AddSingleton<JobDispatcher>();
        services.AddSingleton<MediaProcessing.Jobs.JobDispatcher>();

        services.AddVideoHubServices();
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
        services.AddLogging(logging =>
        {
            // logging.ClearProviders();
            // logging.AddFilter("Microsoft", LogLevel.Critical);
            // logging.AddFilter("System", LogLevel.Critical);
            // logging.AddFilter("Network", LogLevel.Critical);
            // logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);
            // logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Critical);
            //
            // logging.AddFilter("Microsoft.AspNetCore", LogLevel.Critical);
            // logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Critical);
            // logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Critical);
            // logging.AddFilter("Microsoft.AspNetCore.Mvc", LogLevel.Critical);
            //
            // logging.AddFilter("Microsoft.AspNetCore.HostFiltering.HostFilteringMiddleware", LogLevel.Critical);
            // logging.AddFilter("Microsoft.AspNetCore.Cors.Infrastructure.CorsMiddleware", LogLevel.Critical);
            // logging.AddFilter("Microsoft.AspNetCore.Middleware", LogLevel.Critical);
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
        ConfigureApiVersioning(services);
            
        // Add Controllers and JSON Options
        services.AddControllers(options =>
            {
                options.EnableEndpointRouting = true;
            })
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                // options.SerializerSettings.Culture = System.Globalization.CultureInfo.InvariantCulture;
                options.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                // options.JsonSerializerOptions.Encoder =
                //     System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
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
                
                // Add error logging filter for invalid method calls and wrong arguments
                o.AddFilter<HubErrorLoggingFilter>();
            })
            .AddNewtonsoftJsonProtocol(options => { options.PayloadSerializerSettings = JsonHelper.Settings; });

        services.AddResponseCompression(options => { options.EnableForHttps = true; });

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
        services.AddApiVersioning(config =>
            {
                config.ReportApiVersions = true;
                config.AssumeDefaultVersionWhenUnspecified = true;
                config.DefaultApiVersion = new(1, 0);
                config.UnsupportedApiVersionStatusCode = 418;
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
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

