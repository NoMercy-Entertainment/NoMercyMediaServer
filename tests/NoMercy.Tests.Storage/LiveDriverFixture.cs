using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Drivers.WebDav;
using WebDav;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Shared plumbing for live integration tests against real remote drivers.
//
// Config is sourced from the running server's REST API rather than direct DB
// access — NoMercy.Tests.Storage intentionally excludes NmSystem to keep its
// dependency graph minimal, so AppFiles / Config are not available here.
//
// API: GET https://192-168-2-201.fcbb730a-a562-2ca5-d054-5d6acf2a1aaa.nomercy.tv:7626/api/v1/dashboard/drivers
// Bearer token: %LOCALAPPDATA%\NoMercy_dev\config\tokens.json → access_token
// CA cert:      %LOCALAPPDATA%\NoMercy_dev\security\certs\ca.pem
// ============================================================================

/// <summary>
/// Raw shape of a driver entry returned by the dashboard API.
/// </summary>
internal sealed class ApiDriverDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("config")]
    public JsonElement? Config { get; set; }

    [JsonPropertyName("credentials_configured")]
    public bool CredentialsConfigured { get; set; }
}

/// <summary>
/// Resolved driver ready for use in tests. Holds the raw config fields so
/// fixture subclasses can build the concrete driver.
/// </summary>
public sealed class ResolvedDriver
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool CredentialsConfigured { get; init; }

    // S3 / R2 fields (null when not applicable)
    public string? Bucket { get; init; }
    public string? Region { get; init; }
    public string? Endpoint { get; init; }
    public string? Prefix { get; init; }
    public string? CredentialsRef { get; init; }

    // WebDAV fields
    public string? Url { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Fetches driver config from the running dev server once per fixture lifetime.
/// Caches results. Thread-safe (lazy init in InitializeAsync).
/// </summary>
internal sealed class LiveDriverCatalog
{
    private static readonly string ServerBaseUrl =
        "https://192-168-2-201.fcbb730a-a562-2ca5-d054-5d6acf2a1aaa.nomercy.tv:7626";

    private static readonly string ApiUrl = $"{ServerBaseUrl}/api/v1/dashboard/drivers";

    private List<ApiDriverDto>? _drivers;

    public string? FetchError { get; private set; }

    public async Task InitAsync()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );
            string tokenFile = Path.Combine(localAppData, "NoMercy_dev", "config", "tokens.json");
            string caFile = Path.Combine(
                localAppData,
                "NoMercy_dev",
                "security",
                "certs",
                "ca.pem"
            );

            if (!File.Exists(tokenFile))
            {
                FetchError = $"Token file not found at {tokenFile}";
                return;
            }

            string tokenJson = await File.ReadAllTextAsync(tokenFile);
            JsonDocument tokenDoc = JsonDocument.Parse(tokenJson);
            string accessToken =
                tokenDoc.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("access_token missing from tokens.json");

            HttpClientHandler handler = new();

            if (File.Exists(caFile))
            {
                System.Security.Cryptography.X509Certificates.X509Certificate2Collection certs =
                    new();
                certs.ImportFromPem(await File.ReadAllTextAsync(caFile));
                handler.ServerCertificateCustomValidationCallback = (_, cert, chain, errors) =>
                {
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                        return true;
                    if (cert is null || chain is null)
                        return false;
                    chain.ChainPolicy.ExtraStore.AddRange(certs);
                    chain.ChainPolicy.VerificationFlags = System
                        .Security
                        .Cryptography
                        .X509Certificates
                        .X509VerificationFlags
                        .AllowUnknownCertificateAuthority;
                    return chain.Build(
                        new System.Security.Cryptography.X509Certificates.X509Certificate2(cert)
                    );
                };
            }

            using HttpClient http = new(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(10),
            };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

            HttpResponseMessage response = await http.GetAsync(ApiUrl);

            if (!response.IsSuccessStatusCode)
            {
                FetchError =
                    $"Server returned {(int)response.StatusCode} fetching drivers — is the server running?";
                return;
            }

            string body = await response.Content.ReadAsStringAsync();
            JsonSerializerOptions opts = new() { PropertyNameCaseInsensitive = true };
            _drivers =
                JsonSerializer.Deserialize<List<ApiDriverDto>>(body, opts)
                ?? throw new InvalidOperationException("Null deserialization result");
        }
        catch (HttpRequestException ex)
        {
            FetchError = $"Server unreachable ({ex.Message}) — start the dev server first";
        }
        catch (SocketException ex)
        {
            FetchError = $"Network error ({ex.Message})";
        }
        catch (Exception ex)
        {
            FetchError = $"Fixture init failed: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>
    /// Returns the first driver of the given type, or null when none exists.
    /// </summary>
    public ApiDriverDto? FirstOfType(string type) =>
        _drivers?.FirstOrDefault(d =>
            string.Equals(d.Type, type, StringComparison.OrdinalIgnoreCase)
        );
}

