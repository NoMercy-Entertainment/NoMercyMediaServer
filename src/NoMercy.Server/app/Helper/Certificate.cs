using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Newtonsoft.Json;
using NoMercy.Networking;
using NoMercy.NmSystem;

namespace NoMercy.Server.app.Helper;

public static class Certificate
{
    public static void KestrelConfig(KestrelServerOptions options)
    {
        options.ConfigureEndpointDefaults(listenOptions =>
#pragma warning disable CS0618 // Type or member is obsolete
            listenOptions.UseHttps(HttpsConnectionAdapterOptions()));
#pragma warning restore CS0618 // Type or member is obsolete
        options.AddServerHeader = false;
    }

    [Obsolete("Obsolete")]
    private static X509Certificate2 CombinePublicAndPrivateCerts()
    {
        byte[] publicPemBytes = File.ReadAllBytes(Path.Combine(AppFiles.CertFile));

        using X509Certificate2 publicX509 = new(publicPemBytes);

        string privateKeyText = File.ReadAllText(Path.Combine(AppFiles.KeyFile));
        string[] privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
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
        }

        X509Certificate2 keyPair = publicX509.CopyWithPrivateKey(rsa);
        return new(keyPair.Export(X509ContentType.Pfx));
    }

    [Obsolete("Obsolete")]
    private static HttpsConnectionAdapterOptions HttpsConnectionAdapterOptions()
    {
        return new()
        {
            SslProtocols = SslProtocols.Tls12,
            ServerCertificate = CombinePublicAndPrivateCerts(),
            ServerCertificateChain = [new(Path.Combine(AppFiles.CaFile))]
        };
    }

    [Obsolete("Obsolete")]
    private static bool ValidateSslCertificate()
    {
        if (!File.Exists(Path.Combine(AppFiles.CertFile)))
            return false;

        X509Certificate2 certificate = CombinePublicAndPrivateCerts();

        if (!certificate.Verify())
            return false;

        return certificate.NotAfter >= DateTime.Now - TimeSpan.FromDays(30);
    }

    [Obsolete("Obsolete")]
    public static async Task RenewSslCertificate()
    {
        if (ValidateSslCertificate())
        {
            Logger.Certificate(@"SSL Certificate is valid");
            await Task.CompletedTask;
            return;
        }

        Logger.Certificate(@"Renewing SSL Certificate...");

        HttpClient client = new();
        client.Timeout = new(0, 10, 0);
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        client.DefaultRequestHeaders.Authorization = new("Bearer", Auth.AccessToken);

        string serverUrl = $"{Config.ApiServerBaseUrl}certificate?server_id={Info.DeviceId}";

        if (File.Exists(AppFiles.CertFile))
            serverUrl = $"{Config.ApiServerBaseUrl}renewcertificate?server_id={Info.DeviceId}";

        string response = client
            .GetStringAsync(serverUrl)
            .Result;

        CertificateResponse data = JsonConvert.DeserializeObject<CertificateResponse>(response)
                                   ?? throw new("Failed to deserialize JSON");

        if (File.Exists(AppFiles.KeyFile))
            File.Delete(AppFiles.KeyFile);

        if (File.Exists(AppFiles.CaFile))
            File.Delete(AppFiles.CaFile);

        if (File.Exists(AppFiles.CertFile))
            File.Delete(AppFiles.CertFile);

        await File.WriteAllTextAsync(AppFiles.KeyFile, $"{data.PrivateKey}");
        await File.WriteAllTextAsync(AppFiles.CaFile, $"{data.CertificateAuthority}");
        await File.WriteAllTextAsync(AppFiles.CertFile, @$"{data.Certificate}\n{data.IssuerCertificate}");

        Logger.Certificate("SSL Certificate renewed");

        await Task.CompletedTask;
    }

    public class CertificateResponse
    {
        [JsonProperty("status")] public string Status { get; set; } = string.Empty;
        [JsonProperty("certificate")] public string Certificate { get; set; } = string.Empty;
        [JsonProperty("private_key")] public string PrivateKey { get; set; } = string.Empty;
        [JsonProperty("issuer_certificate")] public string IssuerCertificate { get; set; } = string.Empty;

        [JsonProperty("certificate_authority")]
        public string CertificateAuthority { get; set; } = string.Empty;
    }
}