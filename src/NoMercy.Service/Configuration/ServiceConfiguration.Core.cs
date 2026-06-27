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

using I18N.DotNet;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NoMercy.Api.Hubs;
using NoMercy.Api.Services;
using NoMercy.Api.WebSockets;
using NoMercy.Data.Activity;
using NoMercy.Data.Repositories;
using NoMercy.Data.Resolvers;
using NoMercy.Database;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Startup;
using NoMercy.Events;
using NoMercy.Events.Audit;
using NoMercy.Helpers;
using NoMercy.Monitoring;
using NoMercy.NmSystem.Wallpaper;
using NoMercy.MediaProcessing.Collections;
using NoMercy.MediaProcessing.Episodes;
using NoMercy.MediaProcessing.EventHandlers;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Images.Palettes;
using NoMercy.MediaProcessing.Images.Palettes.Sources;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.MediaProcessing.Movies;
using NoMercy.MediaProcessing.People;
using NoMercy.MediaProcessing.Seasons;
using NoMercy.MediaProcessing.Shows;
using NoMercy.MediaProcessing.Subtitles;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Connectivity;
using NoMercy.Networking.Connectivity.Strategies;
using NoMercy.Networking.Devices;
using NoMercy.Networking.Discovery;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Images;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.OpticalMedia.Composition;
using NoMercy.Plugins;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Queue.MediaServer;
using NoMercy.Service.Extensions;
using NoMercy.Service.Workers;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Cast;
using NoMercy.Setup.Server;
using NoMercy.Storage;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Extensions;
using Serilog.Events;
using CollectionRepository = NoMercy.Data.Repositories.CollectionRepository;
using DatabaseActivity = NoMercy.Database.Activity;
using DataIMovieRepository = NoMercy.Data.Repositories.IMovieRepository;
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

namespace NoMercy.Service.Configuration;

