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

using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// Kestrel's certificate selector serves ONLY the in-memory cache, but every new
/// DI container starts with that cache empty even when the real cert sits in the
/// DB. The setup→HTTPS restart (ServerRunner.RunWithHttpsRestart) builds exactly
/// such a fresh container: EnsureHttpsCertificate() answered "ready" from the DB
/// row, Kestrel bound TLS, and every handshake then threw "No SSL certificate
/// loaded". These tests pin the contract that a fresh instance which reports a
/// usable certificate can actually serve it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CertificateSelectorFreshContainerTests : IDisposable
{
    public CertificateSelectorFreshContainerTests()
    {
        Directory.CreateDirectory(AppFiles.DataPath);
        using AppDbContext db = new();
        db.Database.EnsureCreated();

        using X509Certificate2 cert = CreateCert(DateTimeOffset.UtcNow.AddDays(60));
        using RSA rsa = cert.GetRSAPrivateKey()!;

        UpsertConfig(db, "ssl_certificate", cert.ExportCertificatePem());
        UpsertConfig(db, "ssl_private_key", rsa.ExportRSAPrivateKeyPem());
        db.SaveChanges();
    }

    public void Dispose()
    {
        using AppDbContext db = new();
        db.Configuration.RemoveRange(
            db.Configuration.Where(c => c.Key == "ssl_certificate" || c.Key == "ssl_private_key")
        );
        db.SaveChanges();
    }

    private static void UpsertConfig(AppDbContext db, string key, string value)
    {
        Configuration? existing = db.Configuration.FirstOrDefault(c => c.Key == key);
        if (existing != null)
            existing.SecureValue = value;
        else
            db.Configuration.Add(new() { Key = key, SecureValue = value });
    }

    private static X509Certificate2 CreateCert(DateTimeOffset notAfter)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest req = new(
            "CN=fresh-container.nomercy.tv",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), notAfter);
    }

    private static CertificateService BuildFreshService()
    {
        return new(NullLogger<CertificateService>.Instance, new NullHttpClientFactory());
    }

    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException(
                "HttpClientFactory must not be called in this test"
            );
    }

    private static Func<
        Microsoft.AspNetCore.Connections.ConnectionContext?,
        string?,
        X509Certificate2?
    > GetServerCertificateSelector(CertificateService service)
    {
        MethodInfo method = typeof(CertificateService).GetMethod(
            "HttpsConnectionAdapterOptions",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        HttpsConnectionAdapterOptions options = (HttpsConnectionAdapterOptions)
            method.Invoke(service, null)!;
        return options.ServerCertificateSelector!;
    }

    [Fact]
    public void EnsureHttpsCertificate_OnFreshInstance_LoadsDbCertIntoSelectorCache()
    {
        CertificateService service = BuildFreshService();

        Assert.True(service.EnsureHttpsCertificate());

        X509Certificate2? served = GetServerCertificateSelector(service)(null, null);

        Assert.NotNull(served);
        Assert.Equal("CN=fresh-container.nomercy.tv", served!.Subject);
    }

    [Fact]
    public void EnsureHttpsCertificate_OnFreshInstance_ServesRealCertNotSelfSignedFallback()
    {
        CertificateService service = BuildFreshService();

        Assert.True(service.EnsureHttpsCertificate());

        X509Certificate2? served = GetServerCertificateSelector(service)(null, null);

        Assert.NotNull(served);
        Assert.DoesNotContain("NoMercy MediaServer", served!.Subject);
    }
}
