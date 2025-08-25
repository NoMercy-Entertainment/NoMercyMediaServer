using System.Security.Authentication;
using System.Security.Cryptography;
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
    public static void KestrelConfig(KestrelServerOptions options)
    {
        options.ConfigureEndpointDefaults(listenOptions => 
            listenOptions.UseHttps(HttpsConnectionAdapterOptions()));
        options.AddServerHeader = false;
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

        try
        {
            X509Certificate2 publicX509 = X509CertificateLoader.LoadCertificateFromFile(AppFiles.CertFile);

            string privateKeyText = File.ReadAllText(AppFiles.KeyFile);
            string[] privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
        
            if (privateKeyBlocks.Length < 2)
                throw new InvalidDataException("Invalid private key format - missing key data");
            
            byte[] privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);

            using RSA rsa = RSA.Create();
            switch (privateKeyBlocks[0])
            {
                case "BEGIN PRIVATE KEY":
                    rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
                    break;
                case "BEGIN RSA PRIVATE KEY":
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported private key format: {privateKeyBlocks[0]}");
            }

            return publicX509.CopyWithPrivateKey(rsa);
        }
        catch (Exception ex)
        {
            throw new CryptographicException($"Failed to load certificate: {ex.Message}", ex);
        }
    }


    private static bool ValidateSslCertificate()
    {
        if (!File.Exists(AppFiles.CertFile))
            return false;

        X509Certificate2 certificate = CombinePublicAndPrivateCerts();

        if (!certificate.Verify())
            return false;

        return certificate.NotAfter >= DateTime.Now - TimeSpan.FromDays(30);
    }

    public static async Task RenewSslCertificate(int maxRetries = 3, int delaySeconds = 5)
    {
        bool hasExistingCert = File.Exists(AppFiles.CertFile);
        if (ValidateSslCertificate())
        {
            Logger.Certificate("SSL Certificate is valid");
            return;
        }

        Logger.Certificate(!hasExistingCert
            ? "Generating SSL Certificate..."
            : "Renewing SSL Certificate...");

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
                if (await FetchCertificate(maxRetries, delaySeconds, client, serverUrl, attempt, hasExistingCert))
                    return;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                Logger.Certificate(
                    $"Request failed: {ex.Message}, retrying in {delaySeconds} seconds (attempt {attempt}/{maxRetries})");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
    }

    private static async Task<bool> FetchCertificate(int maxRetries, int delaySeconds, HttpClient client, string serverUrl,
        int attempt, bool hasExistingCert)
    {
        HttpResponseMessage response = await client.GetAsync(serverUrl);
        if (response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
        {
            if (attempt == maxRetries)
                throw new HttpRequestException("Max retries reached for certificate renewal");

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
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