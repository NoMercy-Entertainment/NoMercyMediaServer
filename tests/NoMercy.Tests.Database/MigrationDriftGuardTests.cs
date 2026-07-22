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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NoMercy.Database;
using NoMercy.NmSystem.Information;

namespace NoMercy.Tests.Database;

public class MigrationDriftGuardTests
{
    [Fact]
    public void MediaContext_HasNoPendingModelChanges()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite(connectionString: "Data Source=:memory:");

        using MediaContext ctx = new(options: builder.Options);

        IMigrationsModelDiffer differ = ctx.GetService<IMigrationsModelDiffer>();
        IModelRuntimeInitializer initializer = ctx.GetService<IModelRuntimeInitializer>();
        ModelSnapshot? snapshot = ctx.GetService<IMigrationsAssembly>().ModelSnapshot;

        Assert.NotNull(@object: snapshot);

        IModel initializedSnapshotModel = initializer.Initialize(
            model: snapshot.Model,
            designTime: true,
            validationLogger: null
        );

        IRelationalModel snapshotRelational = initializedSnapshotModel.GetRelationalModel();
        IRelationalModel currentRelational = ctx.GetService<IDesignTimeModel>()
            .Model.GetRelationalModel();

        bool hasDrift = differ.HasDifferences(source: snapshotRelational, target: currentRelational);

        Assert.False(
            condition: hasDrift,
            userMessage: "MediaContext has pending model changes not captured in a migration. Run: dotnet ef migrations add <Name> --context MediaContext"
        );
    }

    [Fact]
    public void QueueContext_HasNoPendingModelChanges()
    {
        DbContextOptionsBuilder<QueueContext> builder = new();
        builder.UseSqlite(connectionString: "Data Source=:memory:");

        using QueueContext ctx = new(options: builder.Options);

        IMigrationsModelDiffer differ = ctx.GetService<IMigrationsModelDiffer>();
        IModelRuntimeInitializer initializer = ctx.GetService<IModelRuntimeInitializer>();
        ModelSnapshot? snapshot = ctx.GetService<IMigrationsAssembly>().ModelSnapshot;

        Assert.NotNull(@object: snapshot);

        IModel initializedSnapshotModel = initializer.Initialize(
            model: snapshot.Model,
            designTime: true,
            validationLogger: null
        );

        IRelationalModel snapshotRelational = initializedSnapshotModel.GetRelationalModel();
        IRelationalModel currentRelational = ctx.GetService<IDesignTimeModel>()
            .Model.GetRelationalModel();

        bool hasDrift = differ.HasDifferences(source: snapshotRelational, target: currentRelational);

        Assert.False(
            condition: hasDrift,
            userMessage: "QueueContext has pending model changes not captured in a migration. Run: dotnet ef migrations add <Name> --context QueueContext"
        );
    }

    [Fact]
    public void MediaContext_DoesNotContain_QueueEntities()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite(connectionString: "Data Source=:memory:");

        using MediaContext ctx = new(options: builder.Options);

        IEnumerable<string> entityTypeNames = ctx
            .Model.GetEntityTypes()
            .Select(selector: t => t.ClrType.Name);

        Assert.DoesNotContain(expected: "QueueJob", collection: entityTypeNames);
        Assert.DoesNotContain(expected: "FailedJob", collection: entityTypeNames);
        Assert.DoesNotContain(expected: "CronJob", collection: entityTypeNames);
    }

    [Fact]
    public void QueueContext_DoesNotContain_MediaEntities()
    {
        DbContextOptionsBuilder<QueueContext> builder = new();
        builder.UseSqlite(connectionString: "Data Source=:memory:");

        using QueueContext ctx = new(options: builder.Options);

        IEnumerable<string> entityTypeNames = ctx
            .Model.GetEntityTypes()
            .Select(selector: t => t.ClrType.Name);

        Assert.DoesNotContain(expected: "Movie", collection: entityTypeNames);
        Assert.DoesNotContain(expected: "Library", collection: entityTypeNames);
        Assert.DoesNotContain(expected: "Track", collection: entityTypeNames);
        Assert.DoesNotContain(expected: "User", collection: entityTypeNames);
    }

    [Fact]
    public void MediaContext_MapsTo_MediaDatabase()
    {
        string mediaDbPath = AppFiles.MediaDatabase;

        Assert.True(
            condition: mediaDbPath.EndsWith(value: "media.db", comparisonType: StringComparison.OrdinalIgnoreCase),
            userMessage: $"AppFiles.MediaDatabase must resolve to a path ending in 'media.db'. Got: {mediaDbPath}"
        );
    }

    [Fact]
    public void QueueContext_MapsTo_QueueDatabase()
    {
        string queueDbPath = AppFiles.QueueDatabase;

        Assert.True(
            condition: queueDbPath.EndsWith(value: "queue.db", comparisonType: StringComparison.OrdinalIgnoreCase),
            userMessage: $"AppFiles.QueueDatabase must resolve to a path ending in 'queue.db'. Got: {queueDbPath}"
        );
    }

    [Fact]
    public void MediaDatabase_And_QueueDatabase_AreDistinctPaths()
    {
        string mediaDbPath = AppFiles.MediaDatabase;
        string queueDbPath = AppFiles.QueueDatabase;

        Assert.NotEqual(expected: mediaDbPath, actual: queueDbPath, comparer: StringComparer.OrdinalIgnoreCase);
    }
}
