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
        TokenStore.Initialize(serviceProvider: provider);

        _context = CreateFreshContext();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public void ResolveAndPersist_Container_PersistedId_IsStableAcrossCalls()
    {
        Guid first = DeviceIdentityResolver.ResolveAndPersist(db: _context, inContainer: true);
        Guid second = DeviceIdentityResolver.ResolveAndPersist(db: _context, inContainer: true);

        Assert.Equal(expected: first, actual: second);

        Configuration? stored = _context.Configuration.FirstOrDefault(predicate: c =>
            c.Key == DeviceIdentityResolver.ConfigKey
        );
        Assert.NotNull(@object: stored);
        Assert.Equal(expected: first.ToString(), actual: stored.Value);
    }

    [Fact]
    public void ResolveAndPersist_Container_NoPriorRegistration_GetsUniqueNonCollidingId()
    {
        using AppDbContext otherInstall = CreateFreshContext();

        Guid firstInstallId = DeviceIdentityResolver.ResolveAndPersist(db: _context, inContainer: true);
        Guid secondInstallId = DeviceIdentityResolver.ResolveAndPersist(
            db: otherInstall,
            inContainer: true
        );

        Assert.NotEqual(expected: Guid.Empty, actual: firstInstallId);
        Assert.NotEqual(expected: Guid.Empty, actual: secondInstallId);
        Assert.NotEqual(expected: firstInstallId, actual: secondInstallId);
    }

    [Fact]
    public void ResolveAndPersist_Container_EvidenceOfPriorRegistration_KeepsHardwareDerivedId()
    {
        _context.Configuration.Add(
            entity: new() { Key = "ssl_certificate", SecureValue = "existing-cert" }
        );
        _context.Configuration.Add(entity: new() { Key = "ssl_private_key", SecureValue = "existing-key" });
        _context.SaveChanges();

        Guid resolvedId = DeviceIdentityResolver.ResolveAndPersist(db: _context, inContainer: true);

        Assert.Equal(expected: Info.DeviceId, actual: resolvedId);

        // Regression guard: the non-degenerate path must never touch the cert rows.
        Assert.NotNull(@object: _context.Configuration.FirstOrDefault(predicate: c => c.Key == "ssl_certificate"));
        Assert.NotNull(@object: _context.Configuration.FirstOrDefault(predicate: c => c.Key == "ssl_private_key"));
    }

    [Fact]
    public void ResolveAndPersist_BareMetal_DegenerateHardwareId_IsKeptAndCertPreserved()
    {
        // Stoney's regression: a bare-metal box whose DMI reads empty hashes to a
        // known-degenerate value, but its DNS subdomain + certificate were issued
        // for exactly that id. It must NOT be migrated off the machine — the id is
        // returned untouched and the certificate rows are preserved.
        Guid knownDegenerateId = KnownDegenerateDeviceIds.Values.First();

        _context.Configuration.Add(
            entity: new() { Key = "ssl_certificate", SecureValue = "existing-cert" }
        );
        _context.Configuration.Add(entity: new() { Key = "ssl_private_key", SecureValue = "existing-key" });
        _context.SaveChanges();

        Guid resolvedId = DeviceIdentityResolver.ResolveAndPersist(
            db: _context,
            hardwareDerivedId: knownDegenerateId,
            inContainer: false
        );

        Assert.Equal(expected: knownDegenerateId, actual: resolvedId);
        Assert.NotNull(@object: _context.Configuration.FirstOrDefault(predicate: c => c.Key == "ssl_certificate"));
        Assert.NotNull(@object: _context.Configuration.FirstOrDefault(predicate: c => c.Key == "ssl_private_key"));
    }

    [Fact]
    public void ResolveAndPersist_Container_DegenerateHardwareId_MigratesEvenWithPriorRegistrationEvidence()
    {
        Guid knownDegenerateId = KnownDegenerateDeviceIds.Values.First();

        _context.Configuration.Add(
            entity: new() { Key = "ssl_certificate", SecureValue = "existing-cert" }
        );
        _context.Configuration.Add(entity: new() { Key = "ssl_private_key", SecureValue = "existing-key" });
        _context.Configuration.Add(
            entity: new() { Key = "auth_access_token", SecureValue = "existing-access-token" }
        );
        _context.Configuration.Add(
            entity: new() { Key = "auth_refresh_token", SecureValue = "existing-refresh-token" }
        );
        _context.SaveChanges();

        Guid resolvedId = DeviceIdentityResolver.ResolveAndPersist(
            db: _context,
            hardwareDerivedId: knownDegenerateId,
            inContainer: true
        );

        Assert.NotEqual(expected: knownDegenerateId, actual: resolvedId);
        Assert.False(condition: KnownDegenerateDeviceIds.IsDegenerate(id: resolvedId));

        // The stale cert must be gone — otherwise HasValidCertificate() keeps
        // reporting "registered" under a certificate that doesn't cover the new id.
        Assert.Null(@object: _context.Configuration.FirstOrDefault(predicate: c => c.Key == "ssl_certificate"));
        Assert.Null(@object: _context.Configuration.FirstOrDefault(predicate: c => c.Key == "ssl_private_key"));

        // Auth tokens must survive so the re-registration that follows is non-interactive.
        Configuration? accessToken = _context.Configuration.FirstOrDefault(predicate: c =>
            c.Key == "auth_access_token"
        );
        Configuration? refreshToken = _context.Configuration.FirstOrDefault(predicate: c =>
            c.Key == "auth_refresh_token"
        );
        Assert.NotNull(@object: accessToken);
        Assert.Equal(expected: "existing-access-token", actual: accessToken.SecureValue);
        Assert.NotNull(@object: refreshToken);
        Assert.Equal(expected: "existing-refresh-token", actual: refreshToken.SecureValue);

        Configuration? deviceIdRow = _context.Configuration.FirstOrDefault(predicate: c =>
            c.Key == DeviceIdentityResolver.ConfigKey
        );
        Assert.NotNull(@object: deviceIdRow);
        Assert.Equal(expected: resolvedId.ToString(), actual: deviceIdRow.Value);
    }

    [Fact]
    public void ResolveAndPersist_Container_PersistedDegenerateId_IsReplaced()
    {
        Guid knownDegenerateId = KnownDegenerateDeviceIds.Values.First();

        _context.Configuration.Add(
            entity: new() { Key = DeviceIdentityResolver.ConfigKey, Value = knownDegenerateId.ToString() }
        );
        _context.SaveChanges();

        Guid resolvedId = DeviceIdentityResolver.ResolveAndPersist(
            db: _context,
            hardwareDerivedId: knownDegenerateId,
            inContainer: true
        );

        Assert.NotEqual(expected: knownDegenerateId, actual: resolvedId);
        Assert.False(condition: KnownDegenerateDeviceIds.IsDegenerate(id: resolvedId));

        Configuration? stored = _context.Configuration.FirstOrDefault(predicate: c =>
            c.Key == DeviceIdentityResolver.ConfigKey
        );
        Assert.NotNull(@object: stored);
        Assert.Equal(expected: resolvedId.ToString(), actual: stored.Value);
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
