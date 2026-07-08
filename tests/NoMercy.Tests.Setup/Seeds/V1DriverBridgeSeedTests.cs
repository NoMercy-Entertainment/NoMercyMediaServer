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

[Trait("Category", "Unit")]
public class V1DriverBridgeSeedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public V1DriverBridgeSeedTests()
    {
        _connection = new("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                _connection,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            .Options;

        using MediaContext ctx = new(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MediaContext CreateContext() => new(_options);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Driver MakeLocalDriver(Ulid id, string rootPath) =>
        new()
        {
            Id = id,
            Name = rootPath,
            Type = "local",
            Config = JsonConvert.SerializeObject(new { rootPath }),
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
            seed.Drivers.AddRange(
                MakeLocalDriver(moviesId, @"C:\Media\Movies"),
                MakeLocalDriver(tvId, @"C:\Media\TV")
            );
            seed.Folders.AddRange(MakeSelfDriverFolder(moviesId), MakeSelfDriverFolder(tvId));
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(ctx);
        }

        await using MediaContext verify = CreateContext();

        List<Folder> folders = await verify.Folders.ToListAsync();
        Assert.Equal(2, folders.Count);

        Ulid sharedDriverId = folders[0].DriverId;
        Assert.Equal(sharedDriverId, folders[1].DriverId);

        Driver? sharedDriver = await verify.Drivers.FindAsync(sharedDriverId);
        Assert.NotNull(sharedDriver);
        Assert.Equal("local", sharedDriver.Type);
        Assert.Contains("Media", sharedDriver.Config);

        Folder? movies = folders.Find(f => f.Id == moviesId);
        Folder? tv = folders.Find(f => f.Id == tvId);
        Assert.NotNull(movies);
        Assert.NotNull(tv);
        Assert.Equal("Movies", movies.Path);
        Assert.Equal("TV", tv.Path);
    }

    [Fact]
    public async Task RawSqlUpdateBothFolders_PersistsInSameTestClass()
    {
        Ulid moviesId = Ulid.NewUlid();
        Ulid tvId = Ulid.NewUlid();
        Ulid sharedId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.AddRange(
                MakeLocalDriver(moviesId, @"C:\Media\Movies"),
                MakeLocalDriver(tvId, @"C:\Media\TV"),
                new Driver
                {
                    Id = sharedId,
                    Name = @"C:\Media",
                    Type = "local",
                    Config = "{\"rootPath\":\"C:\\\\Media\"}",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                }
            );
            seed.Folders.AddRange(MakeSelfDriverFolder(moviesId), MakeSelfDriverFolder(tvId));
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
                cmd.Parameters.Add(p);
                p = cmd.CreateParameter();
                p.ParameterName = "@path";
                p.Value = "Movies";
                cmd.Parameters.Add(p);
                p = cmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = moviesId.ToString();
                cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (System.Data.Common.DbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE Folders SET DriverId = @d, Path = @path WHERE Id = @id";
                p = cmd.CreateParameter();
                p.ParameterName = "@d";
                p.Value = sharedStr;
                cmd.Parameters.Add(p);
                p = cmd.CreateParameter();
                p.ParameterName = "@path";
                p.Value = "TV";
                cmd.Parameters.Add(p);
                p = cmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = tvId.ToString();
                cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        await using MediaContext verify = CreateContext();
        List<Folder> folders = await verify.Folders.ToListAsync();
        Assert.Equal(2, folders.Count);

        Folder? movies = folders.Find(f => f.Id == moviesId);
        Folder? tv = folders.Find(f => f.Id == tvId);
        Assert.NotNull(movies);
        Assert.NotNull(tv);
        Assert.Equal(sharedId, movies.DriverId);
        Assert.Equal(sharedId, tv.DriverId);
    }

    [Fact]
    public async Task TwoFoldersUnderSameRoot_UsingFind_BothHaveSameDriverId()
    {
        Ulid moviesId = Ulid.NewUlid();
        Ulid tvId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.Drivers.AddRange(
                MakeLocalDriver(moviesId, @"C:\Media\Movies"),
                MakeLocalDriver(tvId, @"C:\Media\TV")
            );
            seed.Folders.AddRange(MakeSelfDriverFolder(moviesId), MakeSelfDriverFolder(tvId));
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(ctx);
        }

        await using MediaContext verify = CreateContext();

        List<Folder> folders = await verify.Folders.ToListAsync();
        Assert.Equal(2, folders.Count);

        Folder? movies = folders.Find(f => f.Id == moviesId);
        Folder? tv = folders.Find(f => f.Id == tvId);
        Assert.NotNull(movies);
        Assert.NotNull(tv);
        Assert.Equal(movies.DriverId, tv.DriverId);
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
            seed.Drivers.Add(MakeLocalDriver(folderId, @"C:\Media\Anime"));
            seed.Folders.Add(MakeSelfDriverFolder(folderId));
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(ctx);
        }

        await using MediaContext verify = CreateContext();

        Folder? folder = await verify.Folders.FindAsync(folderId);
        Assert.NotNull(folder);

        Driver? driver = await verify.Drivers.FindAsync(folder.DriverId);
        Assert.NotNull(driver);
        Assert.Equal("local", driver.Type);
        Assert.Equal(string.Empty, folder.Path);
        Assert.Contains("Media", driver.Config!);
        Assert.Contains("Anime", driver.Config!);
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
                new()
                {
                    Id = realDriverId,
                    Name = "NAS",
                    Type = "nfs",
                    Config = originalDriverConfig,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                }
            );
            seed.Folders.Add(MakeRealDriverFolder(folderId, realDriverId, originalPath));
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(ctx);
        }

        await using MediaContext verify = CreateContext();

        Folder? folder = await verify.Folders.FindAsync(folderId);
        Assert.NotNull(folder);
        Assert.Equal(realDriverId, folder.DriverId);
        Assert.Equal(originalPath, folder.Path);

        Driver? driver = await verify.Drivers.FindAsync(realDriverId);
        Assert.NotNull(driver);
        Assert.Equal("nfs", driver.Type);
        Assert.Equal(originalDriverConfig, driver.Config);
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
            seed.Drivers.AddRange(
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
            );
            seed.Folders.AddRange(
                MakeRealDriverFolder(nfsFolderId, nfsDriverId, "Movies"),
                MakeRealDriverFolder(s3FolderId, s3DriverId, "TV")
            );
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(ctx);
        }

        await using MediaContext verify = CreateContext();

        Driver? nfsDriver = await verify.Drivers.FindAsync(nfsDriverId);
        Driver? s3Driver = await verify.Drivers.FindAsync(s3DriverId);

        Assert.NotNull(nfsDriver);
        Assert.Equal("nfs", nfsDriver.Type);

        Assert.NotNull(s3Driver);
        Assert.Equal("s3", s3Driver.Type);

        int driverCount = await verify.Drivers.CountAsync();
        Assert.Equal(2, driverCount);
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
            seed.Drivers.AddRange(
                new Driver
                {
                    Id = realDriverId,
                    Name = "S3",
                    Type = "s3",
                    Config = "{\"bucket\":\"media\"}",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
                MakeLocalDriver(moviesId, @"D:\Media\Movies"),
                MakeLocalDriver(tvId, @"D:\Media\TV")
            );
            seed.Folders.AddRange(
                MakeRealDriverFolder(realFolderId, realDriverId, "Films"),
                MakeSelfDriverFolder(moviesId),
                MakeSelfDriverFolder(tvId)
            );
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(ctx);
        }

        await using MediaContext verify = CreateContext();

        Folder? realFolder = await verify.Folders.FindAsync(realFolderId);
        Assert.NotNull(realFolder);
        Assert.Equal(realDriverId, realFolder.DriverId);
        Assert.Equal("Films", realFolder.Path);

        Folder? movies = await verify.Folders.FindAsync(moviesId);
        Folder? tv = await verify.Folders.FindAsync(tvId);
        Assert.NotNull(movies);
        Assert.NotNull(tv);
        Assert.Equal(movies.DriverId, tv.DriverId);
        Assert.NotEqual(moviesId, movies.DriverId);
        Assert.NotEqual(tvId, tv.DriverId);
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
            seed.Drivers.AddRange(
                MakeLocalDriver(moviesId, @"C:\Data\Media\Movies"),
                MakeLocalDriver(tvId, @"C:\Data\Media\TV")
            );
            seed.Folders.AddRange(MakeSelfDriverFolder(moviesId), MakeSelfDriverFolder(tvId));
            await seed.SaveChangesAsync();
        }

        await using (MediaContext firstRun = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(firstRun);
        }

        Ulid driverIdAfterFirstRun;
        string pathMoviesAfterFirstRun;
        string pathTvAfterFirstRun;

        await using (MediaContext snapshot = CreateContext())
        {
            Folder movies = await snapshot.Folders.FindAsync(moviesId) ?? throw new();
            Folder tv = await snapshot.Folders.FindAsync(tvId) ?? throw new();
            driverIdAfterFirstRun = movies.DriverId;
            pathMoviesAfterFirstRun = movies.Path;
            pathTvAfterFirstRun = tv.Path;
        }

        await using (MediaContext secondRun = CreateContext())
        {
            await V1DriverBridgeSeed.RunAsync(secondRun);
        }

        await using MediaContext verify = CreateContext();

        Folder? moviesAfter = await verify.Folders.FindAsync(moviesId);
        Folder? tvAfter = await verify.Folders.FindAsync(tvId);
        Assert.NotNull(moviesAfter);
        Assert.NotNull(tvAfter);
        Assert.Equal(driverIdAfterFirstRun, moviesAfter.DriverId);
        Assert.Equal(driverIdAfterFirstRun, tvAfter.DriverId);
        Assert.Equal(pathMoviesAfterFirstRun, moviesAfter.Path);
        Assert.Equal(pathTvAfterFirstRun, tvAfter.Path);

        int driverCount = await verify.Drivers.CountAsync();
        Assert.Equal(1, driverCount);
    }

    // -----------------------------------------------------------------------
    // Empty DB: no-op, no exceptions
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmptyDatabase_DoesNotThrow()
    {
        await using MediaContext ctx = CreateContext();
        await V1DriverBridgeSeed.RunAsync(ctx);

        int folderCount = await ctx.Folders.CountAsync();
        int driverCount = await ctx.Drivers.CountAsync();
        Assert.Equal(0, folderCount);
        Assert.Equal(0, driverCount);
    }

    // -----------------------------------------------------------------------
    // Production correctness: file-based SQLite proves the bridge works
    // against a real on-disk DB, not just in-memory.
    // Covers: regrouping, sub-path calculation, configured-driver safety.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FileBased_Bridge_Regroups_AutoSeeded_AndLeavesConfiguredDriverUntouched()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"nmtest_{Ulid.NewUlid()}.db");
        string connectionString = $"Data Source={dbPath}; Foreign Keys=True;";
        try
        {
            DbContextOptions<MediaContext> fileOptions = new DbContextOptionsBuilder<MediaContext>()
                .UseSqlite(
                    connectionString,
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                )
                .Options;

            // ── seed ──────────────────────────────────────────────────────────
            Ulid moviesId = Ulid.NewUlid();
            Ulid tvId = Ulid.NewUlid();
            Ulid configuredDriverId = Ulid.NewUlid();
            Ulid configuredFolderId = Ulid.NewUlid();

            await using (MediaContext seedCtx = new(fileOptions))
            {
                await seedCtx.Database.EnsureCreatedAsync();

                seedCtx.Drivers.AddRange(
                    MakeLocalDriver(moviesId, @"C:\Media\Movies"),
                    MakeLocalDriver(tvId, @"C:\Media\TV"),
                    new Driver
                    {
                        Id = configuredDriverId,
                        Name = "NAS",
                        Type = "nfs",
                        Config = "{\"host\":\"192.168.1.1\"}",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    }
                );
                seedCtx.Folders.AddRange(
                    MakeSelfDriverFolder(moviesId),
                    MakeSelfDriverFolder(tvId),
                    MakeRealDriverFolder(configuredFolderId, configuredDriverId, "Films")
                );
                await seedCtx.SaveChangesAsync();
            }

            // ── run bridge ────────────────────────────────────────────────────
            await using (MediaContext bridgeCtx = new(fileOptions))
            {
                await V1DriverBridgeSeed.RunAsync(bridgeCtx);
            }

            // ── verify ────────────────────────────────────────────────────────
            Ulid sharedDriverId;
            await using (MediaContext verifyCtx = new(fileOptions))
            {
                Folder? movies = await verifyCtx.Folders.FindAsync(moviesId);
                Folder? tv = await verifyCtx.Folders.FindAsync(tvId);
                Folder? configured = await verifyCtx.Folders.FindAsync(configuredFolderId);

                Assert.NotNull(movies);
                Assert.NotNull(tv);
                Assert.NotNull(configured);

                // auto-seeded folders share one driver
                Assert.Equal(movies.DriverId, tv.DriverId);
                sharedDriverId = movies.DriverId;

                // the shared driver is neither of the original per-folder drivers
                Assert.NotEqual(moviesId, movies.DriverId);
                Assert.NotEqual(tvId, tv.DriverId);

                // sub-paths are set correctly
                Assert.Equal("Movies", movies.Path);
                Assert.Equal("TV", tv.Path);

                // shared driver exists and has the common root in its config
                Driver? sharedDriver = await verifyCtx.Drivers.FindAsync(movies.DriverId);
                Assert.NotNull(sharedDriver);
                Assert.Equal("local", sharedDriver.Type);
                Assert.Contains("Media", sharedDriver.Config);

                // configured driver is completely untouched
                Assert.Equal(configuredDriverId, configured.DriverId);
                Assert.Equal("Films", configured.Path);

                Driver? nfsDriver = await verifyCtx.Drivers.FindAsync(configuredDriverId);
                Assert.NotNull(nfsDriver);
                Assert.Equal("nfs", nfsDriver.Type);
            }

            // ── second run is a no-op ─────────────────────────────────────────
            await using (MediaContext secondRunCtx = new(fileOptions))
            {
                await V1DriverBridgeSeed.RunAsync(secondRunCtx);
            }

            await using (MediaContext afterSecondRun = new(fileOptions))
            {
                Folder? moviesAgain = await afterSecondRun.Folders.FindAsync(moviesId);
                Folder? tvAgain = await afterSecondRun.Folders.FindAsync(tvId);
                Assert.NotNull(moviesAgain);
                Assert.NotNull(tvAgain);
                Assert.Equal(sharedDriverId, moviesAgain.DriverId);
                Assert.Equal(sharedDriverId, tvAgain.DriverId);
                Assert.Equal("Movies", moviesAgain.Path);
                Assert.Equal("TV", tvAgain.Path);
            }
        }
        finally
        {
            // Clear connection pool so SQLite releases all file handles.
            SqliteConnection.ClearPool(new(connectionString));

            if (File.Exists(dbPath))
                File.Delete(dbPath);
            string walPath = dbPath + "-wal";
            string shmPath = dbPath + "-shm";
            if (File.Exists(walPath))
                File.Delete(walPath);
            if (File.Exists(shmPath))
                File.Delete(shmPath);
        }
    }
}
