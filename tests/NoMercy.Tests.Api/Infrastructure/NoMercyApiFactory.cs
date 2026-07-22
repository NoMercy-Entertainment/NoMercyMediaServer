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

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Storage;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Security;
using NoMercy.Plugins.Abstractions;
using NoMercy.Providers.AcoustId;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Service;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Server;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Tests.Api.Infrastructure;

public class NoMercyApiFactory : WebApplicationFactory<Startup>
{
    private static readonly object DbLock = new();
    private static bool _dbInitialized;

    public NoMercyApiFactory()
    {
        lock (DbLock)
        {
            if (!_dbInitialized)
            {
                EnsureDirectoriesAndSeedDatabase();
                _dbInitialized = true;
            }
        }

        // Re-seed the process-wide ClaimsPrincipalExtensions user cache from the
        // database at the start of every test class. Worker/coordinator tests
        // Reset() that static; with serialized collections a later class would
        // otherwise inherit an owner-less cache and get spurious 403s.
        using MediaContext userCacheContext = new();
        UserCache.Current.InitializeAsync(context: userCacheContext).GetAwaiter().GetResult();
    }

    // Every existing test in this fixture authenticates via TestAuthHandler (a header-
    // driven fake scheme), which never exercises the real Keycloak JwtBearer pipeline
    // wired in ServiceConfiguration.Auth.cs. A subclass overrides this to true (and
    // ConfigureRealAuthentication) when a test needs to prove something about that real
    // pipeline itself — e.g. that an invalid/expired token is still rejected. Default
    // behavior for every other test is unchanged.
    protected virtual bool UseRealAuthentication => false;

    protected virtual void ConfigureRealAuthentication(IServiceCollection services) { }

