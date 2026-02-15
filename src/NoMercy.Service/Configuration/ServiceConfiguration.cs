using System.Security.Claims;
using I18N.DotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NoMercy.Api.Constraints;
using NoMercy.Api.Middleware;
using NoMercy.Api.Services;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
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
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Events;
using NoMercy.Events.Audit;
using NoMercy.Plugins;
using NoMercy.Queue.Extensions;
using NoMercy.Queue.MediaServer;
using NoMercy.Queue.MediaServer.Jobs;
using NoMercy.Service.Extensions;
using NoMercy.Helpers.Wallpaper;
using NoMercy.Service.Configuration.Swagger;
using NoMercy.Service.Services;
using NoMercy.Setup;
using CollectionRepository = NoMercy.Data.Repositories.CollectionRepository;
using LibraryRepository = NoMercy.Data.Repositories.LibraryRepository;
using MovieRepository = NoMercy.Data.Repositories.MovieRepository;
using MediaProcessingLibraryRepository = NoMercy.MediaProcessing.Libraries.LibraryRepository;
using MediaProcessingMovieRepository = NoMercy.MediaProcessing.Movies.MovieRepository;
using MediaProcessingCollectionRepository = NoMercy.MediaProcessing.Collections.CollectionRepository;
using MediaProcessingShowRepository = NoMercy.MediaProcessing.Shows.ShowRepository;
using MediaProcessingSeasonRepository = NoMercy.MediaProcessing.Seasons.SeasonRepository;
using MediaProcessingEpisodeRepository = NoMercy.MediaProcessing.Episodes.EpisodeRepository;
using MediaProcessingPersonRepository = NoMercy.MediaProcessing.People.PersonRepository;
using MediaProcessingFileRepository = NoMercy.MediaProcessing.Files.FileRepository;