// ============================================================================
// R2 fixture
// ============================================================================

public sealed class R2LiveFixture : IAsyncLifetime
{
    private readonly LiveDriverCatalog _catalog = new();

    public string? SkipReason { get; private set; }

    // Resolved when fixture is ready and not skipped.
    public ResolvedDriver? Driver { get; private set; }

    public async Task InitializeAsync()
    {
        await _catalog.InitAsync();

        if (_catalog.FetchError is not null)
        {
            SkipReason = _catalog.FetchError;
            return;
        }

        ApiDriverDto? row = _catalog.FirstOfType("r2");
        if (row is null)
        {
            SkipReason = "R2 driver not configured (no row of type=r2 in Drivers table)";
            return;
        }

        if (!row.CredentialsConfigured)
        {
            SkipReason =
                "R2 driver lacks credentials — set credentials in secrets store before running";
            return;
        }

        Driver = ParseS3Config(row);
        if (Driver is null)
            SkipReason =
                $"R2 driver config is malformed — could not parse config JSON for {row.Id}";
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Builds a live S3StorageDriver for R2, or returns null when the endpoint
    /// is unreachable (transport-layer failure). Auth failures are NOT swallowed.
    /// </summary>
    public S3StorageDriver? BuildDriver(string? accessKey, string? secretKey)
    {
        if (Driver is null)
            return null;

        AmazonS3Config cfg = new()
        {
            ServiceURL = Driver.Endpoint,
            ForcePathStyle = true,
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(Driver.Region ?? "auto"),
        };

        AWSCredentials creds =
            accessKey is not null && secretKey is not null
                ? new BasicAWSCredentials(accessKey, secretKey)
                : new AnonymousAWSCredentials();

        IAmazonS3 client = new AmazonS3Client(creds, cfg);
        return new S3StorageDriver(client, Driver.Bucket!, Driver.Prefix);
    }

    private static ResolvedDriver? ParseS3Config(ApiDriverDto row)
    {
        if (row.Config is null)
            return null;

        JsonElement cfg = row.Config.Value;
        string? bucket = cfg.TryGetProperty("bucket", out JsonElement b) ? b.GetString() : null;
        string? region = cfg.TryGetProperty("region", out JsonElement r) ? r.GetString() : null;
        string? endpoint = cfg.TryGetProperty("endpoint", out JsonElement e) ? e.GetString() : null;
        string? prefix = cfg.TryGetProperty("prefix", out JsonElement p) ? p.GetString() : null;

        if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(region))
            return null;

        return new ResolvedDriver
        {
            Id = row.Id,
            Type = row.Type,
            CredentialsConfigured = row.CredentialsConfigured,
            Bucket = bucket,
            Region = region,
            Endpoint = endpoint,
            Prefix = prefix,
        };
    }
}

// ============================================================================
// S3 fixture
// ============================================================================

public sealed class S3LiveFixture : IAsyncLifetime
{
    private readonly LiveDriverCatalog _catalog = new();

    public string? SkipReason { get; private set; }
    public ResolvedDriver? Driver { get; private set; }