    // Default plugin manager for tests that don't care about plugins. A subclass
    // overrides this to supply a manager with real/fake plugin instances (e.g. an auth
    // plugin, to prove OnTokenValidated can only ever add claims, never grant access).
    protected virtual IPluginManager CreateTestPluginManager() => new StubPluginManager();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment: "Testing");

        builder.ConfigureTestServices(servicesConfiguration: services =>
        {
            RemoveHostedServices(services: services);

            if (UseRealAuthentication)
                ConfigureRealAuthentication(services: services);
            else
                ReplaceAuth(services: services);

            ReplacePluginManager(services: services);
            ReplaceSetupState(services: services);

            // Ensure all ConnectedClients registrations (from Startup AND from
            // CreateWebHostBuilder.ConfigureServices) are replaced by the single
            // shared instance so controllers and tests operate on the same dictionary.
            services.RemoveAll<ConnectedClients>();
            services.AddSingleton(implementationInstance: SharedConnectedClients);

            // ServerController depends on IAudioFingerprinter. The real
            // ChromaprintFingerprinter needs the native fpcalc/chromaprint
            // library, which the test host doesn't provide, so register a
            // no-op stand-in — the dashboard endpoints under test only need
            // the controller to construct, not to fingerprint audio.
            services.RemoveAll<IAudioFingerprinter>();
            services.AddTransient<IAudioFingerprinter>(implementationFactory: _ => Mock.Of<IAudioFingerprinter>());

            // RecommendationsController's /{type}/{id} detail route calls straight through
            // to the real TMDB HTTP client for movie/tv metadata. Tests must never reach
            // the network, so replace both providers with loose mocks — Moq returns a
            // completed Task<null> for unconfigured Task<T?> members, which exercises the
            // controller's real "detail not found" 404 path deterministically.
            services.RemoveAll<IMovieMetadataProvider>();
            services.AddScoped<IMovieMetadataProvider>(implementationFactory: _ => Mock.Of<IMovieMetadataProvider>());
            services.RemoveAll<ITvShowMetadataProvider>();
            services.AddScoped<ITvShowMetadataProvider>(implementationFactory: _ => Mock.Of<ITvShowMetadataProvider>());
        });
    }

    protected override IWebHostBuilder? CreateWebHostBuilder()
    {
#pragma warning disable ASPDEPR008 // WebHost kept until Startup is migrated to minimal-hosting
        // Capture the instance into a local so the lambda below closes over it — the
        // lambda must not reference `this` because the lambda outlives the factory in
        // some test harness configurations.
        ConnectedClients sharedClients = SharedConnectedClients;

        return WebHost
            .CreateDefaultBuilder(args: [])
            .UseContentRoot(contentRoot: AppContext.BaseDirectory)
            .ConfigureLogging(configureLogging: logging => logging.ClearProviders())
            .UseStartup<Startup>()
            .ConfigureServices(configureServices: services =>
            {
                services.AddSingleton(implementationInstance: new StartupOptions());
                services.AddSingleton<ISunsetPolicyManager>(implementationInstance: new NoOpSunsetPolicyManager());
                services.AddSingleton<
                    IApiVersionDescriptionProvider,
                    DefaultApiVersionDescriptionProvider
                >();

                // CustomLogger<T> depends on NoMercyLoggerProvider (which needs
                // NoMercyLoggerOptions); production wires all three in WebHostFactory.
                // The test host registers the logger itself, so it must register the
                // provider + options too, or activating any ILogger<T> throws.
                // LogDirectory defaults to null here, so tests write no log files.
                services.AddSingleton(implementationInstance: new NmSystem.Logging.NoMercyLoggerOptions());
                services.AddSingleton<NmSystem.Logging.NoMercyLoggerProvider>();
                services.AddSingleton(serviceType: typeof(ILogger<>), implementationType: typeof(CustomLogger<>));

                // Register the shared ConnectedClients instance early.  Because
                // ConfigureServices callbacks are applied in registration order,
                // this runs before Startup.ConfigureServices.  Startup's later
                // AddSingleton<ConnectedClients>() call adds a SECOND descriptor; the
                // last-registered wins for plain resolution — so we must remove the
                // Startup-registered one in ConfigureTestServices (below) to guarantee
                // our instance is used.
                services.AddSingleton(implementationInstance: sharedClients);
            });
#pragma warning restore ASPDEPR008
    }

    // The ConnectedClients singleton injected into the server's DI container.
    // Configured via ConfigureWebHost so it is the exact same instance the request
    // pipeline (and controllers) receive.  Tests that need to simulate an "online"
    // device seed this dictionary directly.
    public ConnectedClients SharedConnectedClients { get; } = new();

    // Forces the test server to start (if not already started) and returns the
    // ConnectedClients singleton from the server's root service provider.
    // This is the same instance that controllers receive via constructor injection.
    public ConnectedClients GetConnectedClients() => SharedConnectedClients;

    public static readonly Ulid MovieLibraryId = Ulid.NewUlid();
    public static readonly Ulid TvLibraryId = Ulid.NewUlid();
    public static readonly Ulid MusicLibraryId = Ulid.NewUlid();
    public static readonly Ulid MovieFolderId = Ulid.NewUlid();
    public static readonly Ulid TvFolderId = Ulid.NewUlid();
    public static readonly Ulid MusicFolderId = Ulid.NewUlid();

    public static readonly Guid ArtistId1 = Guid.Parse(input: "11111111-1111-1111-1111-111111111111");
    public static readonly Guid AlbumId1 = Guid.Parse(input: "22222222-2222-2222-2222-222222222222");
    public static readonly Guid TrackId1 = Guid.Parse(input: "33333333-3333-3333-3333-333333333333");
    public static readonly Guid TrackId2 = Guid.Parse(input: "33333333-3333-3333-3333-333333333334");
    public static readonly Guid PlaylistId1 = Guid.Parse(input: "44444444-4444-4444-4444-444444444444");
    public static readonly Guid MusicGenreId1 = Guid.Parse(input: "55555555-5555-5555-5555-555555555555");

    public static readonly int FavoriteCollectionId = 900001;
    public static readonly Ulid FavoriteSpecialId = Ulid.NewUlid();

    private static void EnsureDirectoriesAndSeedDatabase()
    {
        foreach (string path in AppFiles.AllPaths())
        {
            if (!Directory.Exists(path: path))
                Directory.CreateDirectory(path: path);
        }

        // Initialize DataProtection + TokenStore before any AppDbContext access.
        // AppDbContext uses TokenStore value converters on the Configuration table,
        // so the protector must exist before EnsureCreated() or any query runs.
        ServiceCollection tokenServices = new();
        tokenServices
            .AddDataProtection()
            .PersistKeysToFileSystem(directory: new(path: AppFiles.DataProtectionKeysDir))
            .SetApplicationName(applicationName: "NoMercyMediaServer");
        ServiceProvider tokenProvider = tokenServices.BuildServiceProvider();
        TokenStore.Initialize(serviceProvider: tokenProvider);

        // Create app.db for AppDbContext (Configuration table, SecureValue columns).
        // Use EnsureCreated rather than delete+recreate — parallel test assembly runs
        // share the same NoMercy_test path and file deletion races cause lock errors.
        using AppDbContext appContext = new();
        appContext.Database.EnsureCreated();

        string mediaDbPath = Path.Combine(path1: AppFiles.DataPath, path2: "media.db");
        foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            string file = mediaDbPath + suffix;
            if (File.Exists(path: file))
                File.Delete(path: file);
        }

        using MediaContext mediaContext = new();
        mediaContext.Database.EnsureCreated();

        if (!mediaContext.Users.Any())
        {
            User testUser = new()
            {
                Id = TestAuthHandler.DefaultUserId,
                Email = TestAuthHandler.DefaultUserEmail,
                Name = TestAuthHandler.DefaultUserName,
                Owner = true,
                Allowed = true,
                Manage = true,
            };
            // A second, unrelated-but-allowed identity — impersonated via
            // HttpClientAuthExtensions.AsSecondaryUser() — so ownership-isolation
            // tests (e.g. UserPlaylistsControllerTests) exercise a real 404 from
            // the endpoint's own ownership check, not a 403 from MediaAccess.
            User secondaryTestUser = new()
            {
                Id = TestAuthHandler.SecondaryUserId,
                Email = TestAuthHandler.SecondaryUserEmail,
                Name = TestAuthHandler.SecondaryUserName,
                Owner = false,
                Allowed = true,
                Manage = false,
            };
            mediaContext.Users.AddRange(entities: [testUser, secondaryTestUser]);
            mediaContext.SaveChanges();
        }

        SeedMediaData(context: mediaContext);

        UserCache.Current.InitializeAsync(context: mediaContext).GetAwaiter().GetResult();

        string queueDbPath = Path.Combine(path1: AppFiles.DataPath, path2: "queue.db");
        foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            string file = queueDbPath + suffix;
            if (File.Exists(path: file))
            {
                try
                {
                    File.Delete(path: file);
                }
                catch (IOException)
                {
                    // Another parallel test process may hold the file; EnsureCreated will
                    // use the existing DB, which is acceptable for queue (read-only in tests).
                }
            }
        }

        using QueueContext queueContext = new();
        queueContext.Database.EnsureCreated();
    }

    private static void SeedMediaData(MediaContext context)
    {
        if (context.Libraries.Any())
            return;

        // Step 1: Core entities (no FK dependencies)
        Library movieLibrary = new()
        {
            Id = MovieLibraryId,
            Title = "Movies",
            Type = "movie",
            Order = 1,
        };
        Library tvLibrary = new()
        {
            Id = TvLibraryId,
            Title = "TV Shows",
            Type = "tv",
            Order = 2,
        };
        context.Libraries.AddRange(entities: [movieLibrary, tvLibrary]);

        Driver systemLocalDriver = new()
        {
            Id = Driver.SystemLocalDriverId,
            Name = "Local Filesystem",
            Type = "local",
            Config = """{"rootPath":"/"}""",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(entity: systemLocalDriver);

        Folder movieFolder = new()
        {
            Id = MovieFolderId,
            Path = "/media/movies",
            DriverId = Driver.SystemLocalDriverId,
        };
        Folder tvFolder = new()
        {
            Id = TvFolderId,
            Path = "/media/tv",
            DriverId = Driver.SystemLocalDriverId,
        };
        context.Folders.AddRange(entities: [movieFolder, tvFolder]);

        Genre actionGenre = new() { Id = 28, Name = "Action" };
        Genre dramaGenre = new() { Id = 18, Name = "Drama" };
        context.Genres.AddRange(entities: [actionGenre, dramaGenre]);

        context.SaveChanges();

        // Step 2: Entities with FK to libraries/folders/user
        context.LibraryUser.AddRange(entities: [new LibraryUser(libraryId: MovieLibraryId, userId: TestAuthHandler.DefaultUserId), new LibraryUser(libraryId: TvLibraryId, userId: TestAuthHandler.DefaultUserId)]
        );

        context.FolderLibrary.AddRange(entities: [new(folderId: MovieFolderId, libraryId: MovieLibraryId), new(folderId: TvFolderId, libraryId: TvLibraryId)]
        );

        Movie movie1 = new()
        {
            Id = 129,
            Title = "Spirited Away",
            TitleSort = "spirited away",
            Overview =
                "A young girl, Chihiro, becomes trapped in a strange new world of spirits. When her parents undergo a mysterious transformation, she must call upon the courage she never knew she had to free her family.",
            Poster = "/39wmItIWsg5sZMyRUHLkWBcuVCM.jpg",
            Backdrop = "/Ab8mkHmkYADjU7wQiOkia9BzGvS.jpg",
            ReleaseDate = new DateTime(year: 2001, month: 7, day: 20),
            LibraryId = MovieLibraryId,
            VoteAverage = 8.5,
        };
        Movie movie2 = new()
        {
            Id = 680,
            Title = "Pulp Fiction",
            TitleSort = "pulp fiction",
            Overview =
                "The lives of two mob hitmen intertwine in four tales of violence and redemption.",
            Poster = "/d5iIlFn5s0ImszYzBPb8JPIfbXD.jpg",
            Backdrop = "/suaEOtk1N1sgg2MTM7oZd2cfVp3.jpg",
            ReleaseDate = new DateTime(year: 1994, month: 9, day: 10),
            LibraryId = MovieLibraryId,
            VoteAverage = 8.5,
        };
        context.Movies.AddRange(entities: [movie1, movie2]);

        Tv show1 = new()
        {
            Id = 1399,
            Title = "Breaking Bad",
            TitleSort = "breaking bad",
            Overview =
                "A chemistry teacher teams up with a former student to cook and sell crystal meth.",
            Poster = "/ggFHVNu6YYI5L9pCfOacjizRGt.jpg",
            Backdrop = "/tsRy63Mu5cu8etL1X7ZLyf7UP1M.jpg",
            FirstAirDate = new DateTime(year: 2008, month: 1, day: 20),
            NumberOfEpisodes = 62,
            NumberOfSeasons = 5,
            LibraryId = TvLibraryId,
            VoteAverage = 8.9,
        };
        context.Tvs.Add(entity: show1);

        context.SaveChanges();

        // Step 3: Join tables and child entities (FK to movies/tv/genres)
        context.LibraryMovie.AddRange(entities: [new LibraryMovie(libraryId: MovieLibraryId, movieId: 129), new LibraryMovie(libraryId: MovieLibraryId, movieId: 680)]
        );

        context.LibraryTv.Add(entity: new(libraryId: TvLibraryId, tvId: 1399));

        context.GenreMovie.AddRange(entities: [new GenreMovie { GenreId = 28, MovieId = 129 }, new GenreMovie { GenreId = 18, MovieId = 129 }, new GenreMovie { GenreId = 18, MovieId = 680 }]
        );

        context.GenreTv.Add(entity: new() { GenreId = 18, TvId = 1399 });

        Season season1 = new()
        {
            Id = 3572,
            Title = "Season 1",
            SeasonNumber = 1,
            EpisodeCount = 7,
            TvId = 1399,
        };
        context.Seasons.Add(entity: season1);

        context.SaveChanges();

        // Step 4: Episodes (FK to season/tv) and video files (FK to movie/episode)
        Episode episode1 = new()
        {
            Id = 62085,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
            Overview = "Walter White is diagnosed with advanced lung cancer.",
        };
        Episode episode2 = new()
        {
            Id = 62086,
            Title = "Cat's in the Bag...",
            EpisodeNumber = 2,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
            Overview = "Walt and Jesse deal with a corpse and a prisoner.",
        };
        context.Episodes.AddRange(entities: [episode1, episode2]);

        VideoFile movieVideoFile1 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Spirited.Away.2001.1080p.mkv",
            Folder = "/media/movies/Spirited Away (2001)",
            HostFolder = "/media/movies/Spirited Away (2001)",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = MovieFolderId.ToString(),
            MovieId = 129,
        };
        VideoFile movieVideoFile2 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Pulp.Fiction.1994.1080p.mkv",
            Folder = "/media/movies/Pulp Fiction (1994)",
            HostFolder = "/media/movies/Pulp Fiction (1994)",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = MovieFolderId.ToString(),
            MovieId = 680,
        };
        context.VideoFiles.AddRange(entities: [movieVideoFile1, movieVideoFile2]);

        context.SaveChanges();

        // Step 5: TV video files (FK to episodes)
        VideoFile tvVideoFile1 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Breaking.Bad.S01E01.mkv",
            Folder = "/media/tv/Breaking Bad/Season 01",
            HostFolder = "/media/tv/Breaking Bad/Season 01",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = TvFolderId.ToString(),
            EpisodeId = 62085,
        };
        VideoFile tvVideoFile2 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Breaking.Bad.S01E02.mkv",
            Folder = "/media/tv/Breaking Bad/Season 01",
            HostFolder = "/media/tv/Breaking Bad/Season 01",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = TvFolderId.ToString(),
            EpisodeId = 62086,
        };
        context.VideoFiles.AddRange(entities: [tvVideoFile1, tvVideoFile2]);

        context.SaveChanges();

        // Step 6: Music entities — library, folder, artist, album, tracks, playlist, genre
        Library musicLibrary = new()
        {
            Id = MusicLibraryId,
            Title = "Music",
            Type = "music",
            Order = 3,
        };
        context.Libraries.Add(entity: musicLibrary);

        Folder musicFolder = new()
        {
            Id = MusicFolderId,
            Path = "/media/music",
            DriverId = Driver.SystemLocalDriverId,
        };
        context.Folders.Add(entity: musicFolder);

        context.SaveChanges();

        context.LibraryUser.Add(entity: new(libraryId: MusicLibraryId, userId: TestAuthHandler.DefaultUserId));
        context.FolderLibrary.Add(entity: new(folderId: MusicFolderId, libraryId: MusicLibraryId));

        MusicGenre rockGenre = new() { Id = MusicGenreId1, Name = "Rock" };
        context.MusicGenres.Add(entity: rockGenre);

        Artist artist1 = new()
        {
            Id = ArtistId1,
            Name = "Test Artist",
            TitleSort = "test artist",
            Description = "A test artist for snapshot testing",
            Cover = "/test-artist.jpg",
            HostFolder = "/media/music/Test Artist",
            LibraryId = MusicLibraryId,
            FolderId = MusicFolderId,
        };
        context.Artists.Add(entity: artist1);

        Album album1 = new()
        {
            Id = AlbumId1,
            Name = "Test Album",
            Description = "A test album",
            Cover = "/test-album.jpg",
            Year = 2020,
            Tracks = 2,
            HostFolder = "/media/music/Test Artist/Test Album",
            LibraryId = MusicLibraryId,
            FolderId = MusicFolderId,
            LibraryFolder = null!,
        };
        context.Albums.Add(entity: album1);

        context.SaveChanges();

        Track track1 = new()
        {
            Id = TrackId1,
            Name = "Test Track 1",
            TrackNumber = 1,
            DiscNumber = 1,
            Duration = "3:45",
            Filename = "01-test-track-1.flac",
            Folder = "/media/music/Test Artist/Test Album",
            HostFolder = "/media/music/Test Artist/Test Album",
            FolderId = MusicFolderId,
        };
        Track track2 = new()
        {
            Id = TrackId2,
            Name = "Test Track 2",
            TrackNumber = 2,
            DiscNumber = 1,
            Duration = "4:20",
            Filename = "02-test-track-2.flac",
            Folder = "/media/music/Test Artist/Test Album",
            HostFolder = "/media/music/Test Artist/Test Album",
            FolderId = MusicFolderId,
        };
        context.Tracks.AddRange(entities: [track1, track2]);

        context.SaveChanges();

        // Step 7: Music join tables
        context.ArtistTrack.AddRange(entities: [new ArtistTrack { ArtistId = ArtistId1, TrackId = TrackId1 }, new ArtistTrack { ArtistId = ArtistId1, TrackId = TrackId2 }]
        );

        context.AlbumTrack.AddRange(entities: [new AlbumTrack { AlbumId = AlbumId1, TrackId = TrackId1 }, new AlbumTrack { AlbumId = AlbumId1, TrackId = TrackId2 }]
        );

        context.AlbumArtist.Add(entity: new() { AlbumId = AlbumId1, ArtistId = ArtistId1 });

        context.ArtistLibrary.Add(entity: new(artistId: ArtistId1, libraryId: MusicLibraryId));
        context.AlbumLibrary.Add(entity: new(albumId: AlbumId1, libraryId: MusicLibraryId));

        context.LibraryTrack.AddRange(entities: [new LibraryTrack { LibraryId = MusicLibraryId, TrackId = TrackId1 }, new LibraryTrack { LibraryId = MusicLibraryId, TrackId = TrackId2 }]
        );

        context.ArtistMusicGenre.Add(entity: new() { ArtistId = ArtistId1, MusicGenreId = MusicGenreId1 });

        Playlist playlist1 = new()
        {
            Id = PlaylistId1,
            Name = "Test Playlist",
            Description = "A test playlist",
            UserId = TestAuthHandler.DefaultUserId,
        };
        context.Playlists.Add(entity: playlist1);

        context.SaveChanges();

        context.PlaylistTrack.Add(entity: new() { PlaylistId = PlaylistId1, TrackId = TrackId1 });

        // Favorite the artist/track so favorites endpoints have data
        context.ArtistUser.Add(
            entity: new() { ArtistId = ArtistId1, UserId = TestAuthHandler.DefaultUserId }
        );
        context.TrackUser.Add(entity: new() { TrackId = TrackId1, UserId = TestAuthHandler.DefaultUserId });

        context.SaveChanges();

        // Step 8: A collection and a special so GET /userData/favorites has all four
        // video media types (movie, tv, collection, special) to browse, not just movie/tv.
        Collection favoriteCollection = new()
        {
            Id = FavoriteCollectionId,
            Title = "Test Collection",
            TitleSort = "test collection",
            LibraryId = MovieLibraryId,
            Parts = 1,
        };
        context.Collections.Add(entity: favoriteCollection);

        Special favoriteSpecial = new() { Id = FavoriteSpecialId, Title = "Test Special" };
        context.Specials.Add(entity: favoriteSpecial);

        context.SaveChanges();

        context.CollectionMovie.Add(entity: new(collectionId: FavoriteCollectionId, movieId: movie1.Id));
        context.SpecialItems.Add(entity: new() { SpecialId = FavoriteSpecialId, MovieId = movie1.Id });

        context.SaveChanges();

        // Favorite movie 129, tv 1399, the collection and the special so
        // GET /userData/favorites has one of each type to browse.
        context.MovieUser.Add(entity: new(movieId: movie1.Id, userId: TestAuthHandler.DefaultUserId));
        context.TvUser.Add(entity: new(tvId: show1.Id, userId: TestAuthHandler.DefaultUserId));
        context.CollectionUser.Add(entity: new(collectionId: FavoriteCollectionId, userId: TestAuthHandler.DefaultUserId));
        context.SpecialUser.Add(entity: new(specialId: FavoriteSpecialId, userId: TestAuthHandler.DefaultUserId));

        context.SaveChanges();
    }

    private static void RemoveHostedServices(IServiceCollection services)
    {
        List<ServiceDescriptor> hostedServices = services
            .Where(predicate: d => d.ServiceType == typeof(IHostedService))
            .ToList();

        foreach (ServiceDescriptor descriptor in hostedServices)
            services.Remove(item: descriptor);
    }

    private static void ReplaceSetupState(IServiceCollection services)
    {
        services.RemoveAll<SetupState>();
        SetupState completedState = new();
        completedState.DetermineInitialPhase(hasValidToken: true);
        services.AddSingleton(implementationInstance: completedState);

        // AuthManager is registered as a singleton that creates its own AppDbContext scope.
        // In tests, app.db is already seeded — provide a standalone instance with its own
        // AppDbContext so the real DI scope factory pattern doesn't run and hit a missing DB.
        services.RemoveAll<AuthManager>();
        services.RemoveAll<SetupEndpoints>();
        services.RemoveAll<BootOrchestrator>();

        AppDbContext testAppContext = new();
        testAppContext.Database.EnsureCreated();

        AuthManager testAuthManager = new(
            appContext: testAppContext,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore()
        );
        services.AddSingleton(implementationInstance: testAuthManager);
        IServerRegistrationService registrationService = Mock.Of<IServerRegistrationService>();
        services.AddSingleton(
            implementationInstance: new SetupEndpoints(state: completedState, authManager: testAuthManager, serverRegistrationService: registrationService)
        );
        services.AddSingleton(
            implementationInstance: new BootOrchestrator(
                setupState: completedState,
                authManager: testAuthManager,
                apiKeyLoader: Mock.Of<IApiKeyLoader>(),
                degradedModeRecovery: Mock.Of<IDegradedModeRecovery>(),
                serverRegistrationService: registrationService,
                authTokenStore: new AuthTokenStore(),
                certificateService: new CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: null!)
            )
        );
    }

    private void ReplacePluginManager(IServiceCollection services)
    {
        services.RemoveAll<IPluginManager>();
        services.AddSingleton(implementationInstance: CreateTestPluginManager());
    }

    private static void ReplaceAuth(IServiceCollection services)
    {
        services.RemoveAll<IAuthenticationSchemeProvider>();
        services.RemoveAll<IAuthenticationHandlerProvider>();

        services
            .AddAuthentication(defaultScheme: TestAuthDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                authenticationScheme: TestAuthDefaults.AuthenticationScheme,
                configureOptions: _ => { }
            );

        services
            .AddAuthorizationBuilder()
            .SetDefaultPolicy(
                policy: new AuthorizationPolicyBuilder(authenticationSchemes: TestAuthDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build()
            )
            .AddPolicy(
                name: "api",
                policy: new AuthorizationPolicyBuilder(authenticationSchemes: TestAuthDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build()
            );
    }

    private sealed class NoOpSunsetPolicyManager : ISunsetPolicyManager
    {
        public bool TryGetPolicy(
            string? name,
            ApiVersion? apiVersion,
            out SunsetPolicy sunsetPolicy
        )
        {
            sunsetPolicy = default!;
            return false;
        }
    }

    private sealed class StubPluginManager : IPluginManager
    {
        public IReadOnlyList<PluginInfo> GetInstalledPlugins() => Array.Empty<PluginInfo>();

        public Task InstallPluginAsync(string packageUrl, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task EnablePluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DisablePluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UninstallPluginAsync(Guid pluginId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<PluginLoadResult>> LoadAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PluginLoadResult>>(result: Array.Empty<PluginLoadResult>());

        public IEnumerable<T> GetPluginsOfType<T>()
            where T : IPlugin => Array.Empty<T>();
    }
}
