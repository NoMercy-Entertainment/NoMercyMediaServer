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
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Security;

namespace NoMercy.Tests.Database;

public class DeviceIdentityResolverTests : IDisposable
{
    private readonly AppDbContext _context;

    public DeviceIdentityResolverTests()
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
    public void ResolveAndPersist_PersistedId_IsStableAcrossCalls()
    {
        Guid first = DeviceIdentityResolver.ResolveAndPersist(_context);
        Guid second = DeviceIdentityResolver.ResolveAndPersist(_context);

        Assert.Equal(first, second);

        Configuration? stored = _context.Configuration.FirstOrDefault(c =>
            c.Key == DeviceIdentityResolver.ConfigKey
        );
        Assert.NotNull(stored);
        Assert.Equal(first.ToString(), stored.Value);
    }

    [Fact]
    public void ResolveAndPersist_NoPriorRegistration_GetsUniqueNonCollidingId()
    {
        using AppDbContext otherInstall = CreateFreshContext();

        Guid firstInstallId = DeviceIdentityResolver.ResolveAndPersist(_context);
        Guid secondInstallId = DeviceIdentityResolver.ResolveAndPersist(otherInstall);

        Assert.NotEqual(Guid.Empty, firstInstallId);
        Assert.NotEqual(Guid.Empty, secondInstallId);
        Assert.NotEqual(firstInstallId, secondInstallId);
    }

    [Fact]
    public void ResolveAndPersist_EvidenceOfPriorRegistration_KeepsHardwareDerivedId()
    {
        _context.Configuration.Add(
            new() { Key = "ssl_certificate", SecureValue = "existing-cert" }
        );
        _context.Configuration.Add(new() { Key = "ssl_private_key", SecureValue = "existing-key" });
        _context.SaveChanges();

        Guid resolvedId = DeviceIdentityResolver.ResolveAndPersist(_context);

        Assert.Equal(Info.DeviceId, resolvedId);

        // Regression guard: the non-degenerate path must never touch the cert rows.
        Assert.NotNull(_context.Configuration.FirstOrDefault(c => c.Key == "ssl_certificate"));
        Assert.NotNull(_context.Configuration.FirstOrDefault(c => c.Key == "ssl_private_key"));
    }

    [Fact]
    public void ResolveAndPersist_DegenerateHardwareId_MigratesEvenWithPriorRegistrationEvidence()
    {
        Guid knownDegenerateId = KnownDegenerateDeviceIds.Values.First();

        _context.Configuration.Add(
            new() { Key = "ssl_certificate", SecureValue = "existing-cert" }
        );
        _context.Configuration.Add(new() { Key = "ssl_private_key", SecureValue = "existing-key" });
        _context.Configuration.Add(
            new() { Key = "auth_access_token", SecureValue = "existing-access-token" }
        );
        _context.Configuration.Add(
            new() { Key = "auth_refresh_token", SecureValue = "existing-refresh-token" }
        );
        _context.SaveChanges();

        Guid resolvedId = DeviceIdentityResolver.ResolveAndPersist(_context, knownDegenerateId);

        Assert.NotEqual(knownDegenerateId, resolvedId);
        Assert.False(KnownDegenerateDeviceIds.IsDegenerate(resolvedId));

        // The stale cert must be gone — otherwise HasValidCertificate() keeps
        // reporting "registered" under a certificate that doesn't cover the new id.
        Assert.Null(_context.Configuration.FirstOrDefault(c => c.Key == "ssl_certificate"));
        Assert.Null(_context.Configuration.FirstOrDefault(c => c.Key == "ssl_private_key"));

        // Auth tokens must survive so the re-registration that follows is non-interactive.
        Configuration? accessToken = _context.Configuration.FirstOrDefault(c =>
            c.Key == "auth_access_token"
        );
        Configuration? refreshToken = _context.Configuration.FirstOrDefault(c =>
            c.Key == "auth_refresh_token"
        );
        Assert.NotNull(accessToken);
        Assert.Equal("existing-access-token", accessToken.SecureValue);
        Assert.NotNull(refreshToken);
        Assert.Equal("existing-refresh-token", refreshToken.SecureValue);

        Configuration? deviceIdRow = _context.Configuration.FirstOrDefault(c =>
            c.Key == DeviceIdentityResolver.ConfigKey
        );
        Assert.NotNull(deviceIdRow);
        Assert.Equal(resolvedId.ToString(), deviceIdRow.Value);
    }

    [Fact]
    public void ResolveAndPersist_PersistedDegenerateId_IsReplaced()
    {
        Guid knownDegenerateId = KnownDegenerateDeviceIds.Values.First();

        _context.Configuration.Add(
            new() { Key = DeviceIdentityResolver.ConfigKey, Value = knownDegenerateId.ToString() }
        );
        _context.SaveChanges();

        Guid resolvedId = DeviceIdentityResolver.ResolveAndPersist(_context, knownDegenerateId);

        Assert.NotEqual(knownDegenerateId, resolvedId);
        Assert.False(KnownDegenerateDeviceIds.IsDegenerate(resolvedId));

        Configuration? stored = _context.Configuration.FirstOrDefault(c =>
            c.Key == DeviceIdentityResolver.ConfigKey
        );
        Assert.NotNull(stored);
        Assert.Equal(resolvedId.ToString(), stored.Value);
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
