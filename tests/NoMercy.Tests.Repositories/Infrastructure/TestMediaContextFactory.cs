using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Tests.Repositories.Infrastructure;

public class TestDbContextFactory : IDbContextFactory<MediaContext>
{
    private readonly DbContextOptions<MediaContext> _options;

    public TestDbContextFactory(DbContextOptions<MediaContext> options)
    {
        _options = options;
    }

    public MediaContext CreateDbContext()
    {
        return new TestMediaContext(_options);
    }
}

public static class TestMediaContextFactory
{
    public static MediaContext CreateContext(string? databaseName = null)
    {
        string dbName = databaseName ?? Guid.NewGuid().ToString();
        SqliteConnection connection = new($"DataSource={dbName};Mode=Memory;Cache=Shared");
        connection.Open();
        connection.CreateFunction("normalize_search", (string? input) =>
            input?.NormalizeSearch() ?? string.Empty);

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(new SqliteNormalizeSearchInterceptor())
            .Options;

        TestMediaContext context = new(options);
        context.Database.EnsureCreated();

        return context;
    }

    public static MediaContext CreateSeededContext()
    {
        MediaContext context = CreateContext();
        SeedData(context);
        return context;
    }

    public static (MediaContext Context, SqlCaptureInterceptor Interceptor) CreateContextWithInterceptor(string? databaseName = null)
    {
        string dbName = databaseName ?? Guid.NewGuid().ToString();
        SqliteConnection connection = new($"DataSource={dbName};Mode=Memory;Cache=Shared");
        connection.Open();
        connection.CreateFunction("normalize_search", (string? input) =>
            input?.NormalizeSearch() ?? string.Empty);

        SqlCaptureInterceptor interceptor = new();
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connection, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(interceptor, new SqliteNormalizeSearchInterceptor())
            .Options;

        TestMediaContext context = new(options);
        context.Database.EnsureCreated();

        return (context, interceptor);
    }

    public static (MediaContext Context, SqlCaptureInterceptor Interceptor) CreateSeededContextWithInterceptor()
    {
        (MediaContext context, SqlCaptureInterceptor interceptor) = CreateContextWithInterceptor();
        SeedData(context);
        interceptor.Clear();
        return (context, interceptor);
    }

