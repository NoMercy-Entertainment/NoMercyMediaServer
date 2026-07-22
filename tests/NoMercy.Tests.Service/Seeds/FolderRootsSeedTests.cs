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
using NoMercy.NmSystem.Information;
using NoMercy.Service.Seeds;
using NoMercy.Storage;
using Xunit;

namespace NoMercy.Tests.Service.Seeds;

/// <summary>
/// <see cref="FolderRootsSeed.Init"/> must no-op cleanly when the seed file
/// (<see cref="AppFiles.FolderRootsSeedFile"/>) is absent — the common case on
/// every boot after the first, since the file is a first-install convenience
/// only. It must never touch the database or the dynamic-static-files
/// middleware registration in that case.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class FolderRootsSeedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public FolderRootsSeedTests()
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

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Init_SeedFileMissing_ReturnsWithoutTouchingDatabase()
    {
        Mock<IStorage> storage = new(behavior: MockBehavior.Strict);
        storage.Setup(expression: s => s.Exists(AppFiles.FolderRootsSeedFile)).Returns(value: false);
        Mock<IStorageDriver> driver = new();

        await using MediaContext context = new(options: _options);

        await FolderRootsSeed.Init(dbContext: context, storage: storage.Object, storageDriver: driver.Object);

        int folderCount = await context.Folders.CountAsync();
        Assert.Equal(expected: 0, actual: folderCount);
        // Strict mock: any call beyond Exists() (ReadAllTextAsync, etc.) would
        // have thrown above.
        storage.Verify(expression: s => s.Exists(AppFiles.FolderRootsSeedFile), times: Times.Once);
    }
}
