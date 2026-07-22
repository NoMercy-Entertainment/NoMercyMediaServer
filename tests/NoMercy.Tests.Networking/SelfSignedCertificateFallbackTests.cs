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

using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Database;
using NoMercy.Networking.Certificate;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// Covers the self-signed HTTPS fallback (critical-path #7): when no valid Let's
/// Encrypt cert is available, the server must still serve HTTPS via a generated,
/// cached, DB-persisted self-signed certificate instead of dropping to plaintext.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class SelfSignedCertificateFallbackTests : IDisposable
{
    public SelfSignedCertificateFallbackTests()
    {
        // The self-signed cert/key are persisted as Configuration.SecureValue rows —
        // same DB, same storage mechanism as the real LE cert. Ensure the data
        // directory and schema exist on this process-isolated test DB
        // (NOMERCY_APP_PATH) before writing — TestEnvironmentSetup only creates the
        // app-data root, not the "data" subfolder SQLite needs for app.db.
        Directory.CreateDirectory(path: AppFiles.DataPath);
        using AppDbContext db = new();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        using AppDbContext db = new();
        db.Configuration.RemoveRange(
            entities: db.Configuration.Where(predicate: c =>
                c.Key == "ssl_selfsigned_certificate" || c.Key == "ssl_selfsigned_private_key"
            )
        );
        db.SaveChanges();
    }

    private static CertificateService BuildService(INetworkDiscovery? networkDiscovery = null)
    {
        return new(
            logger: NullLogger<CertificateService>.Instance,
            httpClientFactory: new NullHttpClientFactory(),
            networkDiscovery: networkDiscovery
        );
    }

    private static X509Certificate2? GetCachedSelfSignedCertificate(CertificateService service)
    {
        FieldInfo field = typeof(CertificateService).GetField(
            name: "_selfSignedCertificate",
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        return (X509Certificate2?)field.GetValue(obj: service);
    }

    private static void InjectCachedRealCertificate(
        CertificateService service,
        X509Certificate2 cert
    )
    {
        FieldInfo field = typeof(CertificateService).GetField(
            name: "_cachedCertificate",
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        field.SetValue(obj: service, value: cert);
    }

    private static X509Certificate2 CreateRealCert(DateTimeOffset notAfter)
    {
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        CertificateRequest req = new(
            subjectName: "CN=real.nomercy.tv",
            key: rsa,
            hashAlgorithm: HashAlgorithmName.SHA256,
            padding: RSASignaturePadding.Pkcs1
        );
        return req.CreateSelfSigned(notBefore: DateTimeOffset.UtcNow.AddDays(days: -1), notAfter: notAfter);
    }

    private static Func<
        Microsoft.AspNetCore.Connections.ConnectionContext?,
        string?,
        X509Certificate2?
    > GetServerCertificateSelector(CertificateService service)
    {
        MethodInfo method = typeof(CertificateService).GetMethod(
            name: "HttpsConnectionAdapterOptions",
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        HttpsConnectionAdapterOptions options = (HttpsConnectionAdapterOptions)
            method.Invoke(obj: service, parameters: null)!;
        return options.ServerCertificateSelector!;
    }

    /// <summary>
    /// <c>ListenOptions.IsTls</c> is internal — there is no public API to observe
    /// whether <c>UseHttps(...)</c> actually ran versus a plaintext HTTP/1.1 listener,
    /// so this reflects into the internal flag to make that outcome observable in a test.
    /// </summary>
    private static bool IsTlsEnabled(ListenOptions listenOptions)
    {
        PropertyInfo property = typeof(ListenOptions).GetProperty(
            name: "IsTls",
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        return (bool)property.GetValue(obj: listenOptions)!;
    }

    /// <summary>
    /// <c>ListenOptionsHttpsExtensions.UseHttps</c> resolves internal Kestrel services
    /// (logging, <c>KestrelMetrics</c>) off <c>KestrelServerOptions.ApplicationServices</c>.
    /// Real Kestrel always has these wired via the host's DI container; the cheapest way
    /// to get an equivalent container in a unit test — without hand-registering Kestrel's
    /// internal types — is to build (never run) a throwaway minimal host, which defaults
    /// to Kestrel and registers everything it needs.
    /// </summary>
    private static IServiceProvider BuildMinimalKestrelServices()
    {
        // Build() only assembles the DI container — it never binds a socket (that
        // only happens on StartAsync/RunAsync, which this helper never calls).
        WebApplication app = WebApplication.CreateBuilder().Build();
        return app.Services;
    }

    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException(
                message: "HttpClientFactory must not be called in this test"
            );
    }

    private sealed class FakeNetworkDiscovery : INetworkDiscovery
    {
        public string InternalIp { get; set; } = "192.168.50.20";
        public string RegistrationInternalIp => InternalIp;
        public string ExternalIp { get; set; } = "203.0.113.9";
        public string? InternalIpV6 => null;
        public string? ExternalIpV6 { get; set; }
        public string InternalDomain => "192-168-50-20.test-device.nomercy.tv";
        public string InternalAddress => $"https://{InternalDomain}:7626";
        public string ExternalDomain => "203-0-113-9.test-device.nomercy.tv";
        public string ExternalAddress => $"https://{ExternalDomain}:7627";
        public string? ExternalAddressV6 => null;
        public bool Ipv6Enabled => false;

        public Task DiscoverExternalIpAsync() => Task.CompletedTask;

        public Task ForceRediscoveryAsync() => Task.CompletedTask;

        public Task<bool> IsPortOpenAsync() => Task.FromResult(result: false);
    }

    [Fact]
    public void EnsureHttpsCertificate_GeneratesSelfSignedCert_WithExpectedSans()
    {
        FakeNetworkDiscovery discovery = new();
        CertificateService service = BuildService(networkDiscovery: discovery);

        bool result = service.EnsureHttpsCertificate();

        Assert.True(condition: result);
        X509Certificate2? cert = GetCachedSelfSignedCertificate(service: service);
        Assert.NotNull(@object: cert);

        X509Extension sanExtension = Assert.Single(
            collection: cert!.Extensions.Cast<X509Extension>(),
            predicate: e => e.Oid?.Value == "2.5.29.17"
        );
        string sanText = sanExtension.Format(multiLine: false);

        Assert.Contains(expectedSubstring: "localhost", actualString: sanText, comparisonType: StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedSubstring: "127.0.0.1", actualString: sanText);
        Assert.Contains(expectedSubstring: discovery.InternalDomain, actualString: sanText);
        Assert.Contains(expectedSubstring: discovery.ExternalDomain, actualString: sanText);
        Assert.True(condition: cert.NotAfter > DateTime.Now.AddMonths(months: 6));
        Assert.True(condition: cert.NotAfter < DateTime.Now.AddYears(value: 2));
    }

    [Fact]
    public void EnsureHttpsCertificate_DoesNotGenerateSelfSigned_WhenRealCertIsValid()
    {
        CertificateService service = BuildService();
        using X509Certificate2 realCert = CreateRealCert(notAfter: DateTimeOffset.UtcNow.AddDays(days: 60));
        InjectCachedRealCertificate(service: service, cert: realCert);

        bool result = service.EnsureHttpsCertificate();

        Assert.True(condition: result);
        Assert.Null(@object: GetCachedSelfSignedCertificate(service: service));
    }

    [Fact]
    public void ServerCertificateSelector_PrefersRealCert_OverSelfSignedFallback()
    {
        CertificateService service = BuildService();
        using X509Certificate2 realCert = CreateRealCert(notAfter: DateTimeOffset.UtcNow.AddDays(days: 60));
        InjectCachedRealCertificate(service: service, cert: realCert);

        // Force a self-signed fallback to exist too, so both are cached simultaneously —
        // the selector must still hand back the real cert.
        Assert.True(condition: service.EnsureHttpsCertificate());
        FieldInfo selfSignedField = typeof(CertificateService).GetField(
            name: "_selfSignedCertificate",
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        using X509Certificate2 forcedSelfSigned = CreateRealCert(notAfter: DateTimeOffset.UtcNow.AddDays(days: 30));
        selfSignedField.SetValue(obj: service, value: forcedSelfSigned);

        Func<
            Microsoft.AspNetCore.Connections.ConnectionContext?,
            string?,
            X509Certificate2?
        > selector = GetServerCertificateSelector(service: service);
        X509Certificate2? selected = selector(arg1: null, arg2: null);

        Assert.NotNull(@object: selected);
        Assert.Equal(expected: realCert.Thumbprint, actual: selected!.Thumbprint);
        Assert.NotEqual(expected: forcedSelfSigned.Thumbprint, actual: selected.Thumbprint);
    }

    [Fact]
    public void ServerCertificateSelector_ReturnsSelfSigned_WhenNoRealCertExists()
    {
        CertificateService service = BuildService();

        Assert.True(condition: service.EnsureHttpsCertificate());
        X509Certificate2? generated = GetCachedSelfSignedCertificate(service: service);
        Assert.NotNull(@object: generated);

        Func<
            Microsoft.AspNetCore.Connections.ConnectionContext?,
            string?,
            X509Certificate2?
        > selector = GetServerCertificateSelector(service: service);
        X509Certificate2? selected = selector(arg1: null, arg2: null);

        Assert.NotNull(@object: selected);
        Assert.Equal(expected: generated!.Thumbprint, actual: selected!.Thumbprint);
    }

    [Fact]
    public void SelfSignedFallback_IsStableAcrossSimulatedRestart_SameCertReloadedNotRegenerated()
    {
        // First "boot": no cached cert anywhere, no DB row yet — must generate and persist.
        CertificateService firstBoot = BuildService();
        Assert.True(condition: firstBoot.EnsureHttpsCertificate());
        X509Certificate2? generated = GetCachedSelfSignedCertificate(service: firstBoot);
        Assert.NotNull(@object: generated);

        // Second "boot": a brand new instance (mirrors a fresh CertificateService built
        // by a new DI container after a restart) with an EMPTY in-memory cache. It must
        // reload the persisted cert from the DB rather than minting a new identity.
        CertificateService secondBoot = BuildService();
        Assert.Null(@object: GetCachedSelfSignedCertificate(service: secondBoot));

        Assert.True(condition: secondBoot.EnsureHttpsCertificate());
        X509Certificate2? reloaded = GetCachedSelfSignedCertificate(service: secondBoot);

        Assert.NotNull(@object: reloaded);
        Assert.Equal(expected: generated!.Thumbprint, actual: reloaded!.Thumbprint);
        Assert.Equal(expected: generated.SerialNumber, actual: reloaded.SerialNumber);
    }

    [Fact]
    public void ConfigureHttpsListener_EnablesHttps_WhenOnlySelfSignedFallbackExists()
    {
        CertificateService service = BuildService();
        KestrelServerOptions kestrelOptions = new()
        {
            ApplicationServices = BuildMinimalKestrelServices(),
        };
        ListenOptions? captured = null;

        kestrelOptions.ListenLocalhost(
            port: 57126,
            configure: listenOptions =>
            {
                // Mirror what WebHostFactory sets before delegating to the cert service.
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                service.ConfigureHttpsListener(listenOptions: listenOptions);
                captured = listenOptions;
            }
        );

        Assert.NotNull(@object: captured);
        Assert.True(condition: IsTlsEnabled(listenOptions: captured!));
        Assert.NotEqual(expected: HttpProtocols.Http1, actual: captured.Protocols);
    }

    [Fact]
    public void ConfigureHttpsListener_FallsBackToPlaintext_WhenSelfSignedGenerationFails()
    {
        // A NetworkDiscovery whose InternalDomain throws simulates the "self-signed
        // generation itself fails" case — ConfigureHttpsListener must still degrade to
        // plaintext HTTP/1.1 rather than crash the listener setup.
        CertificateService service = BuildService(networkDiscovery: new ThrowingNetworkDiscovery());
        KestrelServerOptions kestrelOptions = new()
        {
            ApplicationServices = BuildMinimalKestrelServices(),
        };
        ListenOptions? captured = null;

        kestrelOptions.ListenLocalhost(
            port: 57126,
            configure: listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                service.ConfigureHttpsListener(listenOptions: listenOptions);
                captured = listenOptions;
            }
        );

        Assert.NotNull(@object: captured);
        Assert.False(condition: IsTlsEnabled(listenOptions: captured!));
        Assert.Equal(expected: HttpProtocols.Http1, actual: captured.Protocols);
    }

    private sealed class ThrowingNetworkDiscovery : INetworkDiscovery
    {
        public string InternalIp
        {
            get => throw new InvalidOperationException(message: "boom");
            set => throw new InvalidOperationException(message: "boom");
        }
        public string RegistrationInternalIp => throw new InvalidOperationException(message: "boom");
        public string ExternalIp
        {
            get => throw new InvalidOperationException(message: "boom");
            set => throw new InvalidOperationException(message: "boom");
        }
        public string? InternalIpV6 => null;
        public string? ExternalIpV6 { get; set; }
        public string InternalDomain => throw new InvalidOperationException(message: "boom");
        public string InternalAddress => throw new InvalidOperationException(message: "boom");
        public string ExternalDomain => throw new InvalidOperationException(message: "boom");
        public string ExternalAddress => throw new InvalidOperationException(message: "boom");
        public string? ExternalAddressV6 => null;
        public bool Ipv6Enabled => false;

        public Task DiscoverExternalIpAsync() => Task.CompletedTask;

        public Task ForceRediscoveryAsync() => Task.CompletedTask;

        public Task<bool> IsPortOpenAsync() => Task.FromResult(result: false);
    }
}