public static partial class ServiceConfiguration
{
    private static void ConfigureCoreServices(
        IServiceCollection services,
        IConfiguration configuration
    )
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
            return new(authDbContext, storageDriver, sp.GetRequiredService<IAuthTokenStore>());
        });
        services.AddSingleton<SetupEndpoints>();
        services.AddSingleton<BootOrchestrator>();
        services.AddSingleton<CastSessionTokenService>();
        // Route every container to the same tracker. The Service rebuilds its host
        // on the HTTPS restart and on port-conflict retry; a per-container singleton
        // would mean queue workers in the live host wait on a tracker that the static
        // MarkComplete callers (BootOrchestrator, Setup.Start) never reached.
        services.AddSingleton<NmSystem.Lifecycle.IServerPhaseTracker>(sp =>
            NmSystem.Lifecycle.ServerPhaseTracker.Shared(
                sp.GetService<ILogger<NmSystem.Lifecycle.ServerPhaseTracker>>()
            )
        );

        services.AddScoped<Encoder.Profiles.BuiltinPresetSeeder>();

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

        services.AddSingleton<IApiKeyStore, ApiKeyStore>();
        services.AddSingleton<IApiKeyLoader, ApiKeyLoader>();
        services.AddSingleton<IServerRegistrationService, ServerRegistrationService>();
        services.AddSingleton<IUserProvisioningService, UserProvisioningService>();
        services.AddSingleton<IDegradedModeRecovery, DegradedModeRecovery>();
        services.AddSingleton<AppProcessManager>();
        services.AddSingleton<Monitoring.ResourceMonitor>();

        // Network discovery (replaces static Networking.Networking IP/address members)
        services.AddSingleton<INetworkDiscovery>(sp =>
        {
            IStorageDriver storageDriver = sp.GetRequiredService<IStorageDriver>();
            NetworkDiscovery discovery = new(
                storageDriver,
                sp.GetRequiredService<IAuthTokenStore>(),
                sp.GetRequiredService<IConnectivityStatus>()
            );
            if (!string.IsNullOrEmpty(StartupOptions.OverrideInternalIp))
                discovery.InternalIp = StartupOptions.OverrideInternalIp;
            if (!string.IsNullOrEmpty(StartupOptions.OverrideExternalIp))
                discovery.ExternalIp = StartupOptions.OverrideExternalIp;
            Start.NetworkDiscovery = discovery;
            // Register.Discovery = discovery;
            ChromeCast.NetworkDiscovery = discovery;
            return discovery;
        });

        // Client messaging (replaces static Networking.Networking.SendTo/SendToAll)
        services.AddSingleton<ConnectedClients>();
        services.AddSingleton<IClientMessenger, ClientMessenger>();

        // Connectivity strategies (ordered by priority)
        services.AddSingleton<IConnectivityStrategy>(sp => new PortForwardStrategy(
            (NetworkDiscovery)sp.GetRequiredService<INetworkDiscovery>(),
            sp.GetRequiredService<IConnectivityStatus>()
        ));
        services.AddSingleton<IConnectivityStrategy, StunHolePunchStrategy>();
        services.AddSingleton<IConnectivityStrategy>(sp => new CloudflareTunnelStrategy(
            sp.GetRequiredService<IConnectivityStatus>(),
            () => Task.FromResult(string.Empty) // Register.GetTunnelAvailability
        ));

        // Add Auth services
        services.AddSingleton<IAuthTokenStore, AuthTokenStore>();

        // Add Configuration POCOs
        services.Configure<ExternalServicesConfig>(configuration.GetSection("ExternalServices"));
        services.Configure<ServerConfig>(configuration.GetSection("Server"));
        services.Configure<ConnectivityConfig>(configuration.GetSection("Connectivity"));
        services.Configure<WorkerConfig>(configuration.GetSection("Workers"));
        services.Configure<EncoderResourceConfig>(configuration.GetSection("EncoderResources"));
        services.Configure<ContentPolicy>(configuration.GetSection("ContentPolicy"));

        // Add runtime status singletons
        services.AddSingleton<IBootStatus, BootStatus>();
        services.AddSingleton<IConnectivityStatus, ConnectivityStatus>();
        services.AddSingleton<IUpdateStatus, UpdateStatus>();

        // Runtime server settings (DB-hydrated, dashboard-mutable). DI hands out
        // the same static Current instance the Config facade and boot path use.
        services.AddSingleton(_ => RuntimeServerSettings.Current);

        services.AddSingleton<IImageService, ImageService>();

        // Update checker + periodic background check
        services.AddSingleton<IUpdateChecker, UpdateChecker>();
        services.AddHostedService<PeriodicUpdateCheckService>();

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

        // optionsLifetime: Singleton so the Singleton IDbContextFactory<QueueContext>
        // can consume DbContextOptions without lifetime-validation errors. The
        // DbContext itself stays Scoped (default) for per-request use.
        Action<DbContextOptionsBuilder> configureQueueContext = optionsAction =>
            optionsAction.UseSqlite($"Data Source={AppFiles.QueueDatabase}; Pooling=True;");

        services.AddDbContext<QueueContext>(
            configureQueueContext,
            optionsLifetime: ServiceLifetime.Singleton
        );

        services.AddDbContextFactory<QueueContext>(configureQueueContext);

        // optionsLifetime: Singleton so the Singleton IDbContextFactory below
        // can consume DbContextOptions without lifetime-validation errors.
        // The DbContext itself stays Scoped (default) for per-request use.
        // Interceptors are registered once in MediaContext.OnConfiguring, which runs
        // for every context path; registering them here too double-fired
        // SqliteNormalizeSearchInterceptor on DI/factory-created contexts.
        Action<DbContextOptionsBuilder> configureMediaContext = optionsAction =>
            optionsAction.UseSqlite(
                $"Data Source={AppFiles.MediaDatabase}; Pooling=True; Foreign Keys=True;",
                o =>
                {
                    o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    o.ExecutionStrategy(deps => new SqliteRetryingExecutionStrategy(deps));
                }
            );

        services.AddDbContext<MediaContext>(
            configureMediaContext,
            optionsLifetime: ServiceLifetime.Singleton
        );

        // Factory itself is Singleton (default for AddDbContextFactory) so
        // singleton consumers (MdnsDeviceScanner, DeviceBusRegistry,
        // ActivityLogger) can inject it. Options registered above as Singleton
        // so this resolves cleanly. CreateDbContextAsync() returns fresh
        // disposable contexts per call.
        services.AddDbContextFactory<MediaContext>(configureMediaContext);

        services.AddSingleton<NoMercyQueue.Core.Interfaces.IJobDispatcher, JobDispatcher>();

        // Add Provider Clients
        services.AddScoped<IMovieMetadataProvider, TmdbMovieMetadataProvider>();
        services.AddScoped<ITvShowMetadataProvider, TmdbTvShowMetadataProvider>();
        services.AddScoped<ICollectionMetadataProvider, TmdbCollectionMetadataProvider>();
        services.AddScoped<IPersonMetadataProvider, TmdbPersonMetadataProvider>();

        // Add Repositories
        services.AddScoped<HomeRepository>();
        services.AddScoped<EncoderRepository>();
        services.AddScoped<EncodingHistoryRepository>();
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
        services.AddScoped<
            MediaProcessing.Collections.ICollectionRepository,
            MediaProcessingCollectionRepository
        >();
        services.AddScoped<GenreRepository>();
        services.AddScoped<MovieRepository>();
        services.AddScoped<MediaProcessingMovieRepository>();
        services.AddScoped<
            MediaProcessing.Movies.IMovieRepository,
            MediaProcessingMovieRepository
        >();
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
        services.AddScoped<PeopleRepository>();

        // Read-side interface registrations (Data.Repositories)
        services.AddScoped<Data.Repositories.ICollectionRepository, CollectionRepository>();
        services.AddScoped<IContentSegmentRepository, ContentSegmentRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IEncoderRepository, EncoderRepository>();
        services.AddScoped<IEncodingHistoryRepository, EncodingHistoryRepository>();
        services.AddScoped<IEncodingPresetRepository, EncodingPresetRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IGenreRepository, GenreRepository>();
        services.AddScoped<IHomeRepository, HomeRepository>();
        services.AddScoped<ILanguageRepository, LanguageRepository>();
        services.AddScoped<Data.Repositories.ILibraryRepository, LibraryRepository>();
        services.AddScoped<Data.Repositories.IMovieRepository, MovieRepository>();
        services.AddScoped<IMusicRepository, MusicRepository>();
        services.AddScoped<IPeopleRepository, PeopleRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<ISpecialRepository, SpecialRepository>();
        services.AddScoped<ITvShowRepository, TvShowRepository>();
        services.AddScoped<IUserDataRepository, UserDataRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<IVideoFileRepository, VideoFileRepository>();
        services.AddScoped<InboxRepository>();
        services.AddScoped<IInboxRepository, InboxRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();

        // Add Managers
        // services.AddScoped<EncoderManager>();
        services.AddScoped<LibraryManager>();
        services.AddScoped<MovieManager>();
        services.AddScoped<CollectionManager>();
        services.AddScoped<ShowManager>();
        services.AddScoped<SeasonManager>();
        services.AddScoped<EpisodeManager>();
        services.AddScoped<PersonManager>();
        services.AddScoped<EncoderProfileService>();
        services.AddScoped<HomeService>();
        services.AddScoped<RecommendationService>();
        services.AddScoped<SetupService>();

        // Palette pipeline — contract-based DI, dispatched by EntityType
        services.AddScoped<IPaletteSource, MoviePaletteSource>();
        services.AddScoped<IPaletteSource, TvPaletteSource>();
        services.AddScoped<IPaletteSource, SeasonPaletteSource>();
        services.AddScoped<IPaletteSource, EpisodePaletteSource>();
        services.AddScoped<IPaletteSource, CollectionPaletteSource>();
        services.AddScoped<IPaletteSource, PersonPaletteSource>();
        services.AddScoped<IPaletteSource, RecommendationPaletteSource>();
        services.AddScoped<IPaletteSource, SimilarPaletteSource>();
        services.AddScoped<IPaletteSource, ImagePaletteSource>();
        services.AddScoped<IPaletteSource, ArtistPaletteSource>();
        services.AddScoped<IPaletteSource, AlbumPaletteSource>();
        services.AddScoped<IPaletteSource, PlaylistPaletteSource>();
        services.AddScoped<IPaletteSource, ReleaseGroupPaletteSource>();
        services.AddScoped<PaletteSourceRegistry>();

        services.AddMediaServerQueue();
        services.AddSingleton<JobDispatcher>();
        services.AddSingleton<NoMercy.MediaProcessing.Jobs.IJobDispatcher>(sp =>
            sp.GetRequiredService<JobDispatcher>()
        );

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

        // Transcode-scoped IStorage — paths are relative to AppFiles.TranscodePath.
        // HomeController uses this so it can pass scope-relative paths (Rule 1 of
        // the IStorage path contract) instead of Path.Combine(TranscodePath, ...).
        services.AddKeyedSingleton<IStorage>(
            "transcode",
            (sp, _) =>
            {
                IStorageDriver driver = sp.GetRequiredService<IStorageDriver>();
                Storage.Validation.StoragePathGuard guard = new([AppFiles.TranscodePath], driver);
                return new Storage.Drivers.Local.LocalStorage(driver, guard);
            }
        );

        // Concrete activity probe for the deferred hardware benchmark —
        // Encoder's default is a no-op (always idle) so it stays decoupled
        // from QueueRunner/SessionManager. AddSingleton after AddNoMercyEncoder
        // overrides the TryAddSingleton the encoder registered.
        services.AddSingleton<IEncoderActivityProbe, EncoderActivityProbe>();
        services.AddTransient<IOrphanCheckpointLookup, EncoderOrphanCheckpointLookup>();

        services.AddHostedService<EncodingNotificationSubscriber>();
        services.AddHostedService<AutoEncodeSubscriber>();
        services.AddHostedService<IntroDetectionSubscriber>();
        services.AddHostedService<PaletteBackfillStartupService>();

        services.AddPluginSystem(AppFiles.PluginsPath);
        services.RegisterPluginServicesFromManifests(AppFiles.PluginsPath);

        services.AddVideoHubServices();
        services.AddMusicHubServices();
        services.AddLiveTranscodeHubServices();
        services.AddSingleton<IActivityHubBroadcaster, ActivityHubBroadcaster>();
        services.AddSingleton<IActivityLogger, ActivityLogger>();
        services.AddSingleton<DatabaseActivity.IActivityLogger>(sp =>
            sp.GetRequiredService<IActivityLogger>()
        );
        services.AddSignalREventHandlers();

        // Subtitle acquisition — OpenSubtitlesProvider lives in MediaProcessing so
        // it can reference both NoMercy.Encoder (interface) and NoMercy.Providers (XML-RPC).
        services.AddSingleton<Encoder.Subtitles.IOpenSubtitlesProvider, OpenSubtitlesProvider>();

        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddScoped<ILocalizer, Localizer>();
    }
}
