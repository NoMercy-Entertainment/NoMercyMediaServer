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

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Storage;

namespace NoMercy.Tests.Database.Migrations;

/// <summary>
/// Proves the full migration chain applies cleanly to a brand-new, on-disk
/// SQLite database for all three contexts (the shape of a fresh self-hosted
/// install), that re-running Migrate() is idempotent and never touches rows a
/// prior run already wrote, and that the composite unique index a self-hosted
/// library depends on (Folder.DriverId+Path) is still enforced by the schema
/// the migration chain produces — not just declared on the model.
/// </summary>
public class MigrationSafetyHarnessTests : IDisposable
{
    private readonly string _mediaDbPath;
    private readonly string _queueDbPath;
    private readonly string _appDbPath;

    public MigrationSafetyHarnessTests()
    {
        string runId = Guid.NewGuid().ToString(format: "N");
        _mediaDbPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm_migsafety_media_{runId}.db");
        _queueDbPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm_migsafety_queue_{runId}.db");
        _appDbPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm_migsafety_app_{runId}.db");
    }

    [Fact]
    public void MediaContext_Migrate_OnFreshSqliteFile_AppliesFullChain_CreatesCoreTables()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite(connectionString: $"Data Source={_mediaDbPath}");

        using MediaContext context = new(options: builder.Options);

        context.Database.Migrate();

        List<string> tableNames = QueryTableNames(connection: context.Database.GetDbConnection());

