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
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.Service.Seeds;

namespace NoMercy.Tests.Setup.Seeds;

[Trait(name: "Category", value: "Unit")]
public class V1DriverBridgeSeedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public V1DriverBridgeSeedTests()
    {
        _connection = new(connectionString: "DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                connection: _connection,
                sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
            )
            .Options;

        using MediaContext ctx = new(options: _options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MediaContext CreateContext() => new(options: _options);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Driver MakeLocalDriver(Ulid id, string rootPath) =>
        new()
        {
            Id = id,
            Name = rootPath,
            Type = "local",
            Config = JsonConvert.SerializeObject(value: new { rootPath }),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Folder MakeSelfDriverFolder(Ulid id, string subPath = "") =>
        new()
        {
            Id = id,
            DriverId = id,
            Path = subPath,
        };

    private static Folder MakeRealDriverFolder(Ulid id, Ulid driverId, string subPath) =>
        new()
        {
            Id = id,
            DriverId = driverId,
            Path = subPath,
        };

    // -----------------------------------------------------------------------
    // Core grouping: shared-root folders become one driver
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TwoFoldersUnderSameRoot_ProduceOneSharedDriver()
    {
        Ulid moviesId = Ulid.NewUlid();
        Ulid tvId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.AddRange(entities: [MakeLocalDriver(id: moviesId, rootPath: @"C:\Media\Movies"), MakeLocalDriver(id: tvId, rootPath: @"C:\Media\TV")]
            );
            seed.Folders.AddRange(entities: [MakeSelfDriverFolder(id: moviesId), MakeSelfDriverFolder(id: tvId)]);
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(context: ctx);
        }

        await using MediaContext verify = CreateContext();

        List<Folder> folders = await verify.Folders.ToListAsync();
        Assert.Equal(expected: 2, actual: folders.Count);

        Ulid sharedDriverId = folders[index: 0].DriverId;
        Assert.Equal(expected: sharedDriverId, actual: folders[index: 1].DriverId);

        Driver? sharedDriver = await verify.Drivers.FindAsync(keyValues: sharedDriverId);
        Assert.NotNull(@object: sharedDriver);
        Assert.Equal(expected: "local", actual: sharedDriver.Type);
        Assert.Contains(expectedSubstring: "Media", actualString: sharedDriver.Config);

        Folder? movies = folders.Find(match: f => f.Id == moviesId);
        Folder? tv = folders.Find(match: f => f.Id == tvId);
        Assert.NotNull(@object: movies);
        Assert.NotNull(@object: tv);
        Assert.Equal(expected: "Movies", actual: movies.Path);
        Assert.Equal(expected: "TV", actual: tv.Path);
    }

    [Fact]
    public async Task RawSqlUpdateBothFolders_PersistsInSameTestClass()
    {
        Ulid moviesId = Ulid.NewUlid();
        Ulid tvId = Ulid.NewUlid();
        Ulid sharedId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.AddRange(entities:
                [MakeLocalDriver(id: moviesId, rootPath: @"C:\Media\Movies"), MakeLocalDriver(id: tvId, rootPath: @"C:\Media\TV"), new Driver
                    {
                        Id = sharedId,
                        Name = @"C:\Media",
                        Type = "local",
                        Config = "{\"rootPath\":\"C:\\\\Media\"}",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    }
                ]
            );
            seed.Folders.AddRange(entities: [MakeSelfDriverFolder(id: moviesId), MakeSelfDriverFolder(id: tvId)]);
            await seed.SaveChangesAsync();
        }

        await using (MediaContext bridgeCtx = CreateContext())
        {
            System.Data.Common.DbConnection conn = bridgeCtx.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            string sharedStr = sharedId.ToString();

            System.Data.Common.DbParameter p;

            await using (System.Data.Common.DbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE Folders SET DriverId = @d, Path = @path WHERE Id = @id";
                p = cmd.CreateParameter();
                p.ParameterName = "@d";
                p.Value = sharedStr;
                cmd.Parameters.Add(value: p);
                p = cmd.CreateParameter();
                p.ParameterName = "@path";
                p.Value = "Movies";
                cmd.Parameters.Add(value: p);
                p = cmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = moviesId.ToString();
                cmd.Parameters.Add(value: p);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (System.Data.Common.DbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE Folders SET DriverId = @d, Path = @path WHERE Id = @id";
                p = cmd.CreateParameter();
                p.ParameterName = "@d";
                p.Value = sharedStr;
                cmd.Parameters.Add(value: p);
                p = cmd.CreateParameter();
                p.ParameterName = "@path";
                p.Value = "TV";
                cmd.Parameters.Add(value: p);
                p = cmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = tvId.ToString();
                cmd.Parameters.Add(value: p);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        await using MediaContext verify = CreateContext();
        List<Folder> folders = await verify.Folders.ToListAsync();
        Assert.Equal(expected: 2, actual: folders.Count);

        Folder? movies = folders.Find(match: f => f.Id == moviesId);
        Folder? tv = folders.Find(match: f => f.Id == tvId);
        Assert.NotNull(@object: movies);
        Assert.NotNull(@object: tv);
        Assert.Equal(expected: sharedId, actual: movies.DriverId);
        Assert.Equal(expected: sharedId, actual: tv.DriverId);
    }

    [Fact]
    public async Task TwoFoldersUnderSameRoot_UsingFind_BothHaveSameDriverId()
    {
        Ulid moviesId = Ulid.NewUlid();
        Ulid tvId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.AddRange(entities: [MakeLocalDriver(id: moviesId, rootPath: @"C:\Media\Movies"), MakeLocalDriver(id: tvId, rootPath: @"C:\Media\TV")]
            );
            seed.Folders.AddRange(entities: [MakeSelfDriverFolder(id: moviesId), MakeSelfDriverFolder(id: tvId)]);
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(context: ctx);
        }

        await using MediaContext verify = CreateContext();

        List<Folder> folders = await verify.Folders.ToListAsync();
        Assert.Equal(expected: 2, actual: folders.Count);

        Folder? movies = folders.Find(match: f => f.Id == moviesId);
        Folder? tv = folders.Find(match: f => f.Id == tvId);
        Assert.NotNull(@object: movies);
        Assert.NotNull(@object: tv);
        Assert.Equal(expected: movies.DriverId, actual: tv.DriverId);
    }

    // -----------------------------------------------------------------------
    // Single folder: driver root = the folder itself, SubPath = ""
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SingleAutoSeededFolder_ProducesOneDriverWithEmptySubPath()
    {
        Ulid folderId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.Add(entity: MakeLocalDriver(id: folderId, rootPath: @"C:\Media\Anime"));
            seed.Folders.Add(entity: MakeSelfDriverFolder(id: folderId));
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(context: ctx);
        }

        await using MediaContext verify = CreateContext();

        Folder? folder = await verify.Folders.FindAsync(keyValues: folderId);
        Assert.NotNull(@object: folder);

        Driver? driver = await verify.Drivers.FindAsync(keyValues: folder.DriverId);
        Assert.NotNull(@object: driver);
        Assert.Equal(expected: "local", actual: driver.Type);
        Assert.Equal(expected: string.Empty, actual: folder.Path);
        Assert.Contains(expectedSubstring: "Media", actualString: driver.Config!);
        Assert.Contains(expectedSubstring: "Anime", actualString: driver.Config!);
    }

    // -----------------------------------------------------------------------
    // Safety: folder with existing (real) DriverId is never modified
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FolderWithExistingRealDriver_IsNeverModified()
    {
        Ulid realDriverId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();

        string originalPath = "Films";
        string originalDriverConfig = $"{{\"rootPath\":\"/mnt/nas/media\"}}";

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.Add(
                entity: new()
                {
                    Id = realDriverId,
                    Name = "NAS",
                    Type = "nfs",
                    Config = originalDriverConfig,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                }
            );
            seed.Folders.Add(entity: MakeRealDriverFolder(id: folderId, driverId: realDriverId, subPath: originalPath));
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(context: ctx);
        }

        await using MediaContext verify = CreateContext();

        Folder? folder = await verify.Folders.FindAsync(keyValues: folderId);
        Assert.NotNull(@object: folder);
        Assert.Equal(expected: realDriverId, actual: folder.DriverId);
        Assert.Equal(expected: originalPath, actual: folder.Path);

        Driver? driver = await verify.Drivers.FindAsync(keyValues: realDriverId);
        Assert.NotNull(@object: driver);
        Assert.Equal(expected: "nfs", actual: driver.Type);
        Assert.Equal(expected: originalDriverConfig, actual: driver.Config);
    }

    // -----------------------------------------------------------------------
    // Safety: existing Driver rows (nfs, s3, etc.) are never deleted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExistingNonLocalDriverRows_AreNeverDeletedOrAltered()
    {
        Ulid nfsDriverId = Ulid.NewUlid();
        Ulid s3DriverId = Ulid.NewUlid();

        Ulid nfsFolderId = Ulid.NewUlid();
        Ulid s3FolderId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.AddRange(entities:
                [
                    new Driver
                    {
                        Id = nfsDriverId,
                        Name = "NFS",
                        Type = "nfs",
                        Config = "{\"host\":\"192.168.1.1\",\"share\":\"/media\"}",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    },
                    new Driver
                    {
                        Id = s3DriverId,
                        Name = "S3",
                        Type = "s3",
                        Config = "{\"bucket\":\"my-media\",\"region\":\"eu-west-1\"}",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    }
                ]
            );
            seed.Folders.AddRange(entities: [MakeRealDriverFolder(id: nfsFolderId, driverId: nfsDriverId, subPath: "Movies"), MakeRealDriverFolder(id: s3FolderId, driverId: s3DriverId, subPath: "TV")]
            );
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(context: ctx);
        }

        await using MediaContext verify = CreateContext();

        Driver? nfsDriver = await verify.Drivers.FindAsync(keyValues: nfsDriverId);
        Driver? s3Driver = await verify.Drivers.FindAsync(keyValues: s3DriverId);

        Assert.NotNull(@object: nfsDriver);
        Assert.Equal(expected: "nfs", actual: nfsDriver.Type);

        Assert.NotNull(@object: s3Driver);
        Assert.Equal(expected: "s3", actual: s3Driver.Type);

        int driverCount = await verify.Drivers.CountAsync();
        Assert.Equal(expected: 2, actual: driverCount);
    }

    // -----------------------------------------------------------------------
    // Mixed: auto-seeded folders grouped, real-driver folders untouched
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MixedState_AutoSeededFoldersGrouped_RealDriverFoldersUntouched()
    {
        Ulid realDriverId = Ulid.NewUlid();
        Ulid realFolderId = Ulid.NewUlid();

        Ulid moviesId = Ulid.NewUlid();
        Ulid tvId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.AddRange(entities:
                [
                    new Driver
                    {
                        Id = realDriverId,
                        Name = "S3",
                        Type = "s3",
                        Config = "{\"bucket\":\"media\"}",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    },
                    MakeLocalDriver(id: moviesId, rootPath: @"D:\Media\Movies"), MakeLocalDriver(id: tvId, rootPath: @"D:\Media\TV")
                ]
            );
            seed.Folders.AddRange(entities: [MakeRealDriverFolder(id: realFolderId, driverId: realDriverId, subPath: "Films"), MakeSelfDriverFolder(id: moviesId), MakeSelfDriverFolder(id: tvId)]
            );
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(context: ctx);
        }

        await using MediaContext verify = CreateContext();

        Folder? realFolder = await verify.Folders.FindAsync(keyValues: realFolderId);
        Assert.NotNull(@object: realFolder);
        Assert.Equal(expected: realDriverId, actual: realFolder.DriverId);
        Assert.Equal(expected: "Films", actual: realFolder.Path);

        Folder? movies = await verify.Folders.FindAsync(keyValues: moviesId);
        Folder? tv = await verify.Folders.FindAsync(keyValues: tvId);
        Assert.NotNull(@object: movies);
        Assert.NotNull(@object: tv);
        Assert.Equal(expected: movies.DriverId, actual: tv.DriverId);
        Assert.NotEqual(expected: moviesId, actual: movies.DriverId);
        Assert.NotEqual(expected: tvId, actual: tv.DriverId);
    }

    // -----------------------------------------------------------------------
    // Idempotency: second run is a no-op
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SecondRun_IsNoOp_DriverIdsAndPathsUnchanged()
    {
        Ulid moviesId = Ulid.NewUlid();
        Ulid tvId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.AddRange(entities: [MakeLocalDriver(id: moviesId, rootPath: @"C:\Data\Media\Movies"), MakeLocalDriver(id: tvId, rootPath: @"C:\Data\Media\TV")]
            );
            seed.Folders.AddRange(entities: [MakeSelfDriverFolder(id: moviesId), MakeSelfDriverFolder(id: tvId)]);
            await seed.SaveChangesAsync();
        }

        await using (MediaContext firstRun = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(context: firstRun);
        }

        Ulid driverIdAfterFirstRun;
        string pathMoviesAfterFirstRun;
        string pathTvAfterFirstRun;

        await using (MediaContext snapshot = CreateContext())
        {
            Folder movies = await snapshot.Folders.FindAsync(keyValues: moviesId) ?? throw new();
            Folder tv = await snapshot.Folders.FindAsync(keyValues: tvId) ?? throw new();
            driverIdAfterFirstRun = movies.DriverId;
            pathMoviesAfterFirstRun = movies.Path;
            pathTvAfterFirstRun = tv.Path;
        }

        await using (MediaContext secondRun = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(context: secondRun);
        }

        await using MediaContext verify = CreateContext();

        Folder? moviesAfter = await verify.Folders.FindAsync(keyValues: moviesId);
        Folder? tvAfter = await verify.Folders.FindAsync(keyValues: tvId);
        Assert.NotNull(@object: moviesAfter);
        Assert.NotNull(@object: tvAfter);
        Assert.Equal(expected: driverIdAfterFirstRun, actual: moviesAfter.DriverId);
        Assert.Equal(expected: driverIdAfterFirstRun, actual: tvAfter.DriverId);
        Assert.Equal(expected: pathMoviesAfterFirstRun, actual: moviesAfter.Path);
        Assert.Equal(expected: pathTvAfterFirstRun, actual: tvAfter.Path);

        int driverCount = await verify.Drivers.CountAsync();
        Assert.Equal(expected: 1, actual: driverCount);
    }

    // -----------------------------------------------------------------------
    // Empty DB: no-op, no exceptions
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmptyDatabase_DoesNotThrow()
    {
        await using MediaContext ctx = CreateContext();
        await V1DriverBridgeSeed.RunAsync(context: ctx);

        int folderCount = await ctx.Folders.CountAsync();
        int driverCount = await ctx.Drivers.CountAsync();
        Assert.Equal(expected: 0, actual: folderCount);
        Assert.Equal(expected: 0, actual: driverCount);
    }

    // -----------------------------------------------------------------------
    // Production correctness: file-based SQLite proves the bridge works
    // against a real on-disk DB, not just in-memory.
    // Covers: regrouping, sub-path calculation, configured-driver safety.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FileBased_Bridge_Regroups_AutoSeeded_AndLeavesConfiguredDriverUntouched()
    {
        string dbPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nmtest_{Ulid.NewUlid()}.db");
        string connectionString = $"Data Source={dbPath}; Foreign Keys=True;";
        try
        {
            DbContextOptions<MediaContext> fileOptions = new DbContextOptionsBuilder<MediaContext>()
                .UseSqlite(
                    connectionString: connectionString,
                    sqliteOptionsAction: o => o.UseQuerySplittingBehavior(querySplittingBehavior: QuerySplittingBehavior.SplitQuery)
                )
                .Options;

            // ── seed ──────────────────────────────────────────────────────────
            Ulid moviesId = Ulid.NewUlid();
            Ulid tvId = Ulid.NewUlid();
            Ulid configuredDriverId = Ulid.NewUlid();
            Ulid configuredFolderId = Ulid.NewUlid();

            await using (MediaContext seedCtx = new(options: fileOptions))
            {
                await seedCtx.Database.EnsureCreatedAsync();

                seedCtx.Drivers.AddRange(entities:
                    [MakeLocalDriver(id: moviesId, rootPath: @"C:\Media\Movies"), MakeLocalDriver(id: tvId, rootPath: @"C:\Media\TV"), new Driver
                        {
                            Id = configuredDriverId,
                            Name = "NAS",
                            Type = "nfs",
                            Config = "{\"host\":\"192.168.1.1\"}",
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow,
                        }
                    ]
                );
                seedCtx.Folders.AddRange(entities: [MakeSelfDriverFolder(id: moviesId), MakeSelfDriverFolder(id: tvId), MakeRealDriverFolder(id: configuredFolderId, driverId: configuredDriverId, subPath: "Films")]
                );
                await seedCtx.SaveChangesAsync();
            }

            // ── run bridge ────────────────────────────────────────────────────
            await using (MediaContext bridgeCtx = new(options: fileOptions))
            {
                await V1DriverBridgeSeed.RunAsync(context: bridgeCtx);
            }

            // ── verify ────────────────────────────────────────────────────────
            Ulid sharedDriverId;
            await using (MediaContext verifyCtx = new(options: fileOptions))
            {
                Folder? movies = await verifyCtx.Folders.FindAsync(keyValues: moviesId);
                Folder? tv = await verifyCtx.Folders.FindAsync(keyValues: tvId);
                Folder? configured = await verifyCtx.Folders.FindAsync(keyValues: configuredFolderId);

                Assert.NotNull(@object: movies);
                Assert.NotNull(@object: tv);
                Assert.NotNull(@object: configured);

                // auto-seeded folders share one driver
                Assert.Equal(expected: movies.DriverId, actual: tv.DriverId);
                sharedDriverId = movies.DriverId;

                // the shared driver is neither of the original per-folder drivers
                Assert.NotEqual(expected: moviesId, actual: movies.DriverId);
                Assert.NotEqual(expected: tvId, actual: tv.DriverId);

                // sub-paths are set correctly
                Assert.Equal(expected: "Movies", actual: movies.Path);
                Assert.Equal(expected: "TV", actual: tv.Path);

                // shared driver exists and has the common root in its config
                Driver? sharedDriver = await verifyCtx.Drivers.FindAsync(keyValues: movies.DriverId);
                Assert.NotNull(@object: sharedDriver);
                Assert.Equal(expected: "local", actual: sharedDriver.Type);
                Assert.Contains(expectedSubstring: "Media", actualString: sharedDriver.Config);

                // configured driver is completely untouched
                Assert.Equal(expected: configuredDriverId, actual: configured.DriverId);
                Assert.Equal(expected: "Films", actual: configured.Path);

                Driver? nfsDriver = await verifyCtx.Drivers.FindAsync(keyValues: configuredDriverId);
                Assert.NotNull(@object: nfsDriver);
                Assert.Equal(expected: "nfs", actual: nfsDriver.Type);
            }

            // ── second run is a no-op ─────────────────────────────────────────
            await using (MediaContext secondRunCtx = new(options: fileOptions))
            {
                await V1DriverBridgeSeed.RunAsync(context: secondRunCtx);
            }

            await using (MediaContext afterSecondRun = new(options: fileOptions))
            {
                Folder? moviesAgain = await afterSecondRun.Folders.FindAsync(keyValues: moviesId);
                Folder? tvAgain = await afterSecondRun.Folders.FindAsync(keyValues: tvId);
                Assert.NotNull(@object: moviesAgain);
                Assert.NotNull(@object: tvAgain);
                Assert.Equal(expected: sharedDriverId, actual: moviesAgain.DriverId);
                Assert.Equal(expected: sharedDriverId, actual: tvAgain.DriverId);
                Assert.Equal(expected: "Movies", actual: moviesAgain.Path);
                Assert.Equal(expected: "TV", actual: tvAgain.Path);
            }
        }
        finally
        {
            // Clear connection pool so SQLite releases all file handles.
            SqliteConnection.ClearPool(connection: new(connectionString: connectionString));

            if (File.Exists(path: dbPath))
                File.Delete(path: dbPath);
            string walPath = dbPath + "-wal";
            string shmPath = dbPath + "-shm";
            if (File.Exists(path: walPath))
                File.Delete(path: walPath);
            if (File.Exists(path: shmPath))
                File.Delete(path: shmPath);
        }
    }
}
