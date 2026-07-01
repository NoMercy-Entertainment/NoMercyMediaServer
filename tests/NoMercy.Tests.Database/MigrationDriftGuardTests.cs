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
        builder.UseSqlite("Data Source=:memory:");

        using MediaContext ctx = new(builder.Options);

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
            "MediaContext has pending model changes not captured in a migration. Run: dotnet ef migrations add <Name> --context MediaContext"
        );
    }

    [Fact]
    public void QueueContext_HasNoPendingModelChanges()
    {
        DbContextOptionsBuilder<QueueContext> builder = new();
        builder.UseSqlite("Data Source=:memory:");

        using QueueContext ctx = new(builder.Options);

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
            "QueueContext has pending model changes not captured in a migration. Run: dotnet ef migrations add <Name> --context QueueContext"
        );
    }

    [Fact]
    public void MediaContext_DoesNotContain_QueueEntities()
    {
        DbContextOptionsBuilder<MediaContext> builder = new();
        builder.UseSqlite("Data Source=:memory:");

        using MediaContext ctx = new(builder.Options);

        IEnumerable<string> entityTypeNames = ctx
            .Model.GetEntityTypes()
            .Select(t => t.ClrType.Name);

        Assert.DoesNotContain("QueueJob", entityTypeNames);
        Assert.DoesNotContain("FailedJob", entityTypeNames);
        Assert.DoesNotContain("CronJob", entityTypeNames);
    }

    [Fact]
    public void QueueContext_DoesNotContain_MediaEntities()
    {
        DbContextOptionsBuilder<QueueContext> builder = new();
        builder.UseSqlite("Data Source=:memory:");

        using QueueContext ctx = new(builder.Options);

        IEnumerable<string> entityTypeNames = ctx
            .Model.GetEntityTypes()
            .Select(t => t.ClrType.Name);

        Assert.DoesNotContain("Movie", entityTypeNames);
        Assert.DoesNotContain("Library", entityTypeNames);
        Assert.DoesNotContain("Track", entityTypeNames);
        Assert.DoesNotContain("User", entityTypeNames);
    }

    [Fact]
    public void MediaContext_MapsTo_MediaDatabase()
    {
        string mediaDbPath = AppFiles.MediaDatabase;

        Assert.True(
            mediaDbPath.EndsWith("media.db", StringComparison.OrdinalIgnoreCase),
            $"AppFiles.MediaDatabase must resolve to a path ending in 'media.db'. Got: {mediaDbPath}"
        );
    }

    [Fact]
    public void QueueContext_MapsTo_QueueDatabase()
    {
        string queueDbPath = AppFiles.QueueDatabase;

        Assert.True(
            queueDbPath.EndsWith("queue.db", StringComparison.OrdinalIgnoreCase),
            $"AppFiles.QueueDatabase must resolve to a path ending in 'queue.db'. Got: {queueDbPath}"
        );
    }

    [Fact]
    public void MediaDatabase_And_QueueDatabase_AreDistinctPaths()
    {
        string mediaDbPath = AppFiles.MediaDatabase;
        string queueDbPath = AppFiles.QueueDatabase;

        Assert.NotEqual(mediaDbPath, queueDbPath, StringComparer.OrdinalIgnoreCase);
    }
}
