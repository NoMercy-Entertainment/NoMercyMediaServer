using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Storage.Remote;
using NoMercy.Storage.Validation;

namespace NoMercy.Storage.Factory;

/// <summary>
/// Parsed representation of the required JSON config for local-disk drivers.
/// <see cref="RootPath"/> is the absolute path to the local mount or directory.
/// </summary>
internal sealed record LocalDriverConfig(string? RootPath);

public sealed class StorageFactory : IStorageFactory
{
    private readonly IStorageDriver _driver;
    private readonly ILogger<StorageFactory> _logger;

    /// <summary>
    /// Resolves a driver ID to its (type, configJson). Supplied at DI
    /// registration from a higher-level project that has DB access.
    /// </summary>
    private readonly IDriverConfigResolver? _driverConfigResolver;

    /// <summary>
    /// Resolves a <c>credentialsRef</c> key to an (accessKey, secretKey) pair.
    /// When null, the AWS default credential chain is used for S3/R2.
    /// </summary>
    private readonly ICredentialResolver? _credentialResolver;

    // Cache key = "{folderId}:{driverType}:{sha256 of resolved configJson}".
    private readonly ConcurrentDictionary<string, IStorage> _cache = new();

    public StorageFactory(
        IStorageDriver driver,
        ILogger<StorageFactory> logger,
        IDriverConfigResolver? driverConfigResolver = null,
        ICredentialResolver? credentialResolver = null
    )
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _driverConfigResolver = driverConfigResolver;
        _credentialResolver = credentialResolver;
    }

    public IStorage For(Ulid folderId, Ulid driverId, string subPath)
    {
        string driverType = "local";
        string? configJson = null;

        if (_driverConfigResolver is null)
        {
            _logger.LogWarning(
                "Folder {FolderId} has DriverId {DriverId} but no IDriverConfigResolver is registered; falling back to built-in local",
                folderId,
                driverId
            );
        }
        else
        {
            (string Type, string? ConfigJson)? resolved = _driverConfigResolver.Resolve(driverId);
            if (resolved is null)
            {
                _logger.LogWarning(
                    "Driver {DriverId} not found for folder {FolderId}; falling back to built-in local",
                    driverId,
                    folderId
                );
            }
            else
            {
                driverType = resolved.Value.Type;
                configJson = resolved.Value.ConfigJson;
            }
        }

        // The cache key MUST include subPath: NFS / S3 / WebDAV storages
        // bake the subPath into their root (Export / prefix / URL). Two
        // callers asking for the same (folder, driver) but different subPath
        // would otherwise share a single Storage built for whichever
        // subPath landed first.
        string cacheKey = BuildCacheKey(folderId, driverType, configJson, subPath);
        return _cache.GetOrAdd(cacheKey, _ => Build(folderId, driverType, configJson, subPath));
    }

    public void Invalidate(Ulid folderId)
    {
        string prefix = folderId.ToString();
        foreach (string key in _cache.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                _cache.TryRemove(key, out _);
        }
    }

    public void InvalidateAll() => _cache.Clear();

    // -----------------------------------------------------------------------

    /// <summary>
    /// Joins a driver root and a folder sub-path using the correct separator
    /// for the given driver type. For local/nfs: <see cref="Path.Combine"/>.
    /// For s3/r2: prefix + "/" + subPath with de-duped slashes.
    /// For webdav: URL-style join.
    /// An empty <paramref name="subPath"/> returns <paramref name="root"/> unchanged.
    /// </summary>
    internal static string JoinRoot(string root, string subPath, string driverType)
    {
        if (string.IsNullOrEmpty(subPath))
            return root;

        switch (driverType)
        {
            case "local":
                return Path.Combine(root, subPath);

            case "nfs":
            {
                // NFS export paths are always Unix-style; use forward slashes.
                string trimmedRoot = root.TrimEnd('/');
                string trimmedSub = subPath.TrimStart('/');
                return $"{trimmedRoot}/{trimmedSub}";
            }

            case "s3":
            case "r2":
            {
                string trimmedRoot = root.TrimEnd('/');
                string trimmedSub = subPath.TrimStart('/');
                return $"{trimmedRoot}/{trimmedSub}";
            }

            case "webdav":
            {
                string trimmedRoot = root.TrimEnd('/');
                string trimmedSub = subPath.TrimStart('/');
                return $"{trimmedRoot}/{trimmedSub}";
            }

            default:
                return Path.Combine(root, subPath);
        }
    }

    private IStorage Build(
        Ulid folderId,
        string driverType,
        string? driverConfigJson,
        string subPath
    )
    {
        string normalizedType = driverType.Trim().ToLowerInvariant();

        switch (normalizedType)
        {
            case "local":
                return BuildLocal(folderId, driverConfigJson, subPath);

            case "nfs":
                return BuildNfs(folderId, driverConfigJson, subPath);

            case "s3":
            case "r2":
                return BuildS3(folderId, normalizedType, driverConfigJson, subPath);

            case "webdav":
                return BuildWebDav(folderId, driverConfigJson, subPath);

            default:
                throw new ArgumentException(
                    $"Unknown driver type '{driverType}' for folder {folderId}.",
                    nameof(driverType)
                );
        }
    }

    private IStorage BuildLocal(Ulid folderId, string? driverConfigJson, string subPath)
    {
        if (string.IsNullOrWhiteSpace(driverConfigJson))
            throw new ArgumentException(
                $"driver_config is required for 'local' (folder {folderId}). "
                    + "Supply: {{\"rootPath\": \"<absolute path>\"}}.",
                nameof(driverConfigJson)
            );

        LocalDriverConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<LocalDriverConfig>(
                driverConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"Failed to parse driver_config for folder {folderId} (type=local): {ex.Message}",
                nameof(driverConfigJson),
                ex
            );
        }

        if (config is null || string.IsNullOrWhiteSpace(config.RootPath))
            throw new ArgumentException(
                $"driver_config.rootPath is required for 'local' (folder {folderId}).",
                nameof(driverConfigJson)
            );

        // Incorporate the folder sub-path so callers can pass paths relative
        // to the storage root (consistent with NFS/S3/WebDAV behaviour).
        string allowedRoot = string.IsNullOrEmpty(subPath)
            ? config.RootPath
            : JoinRoot(config.RootPath, subPath, "local");
        StoragePathGuard guard = new([allowedRoot], _driver);
        return new LocalStorage(_driver, guard);
    }

    private IStorage BuildNfs(Ulid folderId, string? driverConfigJson, string subPath)
    {
        if (string.IsNullOrWhiteSpace(driverConfigJson))
            throw new ArgumentException(
                $"driver_config is required for 'nfs' (folder {folderId}). "
                    + "Supply at minimum: server and export.",
                nameof(driverConfigJson)
            );

        NfsDriverConfig nfsConfig = NfsDriverConfig.Parse(driverConfigJson, folderId);

        // Append the folder sub-path to the NFS export path when non-empty.
        if (!string.IsNullOrEmpty(subPath))
        {
            string combinedExport = JoinRoot(nfsConfig.Export, subPath, "nfs");
            nfsConfig = nfsConfig with { Export = combinedExport };
        }

        NfsStorageDriver nfsDriver = new(nfsConfig, _logger);
        return new RemoteStorage(nfsDriver);
    }

    private IStorage BuildS3(
        Ulid folderId,
        string driverType,
        string? driverConfigJson,
        string subPath
    )
    {
        if (string.IsNullOrWhiteSpace(driverConfigJson))
            throw new ArgumentException(
                $"driver_config is required for '{driverType}' (folder {folderId}).",
                nameof(driverConfigJson)
            );

        S3DriverConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<S3DriverConfig>(
                driverConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"Failed to parse driver_config for folder {folderId} (type={driverType}): {ex.Message}",
                nameof(driverConfigJson),
                ex
            );
        }

        if (config is null)
            throw new ArgumentException(
                $"driver_config deserialized to null for folder {folderId} (type={driverType}).",
                nameof(driverConfigJson)
            );

        if (string.IsNullOrWhiteSpace(config.Bucket))
            throw new ArgumentException(
                $"driver_config.bucket is required for '{driverType}' (folder {folderId}).",
                nameof(driverConfigJson)
            );

        if (string.IsNullOrWhiteSpace(config.Region))
            throw new ArgumentException(
                $"driver_config.region is required for '{driverType}' (folder {folderId}).",
                nameof(driverConfigJson)
            );

        if (driverType == "r2" && string.IsNullOrWhiteSpace(config.Endpoint))
            throw new ArgumentException(
                $"driver_config.endpoint is required for 'r2' (folder {folderId}). "
                    + "Set it to your account's R2 endpoint URL.",
                nameof(driverConfigJson)
            );

        // Combine driver prefix with folder sub-path.
        string effectivePrefix = string.IsNullOrEmpty(subPath)
            ? (config.Prefix ?? string.Empty)
            : JoinRoot(config.Prefix ?? string.Empty, subPath, driverType);

        string? accessKey = null;
        string? secretKey = null;

        if (!string.IsNullOrWhiteSpace(config.CredentialsRef) && _credentialResolver is not null)
        {
            (string AccessKey, string SecretKey)? creds = _credentialResolver.Resolve(
                config.CredentialsRef
            );
            if (creds is not null)
            {
                accessKey = creds.Value.AccessKey;
                secretKey = creds.Value.SecretKey;
            }
            else
            {
                _logger.LogWarning(
                    "credentials_ref '{CredentialsRef}' not found in secrets store for folder {FolderId}; falling back to default credential chain",
                    config.CredentialsRef,
                    folderId
                );
            }
        }

        S3StorageDriver s3Driver = new(
            config.Bucket,
            config.Region,
            effectivePrefix,
            config.Endpoint,
            accessKey,
            secretKey
        );

        return new RemoteStorage(s3Driver);
    }

    private IStorage BuildWebDav(Ulid folderId, string? driverConfigJson, string subPath)
    {
        if (string.IsNullOrWhiteSpace(driverConfigJson))
            throw new ArgumentException(
                $"driver_config is required for 'webdav' (folder {folderId}). "
                    + "Supply at minimum: url.",
                nameof(driverConfigJson)
            );

        WebDavDriverConfig webDavConfig = WebDavDriverConfig.Parse(
            driverConfigJson,
            folderId,
            _logger
        );

        string? username = null;
        string? password = null;

        if (_credentialResolver is not null)
        {
            (string AccessKey, string SecretKey)? creds = _credentialResolver.Resolve(
                $"driver:{folderId}"
            );
            if (creds is not null)
            {
                username = creds.Value.AccessKey;
                password = creds.Value.SecretKey;
            }
            else
            {
                _logger.LogWarning(
                    "No credentials found in store for WebDAV driver (folder {FolderId}); connecting anonymously",
                    folderId
                );
            }
        }

        webDavConfig = webDavConfig with { Username = username, Password = password };

        // Append sub-path to the WebDAV base URL when non-empty.
        if (!string.IsNullOrEmpty(subPath))
        {
            string combinedUrl = JoinRoot(webDavConfig.Url, subPath, "webdav");
            webDavConfig = webDavConfig with { Url = combinedUrl };
        }

        WebDavStorageDriver webDavDriver = new(webDavConfig);
        return new RemoteStorage(webDavDriver);
    }

    private static string BuildCacheKey(
        Ulid folderId,
        string driverType,
        string? configJson,
        string subPath
    )
    {
        string configHash = string.IsNullOrEmpty(configJson)
            ? "null"
            : Convert
                .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configJson)))
                .ToLowerInvariant();
        string subPathKey = string.IsNullOrEmpty(subPath) ? "_" : subPath;
        return $"{folderId}:{driverType.Trim().ToLowerInvariant()}:{configHash}:{subPathKey}";
    }
}
