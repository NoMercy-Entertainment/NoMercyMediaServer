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
using NoMercy.Database.Models.Media;
using NoMercy.Service.Seeds;
using NoMercy.Storage;
using Xunit;

namespace NoMercy.Tests.Service.Seeds;

/// <summary>
/// <see cref="EncodingPresetsSeed.Init"/> — unlike the other provider-backed
/// seeds — is pure database work (<see cref="NoMercy.Encoder.Profiles.BuiltinPresetSeeder"/>
/// reads only the curated built-in preset list, no network) and must run on
/// EVERY boot, not once. It must be idempotent (upserts, never duplicates) and
/// must never let one bad preset's failure take down the rest of startup.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class EncodingPresetsSeedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public EncodingPresetsSeedTests()
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
    public async Task Init_FreshDatabase_SeedsBuiltInPresets()
    {
        await using MediaContext context = new(options: _options);

        await EncodingPresetsSeed.Init(context: context, storage: Mock.Of<IStorage>());

        int count = await context.EncodingPresets.CountAsync(predicate: p => p.IsBuiltIn);
        Assert.True(condition: count > 0, userMessage: "expected at least one built-in preset to be seeded");
    }

    [Fact]
    public async Task Init_RunTwice_IsIdempotent()
    {
        await using MediaContext firstRun = new(options: _options);
        await EncodingPresetsSeed.Init(context: firstRun, storage: Mock.Of<IStorage>());

        await using MediaContext secondRun = new(options: _options);
        await EncodingPresetsSeed.Init(context: secondRun, storage: Mock.Of<IStorage>());

        await using MediaContext verifyContext = new(options: _options);
        List<EncodingPreset> presets = await verifyContext
            .EncodingPresets.Where(predicate: p => p.IsBuiltIn)
            .ToListAsync();
        Assert.Equal(expected: presets.Select(selector: p => p.Id).Distinct().Count(), actual: presets.Count);
    }
}
