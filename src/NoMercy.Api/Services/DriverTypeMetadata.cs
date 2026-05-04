using Newtonsoft.Json.Linq;
using NoMercy.Api.DTOs.Dashboard;

namespace NoMercy.Api.Services;

/// <summary>
/// Static catalogue of supported storage driver types, their display names,
/// and the JSON config keys they accept. Used by both the folder-driver picker
/// and the named-driver admin page.
/// </summary>
public static class DriverTypeMetadata
{
    // Types that users are allowed to create through the dashboard.
    // "local" is excluded — it is a built-in system driver, not user-created.
    public static readonly string[] AllUserCreatable = ["nfs", "s3", "r2", "webdav"];

    // All recognised types — used by the factory and for validation of
    // existing rows (e.g. the system local driver uses "local").
    public static readonly string[] AllRecognized = ["local", "nfs", "s3", "r2", "webdav"];

    // Alias kept for call sites that haven't been updated yet.
    public static readonly string[] AllowedTypes = AllUserCreatable;

    public static readonly DriverMetadataDto[] All =
    [
        new()
        {
            Type = "nfs",
            DisplayName = "NFS",
            Available = true,
            ConfigSchema = new()
            {
                { "server", "string (required) — hostname or IP of NFS server" },
                { "export", "string (required) — export path, e.g. /exports/media" },
                { "version", "int? (default 3) — NFS version: 3 or 4" },
                { "uid", "int? — AUTH_UNIX user ID" },
                { "gid", "int? — AUTH_UNIX group ID" },
                { "port", "int? (default 2049) — NFS port" },
                { "mountPort", "int? — mount protocol port (NFS3 only)" },
            },
        },
        new()
        {
            Type = "s3",
            DisplayName = "S3-compatible",
            Available = true,
            ConfigSchema = new()
            {
                { "bucket", "string" },
                { "region", "string" },
                { "prefix", "string?" },
                { "credentialsRef", "string?" },
                { "endpoint", "string?" },
            },
        },
        new()
        {
            Type = "r2",
            DisplayName = "Cloudflare R2",
            Available = true,
            ConfigSchema = new()
            {
                { "bucket", "string" },
                { "region", "string" },
                { "prefix", "string?" },
                { "credentialsRef", "string?" },
                { "endpoint", "string (required for R2)" },
            },
        },
        new()
        {
            Type = "webdav",
            DisplayName = "WebDAV",
            Available = true,
            ConfigSchema = new()
            {
                {
                    "url",
                    "string (required) — base URL of the WebDAV collection, e.g. https://nextcloud.example.com/remote.php/dav/files/user/Movies/"
                },
                {
                    "ignoreCertErrors",
                    "bool? (default false) — skip TLS cert validation (opt-in for self-signed certs)"
                },
                { "timeoutSeconds", "int? (default 30) — HTTP request timeout" },
            },
        },
    ];

    public static string? ValidateConfig(string driverType, JObject? config)
    {
        switch (driverType)
        {
            // "local" is the system driver — users can't create it, so no
            // config validation is needed on the create/update path. The
            // factory handles empty rootPath as the system-local passthrough.
            case "local":
                return null;

            case "nfs":
                if (config is null)
                    return "config is required for 'nfs' and must include 'server' and 'export'.";

                string? nfsServer = config["server"]?.Value<string>();
                string? nfsExport = config["export"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(nfsServer))
                    return "config.server must be a non-empty string for 'nfs'.";

                if (string.IsNullOrWhiteSpace(nfsExport))
                    return "config.export must be a non-empty string for 'nfs'.";

                int? nfsVersion = config["version"]?.Value<int?>();
                if (nfsVersion.HasValue && nfsVersion != 3 && nfsVersion != 4)
                    return "config.version must be 3 or 4 for 'nfs'.";

                return null;

            case "s3":
            case "r2":
                if (config is null)
                    return $"config is required for '{driverType}' and must include 'bucket' and 'region'.";

                string? bucket = config["bucket"]?.Value<string>();
                string? region = config["region"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(bucket))
                    return $"config.bucket must be a non-empty string for '{driverType}'.";

                if (string.IsNullOrWhiteSpace(region))
                    return $"config.region must be a non-empty string for '{driverType}'.";

                if (driverType == "r2")
                {
                    string? endpoint = config["endpoint"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(endpoint))
                        return "config.endpoint is required for 'r2' (set to your R2 endpoint URL).";
                }

                return null;

            case "webdav":
                if (config is null)
                    return "config is required for 'webdav' and must include 'url'.";

                string? webDavUrl = config["url"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(webDavUrl))
                    return "config.url must be a non-empty string for 'webdav'.";

                return null;

            default:
                return null;
        }
    }
}
