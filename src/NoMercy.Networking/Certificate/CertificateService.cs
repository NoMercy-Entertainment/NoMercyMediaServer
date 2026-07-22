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
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Networking.Certificate;

public class CertificateService : ICertificateService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly ILogger<CertificateService> _logger;

    // Optional: absent in the throwaway pre-DI presence-check instance built by
    // ServerBootstrapper before the real DI container exists. Self-signed SANs
    // degrade gracefully (localhost/loopback/LAN-IP only) when this is null.
    private readonly INetworkDiscovery? _networkDiscovery;

    public CertificateService(
        ILogger<CertificateService> logger,
        IHttpClientFactory httpClientFactory,
        INetworkDiscovery? networkDiscovery = null
    )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _networkDiscovery = networkDiscovery;
    }

    // LOCAL-ONLY: Certificate runs before StorageProvider is initialized (startup phase 1-3).
    // DI is not available here; replacing this would require threading IStorageDriver
    // through every startup call site including Program.cs and BootOrchestrator.
    private readonly IStorageDriver _driver = new LocalStorageDriver();
    private X509Certificate2? _cachedCertificate;
    private X509Certificate2? _selfSignedCertificate;
    private readonly object _certLock = new();

    public void LoadFromDb()
    {
        using AppDbContext db = new();
        string? certPem = db
            .Configuration.Where(predicate: c => c.Key == "ssl_certificate")
            .Select(selector: c => c.SecureValue)
            .FirstOrDefault();
        string? keyPem = db
            .Configuration.Where(predicate: c => c.Key == "ssl_private_key")
            .Select(selector: c => c.SecureValue)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(value: certPem) || string.IsNullOrEmpty(value: keyPem))
        {
            // Fallback: load from legacy PEM files (pre-DB-storage installs)
#pragma warning disable CS0618
            if (_driver.FileExists(path: AppFiles.CertFile) && _driver.FileExists(path: AppFiles.KeyFile))
            {
                using (StreamReader certReader = new(stream: _driver.OpenRead(path: AppFiles.CertFile)))
                    certPem = certReader.ReadToEnd();
                using (StreamReader keyReader = new(stream: _driver.OpenRead(path: AppFiles.KeyFile)))
                    keyPem = keyReader.ReadToEnd();
                _logger.LogInformation(message: "Loading SSL certificate from legacy PEM files");
            }
            else
            {
                return;
            }
#pragma warning restore CS0618
        }

        lock (_certLock)
        {
            using X509Certificate2 tempCert = X509Certificate2.CreateFromPem(certPem: certPem, keyPem: keyPem);
            byte[] pkcs12Data = tempCert.Export(contentType: X509ContentType.Pkcs12);
            _cachedCertificate = X509CertificateLoader.LoadPkcs12(data: pkcs12Data, password: null);
        }

        _logger.LogInformation(message: "Loaded SSL certificate into memory cache");
    }

    private void UpsertConfig(AppDbContext db, string key, string value)
    {
        Configuration? existing = db.Configuration.FirstOrDefault(predicate: c => c.Key == key);
        if (existing != null)
        {
            existing.SecureValue = value;
        }
        else
        {
            db.Configuration.Add(entity: new() { Key = key, SecureValue = value });
        }
    }

    public bool HasValidCertificate()
    {
        lock (_certLock)
        {
            if (_cachedCertificate is not null)
                return _cachedCertificate.NotAfter > DateTime.Now;
        }

        // Check DB — must load and verify NotAfter, not just row presence.
        // A row with an expired cert must return false so Kestrel does not
        // enable HTTPS with a cert that browsers reject.
        try
        {
            string? certPem = ReadCertificatePemFromDb();
            if (!string.IsNullOrEmpty(value: certPem))
            {
                using X509Certificate2 dbCert = X509Certificate2.CreateFromPem(certPem: certPem);
                return dbCert.NotAfter > DateTime.Now;
            }
        }
        catch
        {
            // DB not ready or cert PEM is malformed — fall through to file check.
        }

        // Legacy fallback: cert files on disk (pre-DB-storage installs)
#pragma warning disable CS0618
        return _driver.FileExists(path: AppFiles.CertFile) && _driver.FileExists(path: AppFiles.KeyFile);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Returns the raw certificate PEM stored in the DB, or <c>null</c> when no
    /// row exists or the DB is not yet available.  Extracted as a virtual method
    /// so unit tests can override it without a real SQLite connection.
    /// </summary>
    protected virtual string? ReadCertificatePemFromDb()
    {
        using AppDbContext db = new();
        return db
            .Configuration.Where(predicate: c => c.Key == "ssl_certificate")
            .Select(selector: c => c.SecureValue)
            .FirstOrDefault();
    }

    /// <summary>
    /// Waits between certificate-fetch retries. Extracted as a virtual method so
    /// unit tests can override it to a no-op instead of blocking for the real
    /// 10-second production delay on every 202/timeout retry.
    /// </summary>
    protected virtual Task DelayBetweenAttemptsAsync(TimeSpan delay, CancellationToken ct)
    {
        return Task.Delay(delay: delay, cancellationToken: ct);
    }

    public void KestrelConfig(KestrelServerOptions options)
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = 100L * 1024 * 1024 * 1024; // 100GB — 4K remux support
        options.Limits.MaxRequestBufferSize = null; // Kestrel manages adaptively
        options.Limits.MaxConcurrentConnections = 1000; // Many streaming clients
        options.Limits.MaxConcurrentUpgradedConnections = 500; // WebSocket/SignalR
    }

    /// <summary>
    /// Selection order: a valid Let's Encrypt cert (unchanged happy path), else the
    /// cached self-signed fallback (generated/persisted on demand), else — only when
    /// self-signed generation itself fails — plaintext HTTP/1.1. Keeping the origin on
    /// HTTPS (even with a self-signed cert) is what lets an HTTPS dashboard reach a
    /// LAN-only or pre-LE server at all; plaintext is a last resort, not a normal state.
    /// </summary>
    public void ConfigureHttpsListener(ListenOptions listenOptions)
    {
        if (EnsureHttpsCertificate())
        {
            listenOptions.UseHttps(httpsOptions: HttpsConnectionAdapterOptions());
        }
        else
        {
            // Without TLS, HTTP/2 requires prior-knowledge (h2c) and HTTP/3 requires QUIC/TLS.
            // Fall back to HTTP/1.1 only to avoid empty-reply issues from protocol mismatches.
            listenOptions.Protocols = HttpProtocols.Http1;
        }
    }

    /// <summary>
    /// Returns true when a certificate is ready for TLS — either the real Let's Encrypt
    /// cert, or the self-signed fallback (loaded from cache/DB, generating a new one only
    /// if neither exists). This is deliberately broader than <see cref="HasValidCertificate"/>,
    /// which callers (BootOrchestrator's "is this server registered" check, PortManager)
    /// rely on meaning "a real LE cert exists" — that meaning is unchanged.
    /// </summary>
    public bool EnsureHttpsCertificate()
    {
        if (HasValidCertificate())
            return true;

        return TryEnsureSelfSignedCertificate();
    }

    private HttpsConnectionAdapterOptions HttpsConnectionAdapterOptions()
    {
        return new()
        {
            SslProtocols = SslProtocols.Tls12,
            ServerCertificateSelector = (_, _) =>
            {
                lock (_certLock)
                {
                    if (
                        _cachedCertificate is not null
                        && _cachedCertificate.NotAfter > DateTime.Now
                    )
                        return _cachedCertificate;

                    if (
                        _selfSignedCertificate is not null
                        && _selfSignedCertificate.NotAfter > DateTime.Now
                    )
                        return _selfSignedCertificate;
                }

                throw new InvalidOperationException(message: "No SSL certificate loaded");
            },
        };
    }

    private const string SelfSignedCertConfigKey = "ssl_selfsigned_certificate";
    private const string SelfSignedKeyConfigKey = "ssl_selfsigned_private_key";

    // Self-signed certs carry no CA trust, so the 398-day CA/Browser Forum leaf-validity
    // cap doesn't legally apply — but browsers increasingly enforce it for ANY TLS leaf
    // cert regardless of trust chain, and this is meant to double as the base for a future
    // OS-trust-store install. Stay inside that ceiling so it doesn't need special-casing later.
    private const int SelfSignedValidityDays = 397;

    /// <summary>
    /// Ensures a self-signed fallback certificate is cached in memory, loading the
    /// persisted one from the DB or generating (and persisting) a new one if needed.
    /// Never mints a new certificate when a cached/persisted one is still valid — the
    /// fallback must be stable across restarts, not a new identity every boot.
    /// </summary>
    private bool TryEnsureSelfSignedCertificate()
    {
        lock (_certLock)
        {
            if (
                _selfSignedCertificate is not null
                && _selfSignedCertificate.NotAfter > DateTime.Now
            )
                return true;
        }

        try
        {
            if (LoadSelfSignedFromDb())
                return true;

            GenerateAndPersistSelfSignedCertificate();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                exception: ex,
                message: "Failed to generate self-signed fallback certificate — falling back to plaintext HTTP: {Message}",
                args: ex.Message
            );
            return false;
        }
    }

    /// <summary>
    /// Loads a previously-persisted self-signed cert from the DB. Returns false (not an
    /// error) when none exists yet, or the persisted one has expired — the caller
    /// regenerates in that case.
    /// </summary>
    private bool LoadSelfSignedFromDb()
    {
        using AppDbContext db = new();
        string? certPem = db
            .Configuration.Where(predicate: c => c.Key == SelfSignedCertConfigKey)
            .Select(selector: c => c.SecureValue)
            .FirstOrDefault();
        string? keyPem = db
            .Configuration.Where(predicate: c => c.Key == SelfSignedKeyConfigKey)
            .Select(selector: c => c.SecureValue)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(value: certPem) || string.IsNullOrEmpty(value: keyPem))
            return false;

        using X509Certificate2 tempCert = X509Certificate2.CreateFromPem(certPem: certPem, keyPem: keyPem);
        if (tempCert.NotAfter <= DateTime.Now)
            return false;

        lock (_certLock)
        {
            byte[] pkcs12Data = tempCert.Export(contentType: X509ContentType.Pkcs12);
            _selfSignedCertificate = X509CertificateLoader.LoadPkcs12(data: pkcs12Data, password: null);
        }

        _logger.LogInformation(message: "Loaded self-signed fallback certificate from cache");
        return true;
    }

    /// <summary>
    /// Generates a fresh self-signed certificate, persists it as DB SecureValue rows
    /// (same protection mechanism as the real LE cert/key — never plaintext, never a
    /// separate on-disk store), and loads it into the in-memory cache.
    /// </summary>
    private void GenerateAndPersistSelfSignedCertificate()
    {
        using X509Certificate2 generated = BuildSelfSignedCertificate();

        string certPem = generated.ExportCertificatePem();
        using RSA? rsa = generated.GetRSAPrivateKey();
        string keyPem =
            rsa?.ExportRSAPrivateKeyPem()
            ?? throw new InvalidOperationException(
                message: "Generated self-signed certificate has no exportable RSA private key"
            );

        using (AppDbContext db = new())
        {
            UpsertConfig(db: db, key: SelfSignedCertConfigKey, value: certPem);
            UpsertConfig(db: db, key: SelfSignedKeyConfigKey, value: keyPem);
            db.SaveChanges();
        }

        lock (_certLock)
        {
            byte[] pkcs12Data = generated.Export(contentType: X509ContentType.Pkcs12);
            _selfSignedCertificate = X509CertificateLoader.LoadPkcs12(data: pkcs12Data, password: null);
        }

        // Never log PEM/key material — only the non-secret expiry.
        _logger.LogInformation(
            message: "Generated self-signed fallback certificate (expires {NotAfter:yyyy-MM-dd}) so HTTPS "
                     + "stays reachable without a Let's Encrypt cert. Direct browser access will show a "
                     + "trust warning until a real certificate is acquired.",
            args: generated.NotAfter
        );
    }

    /// <summary>
    /// Builds the self-signed certificate covering localhost, loopback, discoverable LAN
    /// IPs, and (when <see cref="INetworkDiscovery"/> is available) the server's
    /// InternalDomain/ExternalDomain. RSA 2048/SHA-256, ~13 months validity.
    /// </summary>
    private X509Certificate2 BuildSelfSignedCertificate()
    {
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        CertificateRequest request = new(
            subjectName: $"CN=NoMercy MediaServer {Info.DeviceId}",
            key: rsa,
            hashAlgorithm: HashAlgorithmName.SHA256,
            padding: RSASignaturePadding.Pkcs1
        );

        request.CertificateExtensions.Add(
            item: new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: false)
        );
        request.CertificateExtensions.Add(
            item: new X509KeyUsageExtension(
                keyUsages: X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: false
            )
        );
        request.CertificateExtensions.Add(
            item: new X509EnhancedKeyUsageExtension(enhancedKeyUsages: [new(oid: "1.3.6.1.5.5.7.3.1")], critical: false)
        );

        SubjectAlternativeNameBuilder sanBuilder = new();
        sanBuilder.AddDnsName(dnsName: "localhost");
        sanBuilder.AddIpAddress(ipAddress: IPAddress.Loopback);
        sanBuilder.AddIpAddress(ipAddress: IPAddress.IPv6Loopback);

        foreach (IPAddress address in CollectLocalIpAddresses(networkDiscovery: _networkDiscovery))
            sanBuilder.AddIpAddress(ipAddress: address);

        if (_networkDiscovery is not null)
        {
            AddDnsNameIfValid(sanBuilder: sanBuilder, dnsName: _networkDiscovery.InternalDomain);

            if (
                !string.IsNullOrEmpty(value: _networkDiscovery.ExternalIp)
                && _networkDiscovery.ExternalIp != "0.0.0.0"
            )
                AddDnsNameIfValid(sanBuilder: sanBuilder, dnsName: _networkDiscovery.ExternalDomain);
        }

        request.CertificateExtensions.Add(item: sanBuilder.Build());

        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(days: -1);
        DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddDays(days: SelfSignedValidityDays);

        return request.CreateSelfSigned(notBefore: notBefore, notAfter: notAfter);
    }

    private void AddDnsNameIfValid(SubjectAlternativeNameBuilder sanBuilder, string? dnsName)
    {
        if (string.IsNullOrWhiteSpace(value: dnsName))
            return;

        try
        {
            sanBuilder.AddDnsName(dnsName: dnsName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                message: "Skipping invalid SAN DNS name {DnsName} for self-signed cert: {Message}", args: [dnsName, ex.Message]
            );
        }
    }

    /// <summary>
    /// Best-effort LAN IP enumeration for self-signed SANs. Duplicates
    /// <c>NetworkDiscovery.GetInternalIp</c>'s NIC-walk rather than depending on it —
    /// this must degrade to "just localhost/loopback" instead of throwing when no NIC
    /// is up (e.g. inside a locked-down container).
    /// </summary>
    private static IEnumerable<IPAddress> CollectLocalIpAddresses(
        INetworkDiscovery? networkDiscovery
    )
    {
        HashSet<IPAddress> addresses = [];

        if (
            networkDiscovery is not null
            && IPAddress.TryParse(ipString: networkDiscovery.InternalIp, address: out IPAddress? discovered)
            && !IPAddress.IsLoopback(address: discovered)
        )
            addresses.Add(item: discovered);

        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (
                    nic.NetworkInterfaceType
                    is NetworkInterfaceType.Loopback
                        or NetworkInterfaceType.Tunnel
                )
                    continue;

                foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (IPAddress.IsLoopback(address: addr.Address))
                        continue;

                    addresses.Add(item: addr.Address);
                }
            }
        }
        catch
        {
            // Best effort only — the fallback cert still works with localhost/loopback SANs.
        }

        return addresses;
    }

    // Kept as fallback — Task 17 will remove this once DB-only path is stable.
    private X509Certificate2 CombinePublicAndPrivateCerts()
    {
#pragma warning disable CS0618 // Obsolete
        if (!_driver.FileExists(path: AppFiles.CertFile))
            throw new FileNotFoundException(message: $"Certificate file not found: {AppFiles.CertFile}");

        if (!_driver.FileExists(path: AppFiles.KeyFile))
            throw new FileNotFoundException(message: $"Private key file not found: {AppFiles.KeyFile}");

        string certPem;
        string keyPem;
        using (StreamReader certReader = new(stream: _driver.OpenRead(path: AppFiles.CertFile)))
            certPem = certReader.ReadToEnd();
        using (StreamReader keyReader = new(stream: _driver.OpenRead(path: AppFiles.KeyFile)))
            keyPem = keyReader.ReadToEnd();
#pragma warning restore CS0618

        using X509Certificate2 tempCert = X509Certificate2.CreateFromPem(certPem: certPem, keyPem: keyPem);

        byte[] pkcs12Data = tempCert.Export(contentType: X509ContentType.Pkcs12);
        return X509CertificateLoader.LoadPkcs12(data: pkcs12Data, password: null);
    }

    // Must stay strictly below the API's renewal window (currently 14 days at
    // nomercy-tv's ServerCertificateController::renew). When the client trips
    // the renewal threshold while the cert is still outside the API window,
    // the API rejects with 400 "Certificate is not due for renewal yet" and
    // we hit a 30-attempt retry storm. 13 days gives a 1-day clock-drift safety
    // margin while keeping the renewal proactive (still ~1 month before
    // browsers start showing warnings for Let's Encrypt's 90-day certs).
    private const int RenewalThresholdDays = 13;

    private bool ValidateSslCertificate()
    {
        lock (_certLock)
        {
            if (_cachedCertificate is null)
                return false;

            // Don't call certificate.Verify() — it may trigger OCSP/CRL network calls
            // which fail when Cloudflare or the internet is down. Just check the expiry date.
            if (_cachedCertificate.NotAfter <= DateTime.Now)
                return false; // Actually expired

            if (_cachedCertificate.NotAfter < DateTime.Now.AddDays(value: RenewalThresholdDays))
            {
                _logger.LogInformation(
                    message: "SSL cert expires {NotAfter:yyyy-MM-dd} — will attempt renewal",
                    args: _cachedCertificate.NotAfter
                );
                return false; // Expiring soon — trigger renewal
            }

            return true;
        }
    }

    private const int CertRetryDelaySeconds = 10;

    public async Task RenewSslCertificate(string? accessToken, int maxRetries = 30)
    {
        if (ValidateSslCertificate())
        {
            _logger.LogInformation(message: "SSL Certificate is valid");
            return;
        }

        bool hasExistingCert = HasValidCertificate();

        _logger.LogInformation(
            message: !hasExistingCert ? "Generating SSL Certificate..." : "Renewing SSL Certificate..."
        );

        try
        {
            string? token = accessToken;
            if (string.IsNullOrEmpty(value: token))
            {
                _logger.LogWarning(message: "Skipping certificate renewal — no auth token available");
                return;
            }

            HttpClient client = _httpClientFactory.CreateClient(name: "cert-renewal");
            client.BaseAddress = new(uriString: ExternalServicesConfig.Current.ApiServerBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(minutes: 10);
            client.DefaultRequestHeaders.Accept.Add(item: new(mediaType: "application/json"));
            client.DefaultRequestHeaders.Authorization = new(scheme: "Bearer", parameter: token);

            string serverUrl = $"certificate?id={Info.DeviceId}";
            if (hasExistingCert)
                serverUrl = $"renew-certificate?id={Info.DeviceId}";

            for (int attempt = 1; attempt <= maxRetries; attempt++)
                try
                {
                    CertificateDto? result = await FetchCertificate(
                        client: client,
                        serverUrl: serverUrl,
                        hasExistingCert: hasExistingCert
                    );
                    if (result != null)
                        return;

                    // null means 202 — cert not ready yet, wait and retry
                    _logger.LogInformation(
                        message: "Certificate not ready, waiting {CertRetryDelaySeconds}s (attempt {Attempt}/{MaxRetries})", args: [CertRetryDelaySeconds, attempt, maxRetries]
                    );
                    await DelayBetweenAttemptsAsync(
                        delay: TimeSpan.FromSeconds(seconds: CertRetryDelaySeconds),
                        ct: CancellationToken.None
                    );
                }
                catch (CertificateNotDueException ex)
                {
                    // Permanent (for this attempt window): the API rejected the
                    // renewal request because the cert isn't due yet. Bail out
                    // without retrying — daily cron will try again tomorrow.
                    _logger.LogInformation(message: "Skipping renewal: {Message}", args: ex.Message);
                    return;
                }
                catch (Exception ex)
                    when (attempt < maxRetries
                        && (ex is HttpRequestException || ex is InvalidOperationException)
                    )
                {
                    _logger.LogInformation(
                        message: "Certificate attempt failed: {Message}, retrying in {CertRetryDelaySeconds}s (attempt {Attempt}/{MaxRetries})", args: [ex.Message, CertRetryDelaySeconds, attempt, maxRetries]
                    );
                    await DelayBetweenAttemptsAsync(
                        delay: TimeSpan.FromSeconds(seconds: CertRetryDelaySeconds),
                        ct: CancellationToken.None
                    );
                }
        }
        catch (Exception ex) when (hasExistingCert)
        {
            // Cert exists in DB but renewal failed (Cloudflare down, network issue, etc.)
            // The existing cert is usable — don't block boot
            _logger.LogWarning(
                message: "Certificate renewal failed: {Message}. Using existing certificate.",
                args: ex.Message
            );
        }
    }

    private async Task<CertificateDto?> FetchCertificate(
        HttpClient client,
        string serverUrl,
        bool hasExistingCert
    )
    {
        using HttpResponseMessage response = await client.GetAsync(requestUri: serverUrl);

        if (response.StatusCode == HttpStatusCode.Accepted) // 202 — cert not ready yet
        {
            _logger.LogInformation(message: "Certificate not ready yet (202 Accepted), will retry");
            return null;
        }

        if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            throw new HttpRequestException(message: "Gateway timeout waiting for certificate");

        // 400 from the renewal endpoint means the API doesn't think the cert is
        // due yet (it gates renewal at 14 days from expiry). Surface the body
        // as a typed exception so the outer retry loop can bail instead of
        // hammering 30 times with the same predictable rejection.
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            string body = await response.Content.ReadAsStringAsync();
            string apiMessage = ExtractApiMessage(body: body);
            throw new CertificateNotDueException(
                message: string.IsNullOrEmpty(value: apiMessage)
                    ? "API rejected renewal with 400 Bad Request (no body)"
                    : apiMessage
            );
        }

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();
        ApiResponse<CertificateResponse> data =
            content.FromJson<ApiResponse<CertificateResponse>>()
            ?? throw new(message: "Failed to deserialize certificate JSON");

        if (data.Data is null)
            throw new InvalidOperationException(
                message: $"Certificate API returned no data (status: {data.Status ?? "unknown"}, message: {data.Message ?? "none"})"
            );

        if (
            string.IsNullOrEmpty(value: data.Data.PrivateKey)
            || string.IsNullOrEmpty(value: data.Data.Certificate)
        )
            throw new InvalidOperationException(
                message: $"Certificate API returned incomplete data (status: {data.Status ?? "unknown"})"
            );

        string certPem = $"{data.Data.Certificate}\n{data.Data.IssuerCertificate}";
        string keyPem = data.Data.PrivateKey;
        string? caPem = string.IsNullOrEmpty(value: data.Data.CertificateAuthority)
            ? null
            : data.Data.CertificateAuthority;

        // Write to DB
        await using AppDbContext db = new();
        UpsertConfig(db: db, key: "ssl_certificate", value: certPem);
        UpsertConfig(db: db, key: "ssl_private_key", value: keyPem);
        if (!string.IsNullOrEmpty(value: caPem))
            UpsertConfig(db: db, key: "ssl_ca", value: caPem);
        await db.SaveChangesAsync();

        // Update in-memory cache
        lock (_certLock)
        {
            using X509Certificate2 tempCert = X509Certificate2.CreateFromPem(certPem: certPem, keyPem: keyPem);
            byte[] pkcs12Data = tempCert.Export(contentType: X509ContentType.Pkcs12);
            _cachedCertificate = X509CertificateLoader.LoadPkcs12(data: pkcs12Data, password: null);
        }

        // Keep file writes alongside DB writes for backwards compat (Task 17 removes these)
#pragma warning disable CS0618 // Obsolete
        if (_driver.FileExists(path: AppFiles.KeyFile))
            _driver.DeleteFile(path: AppFiles.KeyFile);
        if (_driver.FileExists(path: AppFiles.CaFile))
            _driver.DeleteFile(path: AppFiles.CaFile);
        if (_driver.FileExists(path: AppFiles.CertFile))
            _driver.DeleteFile(path: AppFiles.CertFile);

        await WriteTextAsync(path: AppFiles.KeyFile, content: keyPem);
        await WriteTextAsync(path: AppFiles.CaFile, content: data.Data.CertificateAuthority);
        await WriteTextAsync(path: AppFiles.CertFile, content: certPem);
#pragma warning restore CS0618

        _logger.LogInformation(
            message: !hasExistingCert ? "SSL Certificate created" : "SSL Certificate renewed"
        );
        return new();
    }

    private async Task WriteTextAsync(string path, string content)
    {
        await using Stream stream = _driver.OpenWrite(path: path, overwrite: true);
        await using StreamWriter writer = new(stream: stream, encoding: Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync(value: content);
        await writer.FlushAsync();
    }

    /// <summary>Sentinel returned by FetchCertificate to indicate a successfully written certificate.</summary>
    private sealed class CertificateDto { }

    /// <summary>
    /// API responded with 400 because the cert is not yet within its renewal
    /// window. Distinct from generic HttpRequestException so the retry loop
    /// can short-circuit instead of hammering the endpoint.
    /// </summary>
    private sealed class CertificateNotDueException : Exception
    {
        public CertificateNotDueException(string message)
            : base(message: message) { }
    }

    /// <summary>
    /// Pulls the human-readable message out of nomercy-tv's standard error
    /// envelope (<c>{ "status": "...", "message": "..." }</c>). Falls back to
    /// the raw body when the JSON doesn't parse, and to the empty string when
    /// the body is missing — the caller decides how to phrase the no-message
    /// case.
    /// </summary>
    private string ExtractApiMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(value: body))
            return string.Empty;

        try
        {
            ApiResponse<object>? parsed = body.FromJson<ApiResponse<object>>();
            if (!string.IsNullOrEmpty(value: parsed?.Message))
                return parsed.Message;
        }
        catch
        {
            // Body isn't our standard envelope — fall through to raw return.
        }

        return body.Trim();
    }

    public class ApiResponse<T>
    {
        [JsonProperty(propertyName: "status")]
        public string? Status { get; set; }

        [JsonProperty(propertyName: "message")]
        public string? Message { get; set; }

        [JsonProperty(propertyName: "data")]
        public T Data { get; set; } = default!;
    }

    public class CertificateResponse
    {
        [JsonProperty(propertyName: "status")]
        public string Status { get; set; } = null!;

        [JsonProperty(propertyName: "certificate")]
        public string Certificate { get; set; } = null!;

        [JsonProperty(propertyName: "private_key")]
        public string PrivateKey { get; set; } = null!;

        [JsonProperty(propertyName: "issuer_certificate")]
        public string IssuerCertificate { get; set; } = null!;

        [JsonProperty(propertyName: "certificate_authority")]
        public string CertificateAuthority { get; set; } = null!;
    }
}
