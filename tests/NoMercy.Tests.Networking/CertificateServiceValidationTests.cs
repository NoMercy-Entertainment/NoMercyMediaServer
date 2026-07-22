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
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Networking;

[Trait(name: "Category", value: "Unit")]
public sealed class CertificateServiceValidationTests : IDisposable
{
    private readonly string _certDir;
    private bool _disposed;

    public CertificateServiceValidationTests()
    {
        _certDir = Path.Combine(path1: AppFiles.AppPath, path2: "security", path3: "certs");
        Directory.CreateDirectory(path: _certDir);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (Directory.Exists(path: _certDir))
        {
            foreach (string f in Directory.GetFiles(path: _certDir))
                File.Delete(path: f);
        }
    }

    private static CertificateService BuildService(IHttpClientFactory? factory = null)
    {
        return new(logger: NullLogger<CertificateService>.Instance, httpClientFactory: factory ?? new NullHttpClientFactory());
    }

    /// <summary>
    /// Builds a service whose inter-attempt delay is a no-op, so retry tests
    /// exercise the loop's control flow without paying the real 10s production
    /// wait per retry.
    /// </summary>
    private static CertificateService BuildFastRetryService(IHttpClientFactory factory)
    {
        return new NoDelayCertificateService(factory: factory);
    }

