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

using Microsoft.Extensions.Logging;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Remote;

namespace NoMercy.Storage.Factory;

public sealed class NfsDriverBuilder : IStorageDriverBuilder
{
    private readonly ILogger _logger;

    public NfsDriverBuilder(ILogger logger) => _logger = logger;

    public IReadOnlyCollection<string> SupportedTypes { get; } = ["nfs"];

    public IStorage Build(Ulid folderId, string driverType, string? driverConfigJson, string subPath)
    {
        if (string.IsNullOrWhiteSpace(value: driverConfigJson))
            throw new ArgumentException(
                message: $"driver_config is required for 'nfs' (folder {folderId}). "
                         + "Supply at minimum: server and export.",
                paramName: nameof(driverConfigJson)
            );

        NfsDriverConfig nfsConfig = NfsDriverConfig.Parse(json: driverConfigJson, folderId: folderId);

        // Record the folder sub-path as SubPath so the mount stays at the
        // export root. Baking subPath into Export caused libnfs to try mounting
        // a non-existent export (e.g. /mnt/vault/Media/Anime instead of /mnt/vault/Media).
        if (!string.IsNullOrEmpty(value: subPath))
        {
            nfsConfig = nfsConfig with { SubPath = subPath.Replace(oldChar: '\\', newChar: '/').Trim(trimChar: '/') };
        }

        NfsStorageDriver nfsDriver = new(config: nfsConfig, log: _logger);
        return new RemoteStorage(driver: nfsDriver);
    }

}
