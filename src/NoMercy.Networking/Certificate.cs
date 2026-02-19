using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using DnsHttpClient = NoMercy.NmSystem.Extensions.HttpClient;

namespace NoMercy.Networking;

public static class Certificate
{
    public static bool HasValidCertificate()
    {
        return File.Exists(AppFiles.CertFile) && File.Exists(AppFiles.KeyFile);
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
            ServerCertificate = CombinePublicAndPrivateCerts()
        };
    }

    private static X509Certificate2 CombinePublicAndPrivateCerts()
    {
        if (!File.Exists(AppFiles.CertFile))
            throw new FileNotFoundException($"Certificate file not found: {AppFiles.CertFile}");
        
        if (!File.Exists(AppFiles.KeyFile))
            throw new FileNotFoundException($"Private key file not found: {AppFiles.KeyFile}");

        string certPem = File.ReadAllText(AppFiles.CertFile);
        string keyPem = File.ReadAllText(AppFiles.KeyFile);
        
        using X509Certificate2 tempCert = X509Certificate2.CreateFromPem(certPem, keyPem);
        
        byte[] pkcs12Data = tempCert.Export(X509ContentType.Pkcs12);
        return X509CertificateLoader.LoadPkcs12(pkcs12Data, null);
    }


    private static bool ValidateSslCertificate()
    {
        if (!File.Exists(AppFiles.CertFile))
            return false;

        try
        {
            X509Certificate2 certificate = CombinePublicAndPrivateCerts();

            // Don't call certificate.Verify() — it may trigger OCSP/CRL network calls
            // which fail when Cloudflare or the internet is down. Just check the expiry date.
            if (certificate.NotAfter <= DateTime.Now)
                return false; // Actually expired

            if (certificate.NotAfter < DateTime.Now.AddDays(30))
            {
                Logger.Certificate(
                    $"SSL cert expires {certificate.NotAfter:yyyy-MM-dd} — will attempt renewal");
                return false; // Expiring soon — trigger renewal
            }

            return true;
        }
        catch (Exception e)
        {
            Logger.Certificate($"Failed to validate certificate: {e.Message}",
                Serilog.Events.LogEventLevel.Warning);
            return false;
        }
    }

    private static readonly int[] CertBackoffSeconds = [2, 5, 15, 30, 60];

    public static async Task RenewSslCertificate(int maxRetries = 5)
    {
        if (ValidateSslCertificate())
        {
            Logger.Certificate("SSL Certificate is valid");
            return;
        }

        bool hasExistingCert = File.Exists(AppFiles.CertFile);

        Logger.Certificate(!hasExistingCert
            ? "Generating SSL Certificate..."
            : "Renewing SSL Certificate...");

        try
        {
            using HttpClient client = DnsHttpClient.WithDns();
            client.BaseAddress = new(Config.ApiServerBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            client.DefaultRequestHeaders.Authorization = new("Bearer", Globals.Globals.AccessToken);

            string serverUrl = $"certificate?id={Info.DeviceId}";
            if (hasExistingCert)
                serverUrl = $"renew-certificate?id={Info.DeviceId}";

            for (int attempt = 1; attempt <= maxRetries; attempt++)
                try
                {
                    if (await FetchCertificate(maxRetries, client, serverUrl, attempt, hasExistingCert))
                        return;
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    int delay = CertBackoffSeconds[Math.Min(attempt - 1, CertBackoffSeconds.Length - 1)];
                    Logger.Certificate(
                        $"Request failed: {ex.Message}, retrying in {delay}s (attempt {attempt}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(delay));
                }
        }
        catch (Exception ex) when (hasExistingCert)
        {
            // Cert exists on disk but renewal failed (Cloudflare down, network issue, etc.)
            // The existing cert is usable — don't block boot
            Logger.Certificate(
                $"Certificate renewal failed: {ex.Message}. Using existing certificate.",
                Serilog.Events.LogEventLevel.Warning);
        }
    }

    private static async Task<bool> FetchCertificate(int maxRetries, HttpClient client, string serverUrl,
        int attempt, bool hasExistingCert)
    {
        using HttpResponseMessage response = await client.GetAsync(serverUrl);
        if (response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
        {
            if (attempt == maxRetries)
                throw new HttpRequestException("Max retries reached for certificate renewal");

            int delay = CertBackoffSeconds[Math.Min(attempt - 1, CertBackoffSeconds.Length - 1)];
            await Task.Delay(TimeSpan.FromSeconds(delay));
            return false;
        }

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();
        ApiResponse<CertificateResponse> data = content.FromJson<ApiResponse<CertificateResponse>>()
                                                ?? throw new("Failed to deserialize JSON");

        if (File.Exists(AppFiles.KeyFile))
            File.Delete(AppFiles.KeyFile);
        if (File.Exists(AppFiles.CaFile))
            File.Delete(AppFiles.CaFile);
        if (File.Exists(AppFiles.CertFile))
            File.Delete(AppFiles.CertFile);

        await File.WriteAllTextAsync(AppFiles.KeyFile, data.Data.PrivateKey);
        await File.WriteAllTextAsync(AppFiles.CaFile, data.Data.CertificateAuthority);
        await File.WriteAllTextAsync(AppFiles.CertFile,
            $"{data.Data.Certificate}\n{data.Data.IssuerCertificate}");

        Logger.Certificate(!hasExistingCert
            ? "SSL Certificate created"
            : "SSL Certificate renewed");
        return true;
    }

    public class ApiResponse<T>
    {
        [JsonProperty("status")] public string? Status { get; set; }
        [JsonProperty("message")] public string? Message { get; set; }
        [JsonProperty("data")] public T Data { get; set; } = default!;
    }

    public class CertificateResponse
    {
        [JsonProperty("status")] public string Status { get; set; } = null!;
        [JsonProperty("certificate")] public string Certificate { get; set; } = null!;
        [JsonProperty("private_key")] public string PrivateKey { get; set; } = null!;
        [JsonProperty("issuer_certificate")] public string IssuerCertificate { get; set; } = null!;
        [JsonProperty("certificate_authority")] public string CertificateAuthority { get; set; } = null!;
    }
}