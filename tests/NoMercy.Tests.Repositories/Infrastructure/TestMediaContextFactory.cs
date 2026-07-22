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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Storage;
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
        return new TestMediaContext(options: _options);
    }
}

public static class TestMediaContextFactory
{
    public static MediaContext CreateContext(string? databaseName = null)
    {
        string dbName = databaseName ?? Guid.NewGuid().ToString();
        SqliteConnection connection = new(
            connectionString: $"DataSource={dbName};Mode=Memory;Cache=Shared;Foreign Keys=True"
        );
        connection.Open();
        connection.CreateFunction(
            name: "normalize_search",
            function: (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connection: connection,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: new SqliteNormalizeSearchInterceptor())
            .Options;

        TestMediaContext context = new(options: options);
        context.Database.EnsureCreated();

        return context;
    }

    public static MediaContext CreateSeededContext()
    {
        MediaContext context = CreateContext();
        SeedData(context: context);
        return context;
    }

    public static (
        MediaContext Context,
        SqlCaptureInterceptor Interceptor
    ) CreateContextWithInterceptor(string? databaseName = null)
    {
        string dbName = databaseName ?? Guid.NewGuid().ToString();
        SqliteConnection connection = new(
            connectionString: $"DataSource={dbName};Mode=Memory;Cache=Shared;Foreign Keys=True"
        );
        connection.Open();
        connection.CreateFunction(
            name: "normalize_search",
            function: (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        SqlCaptureInterceptor interceptor = new();
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connection: connection,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: [interceptor, new SqliteNormalizeSearchInterceptor()])
            .Options;

        TestMediaContext context = new(options: options);
        context.Database.EnsureCreated();

        return (context, interceptor);
    }

    public static (
        MediaContext Context,
        SqlCaptureInterceptor Interceptor
    ) CreateSeededContextWithInterceptor()
    {
        (MediaContext context, SqlCaptureInterceptor interceptor) = CreateContextWithInterceptor();
        SeedData(context: context);
        interceptor.Clear();
        return (context, interceptor);
    }

    public static (
        IDbContextFactory<MediaContext> Factory,
        SqliteConnection Connection
    ) CreateFactory(string? databaseName = null)
    {
        string dbName = databaseName ?? Guid.NewGuid().ToString();
        string connectionString = $"DataSource={dbName};Mode=Memory;Cache=Shared;Foreign Keys=True";

        // Keep a connection open to prevent the in-memory database from being destroyed
        SqliteConnection keepAliveConnection = new(connectionString: connectionString);
        keepAliveConnection.Open();
        keepAliveConnection.CreateFunction(
            name: "normalize_search",
            function: (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        // Enable WAL mode so concurrent connections don't block on CreateFunction
        using (SqliteCommand walCmd = keepAliveConnection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteNonQuery();
        }

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connectionString: connectionString,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: new SqliteNormalizeSearchInterceptor())
            .Options;

        // Ensure the schema is created
        using (TestMediaContext initContext = new(options: options))
        {
            initContext.Database.EnsureCreated();
        }

        return (new TestDbContextFactory(options: options), keepAliveConnection);
    }

    // Mirrors CreateFactory exactly, plus a caller-supplied interceptor — for tests that need
    // to assert on the SHAPE of the generated SQL (e.g. query count), not just the result.
    public static (
        IDbContextFactory<MediaContext> Factory,
        SqliteConnection Connection
    ) CreateFactoryWithInterceptor(SqlCaptureInterceptor interceptor, string? databaseName = null)
    {
        string dbName = databaseName ?? Guid.NewGuid().ToString();
        string connectionString = $"DataSource={dbName};Mode=Memory;Cache=Shared;Foreign Keys=True";

        SqliteConnection keepAliveConnection = new(connectionString: connectionString);
        keepAliveConnection.Open();
        keepAliveConnection.CreateFunction(
            name: "normalize_search",
            function: (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        using (SqliteCommand walCmd = keepAliveConnection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteNonQuery();
        }

        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connectionString: connectionString,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: [interceptor, new SqliteNormalizeSearchInterceptor()])
            .Options;

        using (TestMediaContext initContext = new(options: options))
        {
            initContext.Database.EnsureCreated();
        }

        return (new TestDbContextFactory(options: options), keepAliveConnection);
    }

    public static (
        IDbContextFactory<MediaContext> Factory,
        SqliteConnection Connection
    ) CreateSeededFactory(string? databaseName = null)
    {
        (IDbContextFactory<MediaContext> factory, SqliteConnection connection) = CreateFactory(
            databaseName: databaseName
        );
        using (MediaContext context = factory.CreateDbContext())
        {
            SeedData(context: context);
        }

        return (factory, connection);
    }

    public static (
        IDbContextFactory<MediaContext> Factory,
        SqlCaptureInterceptor Interceptor,
        SqliteConnection Connection
    ) CreateSeededFactoryWithInterceptor(string? databaseName = null)
    {
        string dbName = databaseName ?? Guid.NewGuid().ToString();
        string connectionString = $"DataSource={dbName};Mode=Memory;Cache=Shared;Foreign Keys=True";

        SqliteConnection keepAliveConnection = new(connectionString: connectionString);
        keepAliveConnection.Open();
        keepAliveConnection.CreateFunction(
            name: "normalize_search",
            function: (string? input) => input?.NormalizeSearch() ?? string.Empty
        );

        using (SqliteCommand walCmd = keepAliveConnection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteNonQuery();
        }

        SqlCaptureInterceptor interceptor = new();
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connectionString: connectionString,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .AddInterceptors(interceptors: [interceptor, new SqliteNormalizeSearchInterceptor()])
            .Options;

        using (TestMediaContext initContext = new(options: options))
        {
            initContext.Database.EnsureCreated();
            SeedData(context: initContext);
        }

        interceptor.Clear();
        return (new TestDbContextFactory(options: options), interceptor, keepAliveConnection);
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
            Manage = true,
        };
        context.Users.Add(entity: testUser);

        Library movieLibrary = new()
        {
            Id = SeedConstants.MovieLibraryId,
            Title = "Movies",
            Type = "movie",
            Order = 1,
        };
        context.Libraries.Add(entity: movieLibrary);

        Library tvLibrary = new()
        {
            Id = SeedConstants.TvLibraryId,
            Title = "TV Shows",
            Type = "tv",
            Order = 2,
        };
        context.Libraries.Add(entity: tvLibrary);

        context.LibraryUser.Add(entity: new(libraryId: SeedConstants.MovieLibraryId, userId: SeedConstants.UserId));
        context.LibraryUser.Add(entity: new(libraryId: SeedConstants.TvLibraryId, userId: SeedConstants.UserId));

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
            Id = SeedConstants.MovieFolderId,
            Path = "/media/movies",
            DriverId = Driver.SystemLocalDriverId,
        };
        context.Folders.Add(entity: movieFolder);
        context.FolderLibrary.Add(entity: new(folderId: SeedConstants.MovieFolderId, libraryId: SeedConstants.MovieLibraryId));

        EncodingPreset encodingPreset = new()
        {
            Id = SeedConstants.EncodingPresetId,
            Name = "Default HLS",
            ProfileJson = "{}",
            IsBuiltIn = false,
        };
        context.EncodingPresets.Add(entity: encodingPreset);
        context.EncodingPresetFolders.Add(
            entity: new()
            {
                PresetId = SeedConstants.EncodingPresetId,
                FolderId = SeedConstants.MovieFolderId,
                IsDefault = true,
            }
        );

        Language english = new()
        {
            Id = 1,
            Iso6391 = "en",
            EnglishName = "English",
            Name = "English",
        };
        context.Languages.Add(entity: english);
        context.LanguageLibrary.Add(entity: new(languageId: 1, libraryId: SeedConstants.MovieLibraryId));

        Genre actionGenre = new() { Id = 28, Name = "Action" };
        Genre dramaGenre = new() { Id = 18, Name = "Drama" };
        context.Genres.AddRange(entities: [actionGenre, dramaGenre]);

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
            LibraryId = SeedConstants.MovieLibraryId,
            VoteAverage = 8.5,
        };

        Movie movie2 = new()
        {
            Id = 680,
            Title = "Pulp Fiction",
            TitleSort = "pulp fiction",
            Overview =
                "The lives of two mob hitmen, a boxer, a gangster and his wife intertwine in four tales of violence and redemption.",
            Poster = "/d5iIlFn5s0ImszYzBPb8JPIfbXD.jpg",
            Backdrop = "/suaEOtk1N1sgg2MTM7oZd2cfVp3.jpg",
            ReleaseDate = new DateTime(year: 1994, month: 9, day: 10),
            LibraryId = SeedConstants.MovieLibraryId,
            VoteAverage = 8.5,
        };
        context.Movies.AddRange(entities: [movie1, movie2]);

        context.LibraryMovie.AddRange(entities: [new LibraryMovie(libraryId: SeedConstants.MovieLibraryId, movieId: 129), new LibraryMovie(libraryId: SeedConstants.MovieLibraryId, movieId: 680)]
        );

        VideoFile movieVideoFile1 = new()
        {
            Id = SeedConstants.MovieVideoFile1Id,
            Filename = "Spirited.Away.2001.1080p.mkv",
            Folder = "/media/movies/Spirited Away (2001)",
            HostFolder = "/media/movies/Spirited Away (2001)",
            Languages = "en",
            Quality = "1080p",
            Share = "movies",
            MovieId = 129,
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
            MovieId = 680,
        };
        context.VideoFiles.AddRange(entities: [movieVideoFile1, movieVideoFile2]);

        context.GenreMovie.AddRange(entities: [new GenreMovie { GenreId = 28, MovieId = 129 }, new GenreMovie { GenreId = 18, MovieId = 129 }, new GenreMovie { GenreId = 18, MovieId = 680 }]
        );

        Tv show1 = new()
        {
            Id = 1399,
            Title = "Breaking Bad",
            TitleSort = "breaking bad",
            Overview =
                "A chemistry teacher diagnosed with lung cancer teams up with a former student to cook and sell crystal meth.",
            Poster = "/ggFHVNu6YYI5L9pCfOacjizRGt.jpg",
            Backdrop = "/tsRy63Mu5cu8etL1X7ZLyf7UP1M.jpg",
            FirstAirDate = new DateTime(year: 2008, month: 1, day: 20),
            NumberOfEpisodes = 62,
            NumberOfSeasons = 5,
            LibraryId = SeedConstants.TvLibraryId,
            VoteAverage = 8.9,
        };
        context.Tvs.Add(entity: show1);

        context.LibraryTv.Add(entity: new(libraryId: SeedConstants.TvLibraryId, tvId: 1399));

        Season season1 = new()
        {
            Id = 3572,
            Title = "Season 1",
            SeasonNumber = 1,
            EpisodeCount = 7,
            TvId = 1399,
        };
        context.Seasons.Add(entity: season1);

        Episode episode1 = new()
        {
            Id = 62085,
            Title = "Pilot",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
            Overview =
                "Walter White, a struggling high school chemistry teacher, is diagnosed with advanced lung cancer.",
        };
        Episode episode2 = new()
        {
            Id = 62086,
            Title = "Cat's in the Bag...",
            EpisodeNumber = 2,
            SeasonNumber = 1,
            TvId = 1399,
            SeasonId = 3572,
            Overview =
                "After their decaying RV breaks down, Walt and Jesse are forced to deal with a corpse and a prisoner.",
        };
        context.Episodes.AddRange(entities: [episode1, episode2]);

        VideoFile tvVideoFile1 = new()
        {
            Id = SeedConstants.TvVideoFile1Id,
            Filename = "Breaking.Bad.S01E01.mkv",
            Folder = "/media/tv/Breaking Bad (2008)/Season 01",
            HostFolder = "/media/tv/Breaking Bad (2008)/Season 01",
            Languages = "en",
            Quality = "1080p",
            Share = "tv",
            EpisodeId = 62085,
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
            EpisodeId = 62086,
        };
        context.VideoFiles.AddRange(entities: [tvVideoFile1, tvVideoFile2]);

        context.GenreTv.AddRange(entities: new GenreTv { GenreId = 18, TvId = 1399 });

        // UserData for continue watching tests
        context.UserData.AddRange(entities:
            [
                new UserData
                {
                    Id = Ulid.Parse(base32: "01JABC0000000000000000MOVI"),
                    UserId = SeedConstants.UserId,
                    MovieId = 129,
                    VideoFileId = SeedConstants.MovieVideoFile1Id,
                    Type = "movie",
                    Time = 3600,
                    LastPlayedDate = "2026-02-01T10:00:00Z",
                },
                // Duplicate entry for same movie (different video file)
                new UserData
                {
                    Id = Ulid.Parse(base32: "01JDBC0000000000000000MDUP"),
                    UserId = SeedConstants.UserId,
                    MovieId = 129,
                    VideoFileId = SeedConstants.MovieVideoFile2Id,
                    Type = "movie",
                    Time = 1800,
                    LastPlayedDate = "2026-01-15T08:00:00Z",
                },
                new UserData
                {
                    Id = Ulid.Parse(base32: "01JBBC0000000000000000TVSH"),
                    UserId = SeedConstants.UserId,
                    TvId = 1399,
                    VideoFileId = SeedConstants.TvVideoFile1Id,
                    Type = "tv",
                    Time = 2400,
                    LastPlayedDate = "2026-02-02T14:00:00Z",
                }
            ]
        );

        context.SaveChanges();
    }
}
