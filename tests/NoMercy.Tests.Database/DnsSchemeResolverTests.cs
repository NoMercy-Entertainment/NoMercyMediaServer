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
        TokenStore.Initialize(serviceProvider: provider);

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
        bool result = DnsSchemeResolver.ResolveAndPersist(db: _context);

        Assert.True(condition: result);
        Assert.True(condition: RuntimeServerSettings.Current.UseSynthesizedDns);

        Configuration? stored = _context.Configuration.FirstOrDefault(predicate: c =>
            c.Key == DnsSchemeResolver.ConfigKey
        );
        Assert.NotNull(@object: stored);
        Assert.Equal(expected: "True", actual: stored.Value);
    }

    [Fact]
    public void ResolveAndPersist_ExistingServerWithAuthToken_ResolvesApexAndPersistsFalse()
    {
        _context.Configuration.Add(
            entity: new() { Key = "auth_access_token", SecureValue = "existing-access-token" }
        );
        _context.SaveChanges();

        bool result = DnsSchemeResolver.ResolveAndPersist(db: _context);

        Assert.False(condition: result);
        Assert.False(condition: RuntimeServerSettings.Current.UseSynthesizedDns);

        Configuration? stored = _context.Configuration.FirstOrDefault(predicate: c =>
            c.Key == DnsSchemeResolver.ConfigKey
        );
        Assert.NotNull(@object: stored);
        Assert.Equal(expected: "False", actual: stored.Value);
    }

    [Fact]
    public void ResolveAndPersist_ExistingServerWithCertificate_ResolvesApexAndPersistsFalse()
    {
        _context.Configuration.Add(
            entity: new() { Key = "ssl_certificate", SecureValue = "existing-cert" }
        );
        _context.Configuration.Add(entity: new() { Key = "ssl_private_key", SecureValue = "existing-key" });
        _context.SaveChanges();

        bool result = DnsSchemeResolver.ResolveAndPersist(db: _context);

        Assert.False(condition: result);

        Configuration? stored = _context.Configuration.FirstOrDefault(predicate: c =>
            c.Key == DnsSchemeResolver.ConfigKey
        );
        Assert.NotNull(@object: stored);
        Assert.Equal(expected: "False", actual: stored.Value);
    }

    [Fact]
    public void ResolveAndPersist_ExplicitKeyPresent_IsHonoredUnchangedAndNeverOverridden()
    {
        _context.Configuration.Add(entity: new() { Key = DnsSchemeResolver.ConfigKey, Value = "True" });
        // Evidence that would otherwise decide "apex" — must not matter once the
        // key is explicit.
        _context.Configuration.Add(
            entity: new() { Key = "auth_access_token", SecureValue = "existing-access-token" }
        );
        _context.SaveChanges();

        bool result = DnsSchemeResolver.ResolveAndPersist(db: _context);

        Assert.True(condition: result);

        List<Configuration> rows = _context
            .Configuration.Where(predicate: c => c.Key == DnsSchemeResolver.ConfigKey)
            .ToList();
        Assert.Single(collection: rows);
        Assert.Equal(expected: "True", actual: rows[index: 0].Value);
    }

    [Fact]
    public void ResolveAndPersist_ExplicitFalseWithNoPriorRegistrationEvidence_StaysApex()
    {
        // A user can explicitly opt a fresh install into apex — that choice must
        // survive even though the "no evidence" heuristic would otherwise pick srv.
        _context.Configuration.Add(entity: new() { Key = DnsSchemeResolver.ConfigKey, Value = "False" });
        _context.SaveChanges();

        bool result = DnsSchemeResolver.ResolveAndPersist(db: _context);

        Assert.False(condition: result);
    }

    [Fact]
    public void ResolveAndPersist_CalledTwiceOnFreshServer_IsIdempotent()
    {
        bool first = DnsSchemeResolver.ResolveAndPersist(db: _context);
        bool second = DnsSchemeResolver.ResolveAndPersist(db: _context);

        Assert.Equal(expected: first, actual: second);

        List<Configuration> rows = _context
            .Configuration.Where(predicate: c => c.Key == DnsSchemeResolver.ConfigKey)
            .ToList();
        Assert.Single(collection: rows);
    }

    private static AppDbContext CreateFreshContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");

        AppDbContext context = new(options: optionsBuilder.Options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return context;
    }
}
