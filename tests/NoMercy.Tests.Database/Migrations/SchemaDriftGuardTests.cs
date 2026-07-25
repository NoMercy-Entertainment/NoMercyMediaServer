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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NoMercy.Database;
using NoMercy.NmSystem.Information;

namespace NoMercy.Tests.Database.Migrations;

/// <summary>
/// Closes the AppDbContext gap left by MigrationDriftGuardTests (which only
/// covers MediaContext/QueueContext) and proves the two/three-DB pattern
/// holds at the schema level, not just the entity-type level: migrating
/// AppDbContext against its own file must never materialize a Media or
/// Queue table, and the three on-disk database paths must never collide.
/// </summary>
public class SchemaDriftGuardTests
{
    [Fact]
    public void AppDbContext_HasNoPendingModelChanges()
    {
        DbContextOptionsBuilder<AppDbContext> builder = new();
        builder.UseSqlite("Data Source=:memory:");

        using AppDbContext ctx = new(builder.Options);

        IMigrationsModelDiffer differ = ctx.GetService<IMigrationsModelDiffer>();
        IModelRuntimeInitializer initializer = ctx.GetService<IModelRuntimeInitializer>();
        ModelSnapshot? snapshot = ctx.GetService<IMigrationsAssembly>().ModelSnapshot;

        Assert.NotNull(snapshot);

        IModel initializedSnapshotModel = initializer.Initialize(
            snapshot.Model,
            designTime: true,
            validationLogger: null
        );

        IRelationalModel snapshotRelational = initializedSnapshotModel.GetRelationalModel();
        IRelationalModel currentRelational = ctx.GetService<IDesignTimeModel>()
            .Model.GetRelationalModel();

        bool hasDrift = differ.HasDifferences(snapshotRelational, currentRelational);

        Assert.False(
            hasDrift,
            "AppDbContext has pending model changes not captured in a migration. Run: dotnet ef migrations add <Name> --context AppDbContext"
        );
    }

    [Fact]
    public void AppDatabase_MigratesInIsolation_NeverCreatesMediaOrQueueTables()
    {
        string appDbPath = Path.Combine(Path.GetTempPath(), $"nm_appdrift_{Guid.NewGuid():N}.db");

        try
        {
            DbContextOptionsBuilder<AppDbContext> builder = new();
            builder.UseSqlite($"Data Source={appDbPath}");

            using AppDbContext context = new(builder.Options);
            context.Database.Migrate();

            DbConnection connection = context.Database.GetDbConnection();
            connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            using DbDataReader reader = command.ExecuteReader();

            List<string> tableNames = [];
            while (reader.Read())
                tableNames.Add(reader.GetString(0));

            Assert.Contains("Configuration", tableNames);

            Assert.DoesNotContain("Movies", tableNames);
            Assert.DoesNotContain("Libraries", tableNames);
            Assert.DoesNotContain("VideoFiles", tableNames);
            Assert.DoesNotContain("QueueJobs", tableNames);
            Assert.DoesNotContain("FailedJobs", tableNames);
            Assert.DoesNotContain("CronJobs", tableNames);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(appDbPath))
                File.Delete(appDbPath);
        }
    }

    [Fact]
    public void AppDatabase_MapsTo_AppDatabaseFile()
    {
        string appDbPath = AppFiles.AppDatabase;

        Assert.True(
            appDbPath.EndsWith("app.db", StringComparison.OrdinalIgnoreCase),
            $"AppFiles.AppDatabase must resolve to a path ending in 'app.db'. Got: {appDbPath}"
        );
    }

    [Fact]
    public void AppDatabase_IsDistinctFrom_MediaAndQueueDatabases()
    {
        string appDbPath = AppFiles.AppDatabase;
        string mediaDbPath = AppFiles.MediaDatabase;
        string queueDbPath = AppFiles.QueueDatabase;

        Assert.NotEqual(appDbPath, mediaDbPath, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(appDbPath, queueDbPath, StringComparer.OrdinalIgnoreCase);
    }
}
