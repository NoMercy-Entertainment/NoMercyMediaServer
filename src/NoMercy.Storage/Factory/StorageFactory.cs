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

        // local/default build on-disk OS paths (the facade's OS boundary);
        // NMS001 targets consumers, not the driver layer itself.
#pragma warning disable NMS001
        switch (driverType)
        {
            case "local":
                return Path.Combine(root, subPath);

            case "nfs":
            case "s3":
            case "r2":
            case "webdav":
            {
                // Remote drivers always speak forward slashes. Replace any
                // Windows backslashes the caller may have introduced via
                // Path.Combine on a server-side path — without this the NFS
                // mount call ends up with a malformed export like
                // '/mnt/vault/Media/Anime/Anime\Black.Butler.(2008)' and
                // libnfs rejects the mount outright.
                string trimmedRoot = root.Replace('\\', '/').TrimEnd('/');
                string trimmedSub = subPath.Replace('\\', '/').TrimStart('/');
                return $"{trimmedRoot}/{trimmedSub}";
            }

            default:
                return Path.Combine(root, subPath);
        }
#pragma warning restore NMS001
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
        // System-local driver: empty config or empty rootPath means no driver-level
        // root restriction. The folder's own subPath becomes the allowed root so
        // each folder constrains itself without needing a per-driver rootPath.
        if (string.IsNullOrWhiteSpace(driverConfigJson))
        {
            StoragePathGuard openGuard = BuildLocalGuardFromSubPath(subPath, _driver);
            return new LocalStorage(_driver, openGuard);
        }

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

        // Empty rootPath = system-local mode: folder subPath is the effective root.
        if (config is null || string.IsNullOrWhiteSpace(config.RootPath))
        {
            StoragePathGuard openGuard = BuildLocalGuardFromSubPath(subPath, _driver);
            return new LocalStorage(_driver, openGuard);
        }

        // Incorporate the folder sub-path so callers can pass paths relative
        // to the storage root (consistent with NFS/S3/WebDAV behaviour).
        string allowedRoot = string.IsNullOrEmpty(subPath)
            ? config.RootPath
            : JoinRoot(config.RootPath, subPath, "local");
        StoragePathGuard guard = new([allowedRoot], _driver);
        return new LocalStorage(_driver, guard);
    }

    /// <summary>
    /// Builds a <see cref="StoragePathGuard"/> for the system-local driver
    /// where no driver-level rootPath is set. When <paramref name="subPath"/>
    /// is non-empty it becomes the single allowed root, constraining the folder
    /// to that absolute path. When subPath is empty no root restriction is
    /// applied (structural-only validation).
    /// </summary>
    private static StoragePathGuard BuildLocalGuardFromSubPath(
        string subPath,
        IStorageDriver driver
    )
    {
        if (string.IsNullOrWhiteSpace(subPath))
            return new StoragePathGuard([], driver);

        return new StoragePathGuard([subPath], driver);
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

        // Record the folder sub-path as SubPath so the mount stays at the
        // export root. Baking subPath into Export caused libnfs to try mounting
        // a non-existent export (e.g. /mnt/vault/Media/Anime instead of /mnt/vault/Media).
        if (!string.IsNullOrEmpty(subPath))
        {
            nfsConfig = nfsConfig with { SubPath = subPath.Replace('\\', '/').Trim('/') };
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

        // Credential resolution order mirrors WebDavStorageDriver: try the
        // explicit credentials_ref first, then fall back to the per-driver key
        // ("driver:{folderId}") that the dashboard uses by default for stored
        // S3/R2 creds. Without this fallback the AWS SDK silently uses the
        // default credential chain (env-vars / IAM) and surfaces "Unable to get
        // IAM security credentials from EC2 Instance Metadata Service".
        if (_credentialResolver is not null)
        {
            (string AccessKey, string SecretKey)? creds = null;

            if (!string.IsNullOrWhiteSpace(config.CredentialsRef))
            {
                creds = _credentialResolver.Resolve(config.CredentialsRef);
                if (creds is null)
                {
                    _logger.LogWarning(
                        "credentials_ref '{CredentialsRef}' not found in secrets store for folder {FolderId}; trying driver:{FallbackKey} fallback",
                        config.CredentialsRef,
                        folderId,
                        $"driver:{folderId}"
                    );
                }
            }

            creds ??= _credentialResolver.Resolve($"driver:{folderId}");

            if (creds is not null)
            {
                accessKey = creds.Value.AccessKey;
                secretKey = creds.Value.SecretKey;
                _logger.LogInformation(
                    "S3/R2 cred resolved for folder {FolderId}: accessKey starts with '{AkPrefix}' len={AkLen}, secret len={SkLen}",
                    folderId,
                    accessKey.Length >= 4 ? accessKey.Substring(0, 4) : accessKey,
                    accessKey.Length,
                    secretKey.Length
                );
            }
            else
            {
                _logger.LogWarning(
                    "No credentials found in store for {DriverType} driver (folder {FolderId}); falling back to default credential chain",
                    driverType,
                    folderId
                );
            }
        }

        // S3/R2 require explicit credentials in a self-hosted media server
        // context — the default AWS credential chain (env vars, EC2 IMDS) is
        // never what an operator wants here. Reject with a message that
        // names the fix instead of letting the SDK throw "Credential access
        // key has length 0" / "Unable to get IAM credentials from EC2 IMDS"
        // deep inside the signing path.
        bool emptyOrNullAccess = string.IsNullOrEmpty(accessKey);
        bool emptyOrNullSecret = string.IsNullOrEmpty(secretKey);
        if (emptyOrNullAccess || emptyOrNullSecret)
            throw new ArgumentException(
                $"{driverType} driver (folder {folderId}) has no credentials configured. "
                    + "Open the driver in the dashboard and set access key + secret key.",
                nameof(driverConfigJson)
            );

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
