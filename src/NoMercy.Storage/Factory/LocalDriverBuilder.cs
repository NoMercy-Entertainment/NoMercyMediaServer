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

using System.Text.Json;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;
using static NoMercy.Storage.Factory.StorageFactory;

namespace NoMercy.Storage.Factory;

public sealed class LocalDriverBuilder : IStorageDriverBuilder
{
    private readonly IStorageDriver _driver;

    public LocalDriverBuilder(IStorageDriver driver) => _driver = driver;

    public IReadOnlyCollection<string> SupportedTypes { get; } = ["local"];

    public IStorage Build(Ulid folderId, string driverType, string? driverConfigJson, string subPath)
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
}
