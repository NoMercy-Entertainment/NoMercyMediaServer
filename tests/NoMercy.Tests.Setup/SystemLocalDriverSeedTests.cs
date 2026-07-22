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

namespace NoMercy.Tests.Setup;

[Trait(name: "Category", value: "Unit")]
public class SystemLocalDriverSeedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public SystemLocalDriverSeedTests()
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

    [Fact]
    public async Task SeedSystemLocalDriver_InsertsSingleRow()
    {
        await using MediaContext ctx = CreateContext();

        await DatabaseSeeder.SeedSystemLocalDriver(mediaContext: ctx);

        int count = await ctx.Drivers.CountAsync(predicate: d => d.Id == Driver.SystemLocalDriverId);
        Assert.Equal(expected: 1, actual: count);
    }

    [Fact]
    public async Task SeedSystemLocalDriver_IsIdempotent_RunTwice_StillOneRow()
    {
        await using MediaContext ctx = CreateContext();

        await DatabaseSeeder.SeedSystemLocalDriver(mediaContext: ctx);
        await DatabaseSeeder.SeedSystemLocalDriver(mediaContext: ctx);

        int count = await ctx.Drivers.CountAsync(predicate: d => d.Id == Driver.SystemLocalDriverId);
        Assert.Equal(expected: 1, actual: count);
    }

    [Fact]
    public async Task SeedSystemLocalDriver_RowHasExpectedValues()
    {
        await using MediaContext ctx = CreateContext();

        await DatabaseSeeder.SeedSystemLocalDriver(mediaContext: ctx);

        Driver? driver = await ctx.Drivers.FindAsync(keyValues: Driver.SystemLocalDriverId);
        Assert.NotNull(@object: driver);
        Assert.Equal(expected: "Local", actual: driver.Name);
        Assert.Equal(expected: "local", actual: driver.Type);
    }

    [Fact]
    public async Task SeedSystemLocalDriver_DoesNotInsertWhenAlreadyPresent()
    {
        await using MediaContext ctx = CreateContext();

        // Pre-insert a row with the same id to simulate an existing install.
        ctx.Drivers.Add(
            entity: new()
            {
                Id = Driver.SystemLocalDriverId,
                Name = "Local",
                Type = "local",
                Config = "{\"rootPath\":\"\"}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        await ctx.SaveChangesAsync();

        // Should not throw a unique-constraint violation.
        await DatabaseSeeder.SeedSystemLocalDriver(mediaContext: ctx);

        int count = await ctx.Drivers.CountAsync(predicate: d => d.Id == Driver.SystemLocalDriverId);
        Assert.Equal(expected: 1, actual: count);
    }

    [Fact]
    public void SystemLocalDriverId_IsStable()
    {
        // The id must never change between builds — clients rely on it.
        Ulid id = Driver.SystemLocalDriverId;
        Assert.Equal(expected: "01JKQSTS00000000000000000A", actual: id.ToString());
    }
}
