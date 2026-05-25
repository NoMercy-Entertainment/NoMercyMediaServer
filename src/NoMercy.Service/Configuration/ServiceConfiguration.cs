using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using I18N.DotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NoMercy.Api.Constraints;
using NoMercy.Api.Hubs;
using NoMercy.Api.Middleware;
using NoMercy.Api.Services;
using NoMercy.Api.WebSockets;
using NoMercy.Data.Activity;
using NoMercy.Data.Repositories;
using NoMercy.Data.Resolvers;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Startup;
using NoMercy.Events;
using NoMercy.Events.Audit;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.Helpers.Monitoring;
using NoMercy.Helpers.Wallpaper;
using NoMercy.MediaProcessing.Collections;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.EventHandlers;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaProcessing.Movies;
using NoMercy.MediaProcessing.People;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
using NoMercy.MediaProcessing.Subtitles;
using NoMercy.Networking;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Connectivity;
using NoMercy.Networking.Connectivity.Strategies;
using NoMercy.Networking.Devices;
using NoMercy.Networking.Discovery;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.OpticalMedia.Composition;
using NoMercy.Plugins;
using NoMercy.Providers.Helpers;
using NoMercy.Queue.MediaServer;
using NoMercy.Queue.MediaServer.Jobs;
using NoMercy.Service.Configuration.Swagger;
using NoMercy.Service.Extensions;
using NoMercy.Service.Workers;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Cast;
using NoMercy.Setup.Server;
using NoMercy.Setup.Ui;
using NoMercy.Storage;
using NoMercyQueue.Extensions;
using Serilog.Events;
using CollectionRepository = NoMercy.Data.Repositories.CollectionRepository;
using DatabaseActivity = NoMercy.Database.Activity;
using LibraryRepository = NoMercy.Data.Repositories.LibraryRepository;
using MediaProcessingCollectionRepository = NoMercy.MediaProcessing.Collections.CollectionRepository;
using MediaProcessingEpisodeRepository = NoMercy.MediaProcessing.Episodes.EpisodeRepository;
using MediaProcessingFileRepository = NoMercy.MediaProcessing.Files.FileRepository;
using MediaProcessingLibraryRepository = NoMercy.MediaProcessing.Libraries.LibraryRepository;
using MediaProcessingMovieRepository = NoMercy.MediaProcessing.Movies.MovieRepository;
using MediaProcessingPersonRepository = NoMercy.MediaProcessing.People.PersonRepository;
using MediaProcessingSeasonRepository = NoMercy.MediaProcessing.Seasons.SeasonRepository;
using MediaProcessingShowRepository = NoMercy.MediaProcessing.Shows.ShowRepository;
using MovieRepository = NoMercy.Data.Repositories.MovieRepository;
using ResourceMonitor = NoMercy.Helpers.Monitoring.ResourceMonitor;

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

    private static void ConfigureKestrel(IServiceCollection services) { }

    private static void ConfigureHttpClients(IServiceCollection services)
    {
        TimeSpan defaultTimeout = TimeSpan.FromMinutes(5);

        services.AddHttpClient(
            HttpClientNames.Tmdb,
            client =>
            {
                client.BaseAddress = new("https://api.themoviedb.org/3/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.TmdbImage,
            client =>
            {
                client.BaseAddress = new("https://image.tmdb.org/t/p/");
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "image/*");
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.Tvdb,
            client =>
            {
                client.BaseAddress = new("https://api4.thetvdb.com/v4/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.TvdbLogin,
            client =>
            {
                client.BaseAddress = new("https://api4.thetvdb.com/v4/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.MusicBrainz,
            client =>
            {
                client.BaseAddress = new("https://musicbrainz.org/ws/2/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                // MusicBrainz puts anonymous UAs in a 50 req/s global shared
                // bucket — fine for our bursty low-traffic use. Deliberate
                // choice for privacy over the per-IP identified tier.
                client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
            }
        );

        services.AddHttpClient(
            HttpClientNames.AcoustId,
            client =>
            {
                client.BaseAddress = new("https://api.acoustid.org/v2/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.OpenSubtitles,
            client =>
            {
                client.BaseAddress = new("https://api.opensubtitles.org/xml-rpc");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("text/xml"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.OpenSubtitlesDownload,
            client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.FanArt,
            client =>
            {
                client.BaseAddress = new("https://webservice.fanart.tv/v3/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.FanArtImage,
            client =>
            {
                client.BaseAddress = new("https://assets.fanart.tv");
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "image/*");
            }
        );

        services.AddHttpClient(
            HttpClientNames.CoverArt,
            client =>
            {
                client.BaseAddress = new("https://coverartarchive.org/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.CoverArtImage,
            client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "image/*");
            }
        );

        services.AddHttpClient(
            HttpClientNames.Lrclib,
            client =>
            {
                client.BaseAddress = new("https://lrclib.net/api/get");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", "anonymous");
            }
        );

        services.AddHttpClient(
            HttpClientNames.MusixMatch,
            client =>
            {
                client.BaseAddress = new("https://apic-desktop.musixmatch.com/ws/1.1/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("authority", "apic-desktop.musixmatch.com");
                client.DefaultRequestHeaders.Add("cookie", "x-mxm-token-guid=");
            }
        );

        services.AddHttpClient(
            HttpClientNames.Tadb,
            client =>
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.NoMercyImage,
            client =>
            {
                client.BaseAddress = new("https://image.nomercy.tv/");
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "image/*");
                client.Timeout = defaultTimeout;
            }
        );

        services.AddHttpClient(
            HttpClientNames.KitsuIo,
            client =>
            {
                client.BaseAddress = new("https://kitsu.io/api/edge/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);
            }
        );

        services.AddHttpClient(
            HttpClientNames.General,
            client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
                client.Timeout = defaultTimeout;
            }
        );
    }

    private static void ConfigureCronJobs(IServiceCollection services)
    {
        services.RegisterCronJob<CertificateRenewalCronJob>("certificate-renewal");
        services.RegisterCronJob<ActivityLogRetentionCronJob>("activity-log-retention");

        services.RegisterCronJob<ShowPaletteCronJob>("show-palette-job");
        services.RegisterCronJob<SeasonPaletteCronJob>("season-palette-job");
        services.RegisterCronJob<EpisodePaletteCronJob>("episode-palette-job");
        services.RegisterCronJob<MoviePaletteCronJob>("movie-palette-job");
        services.RegisterCronJob<CollectionPaletteCronJob>("collection-palette-job");
        services.RegisterCronJob<PersonPaletteCronJob>("person-palette-job");

        services.RegisterCronJob<ImagePaletteCronJob>("image-palette-job");
        services.RegisterCronJob<RecommendationPaletteCronJob>("recommendation-palette-job");
        services.RegisterCronJob<SimilarPaletteCronJob>("similar-palette-job");

        services.RegisterCronJob<ArtistFanartCronJob>("artist-fanart-job");
        services.RegisterCronJob<ArtistPaletteCronJob>("artist-palette-job");
        services.RegisterCronJob<AlbumPaletteCronJob>("album-palette-job");

        // TODO: Remove after all palettes are regenerated with the new Median Cut algorithm
        services.RegisterCronJob<ReprocessAllPalettesCronJob>("reprocess-all-palettes-job");

        services.RegisterCronJob<DeviceDropRuleCronJob>("device-drop-rule-job");
    }

    private static void ConfigureCoreServices(IServiceCollection services)
    {
        services
            .AddDataProtection()
            .PersistKeysToFileSystem(new(AppFiles.DataProtectionKeysDir))
            .SetApplicationName("NoMercyMediaServer");

        // Setup state and services — singletons shared between middleware and setup flow
        services.AddSingleton<SetupState>();
        services.AddSingleton<AuthManager>(sp =>
        {
            // AuthManager is a long-lived singleton that needs a persistent AppDbContext.
            // A dedicated scope is created here so the context lives for the server lifetime
            // rather than being captured from a request scope (which would be disposed early).
            IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            IServiceScope authScope = scopeFactory.CreateScope();
            AppDbContext authDbContext =
                authScope.ServiceProvider.GetRequiredService<AppDbContext>();
            IStorageDriver storageDriver = sp.GetRequiredService<IStorageDriver>();
            return new(authDbContext, storageDriver);
        });
        services.AddSingleton<SetupEndpoints>();
        services.AddSingleton<BootOrchestrator>();
        services.AddSingleton<CastSessionTokenService>();
        // Route every container to the same tracker. The Service rebuilds its host
        // on the HTTPS restart and on port-conflict retry; a per-container singleton
        // would mean queue workers in the live host wait on a tracker that the static
        // MarkComplete callers (BootOrchestrator, Setup.Start) never reached.
        services.AddSingleton<NoMercy.NmSystem.Lifecycle.IServerPhaseTracker>(sp =>
            NoMercy.NmSystem.Lifecycle.ServerPhaseTracker.Shared(
                sp.GetService<Microsoft.Extensions.Logging.ILogger<NoMercy.NmSystem.Lifecycle.ServerPhaseTracker>>()
            )
        );

        services.AddScoped<NoMercy.Encoder.Profiles.BuiltinPresetSeeder>();

        // Add Memory Cache with size limit to prevent unbounded growth
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1024;
            options.CompactionPercentage = 0.25;
        });
        services.AddCronWorker();

        // Register Event Bus with audit logging and event audit trail
        InMemoryEventBus innerBus = new();
        LoggingEventBusDecorator loggingBus = new(
            innerBus,
            message => Logger.App(message, LogEventLevel.Verbose),
            // High-frequency progress events would otherwise spam the verbose
            // log every ~500ms during an encode without adding signal.
            excludedEventTypes:
            [
                "EncoderProgressBroadcastEvent",
                "EncodingProgressEvent",
                "PlaybackProgressEvent",
            ]
        );
        EventAuditLog auditLog = new(
            new()
            {
                Enabled = true,
                MaxEntries = 10_000,
                CompactionPercentage = 0.25,
                ExcludedEventTypes = ["EncodingProgressEvent", "PlaybackProgressEvent"],
            }
        );
        AuditingEventBusDecorator eventBus = new(loggingBus, auditLog);
        services.AddSingleton<IEventBus>(eventBus);
        services.AddSingleton(auditLog);
        EventBusProvider.Configure(eventBus);

        // Add Singleton Services
        services.AddSingleton<AppProcessManager>();
        services.AddSingleton<NoMercy.Helpers.Monitoring.ResourceMonitor>();

        // Network discovery (replaces static Networking.Networking IP/address members)
        services.AddSingleton<INetworkDiscovery>(sp =>
        {
            IStorageDriver storageDriver = sp.GetRequiredService<IStorageDriver>();
            NetworkDiscovery discovery = new(storageDriver);
            if (!string.IsNullOrEmpty(StartupOptions.OverrideInternalIp))
                discovery.InternalIp = StartupOptions.OverrideInternalIp;
            if (!string.IsNullOrEmpty(StartupOptions.OverrideExternalIp))
                discovery.ExternalIp = StartupOptions.OverrideExternalIp;
            Start.NetworkDiscovery = discovery;
            Register.Discovery = discovery;
            ChromeCast.NetworkDiscovery = discovery;
            return discovery;
        });

        // Client messaging (replaces static Networking.Networking.SendTo/SendToAll)
        services.AddSingleton<ConnectedClients>();
        services.AddSingleton<IClientMessenger, ClientMessenger>();

        // Connectivity strategies (ordered by priority)
        services.AddSingleton<IConnectivityStrategy>(sp => new PortForwardStrategy(
            (NetworkDiscovery)sp.GetRequiredService<INetworkDiscovery>()
        ));
        services.AddSingleton<IConnectivityStrategy, StunHolePunchStrategy>();
        services.AddSingleton<IConnectivityStrategy>(sp => new CloudflareTunnelStrategy(
            Register.GetTunnelAvailability
        ));

        // Connectivity manager (replaces ServerRegistrationService + CloudflareTunnelService)
        services.AddSingleton<IConnectivityManager, ConnectivityManager>();
        services.AddHostedService(sp =>
            (ConnectivityManager)sp.GetRequiredService<IConnectivityManager>()
        );

        // Network change monitor
        services.AddSingleton<NetworkChangeMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<NetworkChangeMonitor>());

        // mDNS LAN device scanner
        services.AddSingleton<MdnsDeviceScanner>();
        services.AddHostedService<MdnsDeviceScannerHostedService>();
        services.AddSingleton<DeviceBusRegistry>();
        services.AddSingleton<IDeviceListChangeNotifier>(sp =>
            sp.GetRequiredService<DeviceBusRegistry>()
        );

        services.AddSingleton<StorageMonitor>();
        services.AddSingleton<ChromeCast>();

        // Optical-disc detection + scanning + ripping (NoMercy.OpticalMedia)
        services.AddNoMercyOpticalMedia();
        services.AddHostedService<DriveMonitorWorker>();

        services.AddWallpaperService();

        // Add DbContexts
        services.AddDbContext<AppDbContext>(
            options => options.UseSqlite($"Data Source={AppFiles.AppDatabase}; Foreign Keys=True;"),
            optionsLifetime: ServiceLifetime.Singleton
        );

        // DbDriverFingerprintStore is a singleton — it needs the factory form so
        // each save/load gets a fresh disposable AppDbContext rather than sharing
        // a singleton-scoped tracker.
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={AppFiles.AppDatabase}; Foreign Keys=True;")
        );

        services.AddDbContext<QueueContext>(optionsAction =>
        {
            optionsAction.UseSqlite($"Data Source={AppFiles.QueueDatabase}; Pooling=True;");
        });

        // optionsLifetime: Singleton so the Singleton IDbContextFactory below
        // can consume DbContextOptions without lifetime-validation errors.
        // The DbContext itself stays Scoped (default) for per-request use.
        services.AddDbContext<MediaContext>(
            optionsAction =>
            {
                optionsAction.UseSqlite(
                    $"Data Source={AppFiles.MediaDatabase}; Pooling=True; Foreign Keys=True;",
                    o =>
                    {
                        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        o.ExecutionStrategy(deps => new SqliteRetryingExecutionStrategy(deps));
                    }
                );
                optionsAction.AddInterceptors(new SqliteNormalizeSearchInterceptor());
            },
            optionsLifetime: ServiceLifetime.Singleton
        );

        // Factory itself is Singleton (default for AddDbContextFactory) so
        // singleton consumers (MdnsDeviceScanner, DeviceBusRegistry,
        // ActivityLogger) can inject it. Options registered above as Singleton
        // so this resolves cleanly. CreateDbContextAsync() returns fresh
        // disposable contexts per call.
        services.AddDbContextFactory<MediaContext>(optionsAction =>
        {
            optionsAction.UseSqlite(
                $"Data Source={AppFiles.MediaDatabase}; Pooling=True; Foreign Keys=True;",
                o =>
                {
                    o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    o.ExecutionStrategy(deps => new SqliteRetryingExecutionStrategy(deps));
                }
            );
            optionsAction.AddInterceptors(new SqliteNormalizeSearchInterceptor());
        });

        // Add Repositories
        services.AddScoped<HomeRepository>();
        services.AddScoped<MusicRepository>();
        services.AddScoped<EncoderRepository>();
        services.AddScoped<EncodingHistoryRepository>();
        services.AddScoped<EncodingPresetRepository>();
        services.AddScoped<ContentSegmentRepository>();
        services.AddScoped<LibraryRepository>();
        services.AddScoped<MediaProcessingLibraryRepository>();
        services.AddScoped<DeviceRepository>();
        services.AddScoped<FolderRepository>();
        services.AddScoped<DriverRepository>();
        services.AddScoped<MediaProcessingFileRepository>();
        services.AddScoped<IFileRepository, MediaProcessingFileRepository>();
        services.AddScoped<FilesystemRepository>();
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
        services.AddScoped<RecommendationRepository>();

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
        services.AddScoped<RecommendationService>();
        services.AddScoped<SetupService>();

        services.AddMediaServerQueue();
        services.AddSingleton<JobDispatcher>();

        // Storage driver resolvers — registered before AddNoMercyEncoder so
        // the TryAdd inside AddNoMercyStorage picks them up via GetService<>.
        services.AddSingleton<IDriverConfigResolver>(sp => new DriverConfigResolver(
            sp.GetRequiredService<IDbContextFactory<MediaContext>>()
        ));
        services.AddSingleton<ICredentialResolver, CredentialResolver>();

        services.AddNoMercyEncoder(opts =>
        {
            opts.FfmpegPathOverride = AppFiles.FfmpegPath;
            opts.FfprobePathOverride = AppFiles.FfProbePath;
            opts.TesseractModelsDirectory = AppFiles.TesseractModelsFolder;
            opts.WhisperModelPath = AppFiles.WhisperModelPath;
            // Without this the JsonSpeedIndexStore silently no-ops on Save
            // ("No SpeedIndexCachePath configured — skipping save"), and every
            // reboot triggers a fresh ~20 min hardware benchmark calibration.
            // Pointing at AppFiles.SpeedIndexCachePath persists results so
            // NeedsRecalibration() can honour its 30-day grace window.
            opts.SpeedIndexCachePath = AppFiles.SpeedIndexCachePath;
        });

        // Concrete activity probe for the deferred hardware benchmark —
        // Encoder's default is a no-op (always idle) so it stays decoupled
        // from QueueRunner/SessionManager. AddSingleton after AddNoMercyEncoder
        // overrides the TryAddSingleton the encoder registered.
        services.AddSingleton<IEncoderActivityProbe, EncoderActivityProbe>();

        services.AddHostedService<EncodingNotificationSubscriber>();
        services.AddHostedService<AutoEncodeSubscriber>();
        services.AddHostedService<IntroDetectionSubscriber>();

        services.AddPluginSystem(AppFiles.PluginsPath);
        services.RegisterPluginServicesFromManifests(AppFiles.PluginsPath);

        services.AddVideoHubServices();
        services.AddMusicHubServices();
        services.AddSingleton<IActivityHubBroadcaster, ActivityHubBroadcaster>();
        services.AddSingleton<IActivityLogger, ActivityLogger>();
        services.AddSingleton<DatabaseActivity.IActivityLogger>(sp =>
            sp.GetRequiredService<IActivityLogger>()
        );
        services.AddSignalREventHandlers();

        // Subtitle acquisition — OpenSubtitlesProvider lives in MediaProcessing so
        // it can reference both NoMercy.Encoder (interface) and NoMercy.Providers (XML-RPC).
        services.AddSingleton<
            NoMercy.Encoder.Subtitles.IOpenSubtitlesProvider,
            OpenSubtitlesProvider
        >();

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
        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                "api",
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireClaim("scope", "openid", "profile");
                    policy.AddRequirements(
                        new AssertionRequirement(context =>
                        {
                            string? sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                            if (!Guid.TryParse(sub, out Guid userId))
                                return false;

                            User? user = ClaimsPrincipleExtensions.Users.FirstOrDefault(u =>
                                u.Id == userId
                            );
                            Logger.App($"User: {user?.Name ?? "Unknown"}");
                            return user is not null;
                        })
                    );
                }
            );

        // Eagerly load cached signing key so it's available before auth init completes
        OfflineJwksCache.LoadCachedPublicKey();

        // Configure Authentication
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = Config.AuthBaseUrl;
                options.RequireHttpsMetadata = Config.AuthBaseUrl.StartsWith(
                    "https://",
                    StringComparison.OrdinalIgnoreCase
                );
                options.Audience = Config.TokenClientId;

                // Enable offline token validation via cached signing keys
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.ValidIssuer = Config.AuthBaseUrl;

                // Explicitly enforce audience validation. options.Audience already sets
                // ValidAudience; this line makes the intent unambiguous and guards against
                // future refactors that might inadvertently remove the Audience assignment.
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.IssuerSigningKeyResolver = (
                    token,
                    securityToken,
                    kid,
                    parameters
                ) =>
                {
                    // Use the OIDC-discovered keys first (fresh from Keycloak).
                    // Add the cached key as a fallback for offline/key-rotation scenarios.
                    List<SecurityKey> keys = parameters.IssuerSigningKeys?.ToList() ?? [];
                    RsaSecurityKey? cachedKey = OfflineJwksCache.CachedSigningKey;
                    if (cachedKey is not null && keys.All(k => k != cachedKey))
                        keys.Add(cachedKey);

                    return keys;
                };

                options.Events = new()
                {
                    OnMessageReceived = context =>
                    {
                        StringValues accessToken = context.Request.Query["access_token"];
                        string[] result = accessToken.ToString().Split('&');

                        if (result.Length > 0 && !string.IsNullOrEmpty(result[0]))
                            context.Token = result[0];

                        return Task.CompletedTask;
                    },
                    // OnTokenValidated fires on EVERY authenticated request (every API call,
                    // every SignalR frame), not on actual login. Logging "auth.login" here
                    // produced one row per request and flooded the activity log with
                    // duplicates and FK-failing Ulid.Empty deviceIds. Real login events live
                    // at Keycloak; the server has no notion of "login" via JWT validation.
                    // If we ever want session timeline data, log "session_start" from
                    // ConnectionHub.OnConnectedAsync (one row per actual hub connection).
                    OnAuthenticationFailed = context =>
                    {
                        HttpRequest req = context.Request;

                        // Extract client identity from query string (sent by all hub connections)
                        string clientName =
                            req.Query["client_name"].FirstOrDefault()
                            ?? req.Query["custom_name"].FirstOrDefault()
                            ?? "unknown-client";
                        string clientType = req.Query["client_type"].FirstOrDefault() ?? "unknown";
                        string clientDevice = req.Query["client_device"].FirstOrDefault() ?? "";
                        string clientOs = req.Query["client_os"].FirstOrDefault() ?? "";
                        string remoteIp =
                            context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        // Build a human-readable client description
                        string client = !string.IsNullOrEmpty(clientDevice)
                            ? $"\"{clientName}\" ({clientDevice}, {clientOs})"
                            : $"\"{clientName}\" ({clientType})";

                        // Extract the hub/endpoint name from the path
                        string endpoint =
                            req.Path.Value?.Split('/')
                                .FirstOrDefault(s =>
                                    s.EndsWith("Hub", StringComparison.OrdinalIgnoreCase)
                                    || s == "negotiate"
                                    || s.Length > 0
                                )
                            ?? req.Path.Value
                            ?? "unknown";
                        // Get just the hub name from the path e.g. /videoHub/negotiate → videoHub
                        string[] segments = (req.Path.Value ?? "").Trim('/').Split('/');
                        string hub = segments.Length > 0 ? segments[0] : "unknown";

                        // Try to read token claims for expiry diagnostics
                        string tokenAge = "";
                        try
                        {
                            string? raw = req.Query["access_token"].FirstOrDefault()?.Split('&')[0];
                            if (string.IsNullOrEmpty(raw))
                            {
                                // Check Authorization header
                                string? authHeader = req.Headers.Authorization.FirstOrDefault();
                                if (
                                    authHeader?.StartsWith(
                                        "Bearer ",
                                        StringComparison.OrdinalIgnoreCase
                                    ) == true
                                )
                                    raw = authHeader["Bearer ".Length..];
                            }

                            if (!string.IsNullOrEmpty(raw))
                            {
                                var handler = new JwtSecurityTokenHandler();
                                var jwt = handler.ReadJwtToken(raw);
                                TimeSpan expired = DateTime.UtcNow - jwt.ValidTo;
                                tokenAge =
                                    expired.TotalHours >= 1
                                        ? $" (token expired {expired.TotalHours:F0}h ago)"
                                        : $" (token expired {expired.TotalMinutes:F0}m ago)";
                            }
                        }
                        catch
                        { /* token unreadable — skip */
                        }

                        // Human-readable failure reason
                        string reason = context.Exception switch
                        {
                            SecurityTokenExpiredException => $"Expired token{tokenAge}",
                            SecurityTokenInvalidSignatureException => "Invalid token signature",
                            SecurityTokenInvalidAudienceException => "Token audience mismatch",
                            SecurityTokenInvalidIssuerException => "Token issuer mismatch",
                            _ => $"{context.Exception.GetType().Name}: {context.Exception.Message}",
                        };

                        Logger.Auth(
                            $"{reason} — {client} → {hub} from {remoteIp}",
                            LogEventLevel.Warning
                        );

                        // Activity log write removed — OnAuthenticationFailed fires for every
                        // expired-token request (effectively continuously for any idle client),
                        // not just real login failures. Real failures live at Keycloak. Diagnostic
                        // log above is enough for server-side visibility.
                        return Task.CompletedTask;
                    },
                };
            });
    }

    private static void ConfigureApi(IServiceCollection services)
    {
        ConfigureApiVersioning(services);

        // Add Controllers and JSON Options
        services
            .AddControllers(options =>
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
        services
            .AddSignalR(o =>
            {
                o.EnableDetailedErrors = Config.IsDev;
                o.MaximumReceiveMessageSize = 2 * 1024 * 1024; // 2MB — realistic max is ~1MB for large playlists

                o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                o.KeepAliveInterval = TimeSpan.FromSeconds(15);

                // Add error logging filter for invalid method calls and wrong arguments
                o.AddFilter<HubErrorLoggingFilter>();
            })
            .AddNewtonsoftJsonProtocol(options =>
            {
                options.PayloadSerializerSettings = JsonHelper.Settings;
            });

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            // Subtitle payloads (Aegisub karaoke + hand-drawn signs) reach
            // 60-90 MB. Compressing them strips Content-Length from the
            // OkHttp response on the Android client (transparent gzip leaves
            // the header reflecting the compressed bytes, useless for sizing
            // the read buffer). Result: the client falls into its no-length
            // fallback path and OOMs the 256 MB-heap TV / 384 MB phone. Skip
            // compression for subtitle MIME types so clients always know the
            // real payload size up front.
            options.ExcludedMimeTypes =
            [
                "text/x-ssa",
                "text/x-ass",
                "application/x-subrip",
                "text/vtt",
            ];
        });

        SwaggerConfiguration.AddSwagger(services);
    }

    private static void ConfigureApiVersioning(IServiceCollection services)
    {
        services
            .AddApiVersioning(config =>
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
            options.AddPolicy(
                "AllowNoMercyOrigins",
                builder =>
                {
                    List<string> origins =
                    [
                        "https://nomercy.tv",
                        "https://*.nomercy.tv",
                        "https://cast.nomercy.tv",
                        "https://hlsjs.video-dev.org",
                        "http://localhost:7625",
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
                }
            );
        });
    }

    private static Ulid? TryGetDeviceId(HttpContext httpContext)
    {
        string? raw = httpContext.Request.Query["client_id"].FirstOrDefault();
        if (string.IsNullOrEmpty(raw))
            return null;

        return Ulid.TryParse(raw, out Ulid id) ? id : null;
    }
}
