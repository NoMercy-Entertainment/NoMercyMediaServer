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
[Trait("Category", "Unit")]
public sealed class EncodingPresetsSeedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public EncodingPresetsSeedTests()
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

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Init_FreshDatabase_SeedsBuiltInPresets()
    {
        await using MediaContext context = new(_options);

        await EncodingPresetsSeed.Init(context, Mock.Of<IStorage>());

        int count = await context.EncodingPresets.CountAsync(p => p.IsBuiltIn);
        Assert.True(count > 0, "expected at least one built-in preset to be seeded");
    }

    [Fact]
    public async Task Init_RunTwice_IsIdempotent()
    {
        await using MediaContext firstRun = new(_options);
        await EncodingPresetsSeed.Init(firstRun, Mock.Of<IStorage>());

        await using MediaContext secondRun = new(_options);
        await EncodingPresetsSeed.Init(secondRun, Mock.Of<IStorage>());

        await using MediaContext verifyContext = new(_options);
        List<EncodingPreset> presets = await verifyContext
            .EncodingPresets.Where(p => p.IsBuiltIn)
            .ToListAsync();
        Assert.Equal(presets.Select(p => p.Id).Distinct().Count(), presets.Count);
    }
}
