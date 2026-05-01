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
    public static readonly string[] AllowedTypes =
    [
        "local",
        "smb",
        "nfs",
        "s3",
        "r2",
        "webdav",
    ];

    public static readonly DriverMetadataDto[] All =
    [
        new()
        {
            Type = "local",
            DisplayName = "Local disk",
            Available = true,
            ConfigSchema = new() { { "rootPath", "string?" } },
        },
        new()
        {
            Type = "smb",
            DisplayName = "SMB/CIFS (OS mount)",
            Available = true,
            ConfigSchema = new() { { "mountPath", "string" } },
        },
        new()
        {
            Type = "nfs",
            DisplayName = "NFS (in-process, no OS mount required)",
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
            DisplayName = "WebDAV (Nextcloud, ownCloud, Synology DSM, SharePoint, mod_dav)",
            Available = true,
            ConfigSchema = new()
            {
                {
                    "url",
                    "string (required) — base URL, e.g. https://nextcloud.example.com/remote.php/dav/files/user/Movies/"
                },
                { "username", "string? — Basic Auth username" },
                {
                    "passwordRef",
                    "string? — credential store key; resolved tuple second element is the password"
                },
                {
                    "bearerTokenRef",
                    "string? — credential store key for Bearer token auth (mutually exclusive with username/passwordRef)"
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
            case "local":
                if (config is null)
                    return null;

                string? rootPath = config["rootPath"]?.Value<string>();
                if (rootPath is not null && string.IsNullOrWhiteSpace(rootPath))
                    return "config.rootPath must be a non-empty string when provided.";

                return null;

            case "smb":
                if (config is null)
                    return "config is required for 'smb' and must include 'mountPath'.";

                string? mountPath = config["mountPath"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(mountPath))
                    return "config.mountPath must be a non-empty string for 'smb'.";

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

                bool hasBasicAuth =
                    !string.IsNullOrWhiteSpace(config["username"]?.Value<string>())
                    || !string.IsNullOrWhiteSpace(config["passwordRef"]?.Value<string>());
                bool hasBearerAuth = !string.IsNullOrWhiteSpace(
                    config["bearerTokenRef"]?.Value<string>()
                );
                if (hasBasicAuth && hasBearerAuth)
                    return "config for 'webdav' specifies both Basic Auth (username/passwordRef) and Bearer token (bearerTokenRef). Only one auth scheme may be set.";

                return null;

            default:
                return null;
        }
    }
}
