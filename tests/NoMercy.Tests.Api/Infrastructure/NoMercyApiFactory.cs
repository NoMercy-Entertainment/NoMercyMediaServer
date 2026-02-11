using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Server;

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
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            RemoveHostedServices(services);
            ReplaceAuth(services);
        });
    }

    protected override IWebHostBuilder? CreateWebHostBuilder()
    {
        return Microsoft.AspNetCore.WebHost.CreateDefaultBuilder([])
            .ConfigureLogging(logging => logging.ClearProviders())
            .UseStartup<Startup>()
            .ConfigureServices(services =>
            {
                services.AddSingleton(new StartupOptions());
                services.AddSingleton<ISunsetPolicyManager>(new NoOpSunsetPolicyManager());
                services.AddSingleton<IApiVersionDescriptionProvider, DefaultApiVersionDescriptionProvider>();

                services.AddSingleton(
                    typeof(Microsoft.Extensions.Logging.ILogger<>),
                    typeof(CustomLogger<>));
            });
    }

    public static readonly Ulid MovieLibraryId = Ulid.NewUlid();
    public static readonly Ulid TvLibraryId = Ulid.NewUlid();
    public static readonly Ulid MusicLibraryId = Ulid.NewUlid();
    public static readonly Ulid MovieFolderId = Ulid.NewUlid();
    public static readonly Ulid MusicFolderId = Ulid.NewUlid();

    public static readonly Guid ArtistId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AlbumId1 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid TrackId1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid TrackId2 = Guid.Parse("33333333-3333-3333-3333-333333333334");
    public static readonly Guid PlaylistId1 = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid MusicGenreId1 = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static void EnsureDirectoriesAndSeedDatabase()
    {
        foreach (string path in AppFiles.AllPaths())
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        string mediaDbPath = Path.Combine(AppFiles.DataPath, "media.db");
        foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            string file = mediaDbPath + suffix;
            if (File.Exists(file))
                File.Delete(file);
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
                Manage = true
            };
            mediaContext.Users.Add(testUser);
            mediaContext.SaveChanges();
        }

        SeedMediaData(mediaContext);

        ClaimsPrincipleExtensions.Initialize(mediaContext);

        string queueDbPath = Path.Combine(AppFiles.DataPath, "queue.db");
        foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            string file = queueDbPath + suffix;
            if (File.Exists(file))
                File.Delete(file);
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
            Order = 1
        };
        Library tvLibrary = new()
        {
            Id = TvLibraryId,
            Title = "TV Shows",
            Type = "tv",
            Order = 2
        };
        context.Libraries.AddRange(movieLibrary, tvLibrary);

        Folder movieFolder = new()
        {
            Id = MovieFolderId,
            Path = "/media/movies"
        };
        context.Folders.Add(movieFolder);

        Genre actionGenre = new() { Id = 28, Name = "Action" };
        Genre dramaGenre = new() { Id = 18, Name = "Drama" };
        context.Genres.AddRange(actionGenre, dramaGenre);

        context.SaveChanges();

        // Step 2: Entities with FK to libraries/folders/user
        context.LibraryUser.AddRange(
            new LibraryUser(MovieLibraryId, TestAuthHandler.DefaultUserId),
            new LibraryUser(TvLibraryId, TestAuthHandler.DefaultUserId));

        context.FolderLibrary.Add(new FolderLibrary(MovieFolderId, MovieLibraryId));

        Movie movie1 = new()
        {
            Id = 550,
            Title = "Fight Club",
            TitleSort = "fight club",
            Overview = "An insomniac office worker and a devil-may-care soap maker form an underground fight club.",
            Poster = "/pB8BM7pdSp6B6Ih7QZ4DrQ3PmJK.jpg",
            Backdrop = "/hZkgoQYus5dXo3H8T7Uef6DNknx.jpg",
            ReleaseDate = new DateTime(1999, 10, 15),
            LibraryId = MovieLibraryId,
            VoteAverage = 8.4
        };
        Movie movie2 = new()
        {
            Id = 680,
            Title = "Pulp Fiction",
            TitleSort = "pulp fiction",
            Overview = "The lives of two mob hitmen intertwine in four tales of violence and redemption.",
            Poster = "/d5iIlFn5s0ImszYzBPb8JPIfbXD.jpg",
            Backdrop = "/suaEOtk1N1sgg2MTM7oZd2cfVp3.jpg",
            ReleaseDate = new DateTime(1994, 9, 10),
            LibraryId = MovieLibraryId,
            VoteAverage = 8.5
        };
        context.Movies.AddRange(movie1, movie2);

        Tv show1 = new()
        {
            Id = 1399,
            Title = "Breaking Bad",
            TitleSort = "breaking bad",
            Overview = "A chemistry teacher teams up with a former student to cook and sell crystal meth.",
            Poster = "/ggFHVNu6YYI5L9pCfOacjizRGt.jpg",
            Backdrop = "/tsRy63Mu5cu8etL1X7ZLyf7UP1M.jpg",
            FirstAirDate = new DateTime(2008, 1, 20),
            NumberOfEpisodes = 62,
            NumberOfSeasons = 5,
            LibraryId = TvLibraryId,
            VoteAverage = 8.9
        };
        context.Tvs.Add(show1);

        context.SaveChanges();

        // Step 3: Join tables and child entities (FK to movies/tv/genres)
        context.LibraryMovie.AddRange(
            new LibraryMovie(MovieLibraryId, 550),
            new LibraryMovie(MovieLibraryId, 680));

        context.LibraryTv.Add(new LibraryTv(TvLibraryId, 1399));

        context.GenreMovie.AddRange(
            new GenreMovie { GenreId = 28, MovieId = 550 },
            new GenreMovie { GenreId = 18, MovieId = 550 },
            new GenreMovie { GenreId = 18, MovieId = 680 });

        context.GenreTv.Add(new GenreTv { GenreId = 18, TvId = 1399 });

        Season season1 = new()
        {
            Id = 3572,
            Title = "Season 1",
            SeasonNumber = 1,
            EpisodeCount = 7,
            TvId = 1399
        };
        context.Seasons.Add(season1);

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
            Overview = "Walter White is diagnosed with advanced lung cancer."
        };
        Episode episode2 = new()
        {
            Id = 62086,
            Title = "Cat's in the Bag...",
            EpisodeNumber = 2,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
            Overview = "Walt and Jesse deal with a corpse and a prisoner."
        };
        context.Episodes.AddRange(episode1, episode2);

        VideoFile movieVideoFile1 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Fight.Club.1999.1080p.mkv",
            Folder = "/media/movies/Fight Club (1999)",
            HostFolder = "/media/movies/Fight Club (1999)",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = "movies",
            MovieId = 550
        };
        VideoFile movieVideoFile2 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Pulp.Fiction.1994.1080p.mkv",
            Folder = "/media/movies/Pulp Fiction (1994)",
            HostFolder = "/media/movies/Pulp Fiction (1994)",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = "movies",
            MovieId = 680
        };
        context.VideoFiles.AddRange(movieVideoFile1, movieVideoFile2);

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
            Share = "tv",
            EpisodeId = 62085
        };
        VideoFile tvVideoFile2 = new()
        {
            Id = Ulid.NewUlid(),
            Filename = "Breaking.Bad.S01E02.mkv",
            Folder = "/media/tv/Breaking Bad/Season 01",
            HostFolder = "/media/tv/Breaking Bad/Season 01",
            Languages = "[\"en\"]",
            Quality = "1080p",
            Share = "tv",
            EpisodeId = 62086
        };
        context.VideoFiles.AddRange(tvVideoFile1, tvVideoFile2);

        context.SaveChanges();

        // Step 6: Music entities — library, folder, artist, album, tracks, playlist, genre
        Library musicLibrary = new()
        {
            Id = MusicLibraryId,
            Title = "Music",
            Type = "music",
            Order = 3
        };
        context.Libraries.Add(musicLibrary);

        Folder musicFolder = new()
        {
            Id = MusicFolderId,
            Path = "/media/music"
        };
        context.Folders.Add(musicFolder);

        context.SaveChanges();

        context.LibraryUser.Add(new LibraryUser(MusicLibraryId, TestAuthHandler.DefaultUserId));
        context.FolderLibrary.Add(new FolderLibrary(MusicFolderId, MusicLibraryId));

        MusicGenre rockGenre = new()
        {
            Id = MusicGenreId1,
            Name = "Rock"
        };
        context.MusicGenres.Add(rockGenre);

        Artist artist1 = new()
        {
            Id = ArtistId1,
            Name = "Test Artist",
            TitleSort = "test artist",
            Description = "A test artist for snapshot testing",
            Cover = "/test-artist.jpg",
            HostFolder = "/media/music/Test Artist",
            LibraryId = MusicLibraryId,
            FolderId = MusicFolderId
        };
        context.Artists.Add(artist1);

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
            FolderId = MusicFolderId
        };
        context.Albums.Add(album1);

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
            FolderId = MusicFolderId
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
            FolderId = MusicFolderId
        };
        context.Tracks.AddRange(track1, track2);

        context.SaveChanges();

        // Step 7: Music join tables
        context.ArtistTrack.AddRange(
            new ArtistTrack { ArtistId = ArtistId1, TrackId = TrackId1 },
            new ArtistTrack { ArtistId = ArtistId1, TrackId = TrackId2 });

        context.AlbumTrack.AddRange(
            new AlbumTrack { AlbumId = AlbumId1, TrackId = TrackId1 },
            new AlbumTrack { AlbumId = AlbumId1, TrackId = TrackId2 });

        context.AlbumArtist.Add(new AlbumArtist { AlbumId = AlbumId1, ArtistId = ArtistId1 });

        context.ArtistLibrary.Add(new ArtistLibrary(ArtistId1, MusicLibraryId));
        context.AlbumLibrary.Add(new AlbumLibrary(AlbumId1, MusicLibraryId));

        context.LibraryTrack.AddRange(
            new LibraryTrack { LibraryId = MusicLibraryId, TrackId = TrackId1 },
            new LibraryTrack { LibraryId = MusicLibraryId, TrackId = TrackId2 });

        context.ArtistMusicGenre.Add(
            new ArtistMusicGenre { ArtistId = ArtistId1, MusicGenreId = MusicGenreId1 });

        Playlist playlist1 = new()
        {
            Id = PlaylistId1,
            Name = "Test Playlist",
            Description = "A test playlist",
            UserId = TestAuthHandler.DefaultUserId
        };
        context.Playlists.Add(playlist1);

        context.SaveChanges();

        context.PlaylistTrack.Add(
            new PlaylistTrack { PlaylistId = PlaylistId1, TrackId = TrackId1 });

        // Favorite the artist/track so favorites endpoints have data
        context.ArtistUser.Add(
            new ArtistUser { ArtistId = ArtistId1, UserId = TestAuthHandler.DefaultUserId });
        context.TrackUser.Add(
            new TrackUser { TrackId = TrackId1, UserId = TestAuthHandler.DefaultUserId });

        context.SaveChanges();
    }

    private static void RemoveHostedServices(IServiceCollection services)
    {
        List<ServiceDescriptor> hostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        foreach (ServiceDescriptor descriptor in hostedServices)
            services.Remove(descriptor);
    }

    private static void ReplaceAuth(IServiceCollection services)
    {
        services.RemoveAll<IAuthenticationSchemeProvider>();
        services.RemoveAll<IAuthenticationHandlerProvider>();

        services.AddAuthentication(TestAuthDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthDefaults.AuthenticationScheme, _ => { });

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(TestAuthDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy("api", new AuthorizationPolicyBuilder(TestAuthDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build());
    }

    private sealed class NoOpSunsetPolicyManager : ISunsetPolicyManager
    {
        public bool TryGetPolicy(string? name, ApiVersion apiVersion, out SunsetPolicy sunsetPolicy)
        {
            sunsetPolicy = default;
            return false;
        }
    }
}
