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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Security;

namespace NoMercy.Tests.Database;

public class AppDbContextTests : IDisposable
{
    private readonly AppDbContext _context;

    public AppDbContextTests()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();

        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");

        _context = new(optionsBuilder.Options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task Configuration_StoresPlainValue()
    {
        _context.Configuration.Add(new() { Key = "server_name", Value = "My Server" });
        await _context.SaveChangesAsync();

        Configuration? loaded = await _context.Configuration.FirstOrDefaultAsync(c =>
            c.Key == "server_name"
        );

        Assert.NotNull(loaded);
        Assert.Equal("My Server", loaded.Value);
    }

    [Fact]
    public async Task SecureValue_EncryptsOnSave_DecryptsOnRead()
    {
        string secret = "my-super-secret-token";

        _context.Configuration.Add(new() { Key = "auth_access_token", SecureValue = secret });
        await _context.SaveChangesAsync();

        Configuration? loaded = await _context.Configuration.FirstOrDefaultAsync(c =>
            c.Key == "auth_access_token"
        );

        Assert.NotNull(loaded);
        Assert.Equal(secret, loaded.SecureValue);
    }

    [Fact]
    public async Task SecureValue_NullRoundtrip()
    {
        _context.Configuration.Add(
            new()
            {
                Key = "no_secret",
                Value = "some plain value",
                SecureValue = null,
            }
        );
        await _context.SaveChangesAsync();

        Configuration? loaded = await _context.Configuration.FirstOrDefaultAsync(c =>
            c.Key == "no_secret"
        );

        Assert.NotNull(loaded);
        Assert.Null(loaded.SecureValue);
        Assert.Equal("some plain value", loaded.Value);
    }

    [Fact]
    public async Task Configuration_KeyIsUnique()
    {
        _context.Configuration.Add(new() { Key = "unique_key", Value = "first" });
        await _context.SaveChangesAsync();

        _context.Configuration.Add(new() { Key = "unique_key", Value = "second" });

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }
}
