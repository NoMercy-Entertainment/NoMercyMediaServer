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
using NoMercy.NmSystem.Configuration;
using NoMercy.Service.Seeds;
using Xunit;
using ConfigurationModel = NoMercy.Database.Models.Common.Configuration;

namespace NoMercy.Tests.Service.Seeds;

/// <summary>
/// <see cref="ConfigSeed.Init"/> upserts the current
/// <see cref="RuntimeServerSettings"/> ports into the Configuration table —
/// the row every later boot's <see cref="StartupOptions"/> port resolution
/// reads back. It must both INSERT on a fresh database and UPDATE (not
/// duplicate) on a re-run with a changed port.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConfigSeedTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly int _originalInternalPort = RuntimeServerSettings.Current.InternalServerPort;
    private readonly int _originalExternalPort = RuntimeServerSettings.Current.ExternalServerPort;

    public ConfigSeedTests()
    {
        _connection = new("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using AppDbContext ctx = new(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        RuntimeServerSettings.Current.InternalServerPort = _originalInternalPort;
        RuntimeServerSettings.Current.ExternalServerPort = _originalExternalPort;
    }

    [Fact]
    public async Task Init_FreshDatabase_InsertsBothPortRows()
    {
        RuntimeServerSettings.Current.InternalServerPort = 7626;
        RuntimeServerSettings.Current.ExternalServerPort = 7627;
        await using AppDbContext context = new(_options);

        await context.Init();

        ConfigurationModel? internalPort = await context.Configuration.FirstOrDefaultAsync(c =>
            c.Key == "internalPort"
        );
        ConfigurationModel? externalPort = await context.Configuration.FirstOrDefaultAsync(c =>
            c.Key == "externalPort"
        );
        Assert.Equal("7626", internalPort?.Value);
        Assert.Equal("7627", externalPort?.Value);
    }

    [Fact]
    public async Task Init_ReRunWithChangedPort_UpdatesExistingRowInsteadOfDuplicating()
    {
        RuntimeServerSettings.Current.InternalServerPort = 7626;
        RuntimeServerSettings.Current.ExternalServerPort = 7626;
        await using AppDbContext firstRun = new(_options);
        await firstRun.Init();

        RuntimeServerSettings.Current.InternalServerPort = 8001;
        await using AppDbContext secondRun = new(_options);
        await secondRun.Init();

        await using AppDbContext verifyContext = new(_options);
        int rowCount = await verifyContext.Configuration.CountAsync(c => c.Key == "internalPort");
        ConfigurationModel? internalPort = await verifyContext.Configuration.FirstOrDefaultAsync(
            c => c.Key == "internalPort"
        );
        Assert.Equal(1, rowCount);
        Assert.Equal("8001", internalPort?.Value);
    }
}
