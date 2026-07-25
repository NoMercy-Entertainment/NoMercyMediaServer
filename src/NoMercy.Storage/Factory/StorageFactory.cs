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
using Microsoft.Extensions.Logging;

namespace NoMercy.Storage.Factory;

/// <summary>
/// Parsed representation of the required JSON config for local-disk drivers.
/// <see cref="RootPath"/> is the absolute path to the local mount or directory.
/// </summary>
internal sealed record LocalDriverConfig(string? RootPath);

public sealed class StorageFactory : IStorageFactory, IDisposable
{
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
    // Cache key = "{folderId}:{driverType}:{sha256 of resolved configJson}".
    private readonly ConcurrentDictionary<string, IStorage> _cache = new();

    private readonly IReadOnlyDictionary<string, IStorageDriverBuilder> _builders;

    public StorageFactory(
        ILogger<StorageFactory> logger,
        IEnumerable<IStorageDriverBuilder> builders,
        IDriverConfigResolver? driverConfigResolver = null
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(builders);
        _builders = builders
            .SelectMany(builder => builder.SupportedTypes.Select(type => (type, builder)))
            .ToDictionary(
                pair => pair.type,
                pair => pair.builder,
                StringComparer.OrdinalIgnoreCase
            );
        _driverConfigResolver = driverConfigResolver;
    }

    /// <summary>
    /// The built-in driver builders, used when constructing a factory outside
    /// DI (via the convenience constructor). The DI path instead injects all
    /// registered <see cref="IStorageDriverBuilder"/> services so third parties
    /// can add drivers without editing this class.
    /// </summary>
    internal static IEnumerable<IStorageDriverBuilder> DefaultBuilders(
        IStorageDriver driver,
        ILogger logger,
        ICredentialResolver? credentialResolver
    ) =>
        [
            new LocalDriverBuilder(driver),
            new NfsDriverBuilder(logger),
            new S3DriverBuilder(logger, credentialResolver),
            new WebDavDriverBuilder(logger, credentialResolver),
            new SmbDriverBuilder(logger, credentialResolver),
        ];

    /// <summary>
    /// Convenience constructor for callers that build a factory directly (no DI)
    /// with the built-in driver set. The DI path uses the primary constructor
    /// with all registered <see cref="IStorageDriverBuilder"/> services instead.
    /// </summary>
    public StorageFactory(
        IStorageDriver driver,
        ILogger<StorageFactory> logger,
        IDriverConfigResolver? driverConfigResolver = null,
        ICredentialResolver? credentialResolver = null
    )
        : this(logger, DefaultBuilders(driver, logger, credentialResolver), driverConfigResolver)
    { }

    public IStorage For(Ulid folderId, Ulid driverId, string subPath)
    {
        string driverType = "local";
        string? configJson = null;

        if (_driverConfigResolver is null)
        {
            _logger.LogWarning(
                "Folder {FolderId} has DriverId {DriverId} but no IDriverConfigResolver is registered; falling back to built-in local", [folderId, driverId]
            );
        }
        else
        {
            (string Type, string? ConfigJson)? resolved = _driverConfigResolver.Resolve(driverId);
            if (resolved is null)
            {
                _logger.LogWarning(
                    "Driver {DriverId} not found for folder {FolderId}; falling back to built-in local", [driverId, folderId]
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
            if (
                key.StartsWith(prefix, StringComparison.Ordinal)
                && _cache.TryRemove(key, out IStorage? removed)
            )
            {
                DisposeUnderlyingDriver(removed);
            }
        }
    }

    public void InvalidateAll()
    {
        foreach (IStorage storage in _cache.Values)
            DisposeUnderlyingDriver(storage);
        _cache.Clear();
    }

    /// <summary>
    /// Disposes the process-level singleton on host shutdown so every cached
    /// storage's driver releases its unmanaged/network handles (NFS keep-alive
    /// timer + libnfs context, S3 SDK client, WebDAV HttpClient). The DI
    /// container tracks and disposes this automatically because it's resolved
    /// through a factory delegate (see ServiceCollectionExtensions) rather than
    /// registered as a pre-built instance.
    /// </summary>
    public void Dispose() => InvalidateAll();

    /// <summary>
    /// Evicting an IStorage from the cache used to just drop the reference —
    /// the underlying driver (when it holds real resources: NFS/SMB/S3/WebDAV)
    /// was never told to release them. IStorage/IStorageDriver don't declare
    /// Dispose themselves (LocalStorageDriver has nothing to release), so this
    /// checks for it structurally instead of widening either abstraction.
    /// </summary>
    private static void DisposeUnderlyingDriver(IStorage storage)
    {
        if (storage.Driver is IDisposable disposable)
            disposable.Dispose();
    }

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
            case "smb":
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

        if (!_builders.TryGetValue(normalizedType, out IStorageDriverBuilder? builder))
            throw new ArgumentException(
                $"Unknown driver type '{driverType}' for folder {folderId}.",
                nameof(driverType)
            );

        return builder.Build(folderId, normalizedType, driverConfigJson, subPath);
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
