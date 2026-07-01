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
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Networking.Certificate;

public sealed class CertificateService : ICertificateService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly ILogger<CertificateService> _logger;

    public CertificateService(
        ILogger<CertificateService> logger,
        IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    // LOCAL-ONLY: Certificate runs before StorageProvider is initialized (startup phase 1-3).
    // DI is not available here; replacing this would require threading IStorageDriver
    // through every startup call site including Program.cs and BootOrchestrator.
    private readonly IStorageDriver _driver = new LocalStorageDriver();
    private X509Certificate2? _cachedCertificate;
    private readonly object _certLock = new();

    public void LoadFromDb()
    {
        using AppDbContext db = new();
        string? certPem = db
            .Configuration.Where(c => c.Key == "ssl_certificate")
            .Select(c => c.SecureValue)
            .FirstOrDefault();
        string? keyPem = db
            .Configuration.Where(c => c.Key == "ssl_private_key")
            .Select(c => c.SecureValue)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(certPem) || string.IsNullOrEmpty(keyPem))
        {
            // Fallback: load from legacy PEM files (pre-DB-storage installs)
#pragma warning disable CS0618
            if (_driver.FileExists(AppFiles.CertFile) && _driver.FileExists(AppFiles.KeyFile))
            {
                using (StreamReader certReader = new(_driver.OpenRead(AppFiles.CertFile)))
                    certPem = certReader.ReadToEnd();
                using (StreamReader keyReader = new(_driver.OpenRead(AppFiles.KeyFile)))
                    keyPem = keyReader.ReadToEnd();
                _logger.LogInformation("Loading SSL certificate from legacy PEM files");
            }
            else
            {
                return;
            }
#pragma warning restore CS0618
        }

        lock (_certLock)
        {
            using X509Certificate2 tempCert = X509Certificate2.CreateFromPem(certPem, keyPem);
            byte[] pkcs12Data = tempCert.Export(X509ContentType.Pkcs12);
            _cachedCertificate = X509CertificateLoader.LoadPkcs12(pkcs12Data, null);
        }

        _logger.LogInformation("Loaded SSL certificate into memory cache");
    }

    private void UpsertConfig(AppDbContext db, string key, string value)
    {
        Configuration? existing = db.Configuration.FirstOrDefault(c => c.Key == key);
        if (existing != null)
        {
            existing.SecureValue = value;
        }
        else
        {
            db.Configuration.Add(new() { Key = key, SecureValue = value });
        }
    }

    public bool HasValidCertificate()
    {
        lock (_certLock)
        {
            if (_cachedCertificate is not null)
                return _cachedCertificate.NotAfter > DateTime.Now;
        }

        // Check DB
        try
        {
            using AppDbContext db = new();
            if (db.Configuration.Any(c => c.Key == "ssl_certificate"))
                return true;
        }
        catch
        {
            // DB not ready yet — fall through to file check
        }

        // Legacy fallback: cert files on disk (pre-DB-storage installs)
#pragma warning disable CS0618
        return _driver.FileExists(AppFiles.CertFile) && _driver.FileExists(AppFiles.KeyFile);
#pragma warning restore CS0618
    }

    public void KestrelConfig(KestrelServerOptions options)
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = 100L * 1024 * 1024 * 1024; // 100GB — 4K remux support
        options.Limits.MaxRequestBufferSize = null; // Kestrel manages adaptively
        options.Limits.MaxConcurrentConnections = 1000; // Many streaming clients
        options.Limits.MaxConcurrentUpgradedConnections = 500; // WebSocket/SignalR
    }

    public void ConfigureHttpsListener(ListenOptions listenOptions)
    {
        if (HasValidCertificate())
        {
            listenOptions.UseHttps(HttpsConnectionAdapterOptions());
        }
        else
        {
            // Without TLS, HTTP/2 requires prior-knowledge (h2c) and HTTP/3 requires QUIC/TLS.
            // Fall back to HTTP/1.1 only to avoid empty-reply issues from protocol mismatches.
            listenOptions.Protocols = HttpProtocols.Http1;
        }
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
                    return _cachedCertificate
                        ?? throw new InvalidOperationException("No SSL certificate loaded");
                }
            },
        };
    }

    // Kept as fallback — Task 17 will remove this once DB-only path is stable.
    private X509Certificate2 CombinePublicAndPrivateCerts()
    {
#pragma warning disable CS0618 // Obsolete
        if (!_driver.FileExists(AppFiles.CertFile))
            throw new FileNotFoundException($"Certificate file not found: {AppFiles.CertFile}");

        if (!_driver.FileExists(AppFiles.KeyFile))
            throw new FileNotFoundException($"Private key file not found: {AppFiles.KeyFile}");

        string certPem;
        string keyPem;
        using (StreamReader certReader = new(_driver.OpenRead(AppFiles.CertFile)))
            certPem = certReader.ReadToEnd();
        using (StreamReader keyReader = new(_driver.OpenRead(AppFiles.KeyFile)))
            keyPem = keyReader.ReadToEnd();
#pragma warning restore CS0618

        using X509Certificate2 tempCert = X509Certificate2.CreateFromPem(certPem, keyPem);

        byte[] pkcs12Data = tempCert.Export(X509ContentType.Pkcs12);
        return X509CertificateLoader.LoadPkcs12(pkcs12Data, null);
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

            if (_cachedCertificate.NotAfter < DateTime.Now.AddDays(RenewalThresholdDays))
            {
                _logger.LogInformation(
                    "SSL cert expires {NotAfter:yyyy-MM-dd} — will attempt renewal",
                    _cachedCertificate.NotAfter
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
            _logger.LogInformation("SSL Certificate is valid");
            return;
        }

        bool hasExistingCert = HasValidCertificate();

        _logger.LogInformation(
            !hasExistingCert ? "Generating SSL Certificate..." : "Renewing SSL Certificate..."
        );

        try
        {
            string? token = accessToken;
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Skipping certificate renewal — no auth token available");
                return;
            }

            HttpClient client = _httpClientFactory.CreateClient("cert-renewal");
            client.BaseAddress = new(ExternalServicesConfig.Current.ApiServerBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);

            string serverUrl = $"certificate?id={Info.DeviceId}";
            if (hasExistingCert)
                serverUrl = $"renew-certificate?id={Info.DeviceId}";

            for (int attempt = 1; attempt <= maxRetries; attempt++)
                try
                {
                    CertificateDto? result = await FetchCertificate(
                        client,
                        serverUrl,
                        hasExistingCert
                    );
                    if (result != null)
                        return;

                    // null means 202 — cert not ready yet, wait and retry
                    _logger.LogInformation(
                        "Certificate not ready, waiting {CertRetryDelaySeconds}s (attempt {Attempt}/{MaxRetries})",
                        CertRetryDelaySeconds,
                        attempt,
                        maxRetries
                    );
                    await Task.Delay(TimeSpan.FromSeconds(CertRetryDelaySeconds));
                }
                catch (CertificateNotDueException ex)
                {
                    // Permanent (for this attempt window): the API rejected the
                    // renewal request because the cert isn't due yet. Bail out
                    // without retrying — daily cron will try again tomorrow.
                    _logger.LogInformation("Skipping renewal: {Message}", ex.Message);
                    return;
                }
                catch (Exception ex)
                    when (attempt < maxRetries
                        && (ex is HttpRequestException || ex is InvalidOperationException)
                    )
                {
                    _logger.LogInformation(
                        "Certificate attempt failed: {Message}, retrying in {CertRetryDelaySeconds}s (attempt {Attempt}/{MaxRetries})",
                        ex.Message,
                        CertRetryDelaySeconds,
                        attempt,
                        maxRetries
                    );
                    await Task.Delay(TimeSpan.FromSeconds(CertRetryDelaySeconds));
                }
        }
        catch (Exception ex) when (hasExistingCert)
        {
            // Cert exists in DB but renewal failed (Cloudflare down, network issue, etc.)
            // The existing cert is usable — don't block boot
            _logger.LogWarning(
                "Certificate renewal failed: {Message}. Using existing certificate.",
                ex.Message
            );
        }
    }

    private async Task<CertificateDto?> FetchCertificate(
        HttpClient client,
        string serverUrl,
        bool hasExistingCert
    )
    {
        using HttpResponseMessage response = await client.GetAsync(serverUrl);

        if (response.StatusCode == HttpStatusCode.Accepted) // 202 — cert not ready yet
        {
            _logger.LogInformation("Certificate not ready yet (202 Accepted), will retry");
            return null;
        }

        if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            throw new HttpRequestException("Gateway timeout waiting for certificate");

        // 400 from the renewal endpoint means the API doesn't think the cert is
        // due yet (it gates renewal at 14 days from expiry). Surface the body
        // as a typed exception so the outer retry loop can bail instead of
        // hammering 30 times with the same predictable rejection.
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            string body = await response.Content.ReadAsStringAsync();
            string apiMessage = ExtractApiMessage(body);
            throw new CertificateNotDueException(
                string.IsNullOrEmpty(apiMessage)
                    ? "API rejected renewal with 400 Bad Request (no body)"
                    : apiMessage
            );
        }

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();
        ApiResponse<CertificateResponse> data =
            content.FromJson<ApiResponse<CertificateResponse>>()
            ?? throw new("Failed to deserialize certificate JSON");

        if (data.Data is null)
            throw new InvalidOperationException(
                $"Certificate API returned no data (status: {data.Status ?? "unknown"}, message: {data.Message ?? "none"})"
            );

        if (
            string.IsNullOrEmpty(data.Data.PrivateKey)
            || string.IsNullOrEmpty(data.Data.Certificate)
        )
            throw new InvalidOperationException(
                $"Certificate API returned incomplete data (status: {data.Status ?? "unknown"})"
            );

        string certPem = $"{data.Data.Certificate}\n{data.Data.IssuerCertificate}";
        string keyPem = data.Data.PrivateKey;
        string? caPem = string.IsNullOrEmpty(data.Data.CertificateAuthority)
            ? null
            : data.Data.CertificateAuthority;

        // Write to DB
        await using AppDbContext db = new();
        UpsertConfig(db, "ssl_certificate", certPem);
        UpsertConfig(db, "ssl_private_key", keyPem);
        if (!string.IsNullOrEmpty(caPem))
            UpsertConfig(db, "ssl_ca", caPem);
        await db.SaveChangesAsync();

        // Update in-memory cache
        lock (_certLock)
        {
            using X509Certificate2 tempCert = X509Certificate2.CreateFromPem(certPem, keyPem);
            byte[] pkcs12Data = tempCert.Export(X509ContentType.Pkcs12);
            _cachedCertificate = X509CertificateLoader.LoadPkcs12(pkcs12Data, null);
        }

        // Keep file writes alongside DB writes for backwards compat (Task 17 removes these)