namespace NoMercy.Service.Configuration;

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
            client.BaseAddress = new("https://api.themoviedb.org/3/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.TmdbImage, client =>
        {
            client.BaseAddress = new("https://image.tmdb.org/t/p/");
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "image/*");
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.Tvdb, client =>
        {
            client.BaseAddress = new("https://api4.thetvdb.com/v4/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.TvdbLogin, client =>
        {
            client.BaseAddress = new("https://api4.thetvdb.com/v4/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        });

        services.AddHttpClient(HttpClientNames.MusicBrainz, client =>
        {
            client.BaseAddress = new("https://musicbrainz.org/ws/2/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
        });

        services.AddHttpClient(HttpClientNames.AcoustId, client =>
        {
            client.BaseAddress = new("https://api.acoustid.org/v2/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        });

        services.AddHttpClient(HttpClientNames.OpenSubtitles, client =>
        {
            client.BaseAddress = new("https://api.opensubtitles.org/xml-rpc");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("text/xml"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.FanArt, client =>
        {
            client.BaseAddress = new("http://webservice.fanart.tv/v3/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        });

        services.AddHttpClient(HttpClientNames.FanArtImage, client =>
        {
            client.BaseAddress = new("https://assets.fanart.tv");
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "image/*");
        });

        services.AddHttpClient(HttpClientNames.CoverArt, client =>
        {
            client.BaseAddress = new("https://coverartarchive.org/");
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
            client.BaseAddress = new("https://lrclib.net/api/get");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
        });

        services.AddHttpClient(HttpClientNames.MusixMatch, client =>
        {
            client.BaseAddress = new("https://apic-desktop.musixmatch.com/ws/1.1/");
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
            client.BaseAddress = new("https://image.nomercy.tv/");
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "image/*");
            client.Timeout = defaultTimeout;
        });

        services.AddHttpClient(HttpClientNames.KitsuIo, client =>
        {
            client.BaseAddress = new("https://kitsu.io/api/edge/");
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
        services.RegisterCronJob<CertificateRenewalJob>("certificate-renewal");

        services.RegisterCronJob<TvPaletteCronJob>("tv-palette-job");
        services.RegisterCronJob<SeasonPaletteCronJob>("season-palette-job");
        services.RegisterCronJob<EpisodePaletteCronJob>("episode-palette-job");
        services.RegisterCronJob<MoviePaletteCronJob>("movie-palette-job");
        services.RegisterCronJob<CollectionPaletteCronJob>("collection-palette-job");
        services.RegisterCronJob<PersonPaletteCronJob>("person-palette-job");

        services.RegisterCronJob<ImagePaletteCronJob>("image-palette-job");
        services.RegisterCronJob<RecommendationPaletteCronJob>("recommendation-palette-job");
        services.RegisterCronJob<SimilarPaletteCronJob>("similar-palette-job");

        services.RegisterCronJob<FanartArtistImagesCronJob>("fanart-images-job");
    }
    

    private static void ConfigureCoreServices(IServiceCollection services)
    {
        // Setup state and server — singletons shared between middleware and setup flow
        services.AddSingleton<SetupState>();
        services.AddSingleton(sp => new SetupServer(sp.GetRequiredService<SetupState>()));

        // Add Memory Cache with size limit to prevent unbounded growth
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1024;
            options.CompactionPercentage = 0.25;
        });
        services.AddCronWorker();

        // Register Event Bus with audit logging and event audit trail
        InMemoryEventBus innerBus = new();
        LoggingEventBusDecorator loggingBus = new(innerBus, message => Logger.App(message, Serilog.Events.LogEventLevel.Verbose));
        EventAuditLog auditLog = new(new()
        {
            Enabled = true,
            MaxEntries = 10_000,
            CompactionPercentage = 0.25,
            ExcludedEventTypes = ["EncodingProgressEvent", "PlaybackProgressEvent"]
        });
        AuditingEventBusDecorator eventBus = new(loggingBus, auditLog);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton(auditLog);
        EventBusProvider.Configure(eventBus);

        // Add Singleton Services
        services.AddSingleton<AppProcessManager>();
        services.AddSingleton<ResourceMonitor>();
        services.AddSingleton<Networking.Networking>();
        services.AddSingleton<StorageMonitor>();
        services.AddSingleton<ChromeCast>();
        services.AddSingleton<DriveMonitor>();
        services.AddWallpaperService();

        // Add DbContexts
        services.AddDbContext<QueueContext>(optionsAction =>
        {
            optionsAction.UseSqlite($"Data Source={AppFiles.QueueDatabase}; Pooling=True;");
        });

        services.AddDbContext<MediaContext>(optionsAction =>
        {
            optionsAction.UseSqlite($"Data Source={AppFiles.MediaDatabase}; Pooling=True; Cache=Shared; Foreign Keys=True;",
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            optionsAction.AddInterceptors(new SqliteNormalizeSearchInterceptor());
        });

        services.AddDbContextFactory<MediaContext>(optionsAction =>
        {
            optionsAction.UseSqlite($"Data Source={AppFiles.MediaDatabase}; Pooling=True; Cache=Shared; Foreign Keys=True;",
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            optionsAction.AddInterceptors(new SqliteNormalizeSearchInterceptor());
        }, ServiceLifetime.Scoped);

        // Add Repositories
        services.AddScoped<HomeRepository>();
        services.AddScoped<MusicRepository>();
        services.AddScoped<EncoderRepository>();
        services.AddScoped<LibraryRepository>();
        services.AddScoped<MediaProcessingLibraryRepository>();
        services.AddScoped<DeviceRepository>();
        services.AddScoped<FolderRepository>();
        services.AddScoped<MediaProcessingFileRepository>();
        services.AddScoped<IFileRepository, MediaProcessingFileRepository>();
        services.AddScoped<LanguageRepository>();
        services.AddScoped<CollectionRepository>();
        services.AddScoped<MediaProcessingCollectionRepository>();
        services.AddScoped<ICollectionRepository, MediaProcessingCollectionRepository>();
        services.AddScoped<GenreRepository>();
        services.AddScoped<MovieRepository>();
        services.AddScoped<MediaProcessingMovieRepository>();
        services.AddScoped<IMovieRepository, MediaProcessingMovieRepository>();
        services.AddScoped<TvShowRepository>();
        services.AddScoped<MediaProcessingShowRepository>();
        services.AddScoped<IShowRepository, MediaProcessingShowRepository>();
        services.AddScoped<MediaProcessingSeasonRepository>();
        services.AddScoped<ISeasonRepository, MediaProcessingSeasonRepository>();
        services.AddScoped<MediaProcessingEpisodeRepository>();
        services.AddScoped<IEpisodeRepository, MediaProcessingEpisodeRepository>();
        services.AddScoped<MediaProcessingPersonRepository>();
        services.AddScoped<IPersonRepository, MediaProcessingPersonRepository>();
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

        services.AddMediaServerQueue();
        services.AddSingleton<MediaProcessing.Jobs.JobDispatcher>();

        services.AddPluginSystem(AppFiles.PluginsPath);

        services.AddVideoHubServices();
        services.AddMusicHubServices();
        services.AddSignalREventHandlers();

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
            // Logging filters are handled by CustomLogger's message filtering
            // since it replaces ILogger<T> and bypasses the built-in filter pipeline
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

        // Eagerly load cached signing key so it's available before auth init completes
        OfflineJwksCache.LoadCachedPublicKey();

        // Configure Authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = Config.AuthBaseUrl;
                options.RequireHttpsMetadata = true;
                options.Audience = Config.TokenClientId;

                // Enable offline token validation via cached signing keys
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.ValidIssuer = Config.AuthBaseUrl;
                options.TokenValidationParameters.IssuerSigningKeyResolver =
                    (token, securityToken, kid, parameters) =>
                    {
                        // When OIDC metadata fetch fails, the default key resolver returns nothing.
                        // Fall back to cached public key for offline validation.
                        RsaSecurityKey? cachedKey = OfflineJwksCache.CachedSigningKey;
                        if (cachedKey is not null)
                            return [cachedKey];

                        return [];
                    };

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
                options.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
                options.SerializerSettings.Converters.Add(new StringEnumConverter());
            });

        services.Configure<RouteOptions>(options =>
        {
            options.ConstraintMap.Add("ulid", typeof(UlidRouteConstraint));
        });

        // Add Other Services
        services.AddDirectoryBrowser();
        services.AddResponseCaching();
        services.AddMvc(option => option.EnableEndpointRouting = false);
        services.AddEndpointsApiExplorer();

        services.AddHttpContextAccessor();
        services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = Config.IsDev;
                o.MaximumReceiveMessageSize = 2 * 1024 * 1024; // 2MB — realistic max is ~1MB for large playlists

                o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                o.KeepAliveInterval = TimeSpan.FromSeconds(15);
                
                // Add error logging filter for invalid method calls and wrong arguments
                o.AddFilter<HubErrorLoggingFilter>();
            })
            .AddNewtonsoftJsonProtocol(options => { options.PayloadSerializerSettings = JsonHelper.Settings; });

        services.AddResponseCompression(options => { options.EnableForHttps = true; });

        SwaggerConfiguration.AddSwagger(services);
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
                    List<string> origins =
                    [
                        "https://nomercy.tv",
                        "https://*.nomercy.tv",
                        "https://cast.nomercy.tv",
                        "https://hlsjs.video-dev.org",
                        "http://localhost:7625"
                    ];

                    if (Config.IsDev)
                    {
                        origins.Add("http://192.168.2.201:5501");
                        origins.Add("http://192.168.2.201:5502");
                        origins.Add("http://192.168.2.201:5503");
                        origins.Add("http://localhost");
                        origins.Add("https://localhost");
                    }

                    builder
                        .WithOrigins(origins.ToArray())
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