    public async Task InitializeAsync()
    {
        await _catalog.InitAsync();

        if (_catalog.FetchError is not null)
        {
            SkipReason = _catalog.FetchError;
            return;
        }

        ApiDriverDto? row = _catalog.FirstOfType("s3");
        if (row is null)
        {
            SkipReason = "S3 driver not configured (no row of type=s3 in Drivers table)";
            return;
        }

        if (!row.CredentialsConfigured)
        {
            SkipReason =
                "S3 driver lacks credentials — set credentials in secrets store before running";
            return;
        }

        Driver = ParseS3Config(row);
        if (Driver is null)
            SkipReason =
                $"S3 driver config is malformed — could not parse config JSON for {row.Id}";
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public S3StorageDriver? BuildDriver(string? accessKey, string? secretKey)
    {
        if (Driver is null)
            return null;

        AmazonS3Config cfg = new()
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(Driver.Region ?? "us-east-1"),
        };

        if (!string.IsNullOrWhiteSpace(Driver.Endpoint))
        {
            cfg.ServiceURL = Driver.Endpoint;
            cfg.ForcePathStyle = true;
        }

        AWSCredentials creds =
            accessKey is not null && secretKey is not null
                ? new BasicAWSCredentials(accessKey, secretKey)
                : new AnonymousAWSCredentials();

        IAmazonS3 client = new AmazonS3Client(creds, cfg);
        return new S3StorageDriver(client, Driver.Bucket!, Driver.Prefix);
    }

    private static ResolvedDriver? ParseS3Config(ApiDriverDto row)
    {
        if (row.Config is null)
            return null;

        JsonElement cfg = row.Config.Value;
        string? bucket = cfg.TryGetProperty("bucket", out JsonElement b) ? b.GetString() : null;
        string? region = cfg.TryGetProperty("region", out JsonElement r) ? r.GetString() : null;
        string? endpoint = cfg.TryGetProperty("endpoint", out JsonElement e) ? e.GetString() : null;
        string? prefix = cfg.TryGetProperty("prefix", out JsonElement p) ? p.GetString() : null;

        if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(region))
            return null;

        // The configured S3 bucket field is a URL — extract just the bucket name.
        // "https://console.s3.host/browser/stoney.eagle" → "stoney.eagle"
        if (bucket.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            bucket = bucket.TrimEnd('/');
            int lastSlash = bucket.LastIndexOf('/');
            if (lastSlash >= 0)
                bucket = bucket.Substring(lastSlash + 1);
        }

        return new ResolvedDriver
        {
            Id = row.Id,
            Type = row.Type,
            CredentialsConfigured = row.CredentialsConfigured,
            Bucket = bucket,
            Region = region,
            Endpoint = endpoint,
            Prefix = prefix,
        };
    }
}

// ============================================================================
// WebDAV fixture
// ============================================================================

public sealed class WebDavLiveFixture : IAsyncLifetime
{
    private readonly LiveDriverCatalog _catalog = new();

    public string? SkipReason { get; private set; }
    public ResolvedDriver? Driver { get; private set; }

    public async Task InitializeAsync()
    {
        await _catalog.InitAsync();

        if (_catalog.FetchError is not null)
        {
            SkipReason = _catalog.FetchError;
            return;
        }

        ApiDriverDto? row = _catalog.FirstOfType("webdav");
        if (row is null)
        {
            SkipReason = "WebDAV driver not configured (no row of type=webdav in Drivers table)";
            return;
        }

        if (!row.CredentialsConfigured)
        {
            SkipReason =
                "WebDAV driver lacks credentials — set credentials in secrets store before running";
            return;
        }

        Driver = ParseWebDavConfig(row);
        if (Driver is null)
            SkipReason =
                $"WebDAV driver config is malformed — could not parse config JSON for {row.Id}";
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public WebDavStorageDriver? BuildDriver(string? username, string? password)
    {
        if (Driver?.Url is null)
            return null;

        WebDavClientParams clientParams = new()
        {
            BaseAddress = new Uri(Driver.Url),
            Timeout = TimeSpan.FromSeconds(Driver.TimeoutSeconds),
        };

        if (username is not null && password is not null)
            clientParams.Credentials = new NetworkCredential(username, password);

        WebDavClient client = new(clientParams);
        return new WebDavStorageDriver(client, Driver.Url);
    }

    private static ResolvedDriver? ParseWebDavConfig(ApiDriverDto row)
    {
        if (row.Config is null)
            return null;

        JsonElement cfg = row.Config.Value;
        string? url = cfg.TryGetProperty("url", out JsonElement u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
            return null;

        int timeout = 30;
        if (
            cfg.TryGetProperty("timeoutSeconds", out JsonElement ts)
            && ts.ValueKind == JsonValueKind.Number
        )
            timeout = ts.GetInt32();

        return new ResolvedDriver
        {
            Id = row.Id,
            Type = row.Type,
            CredentialsConfigured = row.CredentialsConfigured,
            Url = url.TrimEnd('/') + "/",
            TimeoutSeconds = timeout,
        };
    }
}