    private sealed class NoDelayCertificateService(IHttpClientFactory factory)
        : CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: factory)
    {
        protected override Task DelayBetweenAttemptsAsync(TimeSpan delay, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private static X509Certificate2 CreateSelfSignedCert(DateTimeOffset notAfter)
    {
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        CertificateRequest req = new(
            subjectName: "CN=test.nomercy.tv",
            key: rsa,
            hashAlgorithm: HashAlgorithmName.SHA256,
            padding: RSASignaturePadding.Pkcs1
        );
        return req.CreateSelfSigned(notBefore: DateTimeOffset.UtcNow.AddDays(days: -1), notAfter: notAfter);
    }

    private static void InjectCachedCertificate(CertificateService service, X509Certificate2? cert)
    {
        FieldInfo field = typeof(CertificateService).GetField(
            name: "_cachedCertificate",
            bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic
        )!;
        field.SetValue(obj: service, value: cert);
    }

    private static void WritePemFiles(string certDir, X509Certificate2 cert)
    {
#pragma warning disable CS0618
        string certPath = AppFiles.CertFile;
        string keyPath = AppFiles.KeyFile;
#pragma warning restore CS0618

        Directory.CreateDirectory(path: Path.GetDirectoryName(path: certPath)!);
        File.WriteAllText(path: certPath, contents: cert.ExportCertificatePem());

        using RSA? rsa = cert.GetRSAPrivateKey();
        File.WriteAllText(path: keyPath, contents: rsa is null ? string.Empty : rsa.ExportRSAPrivateKeyPem());
    }

    /// <summary>
    /// Subclass that overrides the DB read seam so tests never need a real SQLite connection.
    /// </summary>
    private sealed class StubCertificateService(string? certPem, IHttpClientFactory? factory = null)
        : CertificateService(
            logger: NullLogger<CertificateService>.Instance,
            httpClientFactory: factory ?? new NullHttpClientFactory()
        )
    {
        protected override string? ReadCertificatePemFromDb() => certPem;
    }

    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException(
                message: "HttpClientFactory must not be called in this test"
            );
    }

    private sealed class StubHttpClientFactory(
        Func<HttpRequestMessage, HttpResponseMessage> handler
    ) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler: new DelegatingFakeHandler(handler: handler))
            {
                BaseAddress = new(uriString: "https://test.nomercy.tv/v1/server/"),
            };

        private sealed class DelegatingFakeHandler(
            Func<HttpRequestMessage, HttpResponseMessage> handler
        ) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            ) => Task.FromResult(result: handler(arg: request));
        }
    }

    [Fact]
    public void HasValidCertificate_ReturnsFalse_WhenCachedCertIsExpired()
    {
        CertificateService service = BuildService();
        using X509Certificate2 expired = CreateSelfSignedCert(notAfter: DateTimeOffset.UtcNow.AddSeconds(seconds: -1));
        InjectCachedCertificate(service: service, cert: expired);

        bool result = service.HasValidCertificate();

        Assert.False(condition: result);
    }

    [Fact]
    public void HasValidCertificate_ReturnsTrue_WhenCachedCertIsValid()
    {
        CertificateService service = BuildService();
        using X509Certificate2 valid = CreateSelfSignedCert(notAfter: DateTimeOffset.UtcNow.AddDays(days: 30));
        InjectCachedCertificate(service: service, cert: valid);

        bool result = service.HasValidCertificate();

        Assert.True(condition: result);
    }

    [Fact]
    public void HasValidCertificate_ReturnsFalse_WhenOnlyCertFileExists_KeyFileMissing()
    {
        CertificateService service = BuildService();
        using X509Certificate2 cert = CreateSelfSignedCert(notAfter: DateTimeOffset.UtcNow.AddDays(days: 30));
#pragma warning disable CS0618
        File.WriteAllText(path: AppFiles.CertFile, contents: cert.ExportCertificatePem());
#pragma warning restore CS0618

        bool result = service.HasValidCertificate();

        Assert.False(condition: result);
    }

    [Fact]
    public void HasValidCertificate_ReturnsTrue_WhenBothLegacyPemFilesExist()
    {
        CertificateService service = BuildService();
        using X509Certificate2 cert = CreateSelfSignedCert(notAfter: DateTimeOffset.UtcNow.AddDays(days: 30));
        WritePemFiles(certDir: _certDir, cert: cert);

        bool result = service.HasValidCertificate();

        Assert.True(condition: result);
    }

    [Fact]
    public void HasValidCertificate_ReturnsFalse_WhenNoCertAnywhere()
    {
        CertificateService service = BuildService();

        bool result = service.HasValidCertificate();

        Assert.False(condition: result);
    }

    // Regression: DB-presence branch must load + check NotAfter, not just check row existence.
    // Without the fix, HasValidCertificate() returned true for any row regardless of expiry.

    [Fact]
    public void HasValidCertificate_ReturnsFalse_WhenDbCertIsExpired()
    {
        using X509Certificate2 expired = CreateSelfSignedCert(notAfter: DateTimeOffset.UtcNow.AddSeconds(seconds: -1));
        string certPem = expired.ExportCertificatePem();
        StubCertificateService service = new(certPem: certPem);

        bool result = service.HasValidCertificate();

        Assert.False(condition: result);
    }

    [Fact]
    public void HasValidCertificate_ReturnsTrue_WhenDbCertIsValid()
    {
        using X509Certificate2 valid = CreateSelfSignedCert(notAfter: DateTimeOffset.UtcNow.AddDays(days: 30));
        string certPem = valid.ExportCertificatePem();
        StubCertificateService service = new(certPem: certPem);

        bool result = service.HasValidCertificate();

        Assert.True(condition: result);
    }

    [Fact]
    public async Task RenewSslCertificate_CallsFactory_WhenCachedCertIsNull()
    {
        bool factoryCalled = false;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            factoryCalled = true;
            return new(statusCode: HttpStatusCode.Accepted);
        });
        CertificateService service = BuildFastRetryService(factory: factory);

        await service.RenewSslCertificate(accessToken: "test-token", maxRetries: 1);

        Assert.True(condition: factoryCalled);
    }

    [Fact]
    public async Task RenewSslCertificate_DoesNotCallFactory_WhenCertValidBeyondRenewalThreshold()
    {
        bool factoryCalled = false;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            factoryCalled = true;
            return new(statusCode: HttpStatusCode.OK);
        });
        CertificateService service = BuildService(factory: factory);
        using X509Certificate2 valid = CreateSelfSignedCert(notAfter: DateTimeOffset.UtcNow.AddDays(days: 14));
        InjectCachedCertificate(service: service, cert: valid);

        await service.RenewSslCertificate(accessToken: "test-token", maxRetries: 1);

        Assert.False(condition: factoryCalled);
    }

    [Fact]
    public async Task RenewSslCertificate_CallsFactory_WhenCertExpiresWithinRenewalThreshold()
    {
        bool factoryCalled = false;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            factoryCalled = true;
            return new(statusCode: HttpStatusCode.Accepted);
        });
        CertificateService service = BuildFastRetryService(factory: factory);
        using X509Certificate2 nearExpiry = CreateSelfSignedCert(notAfter: DateTimeOffset.UtcNow.AddDays(days: 12));
        InjectCachedCertificate(service: service, cert: nearExpiry);

        await service.RenewSslCertificate(accessToken: "test-token", maxRetries: 1);

        Assert.True(condition: factoryCalled);
    }

    [Fact]
    public async Task RenewSslCertificate_CallsFactory_WhenCertIsActuallyExpired()
    {
        bool factoryCalled = false;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            factoryCalled = true;
            return new(statusCode: HttpStatusCode.Accepted);
        });
        CertificateService service = BuildFastRetryService(factory: factory);
        using X509Certificate2 expired = CreateSelfSignedCert(notAfter: DateTimeOffset.UtcNow.AddSeconds(seconds: -1));
        InjectCachedCertificate(service: service, cert: expired);

        await service.RenewSslCertificate(accessToken: "test-token", maxRetries: 1);

        Assert.True(condition: factoryCalled);
    }

    [Fact]
    public async Task RenewSslCertificate_SkipsHttpCall_WhenTokenIsNull()
    {
        bool factoryCalled = false;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            factoryCalled = true;
            return new(statusCode: HttpStatusCode.OK);
        });
        CertificateService service = BuildService(factory: factory);

        await service.RenewSslCertificate(accessToken: null, maxRetries: 1);

        Assert.False(condition: factoryCalled);
    }

    [Fact]
    public async Task RenewSslCertificate_SkipsHttpCall_WhenTokenIsEmpty()
    {
        bool factoryCalled = false;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            factoryCalled = true;
            return new(statusCode: HttpStatusCode.OK);
        });
        CertificateService service = BuildService(factory: factory);

        await service.RenewSslCertificate(accessToken: string.Empty, maxRetries: 1);

        Assert.False(condition: factoryCalled);
    }

    [Fact]
    public async Task RenewSslCertificate_DoesNotRetry_WhenApiReturns400()
    {
        int callCount = 0;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            callCount++;
            return new(statusCode: HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    content: "{\"status\":\"error\",\"message\":\"Certificate is not due for renewal yet\"}"
                ),
            };
        });
        CertificateService service = BuildService(factory: factory);

        await service.RenewSslCertificate(accessToken: "test-token", maxRetries: 5);

        Assert.Equal(expected: 1, actual: callCount);
    }

    [Fact]
    public async Task RenewSslCertificate_ShortCircuits_On400WithEmptyBody()
    {
        int callCount = 0;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            callCount++;
            return new(statusCode: HttpStatusCode.BadRequest) { Content = new StringContent(content: string.Empty) };
        });
        CertificateService service = BuildService(factory: factory);

        await service.RenewSslCertificate(accessToken: "test-token", maxRetries: 5);

        Assert.Equal(expected: 1, actual: callCount);
    }

    [Fact]
    public async Task RenewSslCertificate_FallsBackToRawBody_When400BodyIsNotJson()
    {
        int callCount = 0;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            callCount++;
            return new(statusCode: HttpStatusCode.BadRequest)
            {
                Content = new StringContent(content: "plain text error message"),
            };
        });
        CertificateService service = BuildService(factory: factory);

        await service.RenewSslCertificate(accessToken: "test-token", maxRetries: 3);

        Assert.Equal(expected: 1, actual: callCount);
    }

    [Fact]
    public async Task RenewSslCertificate_Retries_On202_UpToMaxRetries()
    {
        int callCount = 0;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            callCount++;
            return new(statusCode: HttpStatusCode.Accepted);
        });
        CertificateService service = BuildFastRetryService(factory: factory);

        await service.RenewSslCertificate(accessToken: "test-token", maxRetries: 2);

        Assert.Equal(expected: 2, actual: callCount);
    }

    [Fact]
    public async Task RenewSslCertificate_Retries_OnGatewayTimeout()
    {
        int callCount = 0;
        StubHttpClientFactory factory = new(handler: _ =>
        {
            callCount++;
            return new(statusCode: HttpStatusCode.GatewayTimeout);
        });
        CertificateService service = BuildFastRetryService(factory: factory);

        await Assert.ThrowsAsync<HttpRequestException>(testCode: () =>
            service.RenewSslCertificate(accessToken: "test-token", maxRetries: 2)
        );

        Assert.Equal(expected: 2, actual: callCount);
    }

    [Fact]
    public void CatalogueCompleteness_AllGuardRulesAreCoveredOrDocumented()
    {
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Networking/Certificate/CertificateService.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        Assert.Contains(expectedSubstring: "_cachedCertificate.NotAfter > DateTime.Now", actualString: source);

        // DB branch must load + check NotAfter, not just assert row presence.
        Assert.Contains(expectedSubstring: "ReadCertificatePemFromDb", actualString: source);
        Assert.Contains(expectedSubstring: "dbCert.NotAfter > DateTime.Now", actualString: source);

        Assert.Contains(
            expectedSubstring: "_driver.FileExists(AppFiles.CertFile) && _driver.FileExists(AppFiles.KeyFile)",
            actualString: source
        );

        Assert.Contains(expectedSubstring: "_cachedCertificate is null", actualString: source);

        Assert.Contains(expectedSubstring: "_cachedCertificate.NotAfter <= DateTime.Now", actualString: source);

        Assert.Contains(expectedSubstring: "RenewalThresholdDays", actualString: source);
        Assert.Contains(expectedSubstring: "private const int RenewalThresholdDays = 13", actualString: source);

        Assert.Contains(expectedSubstring: "string.IsNullOrEmpty(token)", actualString: source);

        Assert.Contains(expectedSubstring: "HttpStatusCode.Accepted", actualString: source);

        Assert.Contains(expectedSubstring: "HttpStatusCode.BadRequest", actualString: source);

        Assert.Contains(expectedSubstring: "CertificateNotDueException", actualString: source);

        Assert.Contains(expectedSubstring: "string.IsNullOrWhiteSpace(body)", actualString: source);
        Assert.Contains(expectedSubstring: "parsed?.Message", actualString: source);

        Assert.Contains(expectedSubstring: "HttpProtocols.Http1", actualString: source);
    }

    private static string FindSourceFile(string relativePath)
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(path1: dir, path2: relativePath);
            if (File.Exists(path: candidate))
                return candidate;

            string repoCandidate = Path.Combine(paths: [dir, "..", "..", "..", "..", "..", relativePath]);
            string resolved = Path.GetFullPath(path: repoCandidate);
            if (File.Exists(path: resolved))
                return resolved;

            dir = Directory.GetParent(path: dir)?.FullName;
        }

        throw new FileNotFoundException(message: $"Could not locate {relativePath}");
    }
}