#pragma warning disable CS0618 // Obsolete
        if (_driver.FileExists(AppFiles.KeyFile))
            _driver.DeleteFile(AppFiles.KeyFile);
        if (_driver.FileExists(AppFiles.CaFile))
            _driver.DeleteFile(AppFiles.CaFile);
        if (_driver.FileExists(AppFiles.CertFile))
            _driver.DeleteFile(AppFiles.CertFile);

        await WriteTextAsync(AppFiles.KeyFile, keyPem);
        await WriteTextAsync(AppFiles.CaFile, data.Data.CertificateAuthority);
        await WriteTextAsync(AppFiles.CertFile, certPem);
#pragma warning restore CS0618

        _logger.LogInformation(
            !hasExistingCert ? "SSL Certificate created" : "SSL Certificate renewed"
        );
        return new();
    }

    private async Task WriteTextAsync(string path, string content)
    {
        await using Stream stream = _driver.OpenWrite(path, overwrite: true);
        await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync(content);
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
            : base(message) { }
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
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        try
        {
            ApiResponse<object>? parsed = body.FromJson<ApiResponse<object>>();
            if (!string.IsNullOrEmpty(parsed?.Message))
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
        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; } = default!;
    }

    public class CertificateResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; } = null!;

        [JsonProperty("certificate")]
        public string Certificate { get; set; } = null!;

        [JsonProperty("private_key")]
        public string PrivateKey { get; set; } = null!;

        [JsonProperty("issuer_certificate")]
        public string IssuerCertificate { get; set; } = null!;

        [JsonProperty("certificate_authority")]
        public string CertificateAuthority { get; set; } = null!;
    }
}
