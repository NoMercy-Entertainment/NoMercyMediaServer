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
using NoMercy.Database.Maintenance;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Security;

namespace NoMercy.Tests.Database;

public class DnsSchemeResolverTests : IDisposable
{
    private readonly AppDbContext _context;

    public DnsSchemeResolverTests()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(provider);

        _context = CreateFreshContext();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public void ResolveAndPersist_FreshServer_ResolvesSrvAndPersistsTrue()
    {
        bool result = DnsSchemeResolver.ResolveAndPersist(_context);

        Assert.True(result);
        Assert.True(RuntimeServerSettings.Current.UseSynthesizedDns);

        Configuration? stored = _context.Configuration.FirstOrDefault(c =>
            c.Key == DnsSchemeResolver.ConfigKey
        );
        Assert.NotNull(stored);
        Assert.Equal("True", stored.Value);
    }

    [Fact]
    public void ResolveAndPersist_ExistingServerWithAuthToken_ResolvesApexAndPersistsFalse()
    {
        _context.Configuration.Add(
            new() { Key = "auth_access_token", SecureValue = "existing-access-token" }
        );
        _context.SaveChanges();

        bool result = DnsSchemeResolver.ResolveAndPersist(_context);

        Assert.False(result);
        Assert.False(RuntimeServerSettings.Current.UseSynthesizedDns);

        Configuration? stored = _context.Configuration.FirstOrDefault(c =>
            c.Key == DnsSchemeResolver.ConfigKey
        );
        Assert.NotNull(stored);
        Assert.Equal("False", stored.Value);
    }

    [Fact]
    public void ResolveAndPersist_ExistingServerWithCertificate_ResolvesApexAndPersistsFalse()
    {
        _context.Configuration.Add(
            new() { Key = "ssl_certificate", SecureValue = "existing-cert" }
        );
        _context.Configuration.Add(new() { Key = "ssl_private_key", SecureValue = "existing-key" });
        _context.SaveChanges();

        bool result = DnsSchemeResolver.ResolveAndPersist(_context);

        Assert.False(result);

        Configuration? stored = _context.Configuration.FirstOrDefault(c =>
            c.Key == DnsSchemeResolver.ConfigKey
        );
        Assert.NotNull(stored);
        Assert.Equal("False", stored.Value);
    }

    [Fact]
    public void ResolveAndPersist_ExplicitKeyPresent_IsHonoredUnchangedAndNeverOverridden()
    {
        _context.Configuration.Add(new() { Key = DnsSchemeResolver.ConfigKey, Value = "True" });
        // Evidence that would otherwise decide "apex" — must not matter once the
        // key is explicit.
        _context.Configuration.Add(
            new() { Key = "auth_access_token", SecureValue = "existing-access-token" }
        );
        _context.SaveChanges();

        bool result = DnsSchemeResolver.ResolveAndPersist(_context);

        Assert.True(result);

        List<Configuration> rows =
        [
            .. _context.Configuration.Where(c => c.Key == DnsSchemeResolver.ConfigKey),
        ];
        Assert.Single(rows);
        Assert.Equal("True", rows[0].Value);
    }

    [Fact]
    public void ResolveAndPersist_ExplicitFalseWithNoPriorRegistrationEvidence_StaysApex()
    {
        // A user can explicitly opt a fresh install into apex — that choice must
        // survive even though the "no evidence" heuristic would otherwise pick srv.
        _context.Configuration.Add(new() { Key = DnsSchemeResolver.ConfigKey, Value = "False" });
        _context.SaveChanges();

        bool result = DnsSchemeResolver.ResolveAndPersist(_context);

        Assert.False(result);
    }

    [Fact]
    public void ResolveAndPersist_CalledTwiceOnFreshServer_IsIdempotent()
    {
        bool first = DnsSchemeResolver.ResolveAndPersist(_context);
        bool second = DnsSchemeResolver.ResolveAndPersist(_context);

        Assert.Equal(first, second);

        List<Configuration> rows =
        [
            .. _context.Configuration.Where(c => c.Key == DnsSchemeResolver.ConfigKey),
        ];
        Assert.Single(rows);
    }

    private static AppDbContext CreateFreshContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");

        AppDbContext context = new(optionsBuilder.Options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return context;
    }
}
