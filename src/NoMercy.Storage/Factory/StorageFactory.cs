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
/// Parsed representation of the optional JSON config for local-disk folders.
/// When <see cref="RootPath"/> is set it overrides <c>folderPath</c>.
/// </summary>
internal sealed record LocalDriverConfig(string? RootPath);

public sealed class StorageFactory : IStorageFactory
{
    private readonly IStorageDriver _driver;
    private readonly ILogger<StorageFactory> _logger;

    /// <summary>
    /// Resolves a driver ID to its (type, configJson). Supplied at DI
    /// registration from a higher-level project that has DB access.
    /// When null, only built-in local (driverId == null) is supported.
    /// </summary>
    private readonly IDriverConfigResolver? _driverConfigResolver;

    /// <summary>
    /// Resolves a <c>credentialsRef</c> key to an (accessKey, secretKey) pair.
    /// When null, the AWS default credential chain is used for S3/R2.
    /// </summary>
    private readonly ICredentialResolver? _credentialResolver;

    // Cache key = "{folderId}:{driverId|local}:{sha256 of resolved configJson}".
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

    public IStorage For(Ulid folderId, Ulid? driverId, string folderPath)
    {
        string driverType = "local";
        string? configJson = null;

        if (driverId is not null)
        {
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
                (string Type, string? ConfigJson)? resolved = _driverConfigResolver.Resolve(
                    driverId.Value
                );
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
        }

        string cacheKey = BuildCacheKey(folderId, driverType, configJson);
        return _cache.GetOrAdd(
            cacheKey,
            _ => Build(folderId, driverType, configJson, folderPath)
        );
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

    private IStorage Build(
        Ulid folderId,
        string driverType,
        string? driverConfigJson,
        string folderPath
    )
    {
        string normalizedType = driverType.Trim().ToLowerInvariant();

        switch (normalizedType)
        {
            case "local":
            case "smb":
                return BuildLocal(folderId, normalizedType, driverConfigJson, folderPath);

            case "nfs":
                return BuildNfs(folderId, driverConfigJson);

            case "s3":
            case "r2":
                return BuildS3(folderId, normalizedType, driverConfigJson);

            case "webdav":
                return BuildWebDav(folderId, driverConfigJson);

            default:
                throw new ArgumentException(
                    $"Unknown driver type '{driverType}' for folder {folderId}.",
                    nameof(driverType)
                );
        }
    }

    private IStorage BuildNfs(Ulid folderId, string? driverConfigJson)
    {
        if (string.IsNullOrWhiteSpace(driverConfigJson))
            throw new ArgumentException(
                $"driver_config is required for 'nfs' (folder {folderId}). "
                    + "Supply at minimum: server and export.",
                nameof(driverConfigJson)
            );

        NfsDriverConfig nfsConfig = NfsDriverConfig.Parse(driverConfigJson, folderId);
        NfsStorageDriver nfsDriver = new(nfsConfig);
        return new RemoteStorage(nfsDriver);
    }

    private IStorage BuildLocal(
        Ulid folderId,
        string driverType,
        string? driverConfigJson,
        string folderPath
    )
    {
        string allowedRoot = ResolveLocalRoot(folderId, driverType, driverConfigJson, folderPath);
        StoragePathGuard guard = new([allowedRoot], _driver);
        return new LocalStorage(_driver, guard);
    }

    private string ResolveLocalRoot(
        Ulid folderId,
        string driverType,
        string? driverConfigJson,
        string folderPath
    )
    {
        if (string.IsNullOrWhiteSpace(driverConfigJson))
            return folderPath;

        try
        {
            LocalDriverConfig? config = JsonSerializer.Deserialize<LocalDriverConfig>(
                driverConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (config?.RootPath is not null && !string.IsNullOrWhiteSpace(config.RootPath))
                return config.RootPath;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse driver_config for folder {FolderId} (type={DriverType}); falling back to folder path",
                folderId,
                driverType
            );
        }

        return folderPath;
    }

    private IStorage BuildS3(Ulid folderId, string driverType, string? driverConfigJson)
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
            config.Prefix,
            config.Endpoint,
            accessKey,
            secretKey
        );

        return new RemoteStorage(s3Driver);
    }

    private IStorage BuildWebDav(Ulid folderId, string? driverConfigJson)
    {
        if (string.IsNullOrWhiteSpace(driverConfigJson))
            throw new ArgumentException(
                $"driver_config is required for 'webdav' (folder {folderId}). "
                    + "Supply at minimum: url.",
                nameof(driverConfigJson)
            );

        // Adapt ICredentialResolver to the Func<string, (string, string)?> the WebDav driver expects.
        Func<string, (string AccessKey, string SecretKey)?>? resolverFunc =
            _credentialResolver is null
                ? null
                : key => _credentialResolver.Resolve(key);

        WebDavDriverConfig webDavConfig = WebDavDriverConfig.Parse(driverConfigJson, folderId);
        WebDavStorageDriver webDavDriver = new(webDavConfig, resolverFunc);
        return new RemoteStorage(webDavDriver);
    }

    private static string BuildCacheKey(Ulid folderId, string driverType, string? configJson)
    {
        string configHash = string.IsNullOrEmpty(configJson)
            ? "null"
            : Convert
                .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configJson)))
                .ToLowerInvariant();
        return $"{folderId}:{driverType.Trim().ToLowerInvariant()}:{configHash}";
    }
}
