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
using NoMercy.Database.Models.Storage;
using NoMercy.Service.Seeds;
using Xunit;

namespace NoMercy.Tests.Service.Seeds;

/// <summary>
/// <see cref="DatabaseSeeder.SeedSystemLocalDriver"/> must be idempotent — it
/// runs on every boot via <c>SeedOfflineData</c>, so a non-idempotent insert
/// would throw a unique-constraint violation on the second boot of every
/// install. It must also seed the EXACT well-known <see cref="Driver.SystemLocalDriverId"/>
/// with an empty rootPath — every folder's storage guard is built from the
/// folder's own subPath against this driver, so a wrong ID or a non-empty
/// rootPath here breaks per-folder path isolation for every local folder.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class DatabaseSeederTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public DatabaseSeederTests()
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
    public async Task SeedSystemLocalDriver_EmptyDrivers_InsertsWellKnownLocalDriver()
    {
        await using MediaContext context = new(options: _options);

        await DatabaseSeeder.SeedSystemLocalDriver(mediaContext: context);

        Driver? driver = await context.Drivers.FirstOrDefaultAsync(predicate: d =>
            d.Id == Driver.SystemLocalDriverId
        );
        Assert.NotNull(@object: driver);
        Assert.Equal(expected: "local", actual: driver.Type);
        Assert.Equal(expected: "{\"rootPath\":\"\"}", actual: driver.Config);
    }

    [Fact]
    public async Task SeedSystemLocalDriver_AlreadyExists_DoesNotInsertASecondRow()
    {
        await using MediaContext seedContext = new(options: _options);
        await DatabaseSeeder.SeedSystemLocalDriver(mediaContext: seedContext);

        await using MediaContext context = new(options: _options);

        // Idempotent re-run must not throw a unique-constraint violation and
        // must not create a duplicate row.
        await DatabaseSeeder.SeedSystemLocalDriver(mediaContext: context);

        int count = await context.Drivers.CountAsync(predicate: d => d.Id == Driver.SystemLocalDriverId);
        Assert.Equal(expected: 1, actual: count);
    }
}
