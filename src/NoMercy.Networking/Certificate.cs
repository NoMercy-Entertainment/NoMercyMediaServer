using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using DnsHttpClient = NoMercy.NmSystem.Extensions.HttpClient;

namespace NoMercy.Networking;

public static class Certificate
{
    private static X509Certificate2? _cachedCertificate;
    private static readonly object _certLock = new();

    public static void LoadFromDb()
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
            if (File.Exists(AppFiles.CertFile) && File.Exists(AppFiles.KeyFile))
            {
                certPem = File.ReadAllText(AppFiles.CertFile);
                keyPem = File.ReadAllText(AppFiles.KeyFile);
                Logger.Setup("Loading SSL certificate from legacy PEM files");
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

        Logger.Setup("Loaded SSL certificate into memory cache");
    }

    private static void UpsertConfig(AppDbContext db, string key, string value)
    {
        Configuration? existing = db.Configuration.FirstOrDefault(c => c.Key == key);
        if (existing != null)
        {
            existing.SecureValue = value;
        }
        else
        {
            db.Configuration.Add(new Configuration { Key = key, SecureValue = value });
        }
    }

    public static bool HasValidCertificate()
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
        return File.Exists(AppFiles.CertFile) && File.Exists(AppFiles.KeyFile);
#pragma warning restore CS0618
    }

    public static void KestrelConfig(KestrelServerOptions options)
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = 100L * 1024 * 1024 * 1024; // 100GB — 4K remux support
        options.Limits.MaxRequestBufferSize = null; // Kestrel manages adaptively
        options.Limits.MaxConcurrentConnections = 1000; // Many streaming clients
        options.Limits.MaxConcurrentUpgradedConnections = 500; // WebSocket/SignalR
    }

    public static void ConfigureHttpsListener(ListenOptions listenOptions)
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

    private static HttpsConnectionAdapterOptions HttpsConnectionAdapterOptions()
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
    private static X509Certificate2 CombinePublicAndPrivateCerts()
    {
#pragma warning disable CS0618 // Obsolete
        if (!File.Exists(AppFiles.CertFile))
            throw new FileNotFoundException($"Certificate file not found: {AppFiles.CertFile}");

        if (!File.Exists(AppFiles.KeyFile))
            throw new FileNotFoundException($"Private key file not found: {AppFiles.KeyFile}");

        string certPem = File.ReadAllText(AppFiles.CertFile);
        string keyPem = File.ReadAllText(AppFiles.KeyFile);
#pragma warning restore CS0618

        using X509Certificate2 tempCert = X509Certificate2.CreateFromPem(certPem, keyPem);

        byte[] pkcs12Data = tempCert.Export(X509ContentType.Pkcs12);
        return X509CertificateLoader.LoadPkcs12(pkcs12Data, null);
    }

    private static bool ValidateSslCertificate()
    {
        lock (_certLock)
        {
            if (_cachedCertificate is null)
                return false;

            // Don't call certificate.Verify() — it may trigger OCSP/CRL network calls
            // which fail when Cloudflare or the internet is down. Just check the expiry date.
            if (_cachedCertificate.NotAfter <= DateTime.Now)
                return false; // Actually expired

            if (_cachedCertificate.NotAfter < DateTime.Now.AddDays(30))
            {
                Logger.Certificate(
                    $"SSL cert expires {_cachedCertificate.NotAfter:yyyy-MM-dd} — will attempt renewal"
                );
                return false; // Expiring soon — trigger renewal
            }

            return true;
        }
    }

    private const int CertRetryDelaySeconds = 10;

    public static async Task RenewSslCertificate(int maxRetries = 30)
    {
        if (ValidateSslCertificate())
        {
            Logger.Certificate("SSL Certificate is valid");
            return;
        }

        bool hasExistingCert = HasValidCertificate();

        Logger.Certificate(
            !hasExistingCert ? "Generating SSL Certificate..." : "Renewing SSL Certificate..."
        );

        try
        {
            string? token = Globals.Globals.AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                Logger.Setup(
                    "Skipping certificate renewal — no auth token available",
                    LogEventLevel.Warning
                );
                return;
            }

            using HttpClient client = DnsHttpClient.WithDns();
            client.BaseAddress = new(Config.ApiServerBaseUrl);
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
                    Logger.Certificate(
                        $"Certificate not ready, waiting {CertRetryDelaySeconds}s (attempt {attempt}/{maxRetries})"
                    );
                    await Task.Delay(TimeSpan.FromSeconds(CertRetryDelaySeconds));
                }
                catch (Exception ex)
                    when (attempt < maxRetries
                        && (ex is HttpRequestException || ex is InvalidOperationException)
                    )
                {
                    Logger.Certificate(
                        $"Certificate attempt failed: {ex.Message}, retrying in {CertRetryDelaySeconds}s (attempt {attempt}/{maxRetries})"
                    );
                    await Task.Delay(TimeSpan.FromSeconds(CertRetryDelaySeconds));
                }
        }
        catch (Exception ex) when (hasExistingCert)
        {
            // Cert exists in DB but renewal failed (Cloudflare down, network issue, etc.)
            // The existing cert is usable — don't block boot
            Logger.Certificate(
                $"Certificate renewal failed: {ex.Message}. Using existing certificate.",
                LogEventLevel.Warning
            );
        }
    }

    private static async Task<CertificateDto?> FetchCertificate(
        HttpClient client,
        string serverUrl,
        bool hasExistingCert
    )
    {
        using HttpResponseMessage response = await client.GetAsync(serverUrl);

        if (response.StatusCode == System.Net.HttpStatusCode.Accepted) // 202 — cert not ready yet
        {
            Logger.Certificate("Certificate not ready yet (202 Accepted), will retry");
            return null;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
            throw new HttpRequestException("Gateway timeout waiting for certificate");

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
        using AppDbContext db = new();
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
        if (File.Exists(AppFiles.KeyFile))
            File.Delete(AppFiles.KeyFile);
        if (File.Exists(AppFiles.CaFile))
            File.Delete(AppFiles.CaFile);
        if (File.Exists(AppFiles.CertFile))
            File.Delete(AppFiles.CertFile);

        await File.WriteAllTextAsync(AppFiles.KeyFile, keyPem);
        await File.WriteAllTextAsync(AppFiles.CaFile, data.Data.CertificateAuthority);
        await File.WriteAllTextAsync(AppFiles.CertFile, certPem);
#pragma warning restore CS0618

        Logger.Certificate(
            !hasExistingCert ? "SSL Certificate created" : "SSL Certificate renewed"
        );
        return new CertificateDto();
    }

    /// <summary>Sentinel returned by FetchCertificate to indicate a successfully written certificate.</summary>
    private sealed class CertificateDto { }

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