    public static (IDbContextFactory<MediaContext> Factory, SqliteConnection Connection) CreateFactory(string? databaseName = null)
    {
        string dbName = databaseName ?? Guid.NewGuid().ToString();
        string connectionString = $"DataSource={dbName};Mode=Memory;Cache=Shared";

        // Keep a connection open to prevent the in-memory database from being destroyed
        SqliteConnection keepAliveConnection = new(connectionString);
        keepAliveConnection.Open();
        keepAliveConnection.CreateFunction("normalize_search", (string? input) =>
            input?.NormalizeSearch() ?? string.Empty);

        // Enable WAL mode so concurrent connections don't block on CreateFunction
        using (SqliteCommand walCmd = keepAliveConnection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteNonQuery();
        }

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(connectionString, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(new SqliteNormalizeSearchInterceptor())
            .Options;

        // Ensure the schema is created
        using (TestMediaContext initContext = new(options))
        {
            initContext.Database.EnsureCreated();
        }

        return (new TestDbContextFactory(options), keepAliveConnection);
    }

    public static void SeedData(MediaContext context)
    {
        User testUser = new()
        {
            Id = SeedConstants.UserId,
            Email = "test@nomercy.tv",
            Name = "Test User",
            Owner = true,
            Allowed = true,
            Manage = true
        };
        context.Users.Add(testUser);

        Library movieLibrary = new()
        {
            Id = SeedConstants.MovieLibraryId,
            Title = "Movies",
            Type = "movie",
            Order = 1
        };
        context.Libraries.Add(movieLibrary);

        Library tvLibrary = new()
        {
            Id = SeedConstants.TvLibraryId,
            Title = "TV Shows",
            Type = "tv",
            Order = 2
        };
        context.Libraries.Add(tvLibrary);

        context.LibraryUser.Add(new(SeedConstants.MovieLibraryId, SeedConstants.UserId));
        context.LibraryUser.Add(new(SeedConstants.TvLibraryId, SeedConstants.UserId));

        Folder movieFolder = new()
        {
            Id = SeedConstants.MovieFolderId,
            Path = "/media/movies"
        };
        context.Folders.Add(movieFolder);
        context.FolderLibrary.Add(new(SeedConstants.MovieFolderId, SeedConstants.MovieLibraryId));

        EncoderProfile encoderProfile = new()
        {
            Id = SeedConstants.EncoderProfileId,
            Name = "Default HLS",
            Container = "hls"
        };
        context.EncoderProfiles.Add(encoderProfile);
        context.EncoderProfileFolder.Add(new(SeedConstants.EncoderProfileId, SeedConstants.MovieFolderId));

        Language english = new()
        {
            Id = 1,
            Iso6391 = "en",
            EnglishName = "English",
            Name = "English"
        };
        context.Languages.Add(english);
        context.LanguageLibrary.Add(new(1, SeedConstants.MovieLibraryId));

        Genre actionGenre = new() { Id = 28, Name = "Action" };
        Genre dramaGenre = new() { Id = 18, Name = "Drama" };
        context.Genres.AddRange(actionGenre, dramaGenre);

        Movie movie1 = new()
        {
            Id = 129,
            Title = "Spirited Away",
            TitleSort = "spirited away",
            Overview = "A young girl, Chihiro, becomes trapped in a strange new world of spirits. When her parents undergo a mysterious transformation, she must call upon the courage she never knew she had to free her family.",
            Poster = "/39wmItIWsg5sZMyRUHLkWBcuVCM.jpg",
            Backdrop = "/Ab8mkHmkYADjU7wQiOkia9BzGvS.jpg",
            ReleaseDate = new DateTime(2001, 7, 20),
            LibraryId = SeedConstants.MovieLibraryId,
            VoteAverage = 8.5
        };

        Movie movie2 = new()
        {
            Id = 680,
            Title = "Pulp Fiction",
            TitleSort = "pulp fiction",
            Overview = "The lives of two mob hitmen, a boxer, a gangster and his wife intertwine in four tales of violence and redemption.",
            Poster = "/d5iIlFn5s0ImszYzBPb8JPIfbXD.jpg",
            Backdrop = "/suaEOtk1N1sgg2MTM7oZd2cfVp3.jpg",
            ReleaseDate = new DateTime(1994, 9, 10),
            LibraryId = SeedConstants.MovieLibraryId,
            VoteAverage = 8.5
        };
        context.Movies.AddRange(movie1, movie2);

        context.LibraryMovie.AddRange(
            new LibraryMovie(SeedConstants.MovieLibraryId, 129),
            new LibraryMovie(SeedConstants.MovieLibraryId, 680));

        VideoFile movieVideoFile1 = new()
        {
            Id = SeedConstants.MovieVideoFile1Id,
            Filename = "Spirited.Away.2001.1080p.mkv",
            Folder = "/media/movies/Spirited Away (2001)",
            HostFolder = "/media/movies/Spirited Away (2001)",
            Languages = "en",
            Quality = "1080p",
            Share = "movies",
            MovieId = 129
        };
        VideoFile movieVideoFile2 = new()
        {
            Id = SeedConstants.MovieVideoFile2Id,
            Filename = "Pulp.Fiction.1994.1080p.mkv",
            Folder = "/media/movies/Pulp Fiction (1994)",
            HostFolder = "/media/movies/Pulp Fiction (1994)",
            Languages = "en",
            Quality = "1080p",
            Share = "movies",
            MovieId = 680
        };
        context.VideoFiles.AddRange(movieVideoFile1, movieVideoFile2);

        context.GenreMovie.AddRange(
            new GenreMovie { GenreId = 28, MovieId = 129 },
            new GenreMovie { GenreId = 18, MovieId = 129 },
            new GenreMovie { GenreId = 18, MovieId = 680 });

        Tv show1 = new()
        {
            Id = 1399,
            Title = "Breaking Bad",
            TitleSort = "breaking bad",
            Overview = "A chemistry teacher diagnosed with lung cancer teams up with a former student to cook and sell crystal meth.",
            Poster = "/ggFHVNu6YYI5L9pCfOacjizRGt.jpg",
            Backdrop = "/tsRy63Mu5cu8etL1X7ZLyf7UP1M.jpg",
            FirstAirDate = new DateTime(2008, 1, 20),
            NumberOfEpisodes = 62,
            NumberOfSeasons = 5,
            LibraryId = SeedConstants.TvLibraryId,
            VoteAverage = 8.9
        };
        context.Tvs.Add(show1);

        context.LibraryTv.Add(new(SeedConstants.TvLibraryId, 1399));

        Season season1 = new()
        {
            Id = 3572,
            Title = "Season 1",
            SeasonNumber = 1,
            EpisodeCount = 7,
            TvId = 1399
        };
        context.Seasons.Add(season1);

        Episode episode1 = new()
        {
            Id = 62085,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
            Overview = "Walter White, a struggling high school chemistry teacher, is diagnosed with advanced lung cancer."
        };
        Episode episode2 = new()
        {
            Id = 62086,
            Title = "Cat's in the Bag...",
            EpisodeNumber = 2,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
            Overview = "After their decaying RV breaks down, Walt and Jesse are forced to deal with a corpse and a prisoner."
        };
        context.Episodes.AddRange(episode1, episode2);

        VideoFile tvVideoFile1 = new()
        {
            Id = SeedConstants.TvVideoFile1Id,
            Filename = "Breaking.Bad.S01E01.mkv",
            Folder = "/media/tv/Breaking Bad (2008)/Season 01",
            HostFolder = "/media/tv/Breaking Bad (2008)/Season 01",
            Languages = "en",
            Quality = "1080p",
            Share = "tv",
            EpisodeId = 62085
        };
        VideoFile tvVideoFile2 = new()
        {
            Id = SeedConstants.TvVideoFile2Id,
            Filename = "Breaking.Bad.S01E02.mkv",
            Folder = "/media/tv/Breaking Bad (2008)/Season 01",
            HostFolder = "/media/tv/Breaking Bad (2008)/Season 01",
            Languages = "en",
            Quality = "1080p",
            Share = "tv",
            EpisodeId = 62086
        };
        context.VideoFiles.AddRange(tvVideoFile1, tvVideoFile2);

        context.GenreTv.AddRange(
            new GenreTv { GenreId = 18, TvId = 1399 });

        // UserData for continue watching tests
        context.UserData.AddRange(
            new UserData
            {
                Id = Ulid.Parse("01JABC0000000000000000MOVI"),
                UserId = SeedConstants.UserId,
                MovieId = 129,
                VideoFileId = SeedConstants.MovieVideoFile1Id,
                Type = "movie",
                Time = 3600,
                LastPlayedDate = "2026-02-01T10:00:00Z"
            },
            // Duplicate entry for same movie (different video file)
            new UserData
            {
                Id = Ulid.Parse("01JDBC0000000000000000MDUP"),
                UserId = SeedConstants.UserId,
                MovieId = 129,
                VideoFileId = SeedConstants.MovieVideoFile2Id,
                Type = "movie",
                Time = 1800,
                LastPlayedDate = "2026-01-15T08:00:00Z"
            },
            new UserData
            {
                Id = Ulid.Parse("01JBBC0000000000000000TVSH"),
                UserId = SeedConstants.UserId,
                TvId = 1399,
                VideoFileId = SeedConstants.TvVideoFile1Id,
                Type = "tv",
                Time = 2400,
                LastPlayedDate = "2026-02-02T14:00:00Z"
            }
        );

        context.SaveChanges();
    }
}