        Assert.Contains(expected: "Movies", collection: tableNames);
        Assert.Contains(expected: "Tvs", collection: tableNames);
        Assert.Contains(expected: "Libraries", collection: tableNames);
        Assert.Contains(expected: "Folders", collection: tableNames);
        Assert.Contains(expected: "VideoFiles", collection: tableNames);
        Assert.Contains(expected: "Drivers", collection: tableNames);
        Assert.Contains(expected: "Users", collection: tableNames);
        Assert.Contains(expected: "Tracks", collection: tableNames);
        Assert.Contains(expected: "__EFMigrationsHistory", collection: tableNames);
    }

    [Fact]
    public void QueueContext_Migrate_OnFreshSqliteFile_AppliesFullChain_CreatesQueueTables()
    {
        DbContextOptionsBuilder<QueueContext> builder = new();
        builder.UseSqlite(connectionString: $"Data Source={_queueDbPath}");

        using QueueContext context = new(options: builder.Options);

        context.Database.Migrate();

        List<string> tableNames = QueryTableNames(connection: context.Database.GetDbConnection());

        Assert.Contains(expected: "QueueJobs", collection: tableNames);
        Assert.Contains(expected: "FailedJobs", collection: tableNames);
        Assert.Contains(expected: "CronJobs", collection: tableNames);
        Assert.Contains(expected: "__EFMigrationsHistory", collection: tableNames);
    }

    [Fact]
    public void AppContext_Migrate_OnFreshSqliteFile_AppliesFullChain_CreatesConfigurationTable()
    {
        DbContextOptionsBuilder<AppDbContext> builder = new();
        builder.UseSqlite(connectionString: $"Data Source={_appDbPath}");

        using AppDbContext context = new(options: builder.Options);

        context.Database.Migrate();

        List<string> tableNames = QueryTableNames(connection: context.Database.GetDbConnection());

        Assert.Contains(expected: "Configuration", collection: tableNames);
        Assert.Contains(expected: "__EFMigrationsHistory", collection: tableNames);
    }

    [Fact]
    public async Task MediaContext_ReMigrate_IsIdempotent_AndPreservesSeededLibraryFolderVideoFileData()
    {
        Ulid driverId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();
        Ulid libraryId = Ulid.NewUlid();
        Ulid videoFileId = Ulid.NewUlid();
        const int movieId = 1;

        DbContextOptionsBuilder<MediaContext> seedBuilder = new();
        seedBuilder.UseSqlite(connectionString: $"Data Source={_mediaDbPath}");

        await using (MediaContext seedContext = new(options: seedBuilder.Options))
        {
            seedContext.Database.Migrate();

            seedContext.Drivers.Add(
                entity: new Driver
                {
                    Id = driverId,
                    Name = "Local Filesystem",
                    Type = "local",
                }
            );
            seedContext.Folders.Add(
                entity: new Folder
                {
                    Id = folderId,
                    Path = "/media/movies",
                    DriverId = driverId,
                }
            );
            seedContext.Libraries.Add(
                entity: new Library
                {
                    Id = libraryId,
                    Title = "Movies",
                    Type = "movie",
                }
            );
            seedContext.Movies.Add(
                entity: new Movie
                {
                    Id = movieId,
                    Title = "The Eight Year Reel",
                    TitleSort = "eight year reel",
                    LibraryId = libraryId,
                }
            );
            seedContext.VideoFiles.Add(
                entity: new VideoFile
                {
                    Id = videoFileId,
                    Filename = "the-eight-year-reel.mkv",
                    HostFolder = "/media/movies",
                    Folder = "/media/movies",
                    Quality = "1080p",
                    Languages = "en",
                    Share = string.Empty,
                    MovieId = movieId,
                }
            );

            await seedContext.SaveChangesAsync();
        }

        DbContextOptionsBuilder<MediaContext> reMigrateBuilder = new();
        reMigrateBuilder.UseSqlite(connectionString: $"Data Source={_mediaDbPath}");

        await using (MediaContext reMigrateContext = new(options: reMigrateBuilder.Options))
        {
            reMigrateContext.Database.Migrate();
        }

        DbContextOptionsBuilder<MediaContext> readBuilder = new();
        readBuilder.UseSqlite(connectionString: $"Data Source={_mediaDbPath}");

        await using MediaContext readContext = new(options: readBuilder.Options);

        Driver? driver = await readContext
            .Drivers.AsNoTracking()
            .SingleOrDefaultAsync(predicate: d => d.Id == driverId);
        Folder? folder = await readContext
            .Folders.AsNoTracking()
            .SingleOrDefaultAsync(predicate: f => f.Id == folderId);
        Library? library = await readContext
            .Libraries.AsNoTracking()
            .SingleOrDefaultAsync(predicate: l => l.Id == libraryId);
        Movie? movie = await readContext
            .Movies.AsNoTracking()
            .SingleOrDefaultAsync(predicate: m => m.Id == movieId);
        VideoFile? videoFile = await readContext
            .VideoFiles.AsNoTracking()
            .SingleOrDefaultAsync(predicate: v => v.Id == videoFileId);

        Assert.NotNull(@object: driver);
        Assert.Equal(expected: "Local Filesystem", actual: driver.Name);

        Assert.NotNull(@object: folder);
        Assert.Equal(expected: driverId, actual: folder.DriverId);
        Assert.Equal(expected: "/media/movies", actual: folder.Path);

        Assert.NotNull(@object: library);
        Assert.Equal(expected: "Movies", actual: library.Title);

        Assert.NotNull(@object: movie);
        Assert.Equal(expected: libraryId, actual: movie.LibraryId);
        Assert.Equal(expected: "The Eight Year Reel", actual: movie.Title);

        Assert.NotNull(@object: videoFile);
        Assert.Equal(expected: movieId, actual: videoFile.MovieId);
        Assert.Equal(expected: "the-eight-year-reel.mkv", actual: videoFile.Filename);
        Assert.Equal(expected: "/media/movies", actual: videoFile.HostFolder);
    }

    [Fact]
    public async Task Folder_DuplicateDriverIdAndPath_ViolatesUniqueIndex_TheMigrationChainCreated()
    {
        Ulid driverId = Ulid.NewUlid();

        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite(connectionString: $"Data Source={_mediaDbPath}");

        await using MediaContext context = new(options: builder.Options);
        context.Database.Migrate();

        context.Drivers.Add(
            entity: new Driver
            {
                Id = driverId,
                Name = "Local Filesystem",
                Type = "local",
            }
        );
        context.Folders.Add(
            entity: new Folder
            {
                Id = Ulid.NewUlid(),
                Path = "/media/tv",
                DriverId = driverId,
            }
        );
        await context.SaveChangesAsync();

        context.Folders.Add(
            entity: new Folder
            {
                Id = Ulid.NewUlid(),
                Path = "/media/tv",
                DriverId = driverId,
            }
        );

        await Assert.ThrowsAsync<DbUpdateException>(testCode: () => context.SaveChangesAsync());
    }

    private static List<string> QueryTableNames(DbConnection connection)
    {
        connection.Open();

        using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        using DbDataReader reader = command.ExecuteReader();

        List<string> tableNames = [];
        while (reader.Read())
            tableNames.Add(item: reader.GetString(ordinal: 0));

        return tableNames;
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools physical file handles across connections even
        // after Dispose(); on Windows the OS-level lock outlives the `using`
        // context/connection above, so deleting the file immediately fails with
        // "process cannot access the file" unless the pool is cleared first.
        SqliteConnection.ClearAllPools();

        foreach (string path in new[] { _mediaDbPath, _queueDbPath, _appDbPath })
            if (File.Exists(path: path))
                File.Delete(path: path);

        GC.SuppressFinalize(obj: this);
    }
}
