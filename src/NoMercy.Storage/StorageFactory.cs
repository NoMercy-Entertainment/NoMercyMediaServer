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

    // Cache key = (folderId, backendType, sha256 of configJson).
    // Encoding config changes into the key means stale instances are
    // abandoned automatically — no explicit Invalidate needed for updates.
    private readonly ConcurrentDictionary<string, IStorage> _cache = new();

    public StorageFactory(IStorageBackend backend, ILogger<StorageFactory> logger)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                throw new NotSupportedException(
                    $"Backend type '{normalizedType}' is reserved for a future driver. "
                        + $"Folder {folderId} cannot be opened until the driver lands."
                );

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
