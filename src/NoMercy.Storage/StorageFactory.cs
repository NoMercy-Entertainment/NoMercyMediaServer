using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NoMercy.Storage;

/// <summary>
/// Parsed representation of the optional JSON config for local-disk folders.
/// When <see cref="RootPath"/> is set it overrides <c>folderPath</c>
/// (useful when the library root is a remapped symlink location).
/// </summary>
internal sealed record LocalBackendConfig(string? RootPath);

public sealed class StorageFactory : IStorageFactory
{
    private readonly IStorageBackend _backend;
    private readonly ILogger<StorageFactory> _logger;

    /// <summary>
    /// Optional resolver that maps a <c>credentialsRef</c> key to an
    /// (accessKey, secretKey) pair. Supply this at the DI registration
    /// site (e.g. from <c>CredentialManager.Credential</c> in
    /// <c>NoMercy.Helpers</c>). When null, the AWS default credential
    /// chain is used for S3/R2 backends.
    /// </summary>
    private readonly Func<string, (string AccessKey, string SecretKey)?>? _credentialsResolver;

    // Cache key = (folderId, backendType, sha256 of configJson).
    // Encoding config changes into the key means stale instances are
    // abandoned automatically — no explicit Invalidate needed for updates.
    private readonly ConcurrentDictionary<string, IStorage> _cache = new();

    public StorageFactory(
        IStorageBackend backend,
        ILogger<StorageFactory> logger,
        Func<string, (string AccessKey, string SecretKey)?>? credentialsResolver = null
    )
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credentialsResolver = credentialsResolver;
    }

    public IStorage For(
        Ulid folderId,
        string backendType,
        string? backendConfigJson,
        string folderPath
    )
    {
        string cacheKey = BuildCacheKey(folderId, backendType, backendConfigJson);
        return _cache.GetOrAdd(
            cacheKey,
            _ => Build(folderId, backendType, backendConfigJson, folderPath)
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
        string backendType,
        string? backendConfigJson,
        string folderPath
    )
    {
        string normalizedType = backendType.Trim().ToLowerInvariant();

        switch (normalizedType)
        {
            case "local":
            case "smb":
            case "nfs":
                return BuildLocal(folderId, normalizedType, backendConfigJson, folderPath);

            case "s3":
            case "r2":
                return BuildS3(folderId, normalizedType, backendConfigJson);

            default:
                throw new ArgumentException(
                    $"Unknown backend type '{backendType}' for folder {folderId}.",
                    nameof(backendType)
                );
        }
    }

    private IStorage BuildLocal(
        Ulid folderId,
        string backendType,
        string? backendConfigJson,
        string folderPath
    )
    {
        // SMB and NFS work through System.IO once the OS has the share
        // mounted; we treat them as local until an in-process driver lands.
        string allowedRoot = ResolveLocalRoot(folderId, backendType, backendConfigJson, folderPath);

        StoragePathGuard guard = new([allowedRoot], _backend);
        return new LocalStorage(_backend, guard);
    }

    private string ResolveLocalRoot(
        Ulid folderId,
        string backendType,
        string? backendConfigJson,
        string folderPath
    )
    {
        if (string.IsNullOrWhiteSpace(backendConfigJson))
            return folderPath;

        try
        {
            LocalBackendConfig? config = JsonSerializer.Deserialize<LocalBackendConfig>(
                backendConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (config?.RootPath is not null && !string.IsNullOrWhiteSpace(config.RootPath))
                return config.RootPath;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse backend_config for folder {FolderId} (type={BackendType}); falling back to folder path",
                folderId,
                backendType
            );
        }

        return folderPath;
    }

    private IStorage BuildS3(Ulid folderId, string backendType, string? backendConfigJson)
    {
        if (string.IsNullOrWhiteSpace(backendConfigJson))
            throw new ArgumentException(
                $"backend_config is required for '{backendType}' (folder {folderId}).",
                nameof(backendConfigJson)
            );

        S3BackendConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<S3BackendConfig>(
                backendConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"Failed to parse backend_config for folder {folderId} (type={backendType}): {ex.Message}",
                nameof(backendConfigJson),
                ex
            );
        }

        if (config is null)
            throw new ArgumentException(
                $"backend_config deserialized to null for folder {folderId} (type={backendType}).",
                nameof(backendConfigJson)
            );

        if (string.IsNullOrWhiteSpace(config.Bucket))
            throw new ArgumentException(
                $"backend_config.bucket is required for '{backendType}' (folder {folderId}).",
                nameof(backendConfigJson)
            );

        if (string.IsNullOrWhiteSpace(config.Region))
            throw new ArgumentException(
                $"backend_config.region is required for '{backendType}' (folder {folderId}).",
                nameof(backendConfigJson)
            );

        if (backendType == "r2" && string.IsNullOrWhiteSpace(config.Endpoint))
            throw new ArgumentException(
                $"backend_config.endpoint is required for 'r2' (folder {folderId}). "
                    + "Set it to your account's R2 endpoint URL (https://<account-id>.r2.cloudflarestorage.com).",
                nameof(backendConfigJson)
            );

        string? accessKey = null;
        string? secretKey = null;

        if (!string.IsNullOrWhiteSpace(config.CredentialsRef) && _credentialsResolver is not null)
        {
            (string AccessKey, string SecretKey)? creds = _credentialsResolver(
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

        S3StorageBackend s3Backend = new(
            config.Bucket,
            config.Region,
            config.Prefix,
            config.Endpoint,
            accessKey,
            secretKey
        );

        return new RemoteStorage(s3Backend);
    }

    private static string BuildCacheKey(Ulid folderId, string backendType, string? configJson)
    {
        string configHash = string.IsNullOrEmpty(configJson)
            ? "null"
            : Convert
                .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configJson)))
                .ToLowerInvariant();
        return $"{folderId}:{backendType.Trim().ToLowerInvariant()}:{configHash}";
    }
}
